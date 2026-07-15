# 032 · Per-SKU per-market price model

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-PRC-001
- **Title**: Per-SKU per-market price model
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: Prices MUST be defined per SKU per market in the market's currency (US=USD, ES=EUR, MX=MXN, DE=EUR, JP=JPY, IN=INR), with no runtime FX conversion of consumer prices (REQUIREMENTS.md CC-PRC-001).
- **Rationale**: Each market is an independent commercial region with its own currency and tax regime (REQUIREMENTS.md §2, CC-MKT-001); runtime FX conversion would produce unstable, non-compliant consumer prices. Monetary correctness is a P1 money path: all monetary values are integer minor units with overflow-checked arithmetic that fails closed (CC-PRC-003), and the price model is the canonical source the order service recomputes from at submission (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
- **Design**: N/A for the server-side model. Price presentation (locale formatting, tax-display convention) is issue 034 per DESIGN.md §4.4 and §7.

## Scope
- **Applies To**: Both
- **Components**: Pricing & Promotions bounded context (ARCHITECTURE.md, "Server bounded contexts" #3): price entity keyed (SKU, market) with currency fixed by market, persistence schema, and the typed read model consumed by storefront, B2B (consumer catalog surfaces), and the order service. Excludes: promotions (issue 033), formatting/display (issue 034), order-time recomputation (issue 036), wholesale price lists (issue 050 — separate tenancy boundary per CC-WHS-003).
- **Actors**: Pricing service (internal), Ordering service (canonical consumer), storefront SSR, B2B API.
- **Data Classification**: Internal (consumer prices are public once displayed; wholesale prices are out of scope here and confidential per CC-WHS-003).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Monetary integrity loss — floating-point error, overflow wrap on attacker-influenced quantities, currency confusion across markets (STRIDE: Tampering; REQUIREMENTS.md CC-PRC-003); client-supplied price acceptance (business-logic abuse — OWASP Top Ten A04:2021; guarded by CC-PRC-005, authored on issue 036's path).
- **Trust Boundary**: Prices are server-controlled fields set from server state only — never bound from client input (SECURITY.md, Input validation rule 3). Money flows one way: clients and caches display, only the Ordering service computes, from Pricing as canonical source (ARCHITECTURE.md, Dependency rule 2).
- **Zero Trust Consideration**: Nothing depends on client-supplied monetary values; clients render prices only from typed, validated responses (SECURITY.md, Input validation rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation on price administration)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A (market tax-display conventions are CC-PRC-002, issue 034; invoice tax content is CC-INV-001, issue 047).
- **Other**: ISO 4217 currency semantics as embodied in the shared Money type (issue 002), including JPY zero-decimal minor units (CC-QA-004).

## Acceptance Criteria
1. **AC-01**: Given a SKU offered in a market, when its price is defined, then the record is keyed by (SKU ID, market) and denominated in exactly that market's currency — US=USD, ES=EUR, MX=MXN, DE=EUR, JP=JPY, IN=INR — with any other currency rejected at validation (CC-PRC-001).
2. **AC-02**: Given any price value, when it is stored or computed, then it uses the shared Money type in integer minor units (issue 002); no binary floating-point representation exists anywhere on the path, including tests (CC-PRC-003 negative case).
3. **AC-03**: Given a price for one market, when a consumer surface for another market requests pricing, then no runtime FX conversion occurs — a SKU with no price in the transacting market has no consumer price there (CC-PRC-001 negative case).
4. **AC-04**: Given a JPY price, when it is stored, then its minor-unit representation honors JPY's zero-decimal currency semantics via the Money type (CC-PRC-001, CC-QA-004).
5. **AC-05**: Given arithmetic on prices (e.g., quantity × unit price with attacker-influenced quantity), when values would exceed the checked range, then the operation fails closed with an error rather than wrapping (CC-PRC-003).
6. **AC-06**: Given a price-administration write with a missing market, unknown SKU, wrong currency, or non-integer amount, when validation runs, then the write is rejected with 400 (RFC 9457) and nothing is persisted (SECURITY.md, Input validation rule 1).
7. **AC-07**: Given the order service recomputes an order (CC-PRC-005), when it reads unit prices, then it reads them from this model as the canonical source — no other component computes consumer prices (ARCHITECTURE.md, Dependency rule 2).

## Failure Behavior
- **On Invalid Input**: 400 with RFC 9457 problem details; structured validation-rejection log with correlation ID (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed (SECURITY.md, Logging rule 2): a SKU whose price cannot be resolved in the transacting market is not purchasable there; overflow-checked arithmetic throws rather than wraps (CC-PRC-003); no fallback to another market's price or any FX-derived value.
- **Alerting**: Overflow/checked-arithmetic failures and price-resolution failures on purchase paths are logged as structured events with alerting (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the price entity and Money-type usage: per-market currency invariants, JPY zero-decimal, INR large-value handling, overflow-checked multiplication/addition failing closed; tests themselves use exact types, never floats (CC-PRC-003 "including tests").
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL (issue 015): (SKU, market) uniqueness, currency CHECK constraints, exact-decimal/integer column types (ARCHITECTURE.md, "Cross-cutting", Data stores), typed read model per market.
- **Security Tests**: Money-path tests per CC-QA-004 (rounding in all five currencies, JPY zero-decimal, INR grouping at the data level); mutation testing SHOULD run on this money code (CC-QA-001); SAST/SCA gates per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Requirement-tagged test report per REQUIREMENTS.md §17.
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-PRC-001 (and CC-PRC-003 where its clauses are exercised).

## Dependencies
- **Upstream**: 002 Shared kernel: Money type (integer minor units, overflow-checked); 003 Shared kernel identity types (Market, SKU); 015 PostgreSQL Flexible Server; 021 RFC 9457 error handling; 029 SKU domain model.
- **Downstream**: 033 Promotion engine; 034 Locale-aware price formatting and tax-display conventions; 036 Order submission with server-side money recomputation (CC-PRC-005); 050 Wholesale price lists (separate boundary, CC-WHS-003); 066 Menu page; 067 Product detail page; 083 Dashboard sales analytics; 099 Money-path and mutation-testing suite.
- **External**: N/A (Stripe Tax / Razorpay tax handling sits on the order/invoice path, issues 039/040/047).

## Implementation Notes
- **Constraints**: Pricing & Promotions module schema with its own least-privilege PostgreSQL role over TLS (SECURITY.md, Secret handling rule 10); exact-decimal/integer money columns (ARCHITECTURE.md, "Cross-cutting"); the Money type from the minimal shared kernel is the only monetary representation (ARCHITECTURE.md, Dependency rule 9). Price administration surfaces bind only to dedicated DTOs with explicit source attributes (SECURITY.md, Input validation rule 3).
- **Anti-Patterns**: MUST NOT use binary floating point for money anywhere, including tests (CC-PRC-003); MUST NOT perform runtime FX conversion of consumer prices (CC-PRC-001); MUST NOT accept client-supplied prices into any computation (CC-PRC-005; SECURITY.md, Input validation rule 3); MUST NOT let wholesale prices be derivable from consumer API responses (CC-WHS-003 — wholesale is issue 050's boundary); MUST NOT hand-format currency strings (CC-PRC-004 — formatting is issue 034).
- **AI Development Guidance**: Identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); money code additionally gets mutation testing per CC-QA-001. PR cites CC-PRC-001 (REQUIREMENTS.md §17).

## Open Questions
- Behavior when a SKU carries a per-market availability flag (CC-CAT-001) for a market but no price row is undefined in the specs; this issue fails closed (not purchasable) pending confirmation.
- Price effective-dating/versioning (scheduled base-price changes, as distinct from promotions with windows in CC-PRC-006) is not addressed in the specs.
- Whether price history must be retained for audit (CC-DSH-004 covers privileged actions; a price change is presumably one, but the record content is unspecified).
