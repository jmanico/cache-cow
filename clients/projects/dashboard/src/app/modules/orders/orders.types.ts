/**
 * Order-management contracts (issue 082; CC-DSH-003, CC-ORD-006).
 *
 * CLIENT-SIDE TypeScript contracts for the Back Office order endpoints.
 * The server-side endpoints (issue 082 server scope, Ordering & Payments
 * context) are being built concurrently — these shapes MUST be reconciled
 * with the published server schemas before the HTTP implementation lands
 * (ARCHITECTURE.md, Dependency rule 7: schemas are the single source of
 * truth at every trust boundary).
 *
 * Transition legality is SERVER state: `allowedTransitions` and
 * `refundEligible` arrive computed by the issue-035 state machine; the
 * client renders exactly what it is given and never computes legality
 * (issue 082 Zero Trust Consideration).
 */

/** Canonical order states (CC-ORD-006). */
export const ORDER_STATES = [
  'received',
  'confirmed',
  'packed',
  'shipped',
  'delivered',
  'cancelled',
  'refunded',
] as const;

export type OrderState = (typeof ORDER_STATES)[number];

/** Launch markets (CC-MKT-001). */
export const ORDER_MARKETS = ['US', 'ES', 'MX', 'DE', 'JP', 'IN'] as const;

export type OrderMarket = (typeof ORDER_MARKETS)[number];

/**
 * Search criteria; executed server-side (parameterized, allowlisted —
 * SECURITY.md, Input validation rule 4). Searchable fields beyond CC-DSH-003
 * "search" are an issue 082 open question; this minimal set is the client
 * draft to reconcile.
 */
export interface OrderSearchQuery {
  readonly market?: OrderMarket;
  readonly state?: OrderState;
  /** Substring match on the order reference. */
  readonly orderRef?: string;
  /** ISO dates (inclusive bounds) on placement time. */
  readonly placedFrom?: string;
  readonly placedTo?: string;
}

export interface OrderSummary {
  readonly orderRef: string;
  readonly market: OrderMarket;
  readonly state: OrderState;
  readonly placedAt: string;
  readonly itemCount: number;
  /** Server-computed total in integer minor units (CC-PRC-003/005). */
  readonly totalMinor: number;
  readonly currency: string;
}

export interface OrderSearchResult {
  readonly orders: readonly OrderSummary[];
}

export interface OrderStateEvent {
  readonly state: OrderState;
  readonly at: string;
  /** Actor as recorded server-side in the audit stream (CC-DSH-004). */
  readonly actor: string;
}

export interface OrderDetail extends OrderSummary {
  readonly history: readonly OrderStateEvent[];
  /**
   * Next states the SERVER offers from the current state (issue-035
   * machine). The client renders one action per entry — nothing else.
   */
  readonly allowedTransitions: readonly OrderState[];
  /**
   * Whether the server offers a refund for this order. Refund amount
   * semantics (full vs. partial/per-line) are an OPEN question (issue 082);
   * this boolean models only the whole-order terminal branch of CC-ORD-006.
   */
  readonly refundEligible: boolean;
}
