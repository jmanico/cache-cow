/**
 * Order management module page (issue 082; CC-DSH-003, CC-ORD-006,
 * CC-DSH-004; DESIGN.md §12).
 *
 * Search + results table (compact 40px rows, sticky header, Plex Mono
 * right-aligned numerals, units in headers), row detail with state history,
 * and permission-gated actions:
 *
 *  - TRANSITIONS render one button per SERVER-provided `allowedTransitions`
 *    entry — the client never computes legality (issue 035 owns the
 *    CC-ORD-006 machine). Visible only with `orders.transition`.
 *  - REFUND (visible only with `orders.refund`) sequences the StepUpPrompt
 *    seam BEFORE calling the seam. This is UI flow only: the real passkey
 *    re-auth ceremony and its server-side enforcement are issues 060/082
 *    server scope (SECURITY.md, Authentication rule 2). Refund amount
 *    semantics are an OPEN question (issue 082) — whole-order only here.
 *
 * Action-permission gating is presentation only (core/action-permissions.ts,
 * fail closed with no authored matrix); the server authorizes every call
 * regardless. Errors render generic copy only — no raw bodies, no humor
 * (SECURITY.md, Logging rules 1 and 7; DESIGN.md §5.4, §9).
 */

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActionVisibility } from '../../core/action-permissions';
import { formatMinorUnits, formatTimestamp } from '../../core/format-minor-units';
import { RowNav } from '../../core/row-nav';
import { DashboardI18n } from '../../i18n/i18n.service';
import { StepUpPrompt } from '../../shell/step-up-prompt/step-up-prompt';
import { OrdersApi } from './orders.api';
import {
  ORDER_MARKETS,
  ORDER_STATES,
  OrderDetail,
  OrderMarket,
  OrderSearchQuery,
  OrderState,
  OrderSummary,
} from './orders.types';

@Component({
  selector: 'app-orders-page',
  imports: [RowNav, StepUpPrompt],
  templateUrl: './orders-page.html',
  styleUrl: './orders-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersPage {
  protected readonly i18n = inject(DashboardI18n);
  protected readonly actions = inject(ActionVisibility);
  private readonly api = inject(OrdersApi);

  protected readonly markets = ORDER_MARKETS;
  protected readonly states = ORDER_STATES;

  /** Form state as plain strings; narrowed to the typed query on submit. */
  protected readonly market = signal('');
  protected readonly state = signal('');
  protected readonly orderRef = signal('');
  protected readonly placedFrom = signal('');
  protected readonly placedTo = signal('');

  protected readonly results = signal<readonly OrderSummary[]>([]);
  protected readonly detail = signal<OrderDetail | null>(null);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  /** Refund awaiting the step-up seam (SECURITY.md, Authentication rule 2). */
  protected readonly stepUpPending = signal(false);

  constructor() {
    this.search();
  }

  protected setFrom(target: EventTarget | null, field: 'market' | 'state' | 'orderRef' | 'placedFrom' | 'placedTo'): void {
    const value = (target as HTMLInputElement | HTMLSelectElement).value;
    this[field].set(value);
  }

  protected submitSearch(event: Event): void {
    event.preventDefault();
    this.search();
  }

  private search(): void {
    this.loadError.set(false);
    this.api.search(this.buildQuery()).subscribe({
      next: (result) => this.results.set(result.orders),
      error: () => {
        // Fail closed: nothing renders from a rejected payload.
        this.results.set([]);
        this.loadError.set(true);
      },
    });
  }

  private buildQuery(): OrderSearchQuery {
    const market = this.market();
    const state = this.state();
    const orderRef = this.orderRef().trim();
    return {
      ...((ORDER_MARKETS as readonly string[]).includes(market)
        ? { market: market as OrderMarket }
        : {}),
      ...((ORDER_STATES as readonly string[]).includes(state) ? { state: state as OrderState } : {}),
      ...(orderRef.length > 0 ? { orderRef } : {}),
      ...(this.placedFrom().length > 0 ? { placedFrom: this.placedFrom() } : {}),
      ...(this.placedTo().length > 0 ? { placedTo: this.placedTo() } : {}),
    };
  }

  protected open(orderRef: string): void {
    this.actionError.set(false);
    this.stepUpPending.set(false);
    this.api.getOrder(orderRef).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => {
        this.detail.set(null);
        this.loadError.set(true);
      },
    });
  }

  protected closeDetail(): void {
    this.detail.set(null);
    this.stepUpPending.set(false);
  }

  /** `to` comes from the server-provided allowedTransitions — nothing else. */
  protected transition(to: OrderState): void {
    const current = this.detail();
    if (current === null) {
      return;
    }
    this.actionError.set(false);
    this.api.transition(current.orderRef, to).subscribe({
      next: (updated) => {
        this.detail.set(updated);
        this.search();
      },
      error: () => this.actionError.set(true),
    });
  }

  /** Refund: step-up FIRST (UI sequencing; real re-auth is issue 060). */
  protected requestRefund(): void {
    this.actionError.set(false);
    this.stepUpPending.set(true);
  }

  protected onStepUpConfirmed(): void {
    this.stepUpPending.set(false);
    const current = this.detail();
    if (current === null) {
      return;
    }
    this.api.refund(current.orderRef).subscribe({
      next: (updated) => {
        this.detail.set(updated);
        this.search();
      },
      error: () => this.actionError.set(true),
    });
  }

  protected onStepUpDismissed(): void {
    // Cancelled: the sensitive action must not proceed.
    this.stepUpPending.set(false);
  }

  protected money(amountMinor: number, currency: string): string {
    return formatMinorUnits(amountMinor, currency);
  }

  protected when(iso: string): string {
    return formatTimestamp(iso);
  }

  protected stateName(state: OrderState): string {
    return this.i18n.t(`orders.state.${state}`);
  }
}
