# 003 · Shared kernel: Market, Locale, and SKU identity types

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-001 (primary); definitions per REQUIREMENTS.md §2; kernel placement per ARCHITECTURE.md, Dependency rule 9
- **Title**: Shared kernel: Market, Locale, and SKU identity types
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The shared kernel MUST provide immutable value types for Market (exactly the six launch markets US, ES, MX, DE, JP, IN), Locale (a BCP 47 language/region string, selectable independently of market), and SKU identity, and these MUST be the only cross-context representations of those concepts.
- **Rationale**: REQUIREMENTS.md §2 defines Market (a commercial region with its own catalog, currency, tax rules, and compliance regime) and Locale (BCP 47, independent of market — a user MAY shop the DE market in English). CC-MKT-001 [P1] fixes exactly six launch markets. ARCHITECTURE.md Dependency rule 9 places market/locale identifiers and SKU identity in the minimal shared kernel so every bounded context — above all the Market & Gating Policy single enforcement point (Dependency rule 1) — speaks the same identity language. A typed Market/Locale distinction is what later gating code keys on: gating decisions key exclusively off server-side transacting-market state, never off client locale hints (SECURITY.md, Authentication and authorization rule 10; CC-SEC-012).
- **Design**: N/A for the value types. The header exposes market and language as two independent controls (DESIGN.md §7, "Region and language switcher"); that UI is issue 063 — the type-level independence here is what makes it implementable.

## Scope
- **Applies To**: Both
- **Components**: SharedKernel value types only: Market identifier (closed set of six), Locale identifier (BCP 47), SKU identifier. Excluded: market policy/gating data (issue 023), market/locale resolution and persistence (024), gating enforcement (025), SKU domain model with food data (029), currency/price models (002/032), locale string resources (064).
- **Actors**: All server bounded contexts; Angular clients consume these identities via typed API responses (SECURITY.md, Input validation rule 1)
- **Data Classification**: Public (market codes, locale tags, SKU IDs carry no personal data)

