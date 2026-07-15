# 002 · Shared kernel: Money type (integer minor units, overflow-checked)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-PRC-003 (verified with CC-QA-004)
- **Title**: Shared kernel: Money type (integer minor units, overflow-checked)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The shared kernel MUST provide a currency-aware Money type that stores and computes all monetary values as integer minor units (or an exact decimal type), performs all arithmetic (quantity × unit price, discount, tax, totals) with overflow-checked operations that fail closed rather than wrapping, and MUST NOT use binary floating point for money anywhere, including tests (CC-PRC-003).
- **Rationale**: CC-PRC-003 [P1] bans binary floating point for money and requires overflow-checked arithmetic that fails closed — explicitly relevant for large-grouping currencies (INR lakh/crore, JPY) and attacker-influenced quantities (a threat-model-derived clause added in REQUIREMENTS.md v1.3). ARCHITECTURE.md Dependency rule 9 places the money type (integer minor units) in the minimal shared kernel, and Dependency rule 2 makes the Ordering service the only computer of money from Pricing as canonical source — every money path in the platform flows through this type. CC-QA-004 requires money-path tests covering rounding in all five currencies including JPY zero-decimal and INR grouping.
- **Design**: N/A for the type itself. Display formatting is out of scope here (issue 034); worked per-locale price examples live in DESIGN.md §4.4 and formatting rules in CC-PRC-004.

## Scope
- **Applies To**: Both (consumed by server modules; the type is server-side shared kernel)
- **Components**: SharedKernel Money value type and its arithmetic operations; currency identifiers for the five launch currencies (USD, EUR, MXN, JPY, INR per CC-PRC-001) including minor-unit exponent awareness (JPY zero-decimal). Excluded: price storage models (issue 032), promotions (033), locale-aware display formatting (034), order-time recomputation (036).
- **Actors**: All server bounded contexts that handle money (Pricing & Promotions, Ordering & Payments, Invoicing, Wholesale & B2B API, Back Office analytics)
- **Data Classification**: Internal (a value type; monetary data it later carries is Confidential)

## Security Context
- **Defense Layer**: Strict API (a type that makes incorrect money representation and unchecked arithmetic unrepresentable)
- **Threat(s) Addressed**: Integer overflow/wraparound in monetary arithmetic under attacker-influenced quantities (CWE-190); rounding/precision corruption from binary floating point (CC-PRC-003); the threat-model finding that large-grouping currencies (INR lakh/crore, JPY) magnify overflow exposure
- **Trust Boundary**: Not itself a trust boundary, but the mandatory representation on the server side of every money-bearing boundary (client-supplied prices are ignored per CC-PRC-005)
- **Zero Trust Consideration**: Quantities and amounts entering arithmetic are treated as attacker-influenced: every operation is range- and overflow-checked and fails closed; no operation silently wraps, truncates, or coerces through floating point.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 Validation and Business Logic (business-logic integrity of monetary computation), under the platform-wide ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A at type level (market tax/invoice rules are CC-PRC-002/CC-INV-001, other issues)
- **Other**: CWE-190 (integer overflow or wraparound)

## Acceptance Criteria
1. **AC-01**: Given the Money type, when a value is constructed, then it stores an integer minor-unit amount (or exact decimal) plus a currency, and construction with an unsupported currency or a fractional minor unit invalid for that currency (e.g., JPY with sub-yen fractions) is rejected (CC-PRC-003, CC-PRC-001).
2. **AC-02**: Given two Money values in different currencies, when any arithmetic combines them, then the operation is rejected (fails closed) — no implicit FX conversion exists (CC-PRC-001: no runtime FX conversion of consumer prices).
3. **AC-03**: Given a quantity × unit price, discount, tax, or total computation whose result exceeds the representable range, when the operation executes, then it throws/fails closed rather than wrapping or saturating, including at INR- and JPY-scale magnitudes (CC-PRC-003; CC-QA-004).
4. **AC-04**: Given the codebase including test code, when the CI gate scans for binary floating point (`float`/`double`) in any money construction, computation, or assertion, then the build fails on any hit — floating point MUST NOT appear in money paths, tests included (CC-PRC-003).
5. **AC-05**: Given JPY as a zero-decimal currency, when amounts are constructed and summed, then minor-unit arithmetic honors the zero-decimal exponent and produces no fractional yen (CC-QA-004).
6. **AC-06**: Given the Money type's public API, when a caller attempts to obtain a lossy numeric view (e.g., a binary floating-point conversion), then no such member exists on the type (negative case; CC-PRC-003).
7. **AC-07**: Given the architecture tests from issue 001, when a bounded context defines its own parallel money representation, then the build fails (money type is shared kernel per ARCHITECTURE.md, Dependency rule 9).

