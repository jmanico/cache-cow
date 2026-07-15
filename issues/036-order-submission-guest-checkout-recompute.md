# 036 · Order submission: guest checkout and server-side money recomputation

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-001, CC-PRC-005
- **Title**: Order submission: guest checkout and server-side money recomputation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: Order submission MUST support guest checkout in all markets with account creation optional, and the server MUST recompute all prices, discounts, taxes, and totals at submission from canonical data, ignoring client-supplied prices (REQUIREMENTS.md CC-ORD-001, CC-PRC-005).
- **Rationale**: Money flows one way (ARCHITECTURE.md, Dependency rule 2): clients and caches may display prices, but only the Ordering service computes them, from Pricing as canonical source, at submission time — otherwise a tampered client pays an attacker-chosen price. Guest checkout in every market is a launch requirement (CC-ORD-001); forcing accounts is out of scope. Server-controlled fields set only from server state closes mass-assignment/parameter-tampering paths (SECURITY.md, Input validation rule 3).
- **Design**: Checkout page anatomy per DESIGN.md §10 ("Checkout": straight voice, locale tax/units, per-market address formats) — the checkout UI itself is issue 069; DESIGN.md §5.4 bans puns inside checkout. This issue is the server-side submission endpoint and recomputation.

## Scope
- **Applies To**: Both (consumer storefront submission endpoint; the recomputation authority also backs B2B order creation via issue 053/055)
- **Components**: Ordering & Payments bounded context (ARCHITECTURE.md, "Server bounded contexts" #4), consuming Pricing & Promotions (#3) as canonical price source and Catalog & Inventory (#2) for SKU/availability
- **Actors**: Guest consumer (no session identity), authenticated consumer, B2B partner clients (downstream via the B2B API issues)
- **Data Classification**: Restricted/PII (delivery address, email) and Confidential (order/money data)

## Security Context
- **Defense Layer**: Input Validation / Strict API
- **Threat(s) Addressed**: Price manipulation via client-supplied monetary values (parameter tampering); mass assignment / over-posting of server-controlled fields (CWE-915); OWASP API Security Top 10 — broken object property level authorization class; expired-promotion replay (CC-PRC-006: final authority is the order service)
- **Trust Boundary**: Client–server edge at the order-submission endpoint: everything in the request body is attacker-controlled (SECURITY.md, Input validation rule 1)
- **Zero Trust Consideration**: The request contributes only SKU identifiers, quantities, address, and contact/consent inputs; every monetary value, ownership field, timestamp, and applied promotion is derived server-side from canonical data at submission time. Client-supplied prices, discounts, or totals — if present — are ignored, never reconciled.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 (Validation and Business Logic); ASVS 5.0 V4 (API and Web Service)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: Per-market tax computation conventions per REQUIREMENTS.md CC-PRC-002 (computed values; display conventions are issue 034)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a guest with no account in any of the six launch markets (CC-MKT-001), when they submit a valid order, then the order is created in state `received` (issue 035) without requiring authentication or account creation (CC-ORD-001).
2. **AC-02**: Given any submission, when the server accepts it, then unit prices, discounts, taxes, and totals on the persisted order were recomputed from the Pricing & Promotions canonical data for the transacting market at submission time — in integer minor units with overflow-checked arithmetic (CC-PRC-005, CC-PRC-003; shared Money type from issue 002).
3. **AC-03**: Given a request body containing price, discount, tax, or total fields (tampered client), when submitted, then those values are ignored — the persisted order carries only server-computed amounts, and processing does not fail merely because they were present but is never influenced by them (CC-PRC-005; SECURITY.md, Input validation rule 3) (negative case).
4. **AC-04**: Given a promotion whose window has expired in the market's timezone but which cached UI still displayed, when the order is submitted, then the expired promotion MUST NOT be applied — the order service is final authority (CC-PRC-006).
5. **AC-05**: Given a submission referencing a SKU not available in the transacting market (e.g., a non-veg SKU against IN), when submitted, then the order is rejected via the server-side gating enforcement (CC-MKT-003; gating API is issue 025) — the Ordering context MUST NOT implement its own market conditionals (ARCHITECTURE.md, Dependency rule 1).
6. **AC-06**: Given the submission DTO, when requests bind, then binding targets only a dedicated request DTO with explicit source attributes (`[FromBody]` etc.), never an entity/domain model, and server-controlled fields (user ID, ownership, timestamps, prices) are set exclusively from server state (SECURITY.md, Input validation rule 3).
7. **AC-07**: Given a submission failing schema validation (unknown fields, wrong types, missing required fields), when received, then it is rejected with HTTP 400 and RFC 9457 problem details, with no partial order created (SECURITY.md, Input validation rules 1–2).
8. **AC-08**: Given a guest completes checkout, when offered account creation, then declining it MUST NOT block or alter order completion (CC-ORD-001) (negative case).

## Failure Behavior
- **On Invalid Input**: HTTP 400 with RFC 9457 problem details (issue 021); validation rejections logged as structured events with correlation ID; no internal state disclosed (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed — any exception during recomputation, gating consultation, or persistence rejects the submission; no order row and no downstream payment initiation occur (SECURITY.md, Logging rule 2). Duplicate-submission protection is issue 037's idempotency service.
- **Alerting**: Spikes in validation rejections or in submissions carrying client-supplied monetary fields are logged and alertable via centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Recomputation logic against canonical pricing fixtures in all five currencies including JPY zero-decimal and INR grouping edge amounts (CC-QA-004); promotion-boundary cases (start/end/timezone); DTO binding rejects over-posted fields.
- **Integration Tests**: End-to-end submission against PostgreSQL: guest order creation per market; tampered-price request persists only server amounts; expired-promotion rejection; gated-SKU rejection via the Market & Gating Policy service.
- **Security Tests**: Money-path suite per CC-QA-004 (recomputation authority explicitly named there); mutation testing on money code (CC-QA-001); SAST gate per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: Assert emails/receipts contain no more personal data than necessary is issue 043's scope; here, assert order records classify and store address/contact per data-class documentation (CC-CMP-003 documentation dependency).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged `CC-ORD-001`, `CC-PRC-005` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 (scaffold), 002 (Money type — integer minor units, overflow-checked, CC-PRC-003), 003 (Market/Locale/SKU identity), 024 (transacting market/locale resolution), 025 (server-side gating enforcement), 030 (inventory/availability), 032 (per-SKU per-market price model), 033 (promotion engine), 035 (order state machine — orders enter `received`), 037 (idempotency service — double-submission protection), 038 (address capture/validation), 021 (RFC 9457 errors)
- **Downstream**: 039/040 (payment initiation — Stripe/Razorpay; explicitly NOT this issue), 042 (guest order capability tokens), 043 (order emails), 044 (cold-store routing), 045 (serviceability checks at checkout), 046 (invoicing), 053/055 (B2B order creation reusing recomputation authority)
- **External**: None in this issue (payment processors enter at issues 039/040/041)

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core in the Ordering & Payments context; Ordering connects with its own least-privilege PostgreSQL role over TLS (SECURITY.md, Secret handling rule 10). All money uses the shared-kernel Money type (integer minor units, overflow-checked, fail-closed on overflow — CC-PRC-003; ARCHITECTURE.md, Dependency rule 9). Request validation against explicit schemas with unknown fields rejected (SECURITY.md, Input validation rules 1–2). Order-creation endpoint sits under the stricter rate-limit tier (SECURITY.md, HTTP boundary rule 7; baseline middleware is issue 019). API p95 latency budget for order creation: 800ms (CC-NFR-002).
- **Anti-Patterns**: MUST NOT trust or "validate then accept" client-supplied monetary values — ignore them entirely (CC-PRC-005). MUST NOT bind to entity/domain models or accept server-controlled fields from the request (SECURITY.md, Input validation rule 3). MUST NOT use binary floating point anywhere in money code, including tests (CC-PRC-003). MUST NOT implement market conditionals locally (ARCHITECTURE.md, Dependency rule 1). No puns or humor in checkout copy paths (DESIGN.md §5.4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests, coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Money-path and gating tests run on every merge (SECURITY.md, Deployment rule 8).

## Open Questions
- When server recomputation produces a total different from what the client displayed (e.g., a promotion expired between page render and submission), the specs mandate the server value wins (CC-PRC-005/006) but do not say whether the consumer must be re-prompted to confirm the new total or the order proceeds at the recomputed price. Needs a human decision.
- The specs do not define the cart/checkout-session model (server-side cart vs. client-held cart submitted whole). CC-MKT-008 implies server awareness of cart contents across market switches (cart is issue 068), but the submission contract between cart and order service is unspecified.
- How the optional account-creation-at-checkout hand-off to consumer authentication (issue 058, Entra External ID) works — e.g., linking a just-placed guest order to the new account — is not specified.
