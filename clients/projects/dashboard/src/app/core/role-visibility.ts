/**
 * RBAC-aware navigation visibility (issue 079; CC-DSH-002, downstream 080).
 *
 * The real role-permission matrix is UNAUTHORED (epic open question 17 /
 * issue 080 "Dashboard RBAC: role-permission matrix and enforcement") and,
 * per CLAUDE.md, may not be invented here. The seam therefore defaults to
 * NULL and the shell fails closed exactly like the server's unauthored-matrix
 * posture: with no matrix configured, ALL module navigation is hidden and the
 * shell shows an "awaiting authorization configuration" state.
 *
 * This is presentation-layer filtering only. The server enforces RBAC on
 * every dashboard endpoint regardless of what the client shows (SECURITY.md,
 * Authentication and authorization rule 8; deny-by-default, rule 1). Hiding
 * a nav entry is never the access control.
 */

import { Injectable, InjectionToken, Provider, computed, inject } from '@angular/core';
import { DashboardModuleId } from './modules';
import { DashboardRole, StaffSession } from './staff-session';

/**
 * Role -> permitted module ids. Partial: a role absent from the matrix has
 * no modules (fail closed), never a default grant.
 */
export type RolePermissionMatrix = Readonly<
  Partial<Record<DashboardRole, readonly DashboardModuleId[]>>
>;

export const ROLE_PERMISSION_MATRIX = new InjectionToken<RolePermissionMatrix | null>(
  'cc-dashboard-role-permission-matrix',
  {
    providedIn: 'root',
    // Unauthored by default — fail closed (see file header).
    factory: () => null,
  },
);

/** Wiring entry point for issue 080 (and the test seam). */
export function provideRolePermissionMatrix(matrix: RolePermissionMatrix): Provider {
  return { provide: ROLE_PERMISSION_MATRIX, useValue: matrix };
}

@Injectable({ providedIn: 'root' })
export class RoleVisibility {
  private readonly session = inject(StaffSession);
  private readonly matrix = inject(ROLE_PERMISSION_MATRIX);

  /** False until issue 080 wires a matrix; drives the awaiting-config state. */
  readonly matrixConfigured: boolean = this.matrix !== null;

  /** Modules visible to the current session: union across the staff roles. */
  readonly visibleModuleIds = computed<ReadonlySet<DashboardModuleId>>(() => {
    const allowed = new Set<DashboardModuleId>();
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

  canAccess(id: DashboardModuleId): boolean {
    return this.visibleModuleIds().has(id);
  }
}
