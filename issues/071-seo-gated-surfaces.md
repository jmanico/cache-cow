# 071 · SEO surfaces: gated sitemaps, hreflang, structured data, feeds

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-003 (sitemaps, structured data, feeds as named exclusion surfaces), CC-I18N-004 (hreflang alternates); with CC-MKT-005 (Cuts unreachable in IN) and CC-MKT-009/CC-SEC-013 (cache-safe gating) clauses
- **Title**: SEO surfaces: gated sitemaps, hreflang, structured data, feeds
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: Sitemaps, structured data, and feeds MUST be generated per market server-side through the Market & Gating Policy enforcement point so that non-veg SKUs and market-gated content never appear in any IN-market SEO surface, and hreflang alternates MUST be declared correctly for every rendered locale (REQUIREMENTS.md CC-MKT-003, CC-I18N-004; ARCHITECTURE.md, Dependency rule 1).
- **Rationale**: CC-MKT-003 names sitemaps, structured data, and feeds explicitly among the surfaces from which non-veg SKUs MUST be excluded server-side for the IN market — client-side hiding is non-compliant, and ARCHITECTURE.md ("Clients") notes that server rendering is effectively required precisely because sitemaps and structured data demand server-side exclusion. ARCHITECTURE.md Dependency rule 1 routes sitemap/feed generation through the single Market & Gating Policy enforcement point; nothing may implement its own market conditionals (CC-MKT-006). CC-MKT-005 makes "Meet our Cuts" unreachable in IN, which includes not linking it from IN sitemaps or feeds. CC-I18N-004 requires correct `hreflang` alternates per rendered locale for SEO. The 2026-07-15 threat model closed the caching hole: no cached SEO response gated for one market may be served to another, keyed only on server-side transacting-market/locale state (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary rule 10).
- **Design**: N/A (machine-consumed surfaces; no DESIGN.md UI treatment applies).

## Scope
- **Applies To**: Web App (public SEO endpoints served from the storefront host: sitemap XML per market, structured-data payloads for server-rendered pages, catalog feeds)
- **Components**: Per-market sitemap generation; structured-data payload generation sourced from gated catalog queries (embedded by the SSR pages of issues 063/066/067); per-market feed generation; hreflang alternate declarations on these surfaces consistent with page-level declarations (issue 063). Excluded: page-level `lang`/`hreflang` link elements (issue 063), the gating enforcement API itself (issue 025), 404 semantics for gated URLs (issue 026), search (issue 031), the gating CI matrix harness (issue 027), edge/CDN cache-key infrastructure (issue 028).
- **Actors**: Search-engine crawlers and feed consumers (anonymous public traffic)
- **Data Classification**: Public (catalog and URL data only; never personalized)

## Security Context
- **Defense Layer**: Architecture (compliance gating server-side and upstream of everything user-visible; ARCHITECTURE.md, Dependency rule 1)
- **Threat(s) Addressed**: Compliance-gating bypass via secondary surfaces — non-veg SKUs leaking into IN through a sitemap, feed, or structured-data payload even though the storefront pages are gated (CC-MKT-003); cross-market cache reuse serving one market's SEO output to another (CC-SEC-013, CC-MKT-009; THREAT_MODEL.md-derived per REQUIREMENTS.md v1.3); gating keyed off forgeable client hints (CC-SEC-012)
- **Trust Boundary**: Server-side gating decision upstream of response generation; the edge/CDN cache layer between origin and crawlers (governed by SECURITY.md, HTTP boundary rule 10)
- **Zero Trust Consideration**: The market for any SEO response derives exclusively from server-side transacting-market state (e.g., the market addressed by the requested sitemap/feed route), never from `Accept-Language`, IP geolocation, or client-forgeable cookies (CC-SEC-012; SECURITY.md, Authentication rule 10); every emitted URL and SKU passes the gating enforcement point per generation, not per deployment assumption

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, "Baseline"); Business Logic and Access Control chapters (server-side enforcement of the gating policy on every emission path)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (server-side enforcement of the market-gating policy on resource emission)
- **NIST SP 800-207**: N/A
- **Regulatory**: FSSAI vegetarian-market context for IN drives the exclusion requirement (via CC-MKT-003; CC-CNT-006/CC-CMP-004 backdrop)
- **Other**: BCP 47 locale tags (REQUIREMENTS.md §2, CC-I18N-001) for all hreflang values

## Acceptance Criteria
1. **AC-01**: Given a launch market, when its sitemap is generated, then it is produced server-side and lists only URLs available in that market according to the Market & Gating Policy enforcement point (issue 025) — no sitemap code contains its own market conditionals (CC-MKT-003, CC-MKT-006; ARCHITECTURE.md, Dependency rule 1).
2. **AC-02**: Given the IN market, when its sitemap, feeds, and every structured-data payload are inspected, then they contain zero non-veg SKUs, non-veg product URLs, or non-veg data fragments (negative case; CC-MKT-003).
3. **AC-03**: Given the IN market, when its sitemap and feeds are inspected, then no "Meet our Cuts" URL is present or linked (CC-MKT-005).
4. **AC-04**: Given a server-rendered page's structured-data payload, when it is generated, then it contains only data already gated for that page's transacting market and locale — never the ungated catalog or another market's SKUs (CC-MKT-003; SECURITY.md, HTTP boundary rule 10's transfer-state principle applied to embedded payloads).
5. **AC-05**: Given the seven launch locales (CC-I18N-001), when hreflang alternates are emitted on these surfaces, then they use correct BCP 47 tags and are consistent with the page-level `lang`/`hreflang` declarations of issue 063 (CC-I18N-004).
6. **AC-06**: Given a cacheable sitemap/feed/SEO response, when it is cached at SSR, edge, or CDN, then the cache key derives from the server-side transacting market (and locale where applicable) — never from `Accept-Language`, geolocation, or a client-forgeable cookie — and a response generated for one market is never served to another (negative case; CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary rule 10; via issue 028).
7. **AC-07**: Given the market-gating test matrix (CC-QA-003, issue 027), when it runs on merge, then the sitemap/feed/structured-data surfaces are covered for every market × veg/non-veg SKU combination, and any leak fails the build (CC-MKT-003, CC-QA-003).

