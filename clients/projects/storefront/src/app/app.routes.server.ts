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
    path: '**',
    renderMode: RenderMode.Server,
    status: 404,
  },
];
