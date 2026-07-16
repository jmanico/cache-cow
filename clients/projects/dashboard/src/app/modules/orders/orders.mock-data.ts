/**
 * Mock order fixture (issue 082) — a stand-in for the SERVER's Back Office
 * order endpoints until they land (server search/transition/refund are the
 * issue 082 server scope; the state machine itself is issue 035).
 *
 * This module SIMULATES THE SERVER: filtering happens here the way the
 * server query would, and `MOCK_TRANSITIONS` / `MOCK_REFUND_ELIGIBLE`
 * simulate the issue-035 state machine's answer (CC-ORD-006). None of this
 * is client legality logic: the client consumes these payloads through the
 * OrdersApi seam as untrusted `unknown` responses and renders exactly the
 * `allowedTransitions` it is given. Both tables MUST be reconciled with the
 * real server machine — in particular refund eligibility, whose semantics
 * are an issue 082 open question.
 *
 * Money is integer minor units (CC-PRC-003); all values server-computed
 * (CC-PRC-005). Actor strings are what the server's audit stream records
 * (CC-DSH-004) — the mock uses a fixed placeholder.
 */

import { OrderMarket, OrderState } from './orders.types';

/** MOCK simulation of the CC-ORD-006 machine (issue 035 owns the truth). */
export const MOCK_TRANSITIONS: Readonly<Record<OrderState, readonly OrderState[]>> = {
  received: ['confirmed', 'cancelled'],
  confirmed: ['packed', 'cancelled'],
  packed: ['shipped'],
  shipped: ['delivered'],
  delivered: [],
  cancelled: [],
  refunded: [],
};

/** MOCK refund eligibility — an OPEN question server-side (issue 082). */
export const MOCK_REFUND_ELIGIBLE: readonly OrderState[] = [
  'confirmed',
  'packed',
  'shipped',
  'delivered',
];

export interface MockOrder {
  readonly orderRef: string;
  readonly market: OrderMarket;
  state: OrderState;
  readonly placedAt: string;
  readonly itemCount: number;
  readonly totalMinor: number;
  readonly currency: string;
  readonly history: { state: OrderState; at: string; actor: string }[];
}

/** Fresh mutable dataset per mock instance (specs stay independent). */
export function createMockOrders(): MockOrder[] {
  const order = (
    orderRef: string,
    market: OrderMarket,
    state: OrderState,
    placedAt: string,
    itemCount: number,
    totalMinor: number,
    currency: string,
    priorStates: readonly OrderState[],
  ): MockOrder => ({
    orderRef,
    market,
    state,
    placedAt,
    itemCount,
    totalMinor,
    currency,
    history: [...priorStates, state].map((s, i) => ({
      state: s,
      at: new Date(Date.parse(placedAt) + i * 3_600_000).toISOString(),
      actor: i === 0 ? 'system (mock)' : 'ops (mock)',
    })),
  });

  return [
    order('ORD-2026-000141', 'US', 'received', '2026-07-12T14:05:00Z', 3, 14900, 'USD', []),
    order('ORD-2026-000138', 'DE', 'confirmed', '2026-07-11T09:30:00Z', 2, 8950, 'EUR', ['received']),
    order('ORD-2026-000132', 'JP', 'packed', '2026-07-10T02:15:00Z', 1, 14900, 'JPY', ['received', 'confirmed']),
    order('ORD-2026-000127', 'IN', 'shipped', '2026-07-08T11:45:00Z', 4, 419600, 'INR', ['received', 'confirmed', 'packed']),
    order('ORD-2026-000119', 'ES', 'delivered', '2026-07-05T16:20:00Z', 2, 6480, 'EUR', ['received', 'confirmed', 'packed', 'shipped']),
    order('ORD-2026-000104', 'MX', 'cancelled', '2026-07-01T19:10:00Z', 5, 210000, 'MXN', ['received']),
  ];
}

/** Raw wire-shaped payloads (returned as `unknown` through the seam). */
export function toSummaryPayload(o: MockOrder): unknown {
  return {
    orderRef: o.orderRef,
    market: o.market,
    state: o.state,
    placedAt: o.placedAt,
    itemCount: o.itemCount,
    totalMinor: o.totalMinor,
    currency: o.currency,
  };
}

export function toDetailPayload(o: MockOrder): unknown {
  return {
    ...(toSummaryPayload(o) as Record<string, unknown>),
    history: o.history.map((h) => ({ ...h })),
    allowedTransitions: [...MOCK_TRANSITIONS[o.state]],
    refundEligible: MOCK_REFUND_ELIGIBLE.includes(o.state),
  };
}