## Failure Behavior
- **On Invalid Input**: Construction/arithmetic with invalid currency, precision, or out-of-range values throws a typed exception; at HTTP boundaries this surfaces as a generic RFC 9457 problem details response with no internal detail (SECURITY.md, Logging rule 1) — status codes are owned by the consuming endpoints' issues.
- **On System Error**: Fail closed — any overflow or invariant violation aborts the computation; no partial or wrapped result is ever returned (CC-PRC-003; SECURITY.md, Logging rule 2 posture).
- **Alerting**: Overflow failures in money paths are logged as structured validation-rejection security events to centralized monitoring (SECURITY.md, Logging rule 3); alert thresholds for spikes follow the platform alerting in issue 022.

## Test Strategy
- **Unit Tests**: Exhaustive arithmetic tests: quantity × unit price, discount, tax, totals; overflow at boundaries (max/min minor units, attacker-scale quantities); currency-mismatch rejection; JPY zero-decimal behavior; INR-magnitude values (lakh/crore scale); rounding in all five currencies (CC-QA-004). All assertions use integer/exact-decimal expectations — never floating point.
- **Integration Tests**: N/A at type level (order-path recomputation integration is issue 036/099).
- **Security Tests**: Mutation testing SHOULD run on this code — money code is explicitly named by CC-QA-001; CI gate greps money paths and their tests for binary floating point.
- **Compliance Tests**: N/A.
- **Coverage Target**: ≥ 80% per CC-QA-001, with assertion-bearing tests only (CC-QA-001 forbids assertion-free coverage); tests tagged CC-PRC-003 / CC-QA-004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (SharedKernel project exists; architecture tests enforce single money type).
- **Downstream**: 032 "Per-SKU per-market price model" (CC-PRC-001), 033 "Promotion engine" (CC-PRC-006), 034 "Locale-aware price formatting" (CC-PRC-004), 036 "Order submission: server-side money recomputation" (CC-PRC-005), 046 "Invoice core" (CC-INV-001), 099 "Money-path and mutation-testing suite" (CC-QA-004).
- **External**: None (Stripe/Razorpay integration is issues 039/040 and consumes this type's amounts).

## Implementation Notes
- **Constraints**: C# / .NET 10 shared kernel (ARCHITECTURE.md, Dependency rule 9); use checked arithmetic (`checked` context / overflow-detecting operations) so overflow throws rather than wraps; persistence must map to exact-decimal/integer PostgreSQL columns (ARCHITECTURE.md, "Data stores" — exact-decimal/integer money), though schema work belongs to later issues; the five market currencies are fixed by CC-PRC-001 (US=USD, ES=EUR, MX=MXN, DE=EUR, JP=JPY, IN=INR).
- **Anti-Patterns**: MUST NOT use `float`/`double` anywhere in money code or tests (CC-PRC-003); MUST NOT hand-format currency strings (CC-PRC-004 — formatting is `Intl.NumberFormat`/server equivalent, issue 034); MUST NOT trust client-supplied monetary values (CC-PRC-005; SECURITY.md, Input validation rule 3); MUST NOT implement implicit cross-currency arithmetic or runtime FX (CC-PRC-001); no per-context duplicate money types (ARCHITECTURE.md, Dependency rule 9).
- **AI Development Guidance**: AI-generated code passes the identical merge gates (tests, coverage, lint, SAST/SCA/secret scan, raw-HTML-sink grep) plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Money code is a mutation-testing target (CC-QA-001).

## Open Questions
- CC-PRC-003 permits "integer minor units (or exact decimal type)"; the specs do not pick one. ARCHITECTURE.md Dependency rule 9 says "money type (integer minor units)", which suggests integer minor units for the shared kernel, but the alternative is not formally closed.
- No rounding policy (e.g., rounding mode for discount/tax division) is specified anywhere; CC-QA-004 requires rounding tests per currency but the specs do not state the required rounding behavior. Excluded from acceptance criteria pending a decision.
- Whether the Money type must support allocation/proration (splitting a total without losing minor units) is not stated in the specs.
