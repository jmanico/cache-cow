/**
 * Step-up re-authentication prompt — SEAM ONLY (issue 079; CC-DSH-001).
 *
 * SECURITY.md, Authentication rule 2 requires re-auth for sensitive actions
 * (refunds, employee-record access, role changes). Those actions land with
 * the module issues (082–087), and the actual passkey ceremony is host/IdP
 * wiring (issue 060). This component is the presentation seam the modules
 * will render before a sensitive action: `confirmed` hands off to the
 * issue-060 re-auth flow; `dismissed` cancels. It performs NO authentication
 * itself and is not rendered anywhere by the shell.
 */

import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-step-up-prompt',
  templateUrl: './step-up-prompt.html',
  styleUrl: './step-up-prompt.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StepUpPrompt {
  protected readonly i18n = inject(DashboardI18n);

  /** The staff member chose to re-authenticate (issue 060 takes over). */
  readonly confirmed = output<void>();

  /** The staff member cancelled; the sensitive action must not proceed. */
  readonly dismissed = output<void>();
}
