/**
 * Partner (wholesale) management module page (issue 085; CC-DSH-003,
 * CC-WHS-002, CC-WHS-004, CC-DSH-004; DESIGN.md §12).
 *
 * Partner list with onboarding-state badges, and a detail view whose
 * business identity renders MASKED ONLY — the seam never delivers a full
 * identifier and the parser rejects a payload that carries one
 * (partners.types.ts, partners.validate.ts). A reveal flow is NOT
 * implemented: no canonical document authors one for partner identity, and
 * per CLAUDE.md that decision is a human's (flagged in partners.types.ts).
 *
 * Actions (approve / reject / suspend) render only when BOTH hold:
 *  - the SERVER offers the action in `allowedActions` (issue-049 workflow —
 *    the client never computes which workflow steps are legal), and
 *  - the session holds the matching action permission
 *    (core/action-permissions.ts, fail closed with no authored matrix).
 *
 * Each action confirms first, in plain voice with the consequence stated —
 * no puns anywhere near an irreversible workflow decision (DESIGN.md §§5.4,
 * 9). The confirmation is UI friction, never the access control: the server
 * authorizes and audits every action regardless (SECURITY.md, Authentication
 * rules 1 and 8; CC-DSH-004).
 *
 * Whether partner approval also requires step-up re-auth is an OPEN question
 * (issue 085): SECURITY.md, Authentication rule 2 enumerates refunds,
 * employee-record access, and role changes — partner actions are not stated
 * either way, so StepUpPrompt is deliberately NOT wired here. That is a
 * human decision, not an omission to fill in.
 */

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActionVisibility } from '../../core/action-permissions';
import { formatTimestamp } from '../../core/format-minor-units';
import { RowNav } from '../../core/row-nav';
import { DashboardI18n, DashboardMessageKey } from '../../i18n/i18n.service';
import { PartnersApi } from './partners.api';
import { PartnerAction, PartnerDetail, PartnerState, PartnerSummary } from './partners.types';

/** State -> presentation: one accent family per meaning (DESIGN.md §12). */
const STATE_PRESENTATION: Readonly<
  Record<PartnerState, { readonly labelKey: DashboardMessageKey; readonly accent: string }>
> = {
  pending: { labelKey: 'partners.state.pending', accent: 'cc-status-neutral' },
  approved: { labelKey: 'partners.state.approved', accent: 'cc-status-good' },
  rejected: { labelKey: 'partners.state.rejected', accent: 'cc-status-neutral' },
  suspended: { labelKey: 'partners.state.suspended', accent: 'cc-status-alert' },
};

const ACTION_LABEL: Readonly<Record<PartnerAction, DashboardMessageKey>> = {
  approve: 'partners.actions.approve',
  reject: 'partners.actions.reject',
  suspend: 'partners.actions.suspend',
};

const ACTION_CONFIRM: Readonly<Record<PartnerAction, DashboardMessageKey>> = {
  approve: 'partners.confirm.approve',
  reject: 'partners.confirm.reject',
  suspend: 'partners.confirm.suspend',
};

@Component({
  selector: 'app-partners-page',
  imports: [RowNav],
  templateUrl: './partners-page.html',
  styleUrl: './partners-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnersPage {
  protected readonly i18n = inject(DashboardI18n);
  protected readonly actions = inject(ActionVisibility);
  private readonly api = inject(PartnersApi);

  protected readonly partners = signal<readonly PartnerSummary[]>([]);
  protected readonly detail = signal<PartnerDetail | null>(null);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  /** The action awaiting confirmation, if any. */
  protected readonly pendingAction = signal<PartnerAction | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loadError.set(false);
    this.api.list().subscribe({
      next: (result) => this.partners.set(result.partners),
      error: () => {
        // Fail closed: nothing renders from a rejected payload.
        this.partners.set([]);
        this.loadError.set(true);
      },
    });
  }

  protected open(partnerRef: string): void {
    this.actionError.set(false);
    this.pendingAction.set(null);
    this.api.getPartner(partnerRef).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => {
        this.detail.set(null);
        this.loadError.set(true);
      },
    });
  }

  protected closeDetail(): void {
    this.detail.set(null);
    this.pendingAction.set(null);
  }

  /** `action` comes from the server-provided allowedActions — nothing else. */
  protected requestAction(action: PartnerAction): void {
    this.actionError.set(false);
    this.pendingAction.set(action);
  }

  protected confirmAction(): void {
    const action = this.pendingAction();
    const current = this.detail();
    this.pendingAction.set(null);
    if (action === null || current === null) {
      return;
    }
    this.api.act(current.partnerRef, action).subscribe({
      next: (updated) => {
        this.detail.set(updated);
        this.load();
      },
      error: () => this.actionError.set(true),
    });
  }

  protected cancelAction(): void {
    // Cancelled: the workflow action must not proceed.
    this.pendingAction.set(null);
  }

  /** Actions the server offers AND this session is permitted to invoke. */
  protected visibleActions(detail: PartnerDetail): readonly PartnerAction[] {
    return this.actions.can('partners.approve') ? detail.allowedActions : [];
  }

  protected stateLabel(state: PartnerState): string {
    return this.i18n.t(STATE_PRESENTATION[state].labelKey);
  }

  protected stateAccent(state: PartnerState): string {
    return STATE_PRESENTATION[state].accent;
  }

  protected actionLabel(action: PartnerAction): string {
    return this.i18n.t(ACTION_LABEL[action]);
  }

  /** Plain-voice consequence line for the confirmation (DESIGN.md §9). */
  protected confirmMessage(action: PartnerAction, name: string): string {
    return this.i18n.t(ACTION_CONFIRM[action], { name });
  }

  protected when(iso: string): string {
    return formatTimestamp(iso);
  }
}
