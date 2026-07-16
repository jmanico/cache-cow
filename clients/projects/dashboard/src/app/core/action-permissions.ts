/**
 * Per-ACTION permission visibility (issues 082/085; CC-DSH-002).
 *
 * Extends the RoleVisibility pattern (core/role-visibility.ts) from module
 * navigation down to individual privileged actions inside a module: order
 * state transitions, refunds, and partner approval-workflow actions.
 *
 * The real role→permission matrix is UNAUTHORED (issue 080 "Dashboard RBAC:
 * role-permission matrix and enforcement"; epic open question) and, per
 * CLAUDE.md, may not be invented here. The seam therefore defaults to NULL
 * and fails closed: with no action matrix configured, NO privileged action
 * button renders anywhere. Which roles hold which permissions — in
 * particular who may issue refunds and who may approve partners — is issue
 * 080's decision (issue 082/085 open questions).
 *
 * Action-permission granularity note: partner approve/reject/suspend are
 * gated by the single `partners.approve` permission pending the authored
 * matrix; finer per-action ids can replace it when issue 080 lands.
 *
 * This is presentation-layer filtering only. The server enforces
 * authorization on every action endpoint regardless of what the client
 * shows (SECURITY.md, Authentication and authorization rules 1 and 8);
 * hiding a button is never the access control.
 */

import { Injectable, InjectionToken, Provider, computed, inject } from '@angular/core';
import { DashboardRole, StaffSession } from './staff-session';

/** Privileged in-module actions delivered by issues 082/085. */
export const DASHBOARD_ACTION_IDS = [
  'orders.transition',
  'orders.refund',
  'partners.approve',
] as const;

export type DashboardActionId = (typeof DASHBOARD_ACTION_IDS)[number];

/**
 * Role -> permitted action ids. Partial: a role absent from the matrix has
 * no actions (fail closed), never a default grant.
 */
export type ActionPermissionMatrix = Readonly<
  Partial<Record<DashboardRole, readonly DashboardActionId[]>>
>;

export const ACTION_PERMISSION_MATRIX = new InjectionToken<ActionPermissionMatrix | null>(
  'cc-dashboard-action-permission-matrix',
  {
    providedIn: 'root',
    // Unauthored by default — fail closed (see file header).
    factory: () => null,
  },
);

/** Wiring entry point for issue 080 (and the test seam). */
export function provideActionPermissionMatrix(matrix: ActionPermissionMatrix): Provider {
  return { provide: ACTION_PERMISSION_MATRIX, useValue: matrix };
}

@Injectable({ providedIn: 'root' })
export class ActionVisibility {
  private readonly session = inject(StaffSession);
  private readonly matrix = inject(ACTION_PERMISSION_MATRIX);

  /** Actions visible to the current session: union across the staff roles. */
  readonly visibleActionIds = computed<ReadonlySet<DashboardActionId>>(() => {
    const allowed = new Set<DashboardActionId>();
    if (this.matrix === null || !this.session.authenticated()) {
      return allowed; // fail closed: no matrix or no session => nothing
    }
    for (const role of this.session.roles()) {
      for (const id of this.matrix[role] ?? []) {
        allowed.add(id);
      }
    }
    return allowed;
  });

  can(id: DashboardActionId): boolean {
    return this.visibleActionIds().has(id);
  }
}
