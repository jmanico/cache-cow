# 033 · Promotion engine: timezone windows, scope, stacking rules

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-PRC-006, CC-PRC-007
- **Title**: Promotion engine: timezone windows, scope, stacking rules
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Promotions MUST support per-market percentage or fixed discounts, per-SKU or per-category scope, start/end timestamps in the market timezone, and stacking rules defaulting to no stacking; expired promotions MUST NOT apply even if cached UI still displays them, with the order service as final authority (REQUIREMENTS.md CC-PRC-006), and clearance ("Eviction Specials") naming MUST NOT leak into invoice line-item legal descriptions (CC-PRC-007).
- **Rationale**: Promotions move money: a promotion applied outside its window, stacked against policy, or honored from stale cached UI is a direct revenue-integrity defect on an attacker-reachable path (a client can replay an expired promotion at submission). The spec therefore places final authority in the order service (CC-PRC-006; ARCHITECTURE.md, "Server bounded contexts" #3 and Dependency rule 2). "Eviction Specials" is brand presentation only (DESIGN.md §5.3); invoices are legal documents (CC-INV-001) and must carry legal descriptions, not puns (CC-PRC-007; DESIGN.md §5.4 bans comedy in money movement).
- **Design**: Presentation of clearance promotions (Ember treatment, Plex Mono countdown) is DESIGN.md §5.3 and the sale/promo treatment in §7 — owned by issue 066. This issue is the rules engine only.

## Scope
- **Applies To**: Both
- **Components**: Pricing & Promotions bounded context (ARCHITECTURE.md, "Server bounded contexts" #3): promotion entity (market, discount type/value, SKU-or-category scope, start/end in market timezone, stacking rule), evaluation function returning applicable discounts for a (SKU set, market, timestamp), and the authoritative re-evaluation contract the order service calls at submission. Excludes: promo UI (issue 066), order-time total recomputation itself (issue 036), invoice line-item generation (issues 046/047), analytics (issue 083).
- **Actors**: Pricing service (internal), Ordering service (final authority caller), storefront SSR (display only).
- **Data Classification**: Internal

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Business-logic abuse (OWASP Top Ten A04:2021): applying expired promotions from cached UI, stacking discounts against policy, timezone-boundary manipulation to extend a window; monetary overflow on discount arithmetic (CC-PRC-003).
- **Trust Boundary**: Promotion applicability is decided server-side at order submission from canonical data; client-displayed discounts are untrusted display state (CC-PRC-005/006; ARCHITECTURE.md, Dependency rule 2 — "Nothing depends on client-supplied monetary values").
- **Zero Trust Consideration**: The evaluation function trusts only server clock, server promotion records, and the server-side transacting market — never client timestamps, client-claimed promotion IDs' terms, or cached UI state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation on promotion administration)
- **NIST SP 800-207**: N/A
- **Regulatory**: Invoice legal-description integrity intersects CC-INV-001 (per-market legal invoice formats); this issue only guarantees it emits legal descriptions, not brand names.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a promotion defined for one market, when evaluated, then it supports percentage or fixed-amount discounts, scoped per-SKU or per-category, and applies only in its market (CC-PRC-006).
2. **AC-02**: Given a promotion with start/end timestamps, when evaluated at a boundary instant, then the window is interpreted in the promotion's market timezone — a promotion ending 2026-08-01 00:00 in JP stops applying at that instant JST, not UTC (CC-PRC-006; boundary tests per CC-QA-004).
3. **AC-03**: Given an expired promotion still displayed by cached UI, when an order is submitted referencing it, then the order service's re-evaluation excludes it and the order total contains no expired discount (CC-PRC-006 negative case; ARCHITECTURE.md, Dependency rule 2).
4. **AC-04**: Given two promotions whose scopes overlap on a SKU, when no explicit stacking rule permits combination, then exactly one (per the configured rule) applies — the default is no stacking (CC-PRC-006).
5. **AC-05**: Given discount arithmetic, when computed, then it uses the shared Money type in integer minor units with overflow-checked operations that fail closed, and a discount can never drive a line or total negative (CC-PRC-003).
6. **AC-06**: Given a clearance promotion presented as "Eviction Specials", when invoice line-item data is generated from an order using it, then the legal description contains the product/discount legal wording and never the "Eviction Specials" brand naming (CC-PRC-007 negative case).
7. **AC-07**: Given a promotion-administration write with an unknown market, end-before-start window, non-integer amount, or unknown scope target, when validation runs, then it is rejected with 400 (RFC 9457) and nothing is persisted (SECURITY.md, Input validation rule 1).

## Failure Behavior
- **On Invalid Input**: 400 with RFC 9457 problem details; structured validation-rejection log with correlation ID (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed on the money path (SECURITY.md, Logging rule 2): if promotion evaluation fails at order submission, no discount applies (the customer pays list price or the submission errors — a failure never grants a discount).
- **Alerting**: Evaluation exceptions on the order-submission path and overflow failures are structured security-relevant events with alerting (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the evaluation function: percentage vs fixed, per-SKU vs per-category scope, market isolation, timezone window boundaries (start/end/DST where the market timezone observes it), default no-stacking, overflow and negative-total guards. Money types exact, never float (CC-PRC-003).
- **Integration Tests**: ASP.NET Core integration tests with PostgreSQL (issue 015) plus a submission-path test with the order service stub proving expired/cached promotions are rejected at recomputation (CC-PRC-005/006).
- **Security Tests**: Money-path tests per CC-QA-004 (promotion boundaries: start/end/timezone); mutation testing SHOULD run on this money code (CC-QA-001); replay of an expired promotion at submission fails.
- **Compliance Tests**: Assertion that invoice line-item description fields produced from promoted orders never contain the string branding of DESIGN.md §5.3 (CC-PRC-007).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-PRC-006, CC-PRC-007.

## Dependencies
- **Upstream**: 002 Shared kernel Money type; 003 Shared kernel identity types; 015 PostgreSQL Flexible Server; 021 RFC 9457 error handling; 029 SKU domain model (cut/category for scope); 032 Per-SKU per-market price model.
- **Downstream**: 036 Order submission and server-side money recomputation (final authority, CC-PRC-005); 046 Invoice core / 047 Per-market invoice tax content (legal descriptions); 066 Menu page (Eviction Specials presentation, DESIGN.md §5.3); 083 Dashboard sales analytics (promotion performance, CC-DSH-006); 099 Money-path and mutation-testing suite.
- **External**: N/A

## Implementation Notes
- **Constraints**: Lives in the Pricing & Promotions module with its own schema/role (SECURITY.md, Secret handling rule 10); promotion windows stored with explicit market-timezone semantics and evaluated against server time; the evaluation contract is idempotent and side-effect-free so the order service can call it authoritatively at submission (ARCHITECTURE.md, "Server bounded contexts" #3: "the order service is final authority on applied promotions"). Presentation naming stays in the presentation layer via design tokens/strings (ARCHITECTURE.md, Dependency rule 8) — the engine's data model uses neutral legal/technical names.
- **Anti-Patterns**: MUST NOT trust client-displayed or cached discount state at submission (CC-PRC-006); MUST NOT default to stacking (CC-PRC-006); MUST NOT use floating point or unchecked arithmetic for discounts (CC-PRC-003); MUST NOT emit "Eviction Specials" (or any DESIGN.md §5.3 branding) into invoice line-item legal descriptions (CC-PRC-007); MUST NOT let a failed evaluation grant a discount (fail closed, SECURITY.md, Logging rule 2).
- **AI Development Guidance**: Identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); mutation testing on money code per CC-QA-001. PR cites CC-PRC-006/007 (REQUIREMENTS.md §17).

## Open Questions
- "Stacking rules (default: no stacking)" implies configurable non-default stacking, but the permitted semantics (which combinations, precedence, ordering of percentage vs fixed) are unspecified.
- The specs do not define the canonical timezone for multi-timezone markets (the US spans several); "market timezone" needs a per-market definition (likely one IANA zone per market, but the mapping is not in the specs).
- Whether promotion create/update/expire actions are "privileged actions" requiring audit events per CC-DSH-004 (they affect money but the dashboard module list in CC-DSH-003 does not name promotion management) is unspecified.
- Interaction between promotions and preorder (restocking) items — whether a promotion applies at submission time or capture time for preorders — is unspecified.
