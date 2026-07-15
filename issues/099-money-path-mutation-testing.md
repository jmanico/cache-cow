# 099 · Money-path and mutation-testing suite

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-QA-004, CC-QA-001
- **Title**: Money-path and mutation-testing suite
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Operational

## Requirement
- **Description**: The platform MUST run a money-path test suite on every merge covering recomputation authority (CC-PRC-005), idempotent submission (CC-ORD-005, CC-API-005), promotion start/end/timezone boundaries, and rounding in all five currencies including JPY zero-decimal and INR grouping, and coverage MUST NOT be met via assertion-free tests, with mutation testing that SHOULD run on money, gating, and authorization code (REQUIREMENTS.md CC-QA-004, CC-QA-001).
- **Rationale**: Money paths are where defects become direct financial and legal loss: client-supplied prices must never be honored (CC-PRC-005), double submission must never double-charge (CC-ORD-005, CC-API-005, CC-SEC-015), expired promotions must not apply (CC-PRC-006), and monetary arithmetic must be exact and overflow-checked in integer minor units across USD, EUR, MXN, JPY, and INR — including JPY zero-decimal and INR lakh/crore grouping with attacker-influenced quantities (CC-PRC-001/003). CC-QA-001 hardens the gate itself: 80% line coverage per package enforced in CI, but coverage "MUST NOT be met via assertion-free tests", with mutation testing recommended precisely on money, gating, and authz code because those are the paths where a green-but-toothless suite is most dangerous. SECURITY.md, Deployment rule 8 mandates that the money-path tests run on every merge.
- **Design**: N/A (test infrastructure; presentation of prices is verified against DESIGN.md §4.4 worked examples where formatting assertions apply).

## Scope
- **Applies To**: Both
- **Components**: Test suites over the Pricing & Promotions and Ordering & Payments bounded contexts and the shared-kernel Money type (ARCHITECTURE.md, "Server bounded contexts" 3–4; Dependency rule 9); the B2B API order-creation path (idempotency parity); CI jobs: money-path suite on every merge, assertion-quality enforcement, mutation-testing runs.
- **Actors**: CI pipeline; developers. Simulated actors inside tests: consumers (guest and account), B2B partners, and a malicious client submitting tampered monetary values.
- **Data Classification**: Internal (test code and fixtures; no production data — fixtures are synthetic).

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Price manipulation via client-supplied monetary values (OWASP Top Ten A04:2021 Insecure Design / business-logic abuse); duplicate charge or duplicate order via replay (CC-SEC-015; STRIDE: Tampering, Repudiation); integer overflow in monetary arithmetic with attacker-influenced quantities (CWE-190; CC-PRC-003); promotion-window abuse across market timezones (CC-PRC-006). The suite is the regression barrier that keeps these controls enforced.
- **Trust Boundary**: The suite exercises the client-server boundary as an adversary: client-submitted order payloads (prices, quantities, idempotency keys) are the untrusted input class under test (SECURITY.md, Input validation rules 1, 3, 12).
- **Zero Trust Consideration**: Tests assert the server independently recomputes every monetary value from canonical data and ignores client-supplied prices (CC-PRC-005; SECURITY.md, Input validation rule 3 — server-controlled fields from server state only), and that idempotency keys are tenant/session-scoped and fingerprint-bound rather than trusted at face value (CC-SEC-015).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Input Validation and Business Logic chapters (server-side recomputation, anti-automation/replay); Architecture/design chapter for the verification-gate discipline.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation), SA-11 (developer testing and evaluation)
- **NIST SP 800-207**: Server never trusts client-asserted transaction values; every monetary decision is recomputed from server-held canonical state, and the suite verifies it continuously.
- **Regulatory**: Market tax-display and computation conventions under test derive from CC-PRC-002 (MX IVA-inclusive, IN GST, US tax-exclusive, DE per-kg unit price context); exact-arithmetic mandate CC-PRC-003.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given an order submission carrying client-supplied prices, discounts, or totals that differ from canonical data, when the order is processed in the recomputation-authority tests, then the server-computed values are used and the client-supplied values have no effect on any stored or charged amount (CC-PRC-005; negative: a tampered client price MUST NOT change the total).
2. **AC-02**: Given a consumer or B2B order submitted twice with the same idempotency key, when the money-path suite runs, then exactly one order and one charge exist and the replay returns the original result; and the same key with a different request body is rejected with 409, never served the original and never processed as new (CC-ORD-005, CC-API-005, CC-SEC-015).
3. **AC-03**: Given promotions with start/end timestamps in each market's timezone, when orders are submitted just before start, at start, just before end, and just after end (including across a timezone boundary between user and market), then the promotion applies only within its window, the order service is final authority over any cached UI state, and default no-stacking is enforced (CC-PRC-006, CC-QA-004).
4. **AC-04**: Given monetary computations in all five currencies (USD, EUR, MXN, JPY, INR), when quantity × unit price, discount, tax, and total arithmetic runs in tests, then all values are integer minor units (or exact decimal), JPY has zero decimal places, INR grouping is asserted via locale formatting (lakh/crore per DESIGN.md §4.4, never hand-formatted), and no binary floating point appears anywhere including test code (CC-PRC-001/003/004, CC-QA-004).
5. **AC-05**: Given attacker-influenced large quantities, when monetary arithmetic would overflow, then the operation fails closed (checked operation raises/rejects) rather than wrapping, verified for INR/JPY large-magnitude cases (CC-PRC-003).
6. **AC-06**: Given the CI coverage gate, when a package's 80% line coverage is achieved, then an assertion-quality check verifies the contributing tests assert outcomes — coverage met via assertion-free tests fails the gate (CC-QA-001).
7. **AC-07**: Given the mutation-testing job over money, gating, and authz code, when it runs (SHOULD, per CC-QA-001), then surviving mutants in those modules are reported and tracked; the job's scope explicitly includes the Money type, pricing/promotion arithmetic, and order recomputation paths.
8. **AC-08**: Given any merge to main, when CI runs, then the money-path suite executes and is blocking (SECURITY.md, Deployment rule 8; CC-QA-002).

