# 025 · Server-side gating enforcement API with IN veg-only exclusion

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-003, CC-MKT-007
- **Title**: Server-side gating enforcement API with IN veg-only exclusion
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: The Market & Gating Policy context MUST expose the single server-side enforcement point through which storefront rendering, search, the B2B API, and sitemap/feed generation filter content by transacting market, such that non-veg SKUs are excluded server-side from every IN response — catalog listings, product detail, search, recommendations, sitemaps, structured data, and feeds — while US, ES, and DE carry the full catalog including non-veg SKUs and vegetarian SKUs remain available and filterable in all markets (REQUIREMENTS.md CC-MKT-003, CC-MKT-007).
- **Rationale**: The IN market catalog is vegetarian-only as a compliance regime, and "client-side hiding is non-compliant" (CC-MKT-003): if non-veg data is present in any serialized IN response, the requirement is violated regardless of what the UI displays. ARCHITECTURE.md Dependency rule 1 makes this the platform's most upstream user-visible dependency: "Storefront rendering, search, feeds, sitemaps, and the B2B API all depend on the Market & Gating Policy service; nothing may implement its own market conditionals ... Clients never gate; they display what the server already gated." A single enforcement point is what makes the CI matrix (issue 027) and production probe (issue 096) meaningful.
- **Design**: DESIGN.md §8.1 ("The India inversion") governs the user-facing consequences; DESIGN.md §5.2: beef SKUs never render as CACHE MISS in IN — "they are absent from the IN catalog entirely (CC-MKT-003)."

