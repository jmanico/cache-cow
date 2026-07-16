/**
 * Dashboard shell routes (issue 079; CC-DSH-003).
 *
 * One route per launch module, every one behind moduleAccessGuard so direct
 * navigation is denied exactly like nav visibility (fail closed with no
 * matrix; SECURITY.md, Logging rule 2). Unknown paths return to the shell
 * root rather than confirming what exists (hardening default per
 * SECURITY.md, Authentication rule 9).
 *
 * Modules whose content has landed resolve to their own lazily-loaded page
 * (orders 082, inventory 084, partners 085); the rest keep the issue-079
 * placeholder until their issue delivers them (sales 083, invoices 086,
 * employees 087). Lazy loading keeps a module's code out of the shell bundle
 * for staff whose roles never reach it — a bundling nicety, never the access
 * control, which is the guard here plus RBAC server-side (SECURITY.md,
 * Authentication rules 1 and 8).
 */

import { Type } from '@angular/core';
import { Routes } from '@angular/router';
import { moduleAccessGuard } from './core/module-access-guard';
import { DASHBOARD_MODULES, DashboardModuleId } from './core/modules';
import { Home } from './pages/home/home';
import { ModulePlaceholder } from './pages/module-placeholder/module-placeholder';

/** Delivered module pages; an id absent here still renders the placeholder. */
const MODULE_PAGES: Partial<Record<DashboardModuleId, () => Promise<Type<unknown>>>> = {
  orders: () => import('./modules/orders/orders-page').then((m) => m.OrdersPage),
  inventory: () => import('./modules/inventory/inventory-page').then((m) => m.InventoryPage),
  partners: () => import('./modules/partners/partners-page').then((m) => m.PartnersPage),
};

export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  ...DASHBOARD_MODULES.map((module) => {
    const loadComponent = MODULE_PAGES[module.id];
    return {
      path: module.path,
      ...(loadComponent === undefined ? { component: ModulePlaceholder } : { loadComponent }),
      canActivate: [moduleAccessGuard],
      data: { moduleId: module.id },
    };
  }),
  { path: '**', redirectTo: '' },
];
