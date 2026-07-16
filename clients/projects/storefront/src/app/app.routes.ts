import { Routes } from '@angular/router';
import { Home } from './pages/home/home';
import { Menu } from './pages/menu/menu';
import { NotFound } from './pages/not-found/not-found';
import { ProductDetail } from './pages/product-detail/product-detail';
import { Track } from './pages/track/track';

/**
 * Storefront routes: home (issue 063), menu (issue 066), product detail
 * (issue 067), order tracking (issue 070), and the "Signal lost" 404
 * catch-all. Remaining feature pages (cart, checkout, content pages) are
 * issues 068+. The server responds HTTP 404 for the catch-all (see
 * app.routes.server.ts) — unknown URLs are real 404s (the shape CC-MKT-004
 * requires of gated product URLs; the per-SKU 404 status for ungated
 * products is server-owned, issues 025/026).
 */
export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'menu', component: Menu },
  { path: 'product/:sku', component: ProductDetail },
  // /track currently renders a mock order; guest capability-token handling
  // (CC-ORD-010, issue 042) is deferred — see pages/track/track.ts.
  { path: 'track', component: Track },
  { path: '**', component: NotFound },
];
