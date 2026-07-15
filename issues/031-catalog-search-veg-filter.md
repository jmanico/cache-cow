# 031 · Per-market per-locale catalog search with vegetarian filter

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CAT-005, CC-CAT-006
- **Title**: Per-market per-locale catalog search with vegetarian filter
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Catalog search MUST operate per market (search in IN MUST NOT surface non-veg SKUs, per CC-MKT-003) and per locale (search in Japanese matches Japanese product names), and vegetarian filtering MUST be available in all markets as a single-toggle filter (REQUIREMENTS.md CC-CAT-005, CC-CAT-006).
- **Rationale**: Search is explicitly one of the surfaces from which non-veg SKUs MUST be excluded server-side in the IN market (REQUIREMENTS.md CC-MKT-003, a P1 gating requirement) — a search index that bypasses gating is a compliance breach. Per-locale matching is required for the seven launch locales to make catalogs findable (CC-I18N-001); vegetarian filtering is a cross-market product commitment (CC-MKT-007, CC-CAT-006). The confirmed engine is PostgreSQL full-text search, per market and per locale; ja-JP and hi-IN analysis quality must still satisfy CC-CAT-005 (ARCHITECTURE.md, "Technology decisions", Search).
- **Design**: The menu page's cut and veg filters are DESIGN.md §10; the veg indicator is DESIGN.md §3.3. Filter/search UI is issue 066's scope; this issue delivers the server-side search capability and the single-toggle veg predicate.

## Scope
- **Applies To**: Both
- **Components**: Catalog & Inventory bounded context — search over the structured SKU model (issue 029) using PostgreSQL full-text search, scoped by transacting market (via issue 025's enforcement point) and query locale; a single-toggle vegetarian filter predicate usable by storefront and B2B API. Excludes: gating policy itself (issues 023/025), search UI (issue 066), SEO/sitemap surfaces (issue 071).
- **Actors**: Consumer (storefront), B2B API client (`catalog:read`), storefront SSR.
- **Data Classification**: Internal (catalog data); results are market-gated.

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Market-gating bypass through the search surface — non-veg SKUs surfacing in IN responses (OWASP Top Ten A01:2021 Broken Access Control; REQUIREMENTS.md CC-MKT-003); SQL injection via search terms (CWE-89, OWASP Top Ten A03:2021).
- **Trust Boundary**: Search queries are attacker-controlled input at the HTTP boundary (SECURITY.md, Input validation rule 1). Market scoping derives exclusively from server-side transacting-market state, never from `Accept-Language`, geolocation, or client locale hints (SECURITY.md, Authentication rule 10; CC-SEC-012).
- **Zero Trust Consideration**: Search does not implement its own market conditionals — it consults the Market & Gating Policy enforcement point (issue 025) upstream of ranking (ARCHITECTURE.md, Dependency rule 1); query text is parameterized, never concatenated (SECURITY.md, Input validation rule 4).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic (input validation on query parameters); V8 Authorization (market-scoped result filtering).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation), AC-3 (access enforcement on gated results)
- **NIST SP 800-207**: N/A
- **Regulatory**: IN veg-only catalog obligation via CC-MKT-003 (FSSAI context per CC-CNT-006/CC-CMP-004 is owned by those requirements).
- **Other**: BCP 47 locale identifiers (REQUIREMENTS.md §2).

