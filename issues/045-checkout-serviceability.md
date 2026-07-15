# 045 · Checkout serviceability: postal codes and 48-hour frozen transit

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** ARCHITECTURE.md "Known unknowns" — *Cross-border transfer mechanism for processors*. EasyPost processes EU/IN delivery addresses (personal data) and its lawful transfer basis is not decided or documented (CC-CMP-006). Serviceability logic and tests can proceed; production use of EasyPost for EU/IN addresses awaits that human decision. Not resolved here.

## Metadata
- **ID**: CC-FUL-002
- **Title**: Checkout serviceability: postal codes and 48-hour frozen transit
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: Checkout MUST enforce frozen-shipping constraints by rejecting, server-side, any order whose delivery postal code is not serviceable for the transacting market or that cannot be delivered within the ratified maximum of 48 hours carrier transit for frozen product (REQUIREMENTS.md CC-FUL-002).
- **Rationale**: The product is frozen food; transit beyond the cold-chain window is a safety and quality failure, so serviceability is a hard gate at checkout, not a post-order exception (CC-FUL-002, 48-hour maximum ratified 2026-07-15 per ARCHITECTURE.md decision record, "Cold-chain shipping spec"). Carrier selection runs behind the EasyPost multi-carrier aggregator "under the ratified 48-hour max frozen transit constraint" (ARCHITECTURE.md, "Technology decisions", Carriers). Frozen-shipping serviceability checks at checkout are fixed in the Fulfillment bounded context (ARCHITECTURE.md, "Server bounded contexts" item 5).
- **Design**: Checkout copy is straight voice — zero puns inside checkout, payment, and error recovery (DESIGN.md §5.4); errors state what happened and what to do next (DESIGN.md §9). The checkout UI itself is issue 069; JP delivery-window selection is a checkout UI concern (DESIGN.md §8.5) layered on this serviceability decision.

## Scope
- **Applies To**: Both (server-side enforcement in the Fulfillment context; consumed by storefront checkout — issue 069 — and by B2B order creation)
- **Components**: Fulfillment bounded context (serviceability decision); Ordering & Payments (order submission calls the check); EasyPost carrier aggregator integration; inbound carrier-event verification via issue 041's framework.
- **Actors**: Consumer at checkout (guest or authenticated), B2B partner order flows, EasyPost as external carrier aggregator.
- **Data Classification**: Restricted/PII (delivery addresses and postal codes)

## Security Context
- **Defense Layer**: Input Validation (server-side postal-code and transit-constraint validation); Strict API
- **Threat(s) Addressed**: Client-side control bypass (CWE-602: client-side enforcement of server-side security); STRIDE Tampering (forged serviceability state in the client); untrusted inbound carrier callbacks (SECURITY.md, Input validation rule 11).
- **Trust Boundary**: Client-server edge at order submission (every input crossing it is attacker-controlled — SECURITY.md, Input validation rule 1); inbound EasyPost/carrier webhook receiver as a distinct untrusted boundary (SECURITY.md, Input validation rule 11).
- **Zero Trust Consideration**: Any serviceability state displayed in the client is advisory; the server independently re-validates postal-code serviceability and the 48-hour transit constraint at order submission from server-held data, exactly as it recomputes money (analogous to CC-PRC-005). Inbound carrier events are signature-verified over the raw body before parsing (issue 041).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); Input Validation chapter (server-side validation against explicit rules, reject not sanitize).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: No implicit trust in client-asserted serviceability; per-request server-side evaluation.
- **Regulatory**: N/A (the 48-hour limit is an internally ratified cold-chain constraint, not a cited regulation)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a delivery address whose postal code is serviceable for the transacting market and at least one carrier option within 48 hours transit from the serving cold store, when the order is submitted, then serviceability passes and order processing continues (CC-FUL-002).
2. **AC-02**: Given a delivery postal code not on the transacting market's serviceable set, when the order is submitted, then the order is rejected with an RFC 9457 problem-details response, and no order is created (CC-FUL-002; SECURITY.md, Logging rule 1; issue 021).
3. **AC-03**: Given a serviceable postal code but no carrier option within the 48-hour frozen transit limit, when the order is submitted, then the order is rejected and no order is created (CC-FUL-002; ARCHITECTURE.md, Carriers).
4. **AC-04**: Given a client that skips or tampers with client-side serviceability checks (direct API submission), when the order reaches the server, then server-side validation alone blocks the unserviceable order — client-side validation is never the sole enforcement (SECURITY.md, Input validation rule 1).
5. **AC-05**: Given per-market address formats (Japanese address structure, Indian PIN codes) validated by issue 038, when serviceability is evaluated, then it operates on the validated, normalized postal code for the transacting market — never on raw client text (CC-ORD-002; CC-FUL-002).
6. **AC-06**: Given an inbound carrier/EasyPost event, when it arrives, then it is accepted only after signature verification over the raw request body with replay bounds, via issue 041's verification framework; unverified events are rejected and logged (SECURITY.md, Input validation rule 11).
7. **AC-07**: Negative: an order MUST NOT reach `confirmed` (CC-ORD-006) with an unserviceable postal code or with no ≤ 48-hour transit option recorded at submission time; a stale client-cached "serviceable" result MUST NOT override the submission-time server check (CC-FUL-002).

