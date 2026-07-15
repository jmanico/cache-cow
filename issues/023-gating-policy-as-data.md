# 023 · Market & Gating Policy: policy-as-data model and configuration schema

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-001, CC-MKT-006
- **Title**: Market & Gating Policy: policy-as-data model and configuration schema
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: The platform MUST encode all market gating rules — per-market catalog gating, veg/non-veg exclusion, content-page availability, currency/tax convention, and legal content set — as per-market policy configuration data for exactly the six launch markets (US, ES, MX, DE, JP, IN), never as conditionals scattered through application code (REQUIREMENTS.md CC-MKT-001, CC-MKT-006).
- **Rationale**: Each market is an independent compliance regime (REQUIREMENTS.md §2, CC-MKT-001). Scattered per-market conditionals are how gating regressions happen; policy-as-data with a single owning bounded context makes the rules reviewable, schema-validatable, and coverable by an automated test matrix (CC-MKT-006, verified by issue 027). ARCHITECTURE.md fixes this as bounded context 1, "Market & Gating Policy": "per-market policy configuration as data ... The single enforcement point consulted by storefront rendering, search, the B2B API, and sitemap/feed generation," and Dependency rule 1 makes every user-visible surface depend on it ("nothing may implement its own market conditionals ... Clients never gate").
- **Design**: N/A (server-side data model; user-facing behaviors driven by this data are designed in DESIGN.md §8 and implemented by downstream issues).

