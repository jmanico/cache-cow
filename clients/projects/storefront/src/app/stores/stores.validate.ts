/**
 * Runtime validation for store-locator responses (issue 078; SECURITY.md,
 * Input validation rule 1).
 *
 * Every payload — mock included — is parsed as `unknown` and REJECTED on any
 * violation, never sanitized into acceptance. The parser builds the public
 * projection field by field, so an over-sharing response (one carrying
 * wholesale prices, terms, or partner tenancy fields) contributes NOTHING to
 * the validated object the page renders — the consumer surface cannot leak
 * what it never constructs (CC-WHS-003; issue 078 AC-03).
 *
 * Page-size clamping (issue 078 AC-04; SECURITY.md, HTTP boundary rule 7) is
 * server-owned; the client-side cap here is defense in depth against an
 * unbounded list, not the enforcement point.
 */

import { MARKETS, Market } from '../core/transacting-context';
import { StoreLocation, StoreLocationList } from './stores.types';

/** Client-side defense-in-depth cap; the server clamps page size (rule 7). */
const MAX_LOCATIONS = 500;

/** Raised when a locator response fails the typed schema. Message names the
 * field/rule only — never echoes raw payload content (SECURITY.md, Logging
 * rules 1 and 5). */
export class StoreLocationValidationError extends Error {
  constructor(rule: string) {
    super(`Store locator response failed schema validation: ${rule}`);
    this.name = 'StoreLocationValidationError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Plain text only: non-empty, no markup, no control characters. */
function requirePlainText(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new StoreLocationValidationError(`${field} must be a non-empty string`);
  }
  // eslint-disable-next-line no-control-regex
  if (/[<\x00-\x1f]/.test(value)) {
    throw new StoreLocationValidationError(`${field} must be plain text without markup`);
  }
  return value;
}

function parseLocation(value: unknown, field: string): StoreLocation {
  if (!isRecord(value)) {
    throw new StoreLocationValidationError(`${field} must be an object`);
  }
  const linesRaw = value['addressLines'];
  if (!Array.isArray(linesRaw) || linesRaw.length === 0) {
    throw new StoreLocationValidationError(`${field}.addressLines must be a non-empty array`);
  }
  const addressLines = linesRaw.map((line, i) =>
    requirePlainText(line, `${field}.addressLines[${i}]`),
  );
  // Only the declared public fields are read; anything else in the payload
  // is discarded here and never reaches the page (CC-WHS-003).
  return {
    id: requirePlainText(value['id'], `${field}.id`),
    retailer: requirePlainText(value['retailer'], `${field}.retailer`),
    addressLines,
    locality: requirePlainText(value['locality'], `${field}.locality`),
  };
}

/** Validate a store-locator response. Throws StoreLocationValidationError. */
export function parseStoreLocationList(input: unknown): StoreLocationList {
  if (!isRecord(input)) {
    throw new StoreLocationValidationError('locations must be an object');
  }
  const marketRaw = input['market'];
  if (typeof marketRaw !== 'string' || !(MARKETS as readonly string[]).includes(marketRaw)) {
    throw new StoreLocationValidationError('locations.market must be a launch market');
  }
  const market = marketRaw as Market;
  const locationsRaw = input['locations'];
  if (!Array.isArray(locationsRaw)) {
    throw new StoreLocationValidationError('locations.locations must be an array');
  }
  if (locationsRaw.length > MAX_LOCATIONS) {
    throw new StoreLocationValidationError('locations.locations exceeds the maximum page size');
  }
  const locations = locationsRaw.map((l, i) => parseLocation(l, `locations.locations[${i}]`));
  if (new Set(locations.map((l) => l.id)).size !== locations.length) {
    throw new StoreLocationValidationError('locations.locations must not contain duplicate ids');
  }
  return { market, locations };
}
