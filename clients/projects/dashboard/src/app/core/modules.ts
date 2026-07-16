/**
 * Dashboard module registry (issue 079; CC-DSH-003).
 *
 * The six launch modules. The shell provides navigation and placeholder
 * routes only — each module's content lands in its own issue (082–087).
 * Visibility is decided by RoleVisibility (role-permission matrix seam);
 * this registry never grants access by itself.
 */

import { DashboardMessageKey } from '../i18n/i18n.service';

export const DASHBOARD_MODULE_IDS = [
  'sales',
  'orders',
  'invoices',
  'inventory',
  'partners',
  'employees',
] as const;

export type DashboardModuleId = (typeof DASHBOARD_MODULE_IDS)[number];

export interface DashboardModuleDef {
  readonly id: DashboardModuleId;
  /** Route path segment under the shell root. */
  readonly path: string;
  readonly nameKey: DashboardMessageKey;
  /** The epic sub-issue that delivers the module's content. */
  readonly pendingIssue: string;
}

/** Navigation order follows the CC-DSH-003 module list. */
export const DASHBOARD_MODULES: readonly DashboardModuleDef[] = [
  { id: 'sales', path: 'sales', nameKey: 'module.sales.name', pendingIssue: '083' },
  { id: 'orders', path: 'orders', nameKey: 'module.orders.name', pendingIssue: '082' },
  { id: 'invoices', path: 'invoices', nameKey: 'module.invoices.name', pendingIssue: '086' },
  { id: 'inventory', path: 'inventory', nameKey: 'module.inventory.name', pendingIssue: '084' },
  { id: 'partners', path: 'partners', nameKey: 'module.partners.name', pendingIssue: '085' },
  { id: 'employees', path: 'employees', nameKey: 'module.employees.name', pendingIssue: '087' },
];

export const DASHBOARD_MODULE_BY_ID: Readonly<Record<DashboardModuleId, DashboardModuleDef>> =
  Object.fromEntries(DASHBOARD_MODULES.map((m) => [m.id, m])) as Record<
    DashboardModuleId,
    DashboardModuleDef
  >;
