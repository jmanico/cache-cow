# 078 · Store locator

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: N/A — no single CC-* ID; implements DESIGN.md §7 (Store locator component) and §10 (page inventory), within the market-scoping rules of CC-MKT-002/CC-MKT-006 and the wholesale-isolation rule CC-WHS-003
- **Title**: Store locator
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: The storefront MUST provide a store locator page presenting the retail partners stocking Cache Cow frozen retail product as a list filtered by the user's active market region, with the map presentation deferred pending the open provider decision recorded below.
- **Rationale**: DESIGN.md §7 defines the component ("Store locator (grocery). Map plus list of retail partners stocking Cache Cow freezer product, filtered by the active market region") and §10 lists the page in the consumer inventory ("Store locator — Grocery partners carrying frozen retail SKUs") plus a home-page teaser. Grocery wholesale distribution is a launch channel (REQUIREMENTS.md §1), so consumers need to find retail availability. The list view and market filtering are firmly specified; the map half depends on unnamed vendor/data decisions (see Open Questions) and is excluded from this issue's acceptance criteria rather than guessed.
- **Design**: DESIGN.md §7 (Store locator component: map plus list); §10 (page inventory; home "store locator teaser"); §§3–6 (tokens, typography — partner details in the data role use IBM Plex Mono where tabular, §4.1 — grid, card construction); §13 (WCAG 2.2 AA; the list is the accessible base regardless of any future map).

## Scope
- **Applies To**: Both (Angular storefront page; a consumer-facing read endpoint serving partner retail locations)
- **Components**: Storefront store-locator page and list component; consumer read endpoint for retail-partner locations filtered by market region; home-page teaser link slot. Explicitly excluded: the map presentation (blocked on unnamed provider and CSP/CDN tension — Open Questions); partner onboarding and partner master data (issue 049); wholesale price lists and terms (issue 050); the market-resolution machinery (issue 024).
- **Actors**: Consumer storefront visitors (all markets); operations/partner-management staff maintaining partner location data (via the dashboard, issue 085)
- **Data Classification**: Public (retail store names/locations intended for consumers) — sourced from partner records whose commercial terms are Confidential and MUST NOT leak (CC-WHS-003)

## Security Context
- **Defense Layer**: Strict API (consumer endpoint exposes only public location fields; server-side market filtering)
- **Threat(s) Addressed**: Disclosure of wholesale prices, terms, or partner-confidential data through a consumer response (CC-WHS-003; ARCHITECTURE.md, Dependency rule 3; OWASP API Security Top 10 — excessive data exposure); cross-market cache leakage of market-filtered responses (CC-MKT-009)
- **Trust Boundary**: Consumer session → partner-data boundary: the consumer endpoint is a projection of public location fields only, never a pass-through of partner records (ARCHITECTURE.md, Dependency rule 3: consumer surfaces must not depend on wholesale data)
- **Zero Trust Consideration**: The market filter keys off server-side transacting-market state, not client-supplied hints (CC-SEC-012; SECURITY.md, Authentication rule 10); the response schema is an explicit allowlist of public fields, so nothing confidential can be "accidentally serialized".

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (server-side scoping of what a consumer session may see); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement — consumer projection excludes confidential partner data)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA (CC-NFR-004; DESIGN.md §13)

## Acceptance Criteria
1. **AC-01**: Given a session with an active transacting market, when the store locator renders, then it lists only retail partners stocking Cache Cow frozen retail product in that market's region, resolved server-side (DESIGN.md §7; market state per CC-MKT-002 via issue 024).
2. **AC-02**: Given the user switches market via the header market control (DESIGN.md §7, region switcher), when the locator re-renders, then the list reflects the newly selected market's region and no other market's partners.
3. **AC-03**: Given the consumer locator endpoint response, when its payload is inspected, then it contains only public location fields and no wholesale prices, terms, partner identifiers beyond what the public listing requires, or any field derivable into wholesale data (CC-WHS-003; ARCHITECTURE.md, Dependency rule 3). (Negative case.)
4. **AC-04**: Given the locator endpoint, when it is called, then results come from a typed, schema-validated response the client renders without transformation of trust (SECURITY.md, Input validation rule 1), with page-size clamping on any list parameters (SECURITY.md, HTTP boundary rule 7).
5. **AC-05**: Given any cacheable locator response, when it is cached at SSR/edge/CDN, then the cache key derives from server-side transacting market + locale so one market's partner list is never served to another (CC-MKT-009; SECURITY.md, HTTP boundary rule 10). (Negative case.)
6. **AC-06**: Given the locator page, when audited, then the list is fully keyboard operable and meets WCAG 2.2 AA (DESIGN.md §13; CC-NFR-004), and all styling consumes generated design tokens (ARCHITECTURE.md, Dependency rule 8).
7. **AC-07**: Given the page as shipped by this issue, when it renders, then no third-party map script, tile server, or other runtime-CDN asset is loaded (SECURITY.md, Deployment rule 10; HTTP boundary rule 2) — the map ships only after the Open-Question decisions land. (Negative case.)

