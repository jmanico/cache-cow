/**
 * Staff session seam (issue 079; CC-DSH-001, CC-DSH-002).
 *
 * The dashboard authenticates staff via SSO with MANDATORY WebAuthn passkeys
 * against Microsoft Entra ID (SECURITY.md, Authentication and authorization
 * rule 2; ARCHITECTURE.md, "Authentication model"). The SSO/WebAuthn
 * ceremonies and host/IdP wiring are issue 060 — NOT implemented here. This
 * file defines the typed seam that wiring plugs into:
 *
 *   - `StaffSessionSource` — the injectable provider contract. Issue 060
 *     registers the real Entra-backed source via
 *     `provideStaffSessionSource(...)`; its `beginSignIn()` initiates the
 *     SSO redirect (there is NO login form anywhere in the dashboard, and no
 *     client-side credential handling).
 *   - `UnauthenticatedStaffSessionSource` — the DEFAULT source. Fail closed
 *     (SECURITY.md, Logging rule 2): with no host wiring the shell renders
 *     only the sign-in gate; the dashboard never degrades to an
 *     unauthenticated or storefront-session-authenticated mode. Storefront
 *     and portal sessions can never satisfy this seam — the session scopes
 *     are disjoint by construction (CC-SEC-011; SECURITY.md, HTTP boundary
 *     rule 8).
 *   - `StaffSession` — the signal facade the shell consumes (authenticated
 *     staff identity, role claims, session establishment time).
 *
 * The 12-hour session lifetime cap and re-auth for sensitive actions
 * (SECURITY.md, Authentication rule 2) are enforced server-side; the client
 * surfaces them via SessionExpiryBanner and the StepUpPrompt seam.
 */

import {
  Injectable,
  InjectionToken,
  Provider,
  Signal,
  computed,
  inject,
  signal,
} from '@angular/core';

/** The minimum dashboard roles (CC-DSH-002). */
export const DASHBOARD_ROLES = ['sales-viewer', 'ops-agent', 'finance', 'hr-admin', 'admin'] as const;
export type DashboardRole = (typeof DASHBOARD_ROLES)[number];

/** Authenticated staff identity as asserted by the IdP seam (issue 060). */
export interface StaffIdentity {
  /** Stable subject identifier from the IdP (never displayed). */
  readonly subject: string;
  readonly displayName: string;
  /** Role claims; authorization decisions also happen server-side (rule 8). */
  readonly roles: readonly DashboardRole[];
}

export type StaffSessionState =
  | { readonly status: 'unauthenticated' }
  | {
      readonly status: 'authenticated';
      readonly identity: StaffIdentity;
      /** Epoch ms when the server established the session (drives expiry UX). */
      readonly establishedAt: number;
    };

export const UNAUTHENTICATED: StaffSessionState = { status: 'unauthenticated' };

/** The injectable provider contract issue 060 implements. */
export interface StaffSessionSource {
  readonly state: Signal<StaffSessionState>;
  /**
   * Begin staff sign-in. The real source initiates the Entra ID SSO redirect
   * (passkey ceremony at the IdP — issue 060). Never collects credentials.
   */
  beginSignIn(): void;
}

/** Default source: no session, sign-in is a documented no-op seam. */
export class UnauthenticatedStaffSessionSource implements StaffSessionSource {
  readonly state = signal<StaffSessionState>(UNAUTHENTICATED).asReadonly();

  beginSignIn(): void {
    // Intentionally empty: the SSO redirect is the host's job (issue 060).
    // Without host wiring the gate stays up — fail closed, no fallback flow.
  }
}

export const STAFF_SESSION_SOURCE = new InjectionToken<StaffSessionSource>(
  'cc-dashboard-staff-session-source',
  {
    providedIn: 'root',
    // Fail-closed default: unauthenticated until issue 060 provides the
    // real Entra-backed source.
    factory: () => new UnauthenticatedStaffSessionSource(),
  },
);

/** Host wiring entry point (issue 060) — and the test seam. */
export function provideStaffSessionSource(source: StaffSessionSource): Provider {
  return { provide: STAFF_SESSION_SOURCE, useValue: source };
}

@Injectable({ providedIn: 'root' })
export class StaffSession {
  private readonly source = inject(STAFF_SESSION_SOURCE);

  readonly state = computed(() => this.source.state());
  readonly authenticated = computed(() => this.state().status === 'authenticated');

  readonly identity = computed<StaffIdentity | null>(() => {
    const s = this.state();
    return s.status === 'authenticated' ? s.identity : null;
  });

  readonly roles = computed<readonly DashboardRole[]>(() => this.identity()?.roles ?? []);

  readonly establishedAt = computed<number | null>(() => {
    const s = this.state();
    return s.status === 'authenticated' ? s.establishedAt : null;
  });

  beginSignIn(): void {
    this.source.beginSignIn();
  }
}
