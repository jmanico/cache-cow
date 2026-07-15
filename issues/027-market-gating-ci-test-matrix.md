# 027 · Market-gating CI test matrix

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-QA-003, CC-MKT-006
- **Title**: Market-gating CI test matrix
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: A market-gating test matrix — every market × veg/non-veg SKU × storefront/search/API/sitemap, asserting CC-MKT-003 through CC-MKT-007, plus every gated route × every market — MUST run as a blocking gate on every merge (REQUIREMENTS.md CC-QA-003, CC-MKT-006).
- **Rationale**: Market gating is the platform's highest-stakes compliance behavior (IN veg-only, CC-MKT-003), and CC-MKT-006 requires that the policy-as-data rules "be covered by an automated test matrix (every gated route x every market)". A merge-blocking matrix is what makes the policy-as-data architecture (issue 023) and single enforcement point (issue 025) verifiable on every change; SECURITY.md Deployment rule 8 mandates running it on every merge. The production IN gating probe (CC-NFR-003, issue 096) is the runtime counterpart; this issue is the CI counterpart.
- **Design**: N/A (CI infrastructure; asserts server response content, not visual presentation).

## Scope
- **Applies To**: Both
- **Components**: CI pipeline (merge-gate stage per issue 006); ASP.NET Core integration-test harness exercising storefront rendering, catalog search, B2B API endpoints, and sitemap/feed generation against seeded veg and non-veg SKUs across all six markets.
- **Actors**: CI system; engineers whose merges are gated; indirectly every consumer/partner protected by the asserted behavior.
- **Data Classification**: Internal (test fixtures and CI results; fixture SKUs are synthetic).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Regression risk: any code change silently breaking market gating (non-veg leakage into IN, wrong 404 semantics, catalog narrowing in full-catalog markets); OWASP Top Ten A01:2021 Broken Access Control (regression class); STRIDE: Information Disclosure via regression.
- **Trust Boundary**: N/A at runtime — this control operates at the CI/CD boundary: no change crosses into the deployable artifact without the matrix passing (SECURITY.md, Deployment rules 7–8).
- **Zero Trust Consideration**: The matrix does not trust the implementation's internal flags — it asserts on serialized response output (bodies, status codes, sitemap XML) exactly as a client would observe it, per market.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Access Control chapter verification; ASVS-aligned verification-by-test of access-control behavior.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SA-11 (developer security testing and evaluation), CM-3 (change control gating)
- **NIST SP 800-207**: N/A
- **Regulatory**: Continuously verifies the IN market compliance regime (CC-MKT-003/004) before any change ships.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the CI pipeline, when any merge is attempted, then the full matrix executes: all six markets (US, ES, MX, DE, JP, IN) × veg and non-veg fixture SKUs × the four surfaces (storefront rendering, search, B2B API, sitemap), with no skipped cells (CC-QA-003).
2. **AC-02**: Given the IN cells, when they execute, then they assert: zero non-veg SKUs in any IN response payload across all four surfaces (CC-MKT-003), HTTP 404 — not 403, not a redirect — for direct non-veg product URLs (CC-MKT-004), "Meet our Cuts" unreachable and "Meet our Cows" in primary navigation (CC-MKT-005).
3. **AC-03**: Given the US, ES, and DE cells, when they execute, then they assert the full catalog including non-veg SKUs is served, and given all six markets' cells, vegetarian SKUs are present and filterable (CC-MKT-007).
4. **AC-04**: Given the gated-route dimension, when the matrix executes, then every route registered as gated in the policy data (issue 023) is exercised against every market — the route list is derived from the policy configuration, not hand-maintained (CC-MKT-006; ARCHITECTURE.md, Dependency rule 7).
5. **AC-05**: Given cross-market cart preservation rules exist (CC-MKT-008), when the matrix scope is reviewed, then CC-MKT-008 behavior is covered by issue 068's tests, and this matrix asserts CC-MKT-003 through CC-MKT-007 as specified (CC-QA-003).
6. **AC-06** (negative): Given a seeded fault that introduces a non-veg SKU into an IN response on any of the four surfaces (or flips a 404 to a redirect), when the matrix runs, then at least one cell fails and the merge is blocked — verified by an intentional fault-injection check of the matrix itself (CC-QA-003; CC-QA-001 assertion-quality clause).
7. **AC-07**: Given any matrix cell failure, when CI completes, then the merge is blocked (no override short of a human-reviewed change) and the failing cell identifies market, SKU class, surface, and violated CC-* ID (CC-QA-002; SECURITY.md, Deployment rules 7–8).
8. **AC-08**: Given the test suite, when tests are inspected, then every matrix test is tagged with the CC-* IDs it verifies, feeding the requirements-to-tests coverage report (REQUIREMENTS.md §17; issue 007).