## Failure Behavior
- **On Invalid Input**: Reject with 400/422 and RFC 9457 problem details naming the failed constraint in generic, actionable terms; no internal carrier or topology details disclosed (SECURITY.md, Logging rule 1); validation rejections logged as structured events (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — if the serviceability evaluation (including the EasyPost dependency) errors or times out, the order is rejected/held, never optimistically accepted (SECURITY.md, Logging rule 2 applies to gating paths).
- **Alerting**: Spikes in serviceability failures and carrier-webhook signature-verification failures alert via centralized monitoring (SECURITY.md, Logging rule 3; Secret handling rule 9; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for postal-code set evaluation per market, 48-hour transit-limit evaluation, and fail-closed behavior on evaluation errors. Tagged CC-FUL-002 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests with a stubbed EasyPost client: serviceable/unserviceable submissions per market, no-carrier-within-48h rejection, direct-API bypass attempts, carrier-event signature acceptance/rejection through issue 041's framework.
- **Security Tests**: Fuzzing of postal-code inputs (injection, format abuse) rejected by schema validation; DAST against the checkout submission path per CC-QA-007.
- **Compliance Tests**: Evidence that rejected submissions produce logged validation events and create no order rows.
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 023/025 (transacting market context from Market & Gating Policy), 036 (order submission), 038 (per-market address capture and validation), 041 (inbound webhook verification framework, for carrier events), 021 (RFC 9457 error handling), 044 (serving cold store determines transit origin).
- **Downstream**: 069 (checkout UI per market surfaces serviceability errors), 070 (order tracking consumes carrier events).
- **External**: EasyPost multi-carrier aggregator (ARCHITECTURE.md, Carriers — confirmed vendor).
- **Open decision**: AT RISK on the cross-border transfer mechanism for EasyPost (CC-CMP-006; ARCHITECTURE.md, "Known unknowns") — see blockquote.

## Implementation Notes
- **Constraints**: Implement in the Fulfillment bounded context with its own schema and least-privilege PostgreSQL role over TLS (SECURITY.md, Secret handling rule 10). EasyPost credentials live in Azure Key Vault only, fetched via Workload Identity/CSI with TTL caching (SECURITY.md, Secret handling rules 1, 4, 5). Validate submissions against explicit schemas and bind to dedicated DTOs (SECURITY.md, Input validation rules 1, 3). Outbound calls to EasyPost stay within the checkout latency budget context of CC-NFR-002 (order creation p95 < 800ms) — evaluate whether serviceability precomputation/caching is needed, but the submission-time server check remains authoritative.
- **Anti-Patterns**: MUST NOT rely on client-side validation as sole enforcement (SECURITY.md, Input validation rule 1); MUST NOT follow redirects when delivering to or fetching user-influenced URLs, and webhook receiver URLs are validated at registration (SECURITY.md, Input validation rule 8 — applies to the outbound side; inbound events per rule 11); MUST NOT accept unverified carrier events; MUST NOT hardcode carrier choices — per-market carriers are selected behind EasyPost (ARCHITECTURE.md, Carriers).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-FUL-002 (REQUIREMENTS.md §17).

## Open Questions
- The serviceable postal-code sets per market are operational data not defined in the specs (source, update cadence, and ownership are unspecified).
- Whether the 48-hour transit limit is evaluated against carrier *estimated* transit time or *guaranteed/committed* service levels (via EasyPost) is not specified.
- Whether restocking/preorder orders (CC-CAT-003 "WARMING") defer the serviceability/transit evaluation to fulfillment time or evaluate at submission time is not specified.
- Whether B2B wholesale case-quantity orders (CC-WHS-001) are subject to the same 48-hour constraint and the same serviceability check path is not stated in CC-FUL-002 (which says "every consumer order" for routing in CC-FUL-001, but CC-FUL-002 speaks of checkout generally).