## Failure Behavior
- **On Invalid Input**: The behaviors under test must reject invalid monetary input per RFC 9457 with 400 (validation) or 409 (idempotency-key/body mismatch) without processing (SECURITY.md, Input validation rules 2, 12; Logging rule 1); the suite asserts these rejections occur and that no order/charge side effects exist afterward.
- **On System Error**: Fail closed, twice over: (a) the systems under test — any exception in a money or authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2); (b) the gate — if the money-path suite or assertion-quality check cannot run, the merge is blocked, never waved through (CC-QA-002).
- **Alerting**: CI gate failure blocks merge and is visible on the PR; surviving-mutant reports from mutation runs are surfaced in CI output for triage. No runtime alerting applies to the suite itself.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests: Money type arithmetic (minor units, checked overflow, currency mismatch rejection), per-currency rounding tables (JPY zero-decimal, INR magnitudes), promotion-window edge cases in market timezones, `Intl.NumberFormat`-equivalent server formatting against DESIGN.md §4.4 worked examples. Tagged with the CC-* IDs they verify: CC-PRC-001/003/004/005/006, CC-ORD-005, CC-API-005, CC-QA-004 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests through the real order-submission pipeline: tampered-price submissions, double submission with idempotency keys (consumer session-scoped and B2B tenant-scoped per CC-SEC-015), promotion boundary orders, cross-currency order fixtures — asserting database end-state (one order, one charge record, correct totals).
- **Security Tests**: Adversarial fixtures: negative prices, extreme quantities (overflow probes), foreign-tenant idempotency-key reuse (must not collide across tenants, CC-SEC-015), replayed keys with mutated bodies (409). These compose with the merge-gate suites of SECURITY.md, Deployment rule 8.
- **Compliance Tests**: Requirement-tagging report shows every CC-QA-004 clause mapped to at least one test (REQUIREMENTS.md §17); CI retains coverage, assertion-quality, and mutation reports as evidence.
- **Coverage Target**: ≥ 80% line coverage per package, not achievable via assertion-free tests; mutation testing SHOULD run on money, gating, and authz modules (CC-QA-001).

## Dependencies
- **Upstream**: 002 Shared kernel: Money type (the arithmetic under test); 006 CI merge gates (gate composition); 007 Requirement traceability (test tagging); 032 Per-SKU per-market price model; 033 Promotion engine; 034 Locale-aware price formatting; 036 Order submission with server-side money recomputation; 037 Idempotency service; 053 B2B API scaffold (API-side idempotent order creation).
- **Downstream**: 027 Market-gating CI test matrix and 062 Object-level authorization/IDOR suite share the mutation-testing scope (gating and authz code per CC-QA-001) — coordinate the mutation job so each suite owns its own module scope without duplication.
- **External**: None (payment processors are stubbed in tests; no live Stripe/Razorpay calls — card handling never enters the system per CC-ORD-003).

## Implementation Notes
- **Constraints**: xUnit-style .NET 10 test projects per bounded context with the shared-kernel requirement-tagged test utilities (ARCHITECTURE.md, Dependency rule 9); integration tests against PostgreSQL (exact decimal/integer money columns per ARCHITECTURE.md "Cross-cutting") rather than an in-memory substitute for money end-state assertions; the ban on binary floating point extends to test fixtures and expected values (CC-PRC-003 — "including tests"); locale formatting assertions use the platform's ICU-backed formatting, never hand-formatted expected strings for grouping (CC-PRC-004, DESIGN.md §4.4).
- **Anti-Patterns**: MUST NOT use `float`/`double` for money anywhere, including tests (CC-PRC-003); MUST NOT let coverage count assertion-free tests (CC-QA-001); MUST NOT stub out the recomputation step when asserting totals (the authority under test is the server pipeline, CC-PRC-005); MUST NOT hand-format expected currency strings (CC-PRC-004); MUST NOT hit live payment processors from CI.
- **AI Development Guidance**: AI-generated tests are prone to assertion-free scaffolding — exactly what CC-QA-001 prohibits; AI-generated test code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- No mutation-testing tool is named in the specs; tool selection for .NET 10 and the mutation-score threshold (if any — CC-QA-001 says mutation testing SHOULD run but sets no kill-rate bar) need ratification.
- The mechanism for detecting "assertion-free tests" is unspecified (mutation score, assertion-count lint, or reviewer checklist); AC-06 requires one to exist without prescribing which.
- CC-QA-004 requires promotion boundary tests for "start/end/timezone" but the specs do not define behavior for a promotion whose market-timezone window spans a DST transition (relevant for US/ES/DE/MX; JP/IN have no DST) — edge-case expectation needs a human decision before the fixture can assert it.
