/**
 * SessionExpiry tests (issue 079): typed lifetime seam (12h ratified
 * default), warning/expired phases.
 * Requirement tags: CC-DSH-001 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import {
  DEFAULT_SESSION_LIFETIME_MS,
  SESSION_LIFETIME_MS,
  SessionExpiry,
  provideSessionLifetime,
} from './session-expiry';
import { provideStaffSessionSource } from './staff-session';
import { TestStaffSessionSource } from './testing';

function expiry(providers: Provider[]): SessionExpiry {
  TestBed.configureTestingModule({ providers });
  return TestBed.inject(SessionExpiry);
}

describe('SessionExpiry (SECURITY.md, Authentication rule 2)', () => {
  it('defaults the lifetime seam to the ratified 12 hours', () => {
    TestBed.configureTestingModule({});
    expect(TestBed.inject(SESSION_LIFETIME_MS)).toBe(DEFAULT_SESSION_LIFETIME_MS);
    expect(DEFAULT_SESSION_LIFETIME_MS).toBe(12 * 60 * 60 * 1000);
  });

  it('reports nothing while unauthenticated', () => {
    const e = expiry([provideSessionLifetime(60_000)]);
    expect(e.remainingMs()).toBeNull();
    expect(e.phase()).toBe('none');
  });

  it('is quiet while a fresh session is far from its cap', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const e = expiry([provideStaffSessionSource(source)]);
    expect(e.phase()).toBe('none');
    expect(e.remainingMs()).toBeGreaterThan(DEFAULT_SESSION_LIFETIME_MS - 60_000);
  });

  it('enters the warning phase inside the warn window (short test lifetime)', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const e = expiry([provideStaffSessionSource(source), provideSessionLifetime(60_000, 120_000)]);
    expect(e.phase()).toBe('expiring');
    expect(e.minutesRemaining()).toBe(1);
  });

  it('reports expired once the lifetime has elapsed', () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin'], Date.now() - 120_000);
    const e = expiry([provideStaffSessionSource(source), provideSessionLifetime(60_000)]);
    expect(e.phase()).toBe('expired');
  });
});