## Scope
- **Applies To**: Both
- **Components**: Market & Gating Policy bounded context (enforcement API consumed in-process by other modules of the modular monolith); Catalog & Inventory query paths; sitemap/structured-data/feed generation; recommendation surfaces. B2B API parity enforcement (CC-API-007) is wired in issue 055; the enforcement point it calls is built here.
- **Actors**: Storefront rendering (SSR), catalog search, recommendations, sitemap/feed generators, B2B API handlers — all as mandatory consumers; consumers and partners only ever see post-gating output.
- **Data Classification**: Public (catalog content) — but market-gated: distribution of non-veg content into IN is a compliance violation.

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Gated content leaking into the IN market through any response channel — including non-HTML channels (JSON hydration payloads, sitemaps, structured data, feeds) that client-side hiding cannot cover (CC-MKT-003); OWASP Top Ten A01:2021 Broken Access Control; STRIDE: Information Disclosure.
- **Trust Boundary**: Server side of the client–server edge: gating is applied before any response body is serialized, so nothing gated ever crosses the boundary toward an IN client (SECURITY.md, Authentication rule 10 keys the decision off server-side transacting-market state — issue 024).
- **Zero Trust Consideration**: No consumer of catalog data is trusted to self-gate: all query paths are forced through this enforcement point (ARCHITECTURE.md, Dependency rule 1), and the gating input is exclusively the server-resolved transacting-market state, never client hints (CC-SEC-012).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Access Control chapter (centralized server-side enforcement; deny by default).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege applied to content exposure per market)
- **NIST SP 800-207**: Per-request policy enforcement at a single policy enforcement point fed by server-held state.
- **Regulatory**: IN vegetarian-market regime; FSSAI marking context (REQUIREMENTS.md CC-CNT-006, CC-CMP-004 — marking itself is issue 067's scope).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given transacting market IN, when any catalog listing, product-detail, search, or recommendation response is produced, then the serialized response body (HTML, SSR transfer state, and JSON alike) contains zero non-veg SKUs, non-veg SKU identifiers, or non-veg SKU attributes (CC-MKT-003).
2. **AC-02**: Given transacting market IN, when sitemaps, structured data, or feeds are generated, then no non-veg product URL, identifier, or structured-data entity appears in the output (CC-MKT-003).
3. **AC-03**: Given transacting market US, ES, or DE, when the catalog is listed or searched, then the full catalog including non-veg SKUs is returned (CC-MKT-007).
4. **AC-04**: Given any of the six markets, when the catalog is queried with the vegetarian filter, then vegetarian SKUs are available and the filter returns only veg-classified SKUs (CC-MKT-007; single-toggle filter UI per CC-CAT-006 is issue 031's scope).
5. **AC-05** (negative): Given the IN market, when a response is produced by any surface, then gating MUST NOT be achieved by client-side hiding — i.e., no test may find non-veg data present-but-hidden in an IN payload; presence in the payload is the failure condition (CC-MKT-003).
6. **AC-06**: Given any catalog-consuming module (rendering, search, recommendations, sitemap/feed generation), when it queries SKUs, then the query path provably passes through the single enforcement point — verified by an architecture test that fails if a catalog query bypasses it (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1).
7. **AC-07** (negative): Given an exception inside the gating path while serving an IN request, when the response is produced, then the outcome is a denial/exclusion (empty or error result), never an ungated result set (SECURITY.md, Logging rule 2).

## Failure Behavior
- **On Invalid Input**: Unknown market in a gating call fails closed to most-restrictive (issue 023 AC-05); malformed queries rejected with RFC 9457 problem details, no internal detail (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception in a gating path is a denial, never a bypass (SECURITY.md, Logging rule 2). An IN request that cannot be gated returns an error/empty result rather than ungated content.
- **Alerting**: Gating-path exceptions logged as structured security events with alerting (SECURITY.md, Logging rule 3); in production the IN gating synthetic probe (CC-NFR-003, issue 096) independently alerts on any CC-MKT-003/004 violation.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the enforcement API: IN exclusion across every response-type shape, full catalog for US/ES/DE, veg filter correctness in all six markets, fail-closed exception behavior. Tagged CC-MKT-003, CC-MKT-007 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests rendering real responses (listing, PDP payload, search results, sitemap XML, structured data, feeds) per market and asserting payload-level absence/presence of non-veg SKUs — asserting on serialized bytes, not view-model flags (AC-05).
- **Security Tests**: Architecture test for bypass-free query paths (AC-06); the cross-market portions of the authz suite (CC-QA-005) attempt to pull non-veg content into IN via every endpoint.
- **Compliance Tests**: This issue is the primary subject of the market-gating CI matrix (issue 027, CC-QA-003) and the production IN gating probe (issue 096, CC-NFR-003).
- **Coverage Target**: ≥ 80% line coverage (CC-QA-001); mutation testing SHOULD run on this gating code (CC-QA-001).

## Dependencies
- **Upstream**: 023 Market & Gating Policy: policy-as-data model and configuration schema; 024 Transacting market/locale resolution; 029 SKU domain model (veg/non-veg classification, CC-CAT-001); 001 Solution scaffold.
- **Downstream**: 026 404 semantics for market-gated resources; 027 Market-gating CI test matrix; 028 Cache-safe gating; 031 Per-market per-locale catalog search; 055 B2B scope/tenant enforcement with IN gating parity (CC-API-007); 066 Menu page; 071 SEO surfaces (gated sitemaps/structured data/feeds); 096 IN gating probe.
- **External**: None.

## Implementation Notes
- **Constraints**: In-process module API within the .NET 10 modular monolith; enforcement applies at the data-access/query layer so exclusion happens before serialization (server-side exclusion "from every IN response", CC-MKT-003). Gating input is the transacting-market state from issue 024 only. Angular SSR transfer state is a response channel too — the gated result feeds issue 028's guarantee that hydration state carries only gated data (SECURITY.md, HTTP boundary rule 10).
- **Anti-Patterns**: Client-side hiding of non-veg content in IN is explicitly non-compliant (CC-MKT-003); per-module market conditionals prohibited (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); MUST NOT render beef SKUs as CACHE MISS in IN — absent entirely, never "unavailable" (DESIGN.md §5.2); fail-open on gating errors prohibited (SECURITY.md, Logging rule 2).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); gating code is named for mutation testing (CC-QA-001).

## Open Questions
- "Recommendations" are listed as a gated response class (CC-MKT-003), but no requirement or architecture element defines a recommendation engine; this issue gates whatever recommendation surface exists, and the surface itself is otherwise unspecified.
- Whether JP and MX carry the full catalog is not explicitly stated: CC-MKT-007 names only US, ES, DE for the full catalog, and DESIGN.md §8.5 says JP is "full catalog" — REQUIREMENTS.md takes precedence, so JP/MX catalog breadth beyond "veg available and filterable" is recorded here rather than asserted in acceptance criteria.
