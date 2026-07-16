/**
 * Typed navigation-placement policy model (issues 074/075; CC-MKT-005,
 * CC-MKT-006).
 *
 * Navigation placement is MARKET GATING POLICY, encoded as data and owned
 * server-side by the Market & Gating Policy bounded context (ARCHITECTURE.md,
 * bounded context 1; issues 023/025). This is the client half of that
 * contract, consumed only through the NavPolicyApi seam: the client renders
 * whatever placement the policy response declares and holds NO market
 * conditionals of its own (ARCHITECTURE.md, Dependency rule 1 — clients never
 * gate; CC-MKT-006 — no scattered `if (market === 'IN')`).
 *
 * The policy carries both PLACEMENT (primary nav vs. the Our Story group,
 * CC-MKT-005) and REACHABILITY (`reachable` — the gated content routes that
 * exist at all in the transacting market). "Meet our Cuts" is absent from
 * both lists AND from `reachable` in the IN market; real enforcement (HTTP
 * 404 on the route, sitemap exclusion) is server-side (issues 025/026) — the
 * client only mirrors the decision it is handed.
 */

import { Market } from '../core/transacting-context';

/** Content/navigation surfaces the policy places (DESIGN.md §10). */
export const NAV_PAGES = ['menu', 'chefs', 'cows', 'cuts', 'stores'] as const;
export type NavPage = (typeof NAV_PAGES)[number];

export interface NavPolicy {
  readonly market: Market;
  /** Pages linked in primary navigation, in order (IN promotes 'cows' here — CC-MKT-005). */
  readonly primary: readonly NavPage[];
  /** Pages linked under the "Our Story" group (non-IN home of 'cows' — CC-MKT-005). */
  readonly ourStory: readonly NavPage[];
  /**
   * Every gated content route that EXISTS in this market. 'cuts' is absent
   * for IN (CC-MKT-005): the route renders 404 there (server-owned status;
   * the client mirrors by rendering the 404 page).
   */
  readonly reachable: readonly NavPage[];
}