## Failure Behavior
- **On Invalid Input**: Invalid query parameters (unknown market/region values, out-of-range paging) are rejected with HTTP 400 and an RFC 9457 problem body (issue 021); no partial or unfiltered results are returned.
- **On System Error**: Fail closed on the market filter — if transacting-market resolution or the partner-location query fails, the page shows a generic error state (DESIGN.md §9 voice; no internal details per SECURITY.md, Logging rule 1) rather than an unfiltered or cross-market list.
- **Alerting**: Endpoint errors log as structured events with correlation IDs to centralized monitoring (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the market-region filter and the public-field projection (a partner record with wholesale fields in → only allowlisted public fields out); Angular component tests for the list rendering, empty state, and token-based styling.
- **Integration Tests**: SSR/API integration tests per market asserting AC-01/AC-02 filtering, AC-03 payload allowlist (schema snapshot), and cache-key behavior with issue 028's patterns.
- **Security Tests**: Response-schema assertion that serializing the consumer DTO can never emit wholesale fields (compile-time projection type, not runtime stripping); the cross-tenant/authz suite (issue 062) includes attempts to widen the locator query beyond the active market.
- **Compliance Tests**: Automated accessibility checks (issue 098) cover the page; CI evidence of the no-third-party-asset assertion (AC-07).
- **Coverage Target**: ≥ 80% (CC-QA-001); tests tagged with the DESIGN.md §7/§10 sources and CC-WHS-003/CC-MKT-009 where they verify those (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 024 "Transacting market/locale resolution" (the market the filter keys off); 049 "Partner tenancy and onboarding approval workflow" (partner records the locations derive from — see Open Questions on the exact data source); 063 "Storefront SSR shell"; 064 "ICU MessageFormat resource pipeline"; 005 "Design token pipeline"; 021 "RFC 9457 error handling".
- **Downstream**: 085 "Dashboard partner management module" (maintains partner data including any retail-location fields); 028 "Cache-safe gating" (covers this route's cache keys).
- **External**: None confirmed. No map provider is confirmed anywhere in the specs — do not introduce one without a human decision (see Open Questions).

## Implementation Notes
- **Constraints**: The consumer endpoint is a read-only projection living outside the partner tenancy boundary's confidential surface (ARCHITECTURE.md, Dependency rule 3); filtering is server-side off transacting-market state (CC-SEC-012); the list is the accessible foundation — any future map must remain an enhancement over it, not a replacement (DESIGN.md §7 "map plus list", §13); locator data in tables uses Plex Mono per DESIGN.md §4.1 where tabular.
- **Anti-Patterns**: Serializing partner entities directly to the consumer response (runtime field-stripping instead of a dedicated public DTO violates SECURITY.md, Input validation rule 3's DTO discipline and risks CC-WHS-003 leakage); client-side market filtering of a full partner list (ships other markets' data to the client; server gates, clients display — ARCHITECTURE.md, Dependency rule 1); loading map scripts/tiles from third-party runtime CDNs (SECURITY.md, Deployment rule 10) or widening the CSP with wildcard origins (HTTP boundary rule 2); hardcoded brand colors/type (ARCHITECTURE.md, Dependency rule 8).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). AI MUST NOT select a map provider or add a mapping dependency; that is an open human decision.

## Open Questions
- **No map provider is named in the specs**, yet DESIGN.md §7 specifies "map plus list". Any hosted map (scripts, tiles, geocoding) conflicts with the ban on third-party runtime CDNs for scripts (SECURITY.md, Deployment rule 10) and the strict default-deny CSP with exact-origin allowlists (SECURITY.md, HTTP boundary rule 2 / issue 017); self-hosted tiles are a different cost/licensing decision. This requires a human decision; this issue ships the list view firmly and excludes the map from acceptance criteria — it MUST NOT be resolved by silently picking a vendor.
- **The partner-location data source is unspecified.** REQUIREMENTS.md/ARCHITECTURE.md define wholesale partner tenancy (issue 049) and dashboard partner management (issue 085), but not where retail store locations (a partner may have many stores) are authored and stored, nor which fields are public. The consumer projection here needs that model defined.
- Whether the locator filters by "market region" only or supports finer-grained location search (postal code, proximity — which would itself require geocoding, re-entangling the map-provider question) is unspecified; only market-region filtering is asserted.
- The public field set per retail location (name, address, hours?) is not enumerated in the specs.
