# 066 · Menu page: product cards, cache-status badges, filters

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CAT-003, CC-CAT-006, CC-MKT-007
- **Title**: Menu page: product cards, cache-status badges, filters
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The storefront menu page MUST render the server-gated, market-priced catalog as product cards carrying the three cache-status badges mapped from CC-CAT-003's stock states — each paired with its plain-language line and never conveyed by color alone — with a single-toggle vegetarian filter and a cut filter, and MUST NOT render any SKU or state the server did not supply for the transacting market.
- **Rationale**: CC-CAT-003 fixes exactly three user-facing stock states (in stock / restocking-preorder / unavailable in region) with presentation delegated to DESIGN.md §5.2's cache-status language; CC-CAT-006 requires a single-toggle vegetarian filter in all markets; CC-MKT-007 requires US, ES, and DE to carry the full catalog including non-veg SKUs with vegetarian SKUs available and filterable everywhere. DESIGN.md §5.2 adds the load-bearing regional rule: beef SKUs never render as CACHE MISS in the IN market — they are absent from the IN catalog entirely (CC-MKT-003), and a state implying future availability of beef in India is both wrong and offensive. DESIGN.md §10 defines the Menu page (geo-priced catalog, cut and veg filters, cache-status on every card) and §7 the product card contract.
- **Design**: DESIGN.md §7 "Product card" (photo 4:5; name in Archivo 700; cut/weight in Plex Mono; price in Plex Mono; cache-status badge; veg indicator where applicable; the entire card is one link with add-to-cart as a separate action inside it); §5.2 badge/plain-line table and colors (CACHE HIT cache.500, WARMING ember.500, CACHE MISS smoke.400); §5.4 pun budget (at most one cache/tech pun per viewport); §3.3 veg marking; §13 status never color-alone; §6 grid; §10 page inventory ("Menu").

