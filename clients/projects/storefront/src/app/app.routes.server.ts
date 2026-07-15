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
    path: '**',
    renderMode: RenderMode.Server,
    status: 404,
  },
];
