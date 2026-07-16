/**
 * StaffSession seam tests (issue 079): fail-closed default source, typed
 * facade over the issue-060 provider seam.
 * Requirement tags: CC-DSH-001, CC-SEC-011 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import {
  StaffSession,
  UnauthenticatedStaffSessionSource,
  provideStaffSessionSource,
} from './staff-session';
import { TestStaffSessionSource } from './testing';

describe('StaffSession (CC-DSH-001 seam)', () => {
  it('defaults to unauthenticated with no roles and no identity (fail closed)', () => {
    TestBed.configureTestingModule({});
    const session = TestBed.inject(StaffSession);
    expect(session.authenticated()).toBe(false);
    expect(session.identity()).toBeNull();
    expect(session.roles()).toEqual([]);
    expect(session.establishedAt()).toBeNull();
  });

  it('the default source treats sign-in as a documented no-op (SSO wiring is issue 060)', () => {
    const source = new UnauthenticatedStaffSessionSource();
    source.beginSignIn();
    expect(source.state().status).toBe('unauthenticated');
  });

  it('exposes identity, roles, and establishment time from a wired source', () => {
    const source = new TestStaffSessionSource();
    const established = Date.now() - 1000;
    source.authenticateAs(['finance', 'ops-agent'], established);
    TestBed.configureTestingModule({ providers: [provideStaffSessionSource(source)] });
    const session = TestBed.inject(StaffSession);
    expect(session.authenticated()).toBe(true);
    expect(session.identity()?.displayName).toBe('Test Staff');
    expect(session.roles()).toEqual(['finance', 'ops-agent']);
    expect(session.establishedAt()).toBe(established);
  });

  it('delegates beginSignIn to the wired source', () => {
    const source = new TestStaffSessionSource();
    TestBed.configureTestingModule({ providers: [provideStaffSessionSource(source)] });
    TestBed.inject(StaffSession).beginSignIn();
    expect(source.signInRequests).toBe(1);
  });
});
