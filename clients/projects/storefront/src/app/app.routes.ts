import { Routes } from '@angular/router';
import { Home } from './pages/home/home';
import { NotFound } from './pages/not-found/not-found';

/**
 * Shell routes (issue 063): home placeholder and the "Signal lost" 404
 * catch-all. Feature pages (menu, PDP, cart, checkout, content pages) are
 * issues 066–078. The server responds HTTP 404 for the catch-all (see
 * app.routes.server.ts) — unknown URLs are real 404s (the shape CC-MKT-004
 * requires of gated product URLs once they exist).
 */
export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: '**', component: NotFound },
];
