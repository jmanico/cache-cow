/**
 * Typed store-locator response model (issue 078; DESIGN.md §7 "Store
 * locator", §10).
 *
 * This is the client half of the consumer read endpoint's contract: a
 * PUBLIC-FIELD PROJECTION of retail partner locations, never a pass-through
 * of partner records (issue 078 AC-03; CC-WHS-003; ARCHITECTURE.md,
 * Dependency rule 3 — consumer surfaces must not depend on wholesale data).
 *
 * The projection is an explicit allowlist BY TYPE: there is no field here
 * for wholesale prices, payment terms, partner tenancy identifiers, or
 * anything derivable into them, so nothing confidential can be "accidentally
 * serialized" into a consumer response and nothing confidential has a place
 * to land if a future server response over-shares (the validator drops what
 * the type does not declare).
 *
 * MAP: deliberately absent. DESIGN.md §7 specifies "map plus list", but no
 * map provider is named anywhere in the specs and any hosted map (scripts,
 * tiles, geocoding) collides with the ban on third-party runtime CDNs
 * (SECURITY.md, Deployment rule 10) and the exact-origin CSP (HTTP boundary
 * rule 2). That is an OPEN HUMAN DECISION (issue 078 Open Questions; epic
 * open question 11) — so this model carries NO coordinates, no geocoding,
 * and no tile references. The list is the accessible foundation any future
 * map must enhance, never replace (DESIGN.md §7, §13).
 */

import { Market } from '../core/transacting-context';

/** One retail partner location: the public listing fields only. */
export interface StoreLocation {
  readonly id: string;
  /** Retailer name (plain text). */
  readonly retailer: string;
  /** Address lines as structured per-market lines (CC-ORD-002 formats are
   * server-side; the client renders the lines it is handed, in order). */
  readonly addressLines: readonly string[];
  /** City/town/locality (plain text). */
  readonly locality: string;
}

/**
 * The transacting market's retail partner list. Filtering happens SERVER-SIDE
 * off transacting-market state (CC-SEC-012; issue 078 AC-01): the client
 * never receives another market's partners and never filters a full list
 * itself (ARCHITECTURE.md, Dependency rule 1).
 */
export interface StoreLocationList {
  readonly market: Market;
  readonly locations: readonly StoreLocation[];
}