## Scope
- **Applies To**: Web App
- **Components**: Storefront menu page (Angular, SSR via issue 063's shell): product card component, cache-status badge component, veg indicator component, cut filter, vegetarian single-toggle filter, catalog listing consumption from the server. Explicitly excluded: server-side gating and IN exclusion (issue 025), inventory three-state derivation (issue 030), catalog search backend and filter query execution (issue 031), price formatting engine (issue 034), product detail page (issue 067), cart (issue 068), the interactive cuts diagram (issue 075).
- **Actors**: Anonymous and authenticated consumers in all six markets and seven locales.
- **Data Classification**: Public (catalog data), rendered only from typed, validated server responses.

## Security Context
- **Defense Layer**: Encoding (client renders server-gated, typed data; performs no gating or computation of its own)
- **Threat(s) Addressed**: Client-side gating or filtering standing in for server enforcement — non-compliant per CC-MKT-003 ("client-side hiding is non-compliant"); display of client-computed or client-supplied prices (ARCHITECTURE.md, Dependency rule 2); DOM injection via product data (SECURITY.md, Input validation rules 1 and 5).
- **Trust Boundary**: Client–server edge: the menu consumes catalog/inventory/price data across the HTTP boundary as typed, schema-validated responses (SECURITY.md, Input validation rule 1).
- **Zero Trust Consideration**: The page trusts only the server-gated response for the transacting market; it never derives availability, market, or price client-side, and it renders prices and inventory values only from typed, validated responses (SECURITY.md, Input validation rule 1; ARCHITECTURE.md, Dependency rules 1–2).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Web Frontend Security; V1 Encoding and Sanitization (platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A (no specific control; presentation layer)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A on this page beyond what the server-gated data already encodes (FSSAI marking specifics are issue 067's CC-CNT-006 scope; the veg indicator here follows DESIGN.md §3.3)
- **Other**: WCAG 2.2 AA (DESIGN.md §13; CC-NFR-004)

## Acceptance Criteria
1. **AC-01**: Given a catalog listing for any market, when a product card renders, then it contains: photo at 4:5, localized name (Archivo 700), cut/weight in Plex Mono, locale-formatted price in Plex Mono via issue 034 (with the market's tax-inclusion note per CC-PRC-002 and DESIGN.md §7 "Price display"), a cache-status badge, and a veg indicator where applicable; the entire card is a single link and add-to-cart is a separate action inside it (DESIGN.md §7).
2. **AC-02**: Given the three stock states of CC-CAT-003, when a card renders, then the badge and paired plain-language line follow DESIGN.md §5.2 exactly — `CACHE HIT` (cache.500) "Ships today from your regional cold store"; `WARMING` (ember.500) "Restocking, preorder available"; `CACHE MISS` (smoke.400) "Not available in your region yet" — and each badge always carries its text line, never color alone (DESIGN.md §13).
3. **AC-03**: Given any market, when the filters render, then vegetarian filtering is a single-toggle filter (CC-CAT-006) and a cut filter is available (DESIGN.md §10), both applied through server-side catalog queries (issue 031), not client-side list filtering of ungated data.
4. **AC-04**: Given the US, ES, or DE market, when the menu renders, then the full catalog including non-veg SKUs is present and vegetarian SKUs are available and filterable (CC-MKT-007).
5. **AC-05**: Given the IN market, when the menu renders, then no non-veg SKU appears in any state — in particular a beef SKU MUST NOT render as `CACHE MISS` or any other badge, because it is absent from the server's IN catalog response entirely (negative case; DESIGN.md §5.2; CC-MKT-003 enforced server-side by issue 025 — this page contains no client-side hiding logic).
6. **AC-06**: Given the menu page components, when audited, then no market/gating conditionals and no price computation exist in client code (ARCHITECTURE.md, Dependency rules 1–2; negative: a client-side "hide if IN" branch is a defect per CC-MKT-003).
7. **AC-07**: Given a card in the restocking state, when the user acts on it, then the action offered is preorder (CC-CAT-003: restocking = preorder permitted), and in the unavailable-in-region state no purchase action is offered (DESIGN.md §5.2 pairs it with offering the nearest substitute).
8. **AC-08**: Given keyboard-only operation, when navigating cards and filters, then all controls are operable with visible focus and badge status is exposed as text to assistive technology (DESIGN.md §13; CC-NFR-004).

## Failure Behavior
- **On Invalid Input**: A catalog response failing the typed schema at the client HTTP boundary is rejected — the card/listing does not render from unvalidated data (SECURITY.md, Input validation rule 1); errors surface as generic messages with correlation IDs (SECURITY.md, Logging rule 7).
- **On System Error**: Fail closed: if availability or gated catalog data cannot be retrieved, the page renders no fallback ungated content and never guesses a stock state (SECURITY.md, Logging rule 2 posture — an error in a gated data path is a denial, never a bypass); no raw error bodies or internal endpoints surface (Logging rules 1 and 7).
- **Alerting**: Client errors routed through interceptors/global ErrorHandler to server-side structured logging with alerting (SECURITY.md, Logging rules 3 and 7); the production IN-market gating probe (CC-NFR-003, issue 096) covers this page's rendered output.

## Test Strategy
- **Unit Tests**: Angular component tests: product card composition (AC-01), badge/plain-line mapping for all three states (AC-02), single-link card with separate add-to-cart action, veg indicator presence, filter controls emitting server query parameters.
- **Integration Tests**: SSR render of the menu per market asserting full catalog for US/ES/DE (CC-MKT-007) and zero non-veg presence in IN output including no CACHE MISS placeholders (composes with issue 027's market-gating matrix: every market x veg/non-veg SKU x storefront, CC-QA-003).
- **Security Tests**: Static assertion of no gating conditionals/no price math in client code; schema-validation rejection tests for malformed catalog payloads; raw-HTML-sink CI grep gate (SECURITY.md, Deployment rule 7).
- **Compliance Tests**: CC-QA-003 matrix evidence for the storefront surface; DESIGN.md §13 checks (badge text present, contrast via issue 005's CI contrast checks); locale visual regression via issue 065.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CAT-003, CC-CAT-006, CC-MKT-007 (and CC-MKT-003 for the IN negative case) per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 063 "Storefront SSR shell" (hosts the page); 025 "Server-side gating enforcement API with IN veg-only exclusion" (supplies gated catalog); 030 "Inventory per SKU per regional cold store with three-state availability" (supplies the three states); 031 "Per-market per-locale catalog search with vegetarian filter" (executes filters); 034 "Locale-aware price formatting and market tax-display conventions" (price strings and tax note); 005 "Design token pipeline" (badge colors, type roles); 064 "ICU MessageFormat resource pipeline" (badge plain-language lines and all strings).
- **Downstream**: 067 "Product detail page" (card links there); 068 "Cart with cross-market preservation rules" (add-to-cart action); 027 "Market-gating CI test matrix" and 096 "Per-market synthetic probes" (assert this surface); 065 "Locale layout resilience" (menu is plausibly a top-20 template).
- **External**: None.

## Implementation Notes
- **Constraints**: Angular SSR page inside issue 063's shell; tokens from `tokens.json` only (Dependency rule 8); badge vocabulary is fixed by DESIGN.md §5.2 and localized through the string pipeline (issue 064 — DESIGN.md §9: puns that don't survive translation are cut per market); pun budget of at most one cache/tech pun per viewport (DESIGN.md §5.4); Plex Mono for prices/weights sits one step below surrounding Archivo (DESIGN.md §4.3); 12-column grid per DESIGN.md §6; LCP budget applies (CC-NFR-002).
- **Anti-Patterns**: MUST NOT hide non-veg SKUs client-side — server exclusion only (CC-MKT-003); MUST NOT render a beef SKU as CACHE MISS in IN (DESIGN.md §5.2); MUST NOT convey stock state by color alone (DESIGN.md §13); MUST NOT compute or trust client-side prices (CC-PRC-005; Dependency rule 2); MUST NOT hand-format currency strings (CC-PRC-004); MUST NOT animate price text (DESIGN.md §7 "Sale/promo treatment"); MUST NOT use `bypassSecurityTrust*`/unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- DESIGN.md §5.2 pairs CACHE MISS with "offer nearest substitute"; the mechanism for selecting a substitute SKU is not specified anywhere in the specs (recommendation logic is undefined) — excluded from acceptance criteria beyond "no purchase action".
- Whether preorder from a WARMING card is an add-to-cart variant or a distinct flow is not specified (CC-CAT-003 says only "preorder permitted").
- Pagination vs. infinite scroll and default sort order for the menu listing are unspecified.
- Whether "Eviction Specials" clearance presentation (CC-PRC-007, DESIGN.md §5.3) appears on menu cards or only in the home-page band (DESIGN.md §10) is not specified; promotion presentation is owned by issue 033's engine output.