## Scope
- **Applies To**: Both
- **Components**: Market & Gating Policy bounded context (ARCHITECTURE.md, "Server bounded contexts" 1); its PostgreSQL schema and owned configuration data; the policy configuration schema and its CI validation.
- **Actors**: All server-side rendering/query paths as policy consumers (storefront SSR, search, B2B API, sitemap/feed generation — via issue 025); platform engineers authoring policy configuration.
- **Data Classification**: Internal (configuration data; no PII).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Compliance-gating bypass through inconsistent, duplicated market logic (e.g., non-veg SKUs reaching the IN market, CC-MKT-003); OWASP Top Ten A01:2021 Broken Access Control (gating is an access-control decision over market-restricted content); STRIDE: Tampering (unvalidated policy change), Information Disclosure (gated content leaking to the wrong market).
- **Trust Boundary**: None crossed at runtime by this issue itself — the policy store is server-side, on private networking only (ARCHITECTURE.md, Technology decisions). The boundary this issue protects is architectural: it is the single source all boundary-facing surfaces must consult (ARCHITECTURE.md, Dependency rule 1).
- **Zero Trust Consideration**: Policy configuration is treated as validated input: schema-validated before use (SECURITY.md, Input validation rule 1 pattern), rejected — not repaired — when invalid. No client input influences policy content; clients never gate (ARCHITECTURE.md, Dependency rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Architecture/design and Access Control chapters (centralized, data-driven enforcement of access decisions).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement via centralized policy), CM-6 (configuration settings under validation)
- **NIST SP 800-207**: Policy decisions computed by a central policy engine from server-held state, not endpoint-local logic.
- **Regulatory**: IN market compliance regime requiring vegetarian-only catalog and FSSAI marking is the driving regime encoded here (REQUIREMENTS.md CC-MKT-003, CC-CNT-006, CC-CMP-004); DE legal content set (Impressum, Widerrufsbelehrung — CC-CNT-005).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the policy data model, when markets are enumerated, then exactly six markets exist — US, ES, MX, DE, JP, IN — each carrying an independent catalog-gating policy, currency (USD, EUR, MXN, EUR, JPY, INR per CC-PRC-001), tax-display convention (per CC-PRC-002), and legal content set (per CC-CNT-005), and no seventh market can be introduced without a schema-level change (CC-MKT-001).
2. **AC-02**: Given the IN market policy record, when it is read, then it encodes as data: veg-only catalog gating (non-veg SKU exclusion, CC-MKT-003), "Meet our Cuts" unavailable and "Meet our Cows" promoted to primary navigation (CC-MKT-005), and the FSSAI veg-mark rule / non-veg-mark prohibition (CC-CNT-006) — with no IN-specific branch required in any consuming module (CC-MKT-006).
3. **AC-03**: Given the published policy configuration schema, when a policy document with a missing required field, an unknown market code, or an unknown gating attribute is submitted to CI validation, then validation fails and the change is blocked before merge (CC-MKT-006; SECURITY.md, Input validation rule 1 — reject, don't sanitize).
4. **AC-04**: Given an automated architecture/lint test over the .NET solution, when any bounded context other than Market & Gating Policy contains hardcoded market-conditional logic (e.g., branching on a market identifier to gate content), then the test fails the merge (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1).
5. **AC-05**: Given a policy lookup for a market code not among the six launch markets, when any consumer requests policy, then the lookup fails closed — the request is treated as most-restrictive/denied, never defaulted to an ungated policy (SECURITY.md, Logging rule 2).
6. **AC-06** (negative): Given any consuming surface, when the Market & Gating Policy context is unavailable or returns an error, then no consumer falls back to a locally hardcoded or cached-ungated policy that would widen access (SECURITY.md, Logging rule 2).

## Failure Behavior
- **On Invalid Input**: Invalid policy configuration is rejected at CI validation time (build fails) and at load time (service refuses to serve an invalid policy set); runtime policy lookups for unknown markets return a deny/most-restrictive result. No internal schema details disclosed to clients (RFC 9457 generic errors per SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception in a policy load or gating path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Policy-load failures and unknown-market lookups logged as structured security events to Azure Monitor/Log Analytics with alerting (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the policy model: six-market enumeration, per-market attribute completeness, schema validation acceptance/rejection cases, fail-closed unknown-market behavior. Tagged CC-MKT-001, CC-MKT-006 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests loading the real policy configuration into the module and asserting every consuming query path resolves policy through the single context boundary; PostgreSQL schema-level test that the policy schema is owned by the Market & Gating Policy role only (SECURITY.md, Secret handling rule 10).
- **Security Tests**: Architecture test (AC-04) as a CI gate against scattered market conditionals; SAST gates per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: This issue's data model is the substrate for the market-gating test matrix (issue 027, CC-QA-003); CI evidence: schema-validation job output retained.
- **Coverage Target**: ≥ 80% line coverage for the module (CC-QA-001); mutation testing SHOULD run on gating code (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold (bounded-context boundaries); 003 Shared kernel: Market, Locale, and SKU identity types; 015 PostgreSQL Flexible Server (per-context schemas/roles).
- **Downstream**: 024 Transacting market/locale resolution; 025 Server-side gating enforcement API; 026 404 semantics; 027 Market-gating CI test matrix; 028 Cache-safe gating; 031 catalog search; 034 tax-display conventions; 055 B2B scope/tenant enforcement with IN gating parity; 071 SEO surfaces; 074/075 Cows/Cuts pages; 077 legal pages.
- **External**: None.

## Implementation Notes
- **Constraints**: Implement as the Market & Gating Policy bounded context in the .NET 10 modular monolith with its own PostgreSQL schema and least-privilege role over TLS (ARCHITECTURE.md, "Cross-cutting"; SECURITY.md, Secret handling rule 10). Policy attributes must cover at minimum: catalog gating (veg/non-veg exclusion), content-page availability (Cuts/Cows placement per CC-MKT-005), currency and tax-display convention (CC-PRC-001/002 references), and legal content set (CC-CNT-005). Schema is the single source of truth (ARCHITECTURE.md, Dependency rule 7).
- **Anti-Patterns**: MUST NOT scatter per-market `if`/`switch` conditionals through consuming modules (CC-MKT-006); MUST NOT let clients gate (ARCHITECTURE.md, Dependency rule 1); MUST NOT default an unknown market to an ungated policy; MUST NOT hand-maintain a parallel copy of the policy schema (ARCHITECTURE.md, Dependency rule 7).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Gating code is explicitly named for mutation testing (CC-QA-001).

## Open Questions
- The storage and change-management mechanism for policy configuration is unspecified: versioned configuration in the repository, database rows administered via the dashboard, or both. If policy is editable at runtime via the dashboard, edits would be privileged actions requiring audit events (CC-DSH-004) — the specs do not say whether runtime editing exists at all.
- Whether per-market policy data includes the serviceable postal-code sets used at checkout (CC-FUL-002) or those live in the Fulfillment context is not stated; excluded from this issue's scope pending clarification.
