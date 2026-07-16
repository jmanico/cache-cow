/**
 * Runtime validation for nav-policy responses (issues 074/075; CC-MKT-005,
 * CC-MKT-006; SECURITY.md, Input validation rule 1).
 *
 * Every policy payload — mock included — is parsed as `unknown` and rejected
 * on any violation, never sanitized into acceptance. Beyond shape checks,
 * one cross-field policy invariant is enforced FAIL-CLOSED here as defense
 * in depth: a payload that places or reaches 'cuts' in the IN market is a
 * malformed policy (CC-MKT-005), rejected outright — the client will render
 * no navigation rather than render a non-compliant placement. The authoring
 * enforcement is server-side (Market & Gating Policy, issues 023/025).
 */

import { MARKETS, Market } from '../core/transacting-context';
import { NAV_PAGES, NavPage, NavPolicy } from './nav-policy.types';

/** Raised when a nav-policy response fails the typed schema. Message names
 * the field/rule only — never echoes raw payload content (SECURITY.md,
 * Logging rules 1 and 5). */
export class NavPolicyValidationError extends Error {
  constructor(rule: string) {
    super(`Nav policy failed schema validation: ${rule}`);
    this.name = 'NavPolicyValidationError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function requirePages(value: unknown, field: string): readonly NavPage[] {
  if (!Array.isArray(value)) {
    throw new NavPolicyValidationError(`${field} must be an array`);
  }
  const pages = value.map((entry, i) => {
    if (typeof entry !== 'string' || !(NAV_PAGES as readonly string[]).includes(entry)) {
      throw new NavPolicyValidationError(`${field}[${i}] must be a declared nav page`);
    }
    return entry as NavPage;
  });
  if (new Set(pages).size !== pages.length) {
    throw new NavPolicyValidationError(`${field} must not contain duplicates`);
  }
  return pages;
}

/** Validate a nav-policy response. Throws NavPolicyValidationError. */
export function parseNavPolicy(input: unknown): NavPolicy {
  if (!isRecord(input)) {
    throw new NavPolicyValidationError('policy must be an object');
  }
  const marketRaw = input['market'];
  if (typeof marketRaw !== 'string' || !(MARKETS as readonly string[]).includes(marketRaw)) {
    throw new NavPolicyValidationError('policy.market must be a launch market');
  }
  const market = marketRaw as Market;
  const primary = requirePages(input['primary'], 'policy.primary');
  const ourStory = requirePages(input['ourStory'], 'policy.ourStory');
  const reachable = requirePages(input['reachable'], 'policy.reachable');

  // Placement must be a subset of reachability: a page linked anywhere in
  // navigation must exist in the market (fail closed on inconsistency).
  for (const page of [...primary, ...ourStory]) {
    if (!reachable.includes(page)) {
      throw new NavPolicyValidationError('policy places a page that is not reachable');
    }
  }

  // Fail-closed CC-MKT-005 mirror: 'cuts' anywhere in an IN policy is a
  // malformed payload, never rendered.
  if (market === 'IN' && reachable.includes('cuts')) {
    throw new NavPolicyValidationError('policy: cuts must not be reachable in the IN market');
  }

  return { market, primary, ourStory, reachable };
}
