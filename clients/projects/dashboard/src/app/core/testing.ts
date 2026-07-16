/**
 * TEST FIXTURES ONLY (issue 079). Never import this file from application
 * code — it exists so specs can exercise the fail-closed seams in
 * staff-session.ts and role-visibility.ts. Nothing here is wired into the
 * running app; the shipped defaults remain unauthenticated + no matrix.
 */

import { signal } from '@angular/core';
import { DASHBOARD_MODULE_IDS } from './modules';
import { RolePermissionMatrix } from './role-visibility';
import {
  DashboardRole,
  StaffSessionSource,
  StaffSessionState,
  UNAUTHENTICATED,
} from './staff-session';

/** Controllable session source for specs (the seam issue 060 will fill). */
export class TestStaffSessionSource implements StaffSessionSource {
  private readonly mutableState = signal<StaffSessionState>(UNAUTHENTICATED);
  readonly state = this.mutableState.asReadonly();

  /** Number of times the shell asked to begin sign-in (gate action). */
  signInRequests = 0;

  beginSignIn(): void {
    this.signInRequests += 1;
  }

  authenticateAs(roles: readonly DashboardRole[], establishedAt: number = Date.now()): void {
    this.mutableState.set({
      status: 'authenticated',
      identity: { subject: 'test-subject', displayName: 'Test Staff', roles },
      establishedAt,
    });
  }

  signOut(): void {
    this.mutableState.set(UNAUTHENTICATED);
  }
}

/**
 * TEST role-permission matrix — NOT the authored matrix. The real matrix is
 * an open decision (issue 080; epic open question 17) and, per CLAUDE.md,
 * may not be invented here. These grants exist only to verify that the
 * filtering/guard machinery honors whatever matrix is eventually authored.
 */
export const TEST_ROLE_PERMISSION_MATRIX: RolePermissionMatrix = {
  admin: [...DASHBOARD_MODULE_IDS],
  'hr-admin': ['employees'],
  'sales-viewer': ['sales'],
  'ops-agent': ['orders', 'inventory'],
  // 'finance' deliberately absent: an unlisted role gets nothing (fail closed).
};
