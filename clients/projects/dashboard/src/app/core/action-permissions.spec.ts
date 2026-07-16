/**
 * ActionVisibility tests (issues 082/085): fail-closed with no authored
 * action matrix, per-action filtering with a clearly-marked TEST matrix.
 * Requirement tags: CC-DSH-002 (REQUIREMENTS.md §17).
 *
 * The matrix under test is TEST_ACTION_PERMISSION_MATRIX (core/testing.ts) —
 * a FIXTURE, not policy. Which roles may transition orders, refund, or
 * approve partners is an issue 080 human decision; these tests verify only
 * that the gating machinery honors whatever matrix is eventually authored.
 */

import { Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActionVisibility, provideActionPermissionMatrix } from './action-permissions';
import { provideStaffSessionSource } from './staff-session';
import { TEST_ACTION_PERMISSION_MATRIX, TestStaffSessionSource } from './testing';

function visibility(providers: Provider[]): ActionVisibility {
  TestBed.configureTestingModule({ providers });
  return TestBed.inject(ActionVisibility);
}

describe('ActionVisibility (CC-DSH-002 per-action gating)', () => {
  it('with NO action matrix configured, grants nothing — even to admin', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const v = visibility([provideStaffSessionSource(source)]);
    expect(v.visibleActionIds().size).toBe(0);
    expect(v.can('orders.refund')).toBe(false);
  });

  it('with a matrix but no session, grants nothing', () => {
    const v = visibility([provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX)]);
    expect(v.visibleActionIds().size).toBe(0);
  });

  it('grants exactly the role row: ops-agent may transition but NOT refund', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['ops-agent']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
    ]);
    expect(v.can('orders.transition')).toBe(true);
    expect(v.can('orders.refund')).toBe(false);
    expect(v.can('partners.approve')).toBe(false);
  });

  it('grants exactly the role row: finance may refund but NOT transition', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['finance']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
    ]);
    expect(v.can('orders.refund')).toBe(true);
    expect(v.can('orders.transition')).toBe(false);
  });

  it('unions grants across multiple roles', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['ops-agent', 'finance']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
    ]);
    expect([...v.visibleActionIds()].sort()).toEqual(['orders.refund', 'orders.transition']);
  });

  it('a role absent from the matrix gets nothing (fail closed, never a default grant)', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['sales-viewer']); // deliberately unlisted
    const v = visibility([
      provideStaffSessionSource(source),
      provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
    ]);
    expect(v.visibleActionIds().size).toBe(0);
  });

  it('reflects sign-out reactively: grants collapse to nothing', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const v = visibility([
      provideStaffSessionSource(source),
      provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
    ]);
    expect(v.visibleActionIds().size).toBe(3);
    source.signOut();
    expect(v.visibleActionIds().size).toBe(0);
  });
});
