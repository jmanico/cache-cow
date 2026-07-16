import { Routes } from '@angular/router';
import { Chefs } from './pages/chefs/chefs';
import { Cows } from './pages/cows/cows';
import { Cuts } from './pages/cuts/cuts';
import { Home } from './pages/home/home';
import { Legal } from './pages/legal/legal';
import { Menu } from './pages/menu/menu';
import { NotFound } from './pages/not-found/not-found';
import { ProductDetail } from './pages/product-detail/product-detail';
import { Stores } from './pages/stores/stores';
import { Track } from './pages/track/track';

/**
 * Storefront routes: home (issue 063), menu (issue 066), product detail
 * (issue 067), order tracking (issue 070), the content pages — chefs (073),
 * cows (074), cuts (075), legal (077), store locator (078) — and the
 * "Signal lost" 404 catch-all. Remaining feature pages (cart, checkout) are
 * issues 068+. The server responds HTTP 404 for the catch-all (see
 * app.routes.server.ts) — unknown URLs are real 404s (the shape CC-MKT-004
 * requires of gated product URLs; the per-SKU 404 status for ungated
 * products is server-owned, issues 025/026).
 *
 * Market-gated routes are NOT gated here: /cuts exists in this route table
 * in every market and the page mirrors the server's policy decision by
 * rendering the 404 body in IN (CC-MKT-005). The real gating — the HTTP 404
 * status, sitemap exclusion, and cache-variant keys — is server-side
 * (issues 025/026/028). A client route table is not an enforcement point.
 */
export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'menu', component: Menu },
  { path: 'product/:sku', component: ProductDetail },
  // /track currently renders a mock order; guest capability-token handling
  // (CC-ORD-010, issue 042) is deferred — see pages/track/track.ts.
  { path: 'track', component: Track },
  { path: 'chefs', component: Chefs },
  { path: 'cows', component: Cows },
  { path: 'cuts', component: Cuts },
  { path: 'stores', component: Stores },
  // The per-market legal content set decides which :doc values resolve
  // (CC-CNT-005); one outside the set renders the 404 body (server owns the
  // 404 status).
  { path: 'legal/:doc', component: Legal },
  { path: '**', component: NotFound },
];
