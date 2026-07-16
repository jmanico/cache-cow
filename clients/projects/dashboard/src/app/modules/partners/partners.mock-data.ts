/**
 * Mock partner fixture (issue 085) — a stand-in for the SERVER's Back Office
 * partner endpoints until they land (the onboarding approval workflow itself
 * is issue 049; this module is only its dashboard UI surface).
 *
 * This module SIMULATES THE SERVER: `MOCK_ALLOWED_ACTIONS` simulates the
 * issue-049 workflow's answer for each state, and identities are already
 * MASKED here because the real server masks before the response leaves it —
 * no full USt-IdNr./GSTIN exists anywhere in this fixture, so the client's
 * masked-only posture is exercised end to end (partners.types.ts).
 *
 * The action table MUST be reconciled with the real workflow. In particular
 * `suspend` is drafted per this build slice while issue 085's open question
 * records that suspension/offboarding scope is unstated in the canonical
 * docs — CC-WHS-002 authors approval only.
 *
 * Terms default to net-60 (CC-WHS-004, ratified 2026-07-15) and are
 * server-owned; the mock varies one partner to show the per-partner
 * adjustment the requirement allows. Actor strings are what the server's
 * audit stream records (CC-DSH-004) — never client-supplied.
 */

import { PartnerAction, PartnerMarket, PartnerState } from './partners.types';

/** MOCK simulation of the issue-049 workflow's per-state action offer. */
export const MOCK_ALLOWED_ACTIONS: Readonly<Record<PartnerState, readonly PartnerAction[]>> = {
  pending: ['approve', 'reject'],
  approved: ['suspend'],
  rejected: [],
  suspended: ['approve'],
};

export interface MockPartner {
  readonly partnerRef: string;
  readonly name: string;
  readonly market: PartnerMarket;
  state: PartnerState;
  readonly appliedAt: string;
  readonly paymentTermsDays: number;
  /** Already masked — the fixture holds no full identifier by design. */
  readonly businessIdentities: readonly { kind: string; maskedValue: string }[];
}

/** Fresh mutable dataset per mock instance (specs stay independent). */
export function createMockPartners(): MockPartner[] {
  return [
    {
      partnerRef: 'WHP-000117',
      name: 'Nordwind Feinkost GmbH',
      market: 'DE',
      state: 'pending',
      appliedAt: '2026-07-09T08:15:00Z',
      paymentTermsDays: 60,
      businessIdentities: [{ kind: 'USt-IdNr.', maskedValue: '•••••••••4821' }],
    },
    {
      partnerRef: 'WHP-000104',
      name: 'Sahyadri Fresh Foods Pvt Ltd',
      market: 'IN',
      state: 'approved',
      appliedAt: '2026-06-22T05:40:00Z',
      paymentTermsDays: 45,
      businessIdentities: [
        { kind: 'GSTIN', maskedValue: '•••••••••••••4Z5' },
        { kind: 'FSSAI licence', maskedValue: '••••••••••2913' },
      ],
    },
    {
      partnerRef: 'WHP-000098',
      name: 'Lone Star Grocers',
      market: 'US',
      state: 'approved',
      appliedAt: '2026-06-11T16:05:00Z',
      paymentTermsDays: 60,
      businessIdentities: [{ kind: 'EIN', maskedValue: '•••••7734' }],
    },
    {
      partnerRef: 'WHP-000091',
      name: 'Mercado del Norte SA de CV',
      market: 'MX',
      state: 'suspended',
      appliedAt: '2026-05-28T13:22:00Z',
      paymentTermsDays: 30,
      businessIdentities: [{ kind: 'RFC', maskedValue: '••••••••K19' }],
    },
    {
      partnerRef: 'WHP-000085',
      name: 'Kansai Provisions KK',
      market: 'JP',
      state: 'rejected',
      appliedAt: '2026-05-14T01:50:00Z',
      paymentTermsDays: 60,
      businessIdentities: [{ kind: 'Corporate number', maskedValue: '••••••••••6402' }],
    },
  ];
}

/** Raw wire-shaped payloads (returned as `unknown` through the seam). */
export function toSummaryPayload(p: MockPartner): unknown {
  return {
    partnerRef: p.partnerRef,
    name: p.name,
    market: p.market,
    state: p.state,
    appliedAt: p.appliedAt,
  };
}

export function toDetailPayload(p: MockPartner): unknown {
  return {
    ...(toSummaryPayload(p) as Record<string, unknown>),
    businessIdentities: p.businessIdentities.map((id) => ({ ...id })),
    paymentTermsDays: p.paymentTermsDays,
    allowedActions: [...MOCK_ALLOWED_ACTIONS[p.state]],
  };
}
