/**
 * Session-expiry UX (issue 079; CC-DSH-001).
 *
 * SECURITY.md, Authentication and authorization rule 2 caps staff sessions
 * at 12 hours — ENFORCED SERVER-SIDE (issues 060/061). This service only
 * drives the client-side warning banner from a typed session-lifetime input
 * so staff are not surprised by the server ending their session. It never
 * extends, refreshes, or terminates a session itself.
 *
 * `SESSION_LIFETIME_MS` defaults to the ratified 12 hours;
 * `provideSessionLifetime(...)` is the host/test seam for overriding it
 * (e.g. when issue 060 wires the real session metadata, or short lifetimes
 * in tests).
 */

import {
  DestroyRef,
  Injectable,
  InjectionToken,
  Provider,
  computed,
  inject,
  signal,
} from '@angular/core';
import { StaffSession } from './staff-session';

/** 12-hour staff session cap (SECURITY.md, Authentication rule 2; ratified). */
export const DEFAULT_SESSION_LIFETIME_MS = 12 * 60 * 60 * 1000;

/** Warn this long before expiry (shell UX default; not a ratified value). */
export const DEFAULT_SESSION_WARN_BEFORE_MS = 30 * 60 * 1000;

/** How often the remaining-time clock re-evaluates. */
const CLOCK_TICK_MS = 30 * 1000;

export const SESSION_LIFETIME_MS = new InjectionToken<number>('cc-dashboard-session-lifetime-ms', {
  providedIn: 'root',
  factory: () => DEFAULT_SESSION_LIFETIME_MS,
});

export const SESSION_WARN_BEFORE_MS = new InjectionToken<number>(
  'cc-dashboard-session-warn-before-ms',
  {
    providedIn: 'root',
    factory: () => DEFAULT_SESSION_WARN_BEFORE_MS,
  },
);

/** Host/test wiring for the typed session-lifetime input. */
export function provideSessionLifetime(lifetimeMs: number, warnBeforeMs?: number): Provider[] {
  const providers: Provider[] = [{ provide: SESSION_LIFETIME_MS, useValue: lifetimeMs }];
  if (warnBeforeMs !== undefined) {
    providers.push({ provide: SESSION_WARN_BEFORE_MS, useValue: warnBeforeMs });
  }
  return providers;
}

export type SessionExpiryPhase = 'none' | 'expiring' | 'expired';

@Injectable({ providedIn: 'root' })
export class SessionExpiry {
  private readonly session = inject(StaffSession);
  private readonly lifetimeMs = inject(SESSION_LIFETIME_MS);
  private readonly warnBeforeMs = inject(SESSION_WARN_BEFORE_MS);

  private readonly now = signal(Date.now());

  constructor() {
    const handle = setInterval(() => this.now.set(Date.now()), CLOCK_TICK_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(handle));
  }

  /** Milliseconds until the server-side cap; null when unauthenticated. */
  readonly remainingMs = computed<number | null>(() => {
    const establishedAt = this.session.establishedAt();
    if (establishedAt === null) {
      return null;
    }
    return establishedAt + this.lifetimeMs - this.now();
  });

  readonly phase = computed<SessionExpiryPhase>(() => {
    const remaining = this.remainingMs();
    if (remaining === null || remaining > this.warnBeforeMs) {
      return 'none';
    }
    return remaining <= 0 ? 'expired' : 'expiring';
  });

  /** Whole minutes remaining, floored at 1 while still in the warning phase. */
  readonly minutesRemaining = computed<number>(() => {
    const remaining = this.remainingMs();
    return remaining === null ? 0 : Math.max(1, Math.ceil(remaining / 60_000));
  });
}