## Failure Behavior
- **On Invalid Input**: These are read-only public endpoints; malformed requests receive HTTP 400/404 with RFC 9457 bodies and no internal detail (SECURITY.md, Logging rule 1). Requests for another market's gated resources follow the 404 semantics of issue 026 (CC-MKT-004 pattern).
- **On System Error**: Fail closed — if the gating policy lookup fails during generation, the affected URL/SKU is excluded or generation aborts with a 5xx; ungated output is never emitted (SECURITY.md, Logging rule 2: any exception in a gating path is a denial, never a bypass).
- **Alerting**: Gating-path exceptions during generation are logged as structured security events and alerted (SECURITY.md, Logging rule 3; issue 022); in production, the continuous IN-market gating probe (CC-NFR-003; issue 096) asserts CC-MKT-003/004 and alerts on any leak.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the gated query composition per surface (sitemap URL enumeration, feed item selection, structured-data payload assembly) including gating-lookup-failure exclusion (fail closed).
- **Integration Tests**: ASP.NET Core integration tests per market fixture: full sitemap/feed generation with veg and non-veg SKUs seeded, asserting IN exclusion, Cuts absence in IN (CC-MKT-005), presence of the same SKUs in US/ES/DE full-catalog markets (CC-MKT-007 parity context), and hreflang tag correctness against the locale set.
- **Security Tests**: Cache-key assertions — same URL requested under different client hints (`Accept-Language`, forged cookies) never changes the served market variant (CC-SEC-012, CC-SEC-013); XML output built by serializers, and if any XML is ever parsed, `DtdProcessing.Prohibit` with `XmlResolver = null` (SECURITY.md, Input validation rule 9).
- **Compliance Tests**: The CC-QA-003 matrix run (issue 027) covering the sitemap surface every merge, retained as CI evidence; production IN gating probe evidence (issue 096).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this gating code (CC-QA-001); tests tagged CC-MKT-003, CC-MKT-005, CC-I18N-004, CC-SEC-013 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 003 "Shared kernel: Market, Locale, and SKU identity types"; 023 "Market & Gating Policy: policy-as-data model"; 025 "Server-side gating enforcement API with IN veg-only exclusion" (the single enforcement point); 026 "404 semantics for market-gated resources" (URLs listed must be consistent with what resolves); 028 "Cache-safe gating" (cache-key discipline); 029 "SKU domain model"; 063 "Storefront SSR shell: hydration, switchers, lang/hreflang" (page-level declarations these surfaces must match); 066/067 "Menu page" / "Product detail page" (host pages for structured data).
- **Downstream**: 027 "Market-gating CI test matrix" (asserts these surfaces every merge, CC-QA-003); 096 "Per-market synthetic probes including IN gating probe" (production assertion, CC-NFR-003).
- **External**: None.

## Implementation Notes
- **Constraints**: Generation runs in the ASP.NET Core host consuming issue 025's enforcement API — per ARCHITECTURE.md Dependency rule 1, sitemap/feed generation is a named dependent of the Market & Gating Policy service; Angular SSR pages embed structured-data payloads produced from already-gated server data (ARCHITECTURE.md, "Edge & SSR caching"); output responses may be cached only under the market/locale cache-key discipline of SECURITY.md, HTTP boundary rule 10 (issue 028); these surfaces are anonymous and non-personalized by construction — nothing per-user may enter them.
- **Anti-Patterns**: No client-side filtering of SEO output (non-compliant per CC-MKT-003); no market conditionals scattered in generators — policy-as-data via the enforcement point only (CC-MKT-006); no cache keyed on client hints (SECURITY.md, HTTP boundary rule 10); no hand-built locale tags — BCP 47 identifiers from the shared kernel (issue 003); no redirect-to-other-market for gated URLs surfaced here — gated resources 404 (CC-MKT-004, issue 026).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- "Feeds" (CC-MKT-003) is not defined anywhere in the specs — which feed formats and consumers (product/merchant feeds, RSS/Atom, retail-partner feeds?) are intended is unknown; acceptance criteria here can only assert gating on whatever feeds exist.
- The structured-data vocabulary and types (e.g., schema.org Product/Offer, JSON-LD vs. microdata) are unspecified.
- The URL scheme encoding market and locale (path prefix, subdomain, query) is undefined, and with it the exact shape of sitemap URL entries and hreflang alternate URLs; CC-I18N-004 requires the alternates but not the URL model.
- Whether hreflang alternates must additionally be emitted inside sitemap files (xhtml:link entries) or only at page level (issue 063) is unspecified — CC-I18N-004 says "every page MUST declare".
- "Recommendations" is also a named CC-MKT-003 exclusion surface but no recommendations feature appears in the issue index; where its gating lands is unassigned.
- Sitemap/feed regeneration cadence and freshness requirements are unspecified.
