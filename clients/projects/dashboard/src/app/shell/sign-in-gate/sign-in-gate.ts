/**
 * Sign-in-required gate (issue 079; CC-DSH-001).
 *
 * Rendered whenever there is no authenticated staff session. Deliberately
 * NOT a login form: staff authenticate only via SSO with mandatory WebAuthn
 * passkeys at Microsoft Entra ID (SECURITY.md, Authentication rule 2), and
 * that redirect is host wiring delivered by issue 060 through the
 * `StaffSessionSource.beginSignIn()` seam (core/staff-session.ts). The
 * single action button calls that seam and nothing else — no credentials
 * are ever collected client-side, and no storefront/portal session can
 * satisfy the gate (CC-SEC-011; SECURITY.md, HTTP boundary rule 8).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { StaffSession } from '../../core/staff-session';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-sign-in-gate',
  templateUrl: './sign-in-gate.html',
  styleUrl: './sign-in-gate.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInGate {
  protected readonly session = inject(StaffSession);
  protected readonly i18n = inject(DashboardI18n);

  protected beginSignIn(): void {
    this.session.beginSignIn();
  }
}
