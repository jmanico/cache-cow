/**
 * Route guard mirroring nav visibility (issue 079; CC-DSH-002).
 *
 * Client-side courtesy only — the server enforces RBAC on every endpoint
 * (SECURITY.md, Authentication and authorization rules 1 and 8). Fails
 * closed (Logging rule 2): a missing/unknown module id or an unauthorized
 * module denies and returns to the shell root.
 */

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { DASHBOARD_MODULE_BY_ID, DashboardModuleId } from './modules';
import { RoleVisibility } from './role-visibility';

export const moduleAccessGuard: CanActivateFn = (route) => {
  const visibility = inject(RoleVisibility);
  const router = inject(Router);
  const moduleId = route.data['moduleId'] as DashboardModuleId | undefined;
  const known = moduleId !== undefined && moduleId in DASHBOARD_MODULE_BY_ID;
  return known && visibility.canAccess(moduleId) ? true : router.parseUrl('/');
};
