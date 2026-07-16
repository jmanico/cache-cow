/**
 * Order-management data seam (issue 082; CC-DSH-003, CC-ORD-006).
 *
 * `OrdersApi` is the injectable HTTP-or-mock boundary, following the
 * storefront catalog.api.ts pattern without importing it (ARCHITECTURE.md,
 * Dependency rule 4). Until the Back Office order endpoints exist (issue
 * 082 server scope, built concurrently), the root provider resolves to
 * `MockOrdersApi`; swapping in an HTTP client later is a one-line provider
 * change with no page edits — at which point these contracts MUST be
 * reconciled with the published server schemas (Dependency rule 7).
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime parsers as `unknown` (SECURITY.md, Input validation rule 1): a
 * malformed payload throws instead of rendering (fail closed).
 *
 * Server authority (issue 082 Zero Trust Consideration): transition
 * legality is decided by the issue-035 machine and arrives as
 * `allowedTransitions`; refunds additionally require step-up re-auth
 * ENFORCED SERVER-SIDE (issue 060) — the client's StepUpPrompt is UI
 * sequencing only. Every action is audited server-side (CC-DSH-004).
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import {
  MOCK_REFUND_ELIGIBLE,
  MOCK_TRANSITIONS,
  MockOrder,
  createMockOrders,
  toDetailPayload,
  toSummaryPayload,
} from './orders.mock-data';
import { OrderDetail, OrderSearchQuery, OrderSearchResult, OrderState } from './orders.types';
import { parseOrderDetail, parseOrderSearchResult } from './orders.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockOrdersApi) })
export abstract class OrdersApi {
  /** Server-side order search (CC-DSH-003). */
  abstract search(query: OrderSearchQuery): Observable<OrderSearchResult>;

  /** One order's detail, or null when inaccessible (the real server 404s —
   * SECURITY.md, Authentication rule 9). */
  abstract getOrder(orderRef: string): Observable<OrderDetail | null>;

  /** Request a state transition; the server validates legality (issue 035)
   * and audits (CC-DSH-004). Errors mean: rejected, no state change. */
  abstract transition(orderRef: string, to: OrderState): Observable<OrderDetail>;

  /** Initiate a whole-order refund (amount semantics OPEN — issue 082).
   * The server refuses without fresh step-up re-auth (issue 060). */
  abstract refund(orderRef: string): Observable<OrderDetail>;
}

@Injectable({ providedIn: 'root' })
export class MockOrdersApi extends OrdersApi {
  private readonly orders = createMockOrders();

  override search(query: OrderSearchQuery): Observable<OrderSearchResult> {
    // SIMULATES the server's parameterized, allowlisted query (issue 082).
    const matches = this.orders.filter(
      (o) =>
        (query.market === undefined || o.market === query.market) &&
        (query.state === undefined || o.state === query.state) &&
        (query.orderRef === undefined || o.orderRef.includes(query.orderRef)) &&
        (query.placedFrom === undefined || o.placedAt.slice(0, 10) >= query.placedFrom) &&
        (query.placedTo === undefined || o.placedAt.slice(0, 10) <= query.placedTo),
    );
    const response: unknown = { orders: matches.map(toSummaryPayload) };
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseOrderSearchResult(response));
  }

  override getOrder(orderRef: string): Observable<OrderDetail | null> {
    const order = this.find(orderRef);
    return of(order === undefined ? null : parseOrderDetail(toDetailPayload(order)));
  }

  override transition(orderRef: string, to: OrderState): Observable<OrderDetail> {
    const order = this.find(orderRef);
    // SIMULATES the server rejections: generic errors only, no state change
    // (issue 082 AC-03; SECURITY.md, Logging rule 1 — no internal detail).
    if (order === undefined || !MOCK_TRANSITIONS[order.state].includes(to)) {
      return throwError(() => new Error('Request rejected'));
    }
    this.apply(order, to);
    return of(parseOrderDetail(toDetailPayload(order)));
  }

  override refund(orderRef: string): Observable<OrderDetail> {
    const order = this.find(orderRef);
    if (order === undefined || !MOCK_REFUND_ELIGIBLE.includes(order.state)) {
      return throwError(() => new Error('Request rejected'));
    }
    // The real server executes step-up verification (issue 060) and the
    // processor refund (issues 039/040) before this ever succeeds.
    this.apply(order, 'refunded');
    return of(parseOrderDetail(toDetailPayload(order)));
  }

  private find(orderRef: string): MockOrder | undefined {
    return this.orders.find((o) => o.orderRef === orderRef);
  }

  private apply(order: MockOrder, to: OrderState): void {
    order.state = to;
    // Actor is SERVER-set from the authenticated staff identity (audit,
    // CC-DSH-004); the mock records a placeholder.
    order.history.push({ state: to, at: new Date().toISOString(), actor: 'ops (mock)' });
  }
}
