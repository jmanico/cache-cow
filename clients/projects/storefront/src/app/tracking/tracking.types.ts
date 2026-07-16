/**
 * Typed order-tracking view model (issue 070; CC-ORD-008; DESIGN.md §7
 * "Order tracker").
 *
 * The five consumer-facing stages are fixed vocabulary. The mapping from the
 * internal order state machine (`received -> confirmed -> packed -> shipped
 * -> delivered`, CC-ORD-006) plus carrier events onto these stages is
 * ⚠ UNRATIFIED (epic question Q13; issue 070 Open Questions): nothing in the
 * specs says which internal states produce "Smoked" and "Frozen". That
 * mapping is SERVER-side (Ordering & Payments read path, issue 035 + 041)
 * and needs a human decision — so this client type deliberately models
 * ALREADY-MAPPED data and performs no mapping of its own (issue 070 AC-01;
 * ARCHITECTURE.md, Dependency rule 1). Tracker presentation for the
 * `cancelled`/`refunded` terminal branches is likewise open and not modeled.
 */

/** The five consumer-facing stages, in order (CC-ORD-008; DESIGN.md §7). */
export const TRACK_STAGES = ['smoked', 'frozen', 'packed', 'inTransit', 'delivered'] as const;
export type TrackStage = (typeof TRACK_STAGES)[number];

export interface TrackedStage {
  readonly stage: TrackStage;
  /** ISO 8601 timestamp when the stage was reached; null when not yet. */
  readonly reachedAt: string | null;
}

/**
 * A server-mapped tracking view: exactly the five stages in canonical order,
 * where the reached stages form a strict prefix (progress never has holes).
 */
export interface OrderTrackingView {
  /** Display order number (Plex Mono per DESIGN.md §4.1). */
  readonly orderNumber: string;
  readonly stages: readonly TrackedStage[];
}

/** Count of reached stages (display derivation only — never stage mapping). */
export function reachedCount(view: OrderTrackingView): number {
  return view.stages.filter((entry) => entry.reachedAt !== null).length;
}
