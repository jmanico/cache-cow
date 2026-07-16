/**
 * Mock nav-policy fixture (issues 074/075) — a stand-in for the SERVER's
 * Market & Gating Policy responses until the real gating API lands (policy
 * model issue 023, enforcement API issue 025).
 *
 * This module SIMULATES THE SERVER: the per-market placement below is the
 * mock analogue of the policy-as-data configuration CC-MKT-006 requires —
 * it mirrors CC-MKT-005 exactly:
 *
 *   - IN: "Meet our Cows" is PRIMARY navigation; "Meet our Cuts" does not
 *     exist at all (not placed, not reachable).
 *   - All other markets: "Meet our Cows" lives under Our Story; Cuts is
 *     placed and reachable.
 *
 * None of this is client gating logic: the client consumes the payload
 * through the NavPolicyApi seam as an untrusted `unknown` response, exactly
 * as it will consume the real gating context's HTTP response
 * (ARCHITECTURE.md, Dependency rule 1). The REAL enforcement point — the one
 * that owns HTTP 404s, sitemap exclusion, and cached-variant keys — is the
 * server-side Market & Gating Policy service (issues 023/025/026/028).
 */

import { Market } from '../core/transacting-context';

/**
 * Mock server response for the transacting market's navigation policy.
 * Returned as `unknown`: the NavPolicyApi seam validates it like any
 * untrusted response.
 */
export function mockNavPolicyResponse(market: Market): unknown {
  if (market === 'IN') {
    // The India inversion (DESIGN.md §8.1; CC-MKT-005): cows promoted to
    // primary navigation, the cuts experience absent entirely.
    return {
      market,
      primary: ['menu', 'cows', 'stores'],
      ourStory: ['chefs'],
      reachable: ['menu', 'chefs', 'cows', 'stores'],
    };
  }
  return {
    market,
    primary: ['menu', 'stores'],
    ourStory: ['chefs', 'cows', 'cuts'],
    reachable: ['menu', 'chefs', 'cows', 'cuts', 'stores'],
  };
}