## Security Context
- **Defense Layer**: Strict API (closed, validated identity types instead of free strings)
- **Threat(s) Addressed**: Market-gating bypass via forged or malformed market/locale values (CC-MKT-003/004; CC-SEC-012 — geolocation and client locale hints are untrusted); parsing ambiguity where a client-supplied string is confused with server-side transacting-market state
- **Trust Boundary**: These types are the canonical server-side representation; any client-supplied market/locale string must be parsed and validated into them at the boundary before use (SECURITY.md, Input validation rule 1)
- **Zero Trust Consideration**: Construction validates against closed/allowlisted sets (six markets; seven launch locales for UI strings per CC-I18N-001; BCP 47 syntax); invalid input is rejected, never coerced (SECURITY.md, Input validation rule 1 — reject, don't sanitize into acceptance).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 Validation and Business Logic (typed, allowlisted input at boundaries), under the platform-wide Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A at type level (the IN veg-only regime these types enable is CC-MKT-003 and issue 025)
- **Other**: BCP 47 (locale tags, per REQUIREMENTS.md §2)

## Acceptance Criteria
1. **AC-01**: Given the Market type, when its allowed values are enumerated, then they are exactly US, ES, MX, DE, JP, IN — no more, no fewer (CC-MKT-001).
2. **AC-02**: Given a market code outside the launch set (e.g., "FR", "XX", empty, or mixed-case garbage), when Market parsing is attempted, then construction is rejected with a typed failure and no Market value is produced (negative case; CC-MKT-001; SECURITY.md, Input validation rule 1).
3. **AC-03**: Given the Locale type, when constructed from a syntactically valid BCP 47 tag, then it round-trips its canonical string form; when constructed from a malformed tag, then it is rejected (REQUIREMENTS.md §2; CC-I18N-001).
4. **AC-04**: Given Market and Locale types, when their APIs are inspected, then neither derives from or converts implicitly to the other — a Locale can never be used where a Market is required and vice versa (REQUIREMENTS.md §2: independent selections; CC-MKT-002; CC-SEC-012).
5. **AC-05**: Given the SKU identity type, when two SKU IDs are compared, then equality is by value, and a SKU ID is distinct from arbitrary strings at the type level (CC-CAT-001: every SKU carries a unique ID; §2 veg/non-veg classification stays in the Catalog context, issue 029 — not on the identity type).
6. **AC-06**: Given the architecture tests from issue 001, when any bounded context defines its own market, locale, or SKU identifier type, then the build fails (ARCHITECTURE.md, Dependency rule 9).
7. **AC-07**: Given the value types, when reviewed, then they are immutable value types carrying no behavior belonging to other contexts (no catalog data, no pricing, no gating policy) — value types only.

## Failure Behavior
- **On Invalid Input**: Parsing/construction failures throw or return a typed failure; at HTTP boundaries the consuming endpoint rejects with 400 and an RFC 9457 problem details body with no internal detail (SECURITY.md, Input validation rules 1–2; Logging rule 1).
- **On System Error**: Fail closed — an unparseable market/locale never falls back to a default market inside these types; defaulting/proposal logic is the resolution flow's concern (issue 024, CC-MKT-002) and gating paths treat exceptions as denial (SECURITY.md, Logging rule 2).
- **Alerting**: Validation rejections at boundaries are logged as structured security events per SECURITY.md, Logging rule 3 (implemented by consuming endpoints; platform alerting in issue 022).

## Test Strategy
- **Unit Tests**: Market closed-set enumeration and rejection cases; Locale BCP 47 parsing (valid launch locales en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN, plus malformed rejection); SKU ID value equality; immutability; type-level independence of Market and Locale.
- **Integration Tests**: N/A at type level (boundary parsing is exercised by consuming endpoints' issues; the market-gating matrix is issue 027).
- **Security Tests**: Mutation testing SHOULD cover these types — gating code is a named mutation-testing target (CC-QA-001) and these types are its foundation; fuzz/property tests on Locale parsing with hostile inputs.
- **Compliance Tests**: N/A.
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-MKT-001, CC-I18N-001 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (SharedKernel project and architecture tests).
- **Downstream**: 002 "Shared kernel: Money type" (currency set is fixed per market by CC-PRC-001); 023 "Market & Gating Policy: policy-as-data model"; 024 "Transacting market/locale resolution"; 025 "Server-side gating enforcement API"; 029 "SKU domain model"; 031 "Per-market per-locale catalog search"; 034 "Locale-aware price formatting"; 064 "ICU MessageFormat resource pipeline".
- **External**: None.

## Implementation Notes
- **Constraints**: C# immutable value types in the shared kernel (ARCHITECTURE.md, Dependency rule 9); .NET's globalization facilities can back BCP 47 handling, but validation must reject rather than best-effort-normalize unknown input (SECURITY.md, Input validation rule 1); the seven launch UI locales are en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN (CC-I18N-001).
- **Anti-Patterns**: MUST NOT infer market from locale or locale from market (REQUIREMENTS.md §2; DESIGN.md §7 switcher rule: never infer one from the other silently); MUST NOT derive a Market from `Accept-Language`, geolocation, or any client hint inside these types — gating keys off server-side transacting-market state only (CC-SEC-012; SECURITY.md, Authentication and authorization rule 10); MUST NOT pass markets/locales/SKUs as raw strings across context boundaries; MUST NOT put catalog, pricing, or gating behavior on identity types.
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The SKU "unique ID" format (CC-CAT-001) is unspecified — length, alphabet, whether human-meaningful or opaque. The identity type can wrap an opaque string, but any format validation stronger than non-empty is not derivable from the specs.
- Whether the Locale type should restrict to the seven launch locales (CC-I18N-001) or accept any well-formed BCP 47 tag (REQUIREMENTS.md §2's general definition) is ambiguous; AC-03 asserts only BCP 47 validity, with the launch-locale allowlist assumed to live in Content & Localization policy (issue 064) rather than the kernel type.
- Whether the market→currency mapping of CC-PRC-001 belongs on the Market identity type or in the Pricing context (issue 032) is not specified; it is excluded from this issue's acceptance criteria.
