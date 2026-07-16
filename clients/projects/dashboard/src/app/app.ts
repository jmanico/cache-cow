/**
 * Dashboard shell root (issue 079; CC-SEC-011, CC-DSH-001, CC-DSH-003).
 *
 * Renders the sign-in gate until an authenticated staff session exists
 * (fail closed — the shipped default StaffSessionSource is unauthenticated;
 * issue 060 wires the real Entra SSO/passkey source). Once authenticated,
 * renders the Pit-themed chrome: header, session-expiry banner, RBAC-aware
 * side nav, and the routed module area.
 *
 * Isolation (CC-SEC-011; SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md,
 * Dependency rule 4): this application imports NOTHING from the storefront
 * or portal projects. The separate origin, VPN-only network path, and
 * distinct cookie scope are deployment/ingress configuration (issues
 * 008–017, 061) — noted here, not implementable client-side.
 *
 * CSP-compatible by construction: no inline event handlers, no inline
 * styles, no [innerHTML]/bypassSecurityTrust* (SECURITY.md, HTTP boundary
 * rule 2; Input validation rule 5).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { StaffSession } from './core/staff-session';
import { DashboardI18n } from './i18n/i18n.service';
import { SessionExpiryBanner } from './shell/session-expiry-banner/session-expiry-banner';
import { SideNav } from './shell/side-nav/side-nav';
import { SignInGate } from './shell/sign-in-gate/sign-in-gate';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, SignInGate, SideNav, SessionExpiryBanner],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly session = inject(StaffSession);
  protected readonly i18n = inject(DashboardI18n);
}
