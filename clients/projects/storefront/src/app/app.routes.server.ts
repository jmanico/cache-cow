import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Every route is server-rendered per request (RenderMode.Server, not
 * Prerender): the rendered market/locale are per-request server state
 * (issue 024), so baking one variant at build time would be wrong — and any
 * cacheable variant must be keyed on transacting market + locale, never on a
 * client hint (CC-MKT-009/CC-SEC-013; SECURITY.md, HTTP boundary rule 10 —
 * the cache/CDN wiring itself is issue 028).
 *
 * The catch-all returns HTTP 404 with the "Signal lost" page: fail closed
 * with a real 404 status — the hardening default of SECURITY.md,
 * Authentication rule 9, and the behavior CC-MKT-004 requires of gated
 * product URLs later.
 */
export const serverRoutes: ServerRoute[] = [
  {
    path: '',
    renderMode: RenderMode.Server,
  },
  {
    path: 'menu',
    renderMode: RenderMode.Server,
  },
  {
    // Per-request render: the gated product (and its 404-vs-render outcome)
    // is per-market server state. The HTTP 404 status for a SKU absent from
    // the transacting market's catalog is owned by the server gating API
    // (CC-MKT-004, issues 025/026); until it lands, an unknown SKU renders
    // the 404 page body here.
    path: 'product/:sku',
    renderMode: RenderMode.Server,
  },
  {
    // Personalized response: never edge-cached, Cache-Control: no-store is
    // server wiring (SECURITY.md, HTTP boundary rule 10; issue 028).
    path: 'track',
    renderMode: RenderMode.Server,
  },
  {
    // Shared roster, bios localized per request (CC-CNT-001).
    path: 'chefs',
    renderMode: RenderMode.Server,
  },
  {
    // Nav placement differs per market (CC-MKT-005), so the response varies
    // by transacting market: per-request render, never a prerendered variant.
    path: 'cows',
    renderMode: RenderMode.Server,
  },
  {
    // Gated experience (CC-MKT-005): the IN market must receive HTTP 404 —
    // not 403, not a redirect. That STATUS is owned by the server gating API
    // (issues 025/026); until it lands, the IN request renders the 404 page
    // body with a 200 status. The status gap is server-side work, tracked
    // there — see pages/cuts/cuts.ts.
    path: 'cuts',
    renderMode: RenderMode.Server,
  },
  {
    // Market-filtered partner list (issue 078 AC-01/AC-05).
    path: 'stores',
    renderMode: RenderMode.Server,
  },
  {
    // Per-market, per-locale legal content set (CC-CNT-005): any cacheable
    // variant keys on server-side transacting market + locale (CC-MKT-009).
    path: 'legal/:doc',
    renderMode: RenderMode.Server,
  },
  {
    path: '**',
    renderMode: RenderMode.Server,
    status: 404,
  },
];