## Failure Behavior
- **On Invalid Input**: N/A at runtime — a malformed matrix definition (e.g., a market or gated route present in policy data but absent from the matrix) fails the CI job itself rather than silently narrowing coverage.
- **On System Error**: Fail closed: matrix errors, timeouts, or infrastructure failures block the merge — an unrun matrix is a failed gate, never a pass (SECURITY.md, Deployment rule 7 discipline; Logging rule 2 principle applied to CI).
- **Alerting**: CI failure notifies the merging engineer through the pipeline; repeated gating-matrix failures on main are surfaced like any broken merge gate (issue 006).

## Test Strategy
- **Unit Tests**: The matrix generator itself (deriving cells from policy data, AC-04) is unit-tested: cell completeness, tag correctness.
- **Integration Tests**: The matrix *is* an integration suite: ASP.NET Core integration tests (in-process test server) issuing real requests per market against seeded fixtures and asserting on serialized responses — HTML/SSR payloads, search results, `/v1` API responses, sitemap XML.
- **Security Tests**: Fault-injection verification (AC-06) proves the matrix detects gating regressions — the mutation-testing spirit CC-QA-001 requires for gating code.
- **Compliance Tests**: The matrix output is itself the automated compliance evidence for CC-MKT-003–007 per merge; results retained with the CI run.
- **Coverage Target**: ≥ 80% line coverage for matrix-owned code (CC-QA-001); coverage MUST NOT be met via assertion-free tests (CC-QA-001) — every cell carries explicit assertions.

## Dependencies
- **Upstream**: 006 CI merge gates (pipeline stage to hook into); 023 Market & Gating Policy data model (source of markets and gated routes); 025 Server-side gating enforcement API (behavior under test); 026 404 semantics (behavior under test); 007 Requirement traceability (test tagging); 029 SKU domain model (veg/non-veg fixtures); 053 B2B API scaffold (API surface cells).
- **Downstream**: 096 Per-market synthetic probes (production counterpart of the same assertions); 055 B2B IN gating parity test (CC-API-007) extends the API cells.
- **External**: None.

## Implementation Notes
- **Constraints**: Runs on every merge as a blocking gate alongside money-path and authz suites (SECURITY.md, Deployment rule 8). Derive the matrix dimensions from the policy configuration schema (issue 023) so a newly gated route or market automatically expands the matrix — no hand-maintained parallel list (ARCHITECTURE.md, Dependency rule 7). Assert on serialized output (response bytes/status/headers), never on internal gating flags, so client-side-hiding regressions are caught (CC-MKT-003).
- **Anti-Patterns**: MUST NOT skip cells for speed (a partial matrix is a failed gate); MUST NOT assert on view-model state instead of serialized responses; MUST NOT allow assertion-free cells to satisfy coverage (CC-QA-001); MUST NOT hand-maintain the gated-route list separately from policy data.
- **AI Development Guidance**: AI-generated test code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7) — especially relevant here since weak AI-generated assertions would hollow out the platform's primary compliance gate.

## Open Questions
- CC-QA-003 names four surfaces (storefront/search/API/sitemap) while CC-MKT-003 also lists recommendations, structured data, and feeds as exclusion channels; whether the matrix must add cells for those three channels or they are covered within "storefront"/"sitemap" assertions is unspecified. The matrix here covers the four named surfaces; structured data/feeds assertions are proposed as part of the storefront/sitemap cells pending confirmation.
- Whether the matrix must run against a fully composed application (in-process test server) versus a deployed staging environment is unspecified; DAST against staging is separately mandated per release (CC-QA-007).
