/**
 * Runtime response validation for the content seam (issues 073/074/077;
 * SECURITY.md, Input validation rule 1).
 *
 * Every content payload — mock included — is parsed as `unknown`, verified
 * field by field, and REJECTED on any violation (thrown, never sanitized
 * into acceptance). Callers fail closed to an error state; no partial
 * unvalidated content ever renders. Server-side sanitization (issue 072)
 * already guarantees plain text; the '<' check here is defense in depth —
 * a payload carrying markup is malformed, not something to strip.
 *
 * Fail-closed policy mirrors enforced here as defense in depth:
 *   - a non-DE legal doc list carrying impressum/widerruf is malformed
 *     (CC-CNT-005's per-market set — authored server-side, issue 023).
 */

import { LOCALES, Locale, MARKETS, Market } from '../core/transacting-context';
import {
  BLAZES,
  Blaze,
  ChefProfile,
  ChefRoster,
  CowHerd,
  CowProfile,
  LEGAL_DOC_IDS,
  LegalDoc,
  LegalDocId,
  LegalDocList,
  LegalDocSummary,
  LegalSection,
} from './content.types';

/** Raised when a content response fails the typed schema. Message names the
 * field/rule only — never echoes raw payload content (SECURITY.md, Logging
 * rules 1 and 5). */
export class ContentValidationError extends Error {
  constructor(rule: string) {
    super(`Content response failed schema validation: ${rule}`);
    this.name = 'ContentValidationError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Plain text only: non-empty, no markup, no control characters. */
function requirePlainText(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new ContentValidationError(`${field} must be a non-empty string`);
  }
  // eslint-disable-next-line no-control-regex
  if (/[<\x00-\x1f]/.test(value)) {
    throw new ContentValidationError(`${field} must be plain text without markup`);
  }
  return value;
}

function requireEnum<T extends string>(value: unknown, allowed: readonly T[], field: string): T {
  if (typeof value !== 'string' || !(allowed as readonly string[]).includes(value)) {
    throw new ContentValidationError(`${field} must be one of the declared values`);
  }
  return value as T;
}

function requireArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) {
    throw new ContentValidationError(`${field} must be an array`);
  }
  return value;
}

// --- Chefs -------------------------------------------------------------------

function parseChefProfile(value: unknown, field: string): ChefProfile {
  if (!isRecord(value)) {
    throw new ContentValidationError(`${field} must be an object`);
  }
  const markets = requireArray(value['markets'], `${field}.markets`).map(
    (m, i): Market => requireEnum(m, MARKETS, `${field}.markets[${i}]`),
  );
  if (markets.length === 0) {
    throw new ContentValidationError(`${field}.markets must not be empty`);
  }
  return {
    id: requirePlainText(value['id'], `${field}.id`),
    name: requirePlainText(value['name'], `${field}.name`),
    specialty: requirePlainText(value['specialty'], `${field}.specialty`),
    bio: requirePlainText(value['bio'], `${field}.bio`),
    markets,
  };
}

/** Validate a chef-roster response. Throws ContentValidationError. */
export function parseChefRoster(input: unknown): ChefRoster {
  if (!isRecord(input)) {
    throw new ContentValidationError('roster must be an object');
  }
  const locale: Locale = requireEnum(input['locale'], LOCALES, 'roster.locale');
  const roster = requireArray(input['roster'], 'roster.roster').map((c, i) =>
    parseChefProfile(c, `roster.roster[${i}]`),
  );
  return { locale, roster };
}

// --- Cows --------------------------------------------------------------------

function parseCowProfile(value: unknown, field: string): CowProfile {
  if (!isRecord(value)) {
    throw new ContentValidationError(`${field} must be an object`);
  }
  const blaze: Blaze = requireEnum(value['blaze'], BLAZES, `${field}.blaze`);
  return {
    id: requirePlainText(value['id'], `${field}.id`),
    name: requirePlainText(value['name'], `${field}.name`),
    role: requirePlainText(value['role'], `${field}.role`),
    bio: requirePlainText(value['bio'], `${field}.bio`),
    blaze,
  };
}

/** Validate a cow-herd response. Throws ContentValidationError. */
export function parseCowHerd(input: unknown): CowHerd {
  if (!isRecord(input)) {
    throw new ContentValidationError('herd must be an object');
  }
  const locale: Locale = requireEnum(input['locale'], LOCALES, 'herd.locale');
  const herd = requireArray(input['herd'], 'herd.herd').map((c, i) =>
    parseCowProfile(c, `herd.herd[${i}]`),
  );
  return { locale, herd };
}

// --- Legal -------------------------------------------------------------------

const DE_ONLY_DOCS: readonly LegalDocId[] = ['impressum', 'widerruf'];

function parseLegalSummary(value: unknown, field: string): LegalDocSummary {
  if (!isRecord(value)) {
    throw new ContentValidationError(`${field} must be an object`);
  }
  return {
    id: requireEnum(value['id'], LEGAL_DOC_IDS, `${field}.id`),
    title: requirePlainText(value['title'], `${field}.title`),
  };
}

/** Validate a legal-doc-list response. Throws ContentValidationError. */
export function parseLegalDocList(input: unknown): LegalDocList {
  if (!isRecord(input)) {
    throw new ContentValidationError('docList must be an object');
  }
  const market: Market = requireEnum(input['market'], MARKETS, 'docList.market');
  const docs = requireArray(input['docs'], 'docList.docs').map((d, i) =>
    parseLegalSummary(d, `docList.docs[${i}]`),
  );
  if (new Set(docs.map((d) => d.id)).size !== docs.length) {
    throw new ContentValidationError('docList.docs must not contain duplicates');
  }
  // Fail-closed CC-CNT-005 mirror: the DE-only statutory documents in a
  // non-DE market's set is a malformed policy payload, rejected outright.
  if (market !== 'DE' && docs.some((d) => DE_ONLY_DOCS.includes(d.id))) {
    throw new ContentValidationError('docList: DE-only documents in a non-DE market set');
  }
  return { market, docs };
}

function parseLegalSection(value: unknown, field: string): LegalSection {
  if (!isRecord(value)) {
    throw new ContentValidationError(`${field} must be an object`);
  }
  const paragraphs = requireArray(value['paragraphs'], `${field}.paragraphs`).map((p, i) =>
    requirePlainText(p, `${field}.paragraphs[${i}]`),
  );
  if (paragraphs.length === 0) {
    throw new ContentValidationError(`${field}.paragraphs must not be empty`);
  }
  return { heading: requirePlainText(value['heading'], `${field}.heading`), paragraphs };
}

/** Validate a versioned legal-document response. Throws ContentValidationError. */
export function parseLegalDoc(input: unknown): LegalDoc {
  const summary = parseLegalSummary(input, 'doc');
  const record = input as Record<string, unknown>;
  const version = requirePlainText(record['version'], 'doc.version');
  const effectiveDate = requirePlainText(record['effectiveDate'], 'doc.effectiveDate');
  if (!/^\d{4}-\d{2}-\d{2}$/.test(effectiveDate)) {
    throw new ContentValidationError('doc.effectiveDate must be an ISO date (yyyy-mm-dd)');
  }
  const locale: Locale = requireEnum(record['locale'], LOCALES, 'doc.locale');
  const sections = requireArray(record['sections'], 'doc.sections').map((s, i) =>
    parseLegalSection(s, `doc.sections[${i}]`),
  );
  if (sections.length === 0) {
    throw new ContentValidationError('doc.sections must not be empty');
  }
  return { ...summary, version, effectiveDate, locale, sections };
}
