/**
 * Route-guard tests (issue 079): direct navigation is denied exactly like
 * nav visibility — fail closed with no session or no matrix.
 * Requirement tags: CC-DSH-001, CC-DSH-002 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { routes } from '../app.routes';
import { provideRolePermissionMatrix } from './role-visibility';
import { provideStaffSessionSource } from './staff-session';
import { TEST_ROLE_PERMISSION_MATRIX, TestStaffSessionSource } from './testing';

function setup(providers: Provider[]): Router {
  TestBed.configureTestingModule({
    providers: [provideRouter(routes), ...providers],
  });
  return TestBed.inject(Router);
}

describe('moduleAccessGuard (direct-navigation RBAC mirror)', () => {
  it('denies every module route when unauthenticated (shipped default)', async () => {
    const router = setup([provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX)]);
    await router.navigateByUrl('/employees');
    expect(router.url).toBe('/');
  });

  it('denies every module route when no matrix is configured, even for admin', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const router = setup([provideStaffSessionSource(source)]);
    await router.navigateByUrl('/sales');
    expect(router.url).toBe('/');
  });

  it('allows a granted module: hr-admin reaches /employees', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['hr-admin']);
    const router = setup([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    await router.navigateByUrl('/employees');
    expect(router.url).toBe('/employees');
  });

  it('blocks an ungranted module: sales-viewer cannot reach /partners', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['sales-viewer']);
    const router = setup([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    await router.navigateByUrl('/partners');
    expect(router.url).toBe('/');
  });

  it('redirects unknown paths to the shell root (no existence disclosure)', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const router = setup([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    await router.navigateByUrl('/no-such-module');
    expect(router.url).toBe('/');
  });
});
