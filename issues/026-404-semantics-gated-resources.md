# 026 · 404 semantics for market-gated resources

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-004
- **Title**: 404 semantics for market-gated resources
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Requesting a non-veg product URL in the IN market MUST return HTTP 404 — not 403 and not a redirect to the product in another market — applying the platform's hardening default of returning 404 for inaccessible resources so their existence is not confirmed (REQUIREMENTS.md CC-MKT-004; SECURITY.md, Authentication and authorization rule 9).
- **Rationale**: A 403 or a cross-market redirect confirms to an IN visitor that a non-veg product exists and is deliberately withheld — exactly the disclosure the IN compliance regime must avoid. SECURITY.md Authentication rule 9 generalizes this: "Return 404 for inaccessible resources as a derived hardening default (avoids confirming resource existence); the 404-not-403 behavior is a hard requirement for non-veg product URLs in the IN market (CC-MKT-004)." Direct URL requests are the channel that catalog exclusion (issue 025) does not cover, so this closes the remaining path.
- **Design**: DESIGN.md §5.1 — the 404 page is the "Signal lost" page, one of the four permitted smoke-arc placements; DESIGN.md §9 error voice (state what happened and what to do next, no mascots in error states). The gated-404 response uses the same 404 experience as a genuinely nonexistent URL.

## Scope
- **Applies To**: Both
- **Components**: Storefront SSR route handling (product detail and any gated content route, e.g., "Meet our Cuts" in IN per CC-MKT-005); B2B API resource endpoints (parity per CC-API-007, wired in issue 055); Market & Gating Policy enforcement point (issue 025) as the decision source.
- **Actors**: Anonymous and authenticated consumers in any market; B2B API clients; crawlers hitting product URLs.
- **Data Classification**: Public content, market-gated; the protected asset is the non-observability of gated resources.

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Resource-existence disclosure via distinguishable error responses (enumeration of gated SKUs from IN); OWASP Top Ten A01:2021 Broken Access Control; CWE-204 (observable response discrepancy); STRIDE: Information Disclosure.
- **Trust Boundary**: Client–server edge: URL path and identifiers are attacker-controlled input; the gating decision and status-code selection happen entirely server-side from transacting-market state (CC-SEC-012).
- **Zero Trust Consideration**: Every direct resource request is independently authorized against the transacting market at request time — no assumption that gated URLs are unreachable merely because they are unlinked (CC-MKT-003 removes links; this requirement covers direct access).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Access Control chapter (deny by default; no resource-existence disclosure to unauthorized parties).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), SI-11 (error handling that does not reveal exploitable information)
- **NIST SP 800-207**: Per-request access evaluation for every resource, regardless of how the URL was obtained.
- **Regulatory**: Serves the IN market compliance regime (CC-MKT-003/004 context).
- **Other**: RFC 9457 problem details for API error bodies (SECURITY.md, Logging rule 1; CC-API-006).

## Acceptance Criteria
1. **AC-01**: Given transacting market IN, when a non-veg product URL is requested (storefront PDP route), then the response is HTTP 404 with the standard 404 experience (CC-MKT-004; DESIGN.md §5.1).
2. **AC-02** (negative): Given transacting market IN, when a non-veg product URL is requested, then the response MUST NOT be 403, MUST NOT be any 3xx redirect to the product in another market, and MUST NOT include headers or body content referencing the product or an alternate market (CC-MKT-004).
3. **AC-03**: Given transacting market IN, when a non-veg product URL and a genuinely nonexistent product URL are each requested, then the two responses are indistinguishable in status code, body shape, and headers — no observable discrepancy confirms existence (SECURITY.md, Authentication rule 9; CWE-204).
4. **AC-04**: Given transacting market US (or ES/DE), when the same non-veg product URL is requested, then the product page is served normally — gating is per transacting market, not global (CC-MKT-007).
5. **AC-05**: Given the IN market, when a gated content route ("Meet our Cuts", CC-MKT-005) is requested directly, then the same 404-not-403, no-redirect semantics apply (CC-MKT-005 reachability; SECURITY.md, Authentication rule 9 default).
6. **AC-06**: Given a B2B API client whose context resolves to the IN market, when a non-veg SKU resource is requested by URL/ID, then the API returns 404 with an RFC 9457 body, consistent with storefront semantics (CC-API-007 parity — full parity suite in issue 055; CC-API-006).
7. **AC-07** (negative): Given an exception in the gating decision while serving such a request, when the response is produced, then it is 404/denial — never the ungated product page (SECURITY.md, Logging rule 2).

## Failure Behavior
- **On Invalid Input**: Malformed product identifiers return the same 404 experience; API surfaces return RFC 9457 problem details with no internal identifiers or stack detail (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — gating-path exceptions produce 404/denial, never the gated resource (SECURITY.md, Logging rule 2).
- **Alerting**: Gated-404 events are logged as structured security/gating events (authz-denial class, SECURITY.md, Logging rule 3); the production IN gating probe (issue 096) asserts CC-MKT-004 continuously and alerts on violation (CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the route-handling decision: market × SKU-classification → status-code mapping, including exception paths. Tagged CC-MKT-004 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests requesting real gated and nonexistent URLs per market, asserting status code, absence of redirects, and byte-level indistinguishability of the two 404 classes (AC-03).
- **Security Tests**: Enumeration attempt test (sweep of SKU identifiers from IN asserting uniform 404s); DAST against staging includes gated-URL probing (CC-QA-007).
- **Compliance Tests**: Covered as the CC-MKT-004 assertions of the market-gating CI matrix (issue 027, CC-QA-003) and the production IN probe (issue 096).
- **Coverage Target**: ≥ 80% line coverage (CC-QA-001); mutation testing SHOULD cover this gating code (CC-QA-001).

## Dependencies
- **Upstream**: 025 Server-side gating enforcement API (gating decision source); 024 Transacting market/locale resolution; 021 RFC 9457 error handling and fail-closed authorization/gating behavior.
- **Downstream**: 027 Market-gating CI test matrix; 055 B2B IN gating parity (CC-API-007); 062 Object-level authorization suite (shares the 404-not-403 hardening default of SECURITY.md, Authentication rule 9); 096 Per-market synthetic probes.
- **External**: None.

## Implementation Notes
- **Constraints**: Return `NotFound` from the route/resource handler when the enforcement point (issue 025) reports the resource gated for the transacting market — the same result path as a nonexistent ID, so the two are structurally indistinguishable (AC-03). API bodies use RFC 9457 problem details (CC-API-006); storefront uses the standard 404 page (DESIGN.md §5.1). Note the gated 404 must also not be cached across markets — cache keying per issue 028 (CC-MKT-009) ensures an IN 404 is never served to a US session or vice versa.
- **Anti-Patterns**: MUST NOT return 403 for gated resources; MUST NOT redirect to the product in another market (CC-MKT-004); MUST NOT emit distinguishable error bodies, headers, or debug detail that confirm existence (SECURITY.md, Logging rule 1); MUST NOT fail open on gating exceptions (SECURITY.md, Logging rule 2).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Whether response-timing uniformity between "gated" and "nonexistent" 404s is required is not specified; the specs require indistinguishable responses (no 403/redirect) but name constant-time behavior only for OTP comparison (SECURITY.md, Authentication rule 13). Recorded rather than asserted.
- HTTP status for non-veg SKUs requested through IN *search/listing* endpoints is defined by exclusion (they simply never appear, CC-MKT-003); only direct resource URLs are covered here. No ambiguity found beyond that split.
