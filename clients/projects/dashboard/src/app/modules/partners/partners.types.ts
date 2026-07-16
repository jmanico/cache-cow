/**
 * Partner (wholesale) management contracts (issue 085; CC-DSH-003,
 * CC-WHS-002, CC-WHS-004).
 *
 * CLIENT-SIDE TypeScript contracts for the Back Office partner endpoints,
 * which drive the Wholesale & B2B context's onboarding approval workflow
 * (issue 049) through its module boundary. The server endpoints are being
 * built concurrently — these shapes MUST be reconciled with the published
 * server schemas before the HTTP implementation lands (ARCHITECTURE.md,
 * Dependency rule 7).
 *
 * MASKED-BY-CONTRACT: business identity crosses this boundary ONLY as a
 * masked string. There is no full-value field on `BusinessIdentity` — not an
 * optional one, not a nullable one — so the dashboard client cannot render,
 * log, or leak a full USt-IdNr./GSTIN, because it never receives one. The
 * masking happens SERVER-side; the parser additionally REJECTS a payload
 * that carries a full value, so a server regression that starts sending one
 * fails closed instead of quietly rendering it (partners.validate.ts).
 *
 * OPEN QUESTION (flagged, not resolved — CLAUDE.md working rules): no
 * canonical document authors a REVEAL flow for these identifiers, nor its
 * audit/export semantics. CC-DSH-005 and SECURITY.md (Authentication rule
 * 12; Secret handling rule 6) author that pattern for EMPLOYEE PII — audited
 * export function, restricted role — but partner business identity is a
 * different data class with no such rule written. Whether staff may ever see
 * a full identifier, under which role, with what audit event and step-up, is
 * a human decision. Until it exists, masked is the only view and no reveal
 * contract is drafted here.
 *
 * Action legality is SERVER state: `allowedActions` arrives computed by the
 * issue-049 workflow; the client renders exactly what it is given and never
 * computes which workflow steps are legal (mirrors orders.types.ts
 * `allowedTransitions`).
 */

/** Launch markets (CC-MKT-001). */
export const PARTNER_MARKETS = ['US', 'ES', 'MX', 'DE', 'JP', 'IN'] as const;

export type PartnerMarket = (typeof PARTNER_MARKETS)[number];

/**
 * Onboarding states (CC-WHS-002: approval workflow, no self-service
 * activation).
 *
 * OPEN QUESTION (flagged): `suspended` is included because this build slice
 * asks for a suspend action, but issue 085's own open question records that
 * "whether partner suspension/deactivation/offboarding is in v1 scope is not
 * stated" in the canonical docs. CC-WHS-002 authors approval only. The state
 * and its action are drafted here for the workflow the server will own; if a
 * human rules suspension out of v1, both drop out of this contract.
 */
export const PARTNER_STATES = ['pending', 'approved', 'rejected', 'suspended'] as const;

export type PartnerState = (typeof PARTNER_STATES)[number];

/** Workflow actions the server may offer on a partner record. */
export const PARTNER_ACTIONS = ['approve', 'reject', 'suspend'] as const;

export type PartnerAction = (typeof PARTNER_ACTIONS)[number];

export interface PartnerSummary {
  readonly partnerRef: string;
  readonly name: string;
  readonly market: PartnerMarket;
  readonly state: PartnerState;
  readonly appliedAt: string;
}

/**
 * One per-market business identity (CC-WHS-002), masked at the source.
 *
 * `kind` is the SERVER's label for the identifier type (e.g. "USt-IdNr.",
 * "GSTIN"). Which identity fields US, ES, MX, and JP partners require is an
 * issue 085 open question — CC-WHS-002 gives DE and IN only as examples — so
 * this contract carries whatever kinds the server sends rather than encoding
 * a per-market list the docs never authored.
 */
export interface BusinessIdentity {
  readonly kind: string;
  /** Masked to trailing characters only. The full value never crosses here. */
  readonly maskedValue: string;
}

export interface PartnerDetail extends PartnerSummary {
  readonly businessIdentities: readonly BusinessIdentity[];
  /**
   * Wholesale payment terms in days — net-60 default, adjustable per partner
   * (CC-WHS-004, ratified 2026-07-15). READ-ONLY in this client slice: terms
   * ADJUSTMENT (issue 085 AC-04) is a write path not drafted here, and
   * whether it requires step-up re-auth is an issue 085 open question
   * (SECURITY.md, Authentication rule 2 enumerates refunds, employee-record
   * access, and role changes — partner actions are not stated either way).
   */
  readonly paymentTermsDays: number;
  /**
   * Workflow actions the SERVER offers from the current state (issue 049).
   * The client renders one button per entry — nothing else, and never a
   * button it computed itself.
   */
  readonly allowedActions: readonly PartnerAction[];
}

export interface PartnerListResult {
  readonly partners: readonly PartnerSummary[];
}