## Acceptance Criteria
1. **AC-01**: Given the IN transacting market, when any search query is executed (including queries that exactly match a non-veg product name), then zero non-veg SKUs appear in results — enforced server-side via issue 025's enforcement point, not by client-side hiding (CC-CAT-005, CC-MKT-003 negative case).
2. **AC-02**: Given the ja-JP locale, when a user searches using a Japanese product name, then the matching SKU is returned (CC-CAT-005; ARCHITECTURE.md Search: ja-JP analysis quality must satisfy CC-CAT-005); likewise hi-IN queries match Devanagari product names.
3. **AC-03**: Given any of the six markets, when the vegetarian filter toggle is applied, then results contain vegetarian SKUs only, via a single predicate on the structured classification field from issue 029 (CC-CAT-006, CC-MKT-007).
4. **AC-04**: Given any market other than the query's, when a search executes, then results contain only SKUs available in the requester's transacting market — a SKU available solely in another market does not surface (CC-CAT-005).
5. **AC-05**: Given a search request carrying client hints (`Accept-Language`, forged locale cookie, geolocation), when market scoping is applied, then scoping keys exclusively off server-side transacting-market state and the hints change nothing (CC-SEC-012; SECURITY.md, Authentication rule 10).
6. **AC-06**: Given a search term containing SQL metacharacters or `tsquery` syntax, when the query executes, then it is passed through parameterized queries / safe query construction (e.g., `plainto_tsquery`/`websearch_to_tsquery` with parameters) and no injection or server error occurs (SECURITY.md, Input validation rule 4).
7. **AC-07**: Given an oversized or malformed query parameter, when validation runs, then the request is rejected with 400 (RFC 9457) and page-size parameters are clamped (SECURITY.md, HTTP boundary rule 7; Input validation rule 1).

## Failure Behavior
- **On Invalid Input**: 400 with RFC 9457 problem details; structured validation-rejection log with correlation ID (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed on the gating path (SECURITY.md, Logging rule 2): if the gating enforcement point cannot be consulted, return no results (or an error) — never ungated results.
- **Alerting**: Any gated SKU appearing in a gated market's search results is a security event; the production IN gating probe (CC-NFR-003, issue 096) asserts this continuously and alerts on failure.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for query construction (parameterization, locale→search-configuration mapping, veg predicate) and market-scope composition with the issue 025 enforcement point.
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL full-text search with fixtures in all seven locales: Japanese-name matching (ja-JP), Devanagari matching (hi-IN), per-market scoping, veg toggle in all six markets.
- **Security Tests**: Injection fuzzing on the search parameter (SQL/tsquery metacharacters); the CC-MKT-003 search-surface case feeds the market-gating matrix (issue 027, CC-QA-003: every market × veg/non-veg × search). Mutation testing SHOULD run on the gating-composition code (CC-QA-001).
- **Compliance Tests**: Evidence artifact from the gating matrix run per merge (SECURITY.md, Deployment rule 8).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-CAT-005, CC-CAT-006.

## Dependencies
- **Upstream**: 015 PostgreSQL Flexible Server; 021 RFC 9457 error handling; 023 Market & Gating Policy model; 024 Transacting market/locale resolution; 025 Server-side gating enforcement; 029 SKU domain model (localized names, classification).
- **Downstream**: 027 Market-gating CI test matrix (search axis); 055 B2B scope/tenant enforcement with IN gating parity (CC-API-007); 066 Menu page (filter UI); 096 Per-market synthetic probes (IN gating probe).
- **External**: N/A

## Implementation Notes
- **Constraints**: PostgreSQL full-text search only — the confirmed engine (ARCHITECTURE.md, "Technology decisions"); per-locale `tsvector`/search-configuration selection keyed by locale; market scoping composed with the gating enforcement point before ranking, so excluded SKUs are never in the candidate set. Any PostgreSQL extension or library considered for ja/hi analysis must pass SECURITY.md Dependency Rules 1–8 (justification, maintenance ≤ 6 months, no unpatched CVEs, pinned versions).
- **Anti-Patterns**: MUST NOT implement market conditionals inside search (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); MUST NOT filter gated SKUs client-side (CC-MKT-003: client-side hiding is non-compliant); MUST NOT concatenate query text into SQL (SECURITY.md, Input validation rule 4); MUST NOT key results on `Accept-Language` or geolocation (CC-SEC-012).
- **AI Development Guidance**: Identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR cites CC-CAT-005/006 (REQUIREMENTS.md §17).

## Open Questions
- PostgreSQL's built-in full-text parser has no ja-JP tokenizer and limited hi-IN handling; ARCHITECTURE.md fixes PostgreSQL FTS and says analysis quality "must still satisfy CC-CAT-005" but does not name a mechanism (extension, n-gram approach, or normalization strategy). Needs a human decision consistent with SECURITY.md Dependency Rules — not proposing one here.
- Which SKU fields are in search scope (localized name only, or also ingredients/cut/category/description) is unspecified in CC-CAT-005.
- Relevance/ranking requirements (and whether search spans content pages or SKUs only) are unspecified.
