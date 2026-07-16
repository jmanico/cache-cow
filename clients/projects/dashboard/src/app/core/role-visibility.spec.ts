/**
 * RoleVisibility tests (issue 079): fail-closed with no matrix (mirrors the
 * server's unauthored-matrix posture), role filtering with a TEST matrix.
 * Requirement tags: CC-DSH-002 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { RoleVisibility, provideRolePermissionMatrix } from './role-visibility';
import { provideStaffSessionSource } from './staff-session';
import { TEST_ROLE_PERMISSION_MATRIX, TestStaffSessionSource } from './testing';

function visibility(providers: Provider[]): RoleVisibility {
  TestBed.configureTestingModule({ providers });
  return TestBed.inject(RoleVisibility);
}

describe('RoleVisibility (CC-DSH-002)', () => {
  it('with NO matrix configured, reports unconfigured and grants nothing — even to admin', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const v = visibility([provideStaffSessionSource(source)]);
    expect(v.matrixConfigured).toBe(false);
    expect(v.visibleModuleIds().size).toBe(0);
    expect(v.canAccess('sales')).toBe(false);
  });

  it('with a matrix but no session, grants nothing', () => {
    const v = visibility([provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX)]);
    expect(v.matrixConfigured).toBe(true);
    expect(v.visibleModuleIds().size).toBe(0);
  });

  it('grants exactly the role rows: hr-admin gets employees only', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['hr-admin']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    expect([...v.visibleModuleIds()]).toEqual(['employees']);
    expect(v.canAccess('employees')).toBe(true);
    expect(v.canAccess('partners')).toBe(false);
  });

  it('unions grants across multiple roles', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['sales-viewer', 'ops-agent']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    expect([...v.visibleModuleIds()].sort()).toEqual(['inventory', 'orders', 'sales']);
  });

  it('a role absent from the matrix gets nothing (fail closed, never a default grant)', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['finance']); // deliberately unlisted in the TEST matrix
    const v = visibility([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    expect(v.visibleModuleIds().size).toBe(0);
  });

  it('reflects sign-out reactively: grants collapse to nothing', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    expect(v.visibleModuleIds().size).toBe(6);
    source.signOut();
    expect(v.visibleModuleIds().size).toBe(0);
  });
});
