# 067 · Product detail page: structured food data, FSSAI mark, price display

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CAT-004, CC-CNT-006, CC-PRC-002
- **Title**: Product detail page: structured food data, FSSAI mark, price display
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: The product detail page MUST render gallery, weight/serving, per-format reheat instructions, and allergen/nutrition data exclusively from structured fields in every locale, MUST display the regional price per the market's tax-display convention via locale-aware formatting, MUST show the FSSAI green-in-square vegetarian regulation mark on every IN product presentation, and the non-veg mark MUST NOT appear anywhere in the IN market.
- **Rationale**: CC-CAT-004 requires allergen and nutrition data to render from structured fields, never from free-text CMS content, in every locale — the structured data is the single source for per-market food-information compliance (CC-CMP-004: EU FIC, FSSAI, US FDA, JP labeling). CC-CNT-006 mandates the FSSAI vegetarian marking (green mark) on all IN product presentations per regulation, and that the non-veg mark never appears in the IN market. CC-PRC-002 fixes price display convention: DE/ES/MX/JP/IN tax-inclusive (MX IVA-inclusive; IN inclusive with GST line on the invoice), US tax-exclusive with estimated tax at checkout, and DE additionally displaying unit price per kilogram alongside every price (Preisangabenverordnung). DESIGN.md §3.3 requires the regulation mark itself in IN — not a stylized leaf — while other markets use the cache.500 leaf-dot badge plus the word "Vegetarian"; status is never conveyed by color alone (DESIGN.md §13).
- **Design**: DESIGN.md §10 "Product detail" (gallery, weight/serving calc, reheat instructions per-format: oven/sous-vide/steam, nutrition and allergens, regional price); §3.3 veg marking rules per market; §4.4 worked price examples in Plex Mono; §5.4 pun budget — zero puns inside allergen/nutrition content; §8.4 (DE: net weights and unit prices per kg next to every price, in Plex Mono); §8.5 (JP: dense information presentation is expected — do not over-whitespace); §13 accessibility.

## Scope
- **Applies To**: Web App
- **Components**: Storefront product detail page (Angular, SSR via issue 063's shell): gallery, product data sections (weight/serving, per-format reheat instructions, ingredients, allergens, nutrition), veg marking component (FSSAI regulation mark for IN; leaf-dot + "Vegetarian" label elsewhere), regional price display consuming issue 034's formatting/tax conventions. Explicitly excluded: the SKU domain model and structured food fields themselves (issue 029), gating and the IN 404 for non-veg URLs (issues 025/026), price formatting engine and tax conventions implementation (issue 034), cache-status badge component (issue 066), add-to-cart/cart behavior (issue 068), structured data/SEO markup (issue 071), CMS-sourced editorial content rendering (issue 072).
- **Actors**: Anonymous and authenticated consumers in all six markets and seven locales.
- **Data Classification**: Public (catalog and food-information data); regulated in presentation (food-information law per market, CC-CMP-004).

## Security Context
- **Defense Layer**: Encoding (client renders server-gated, typed, structured data; no free-text safety content, no client-side computation)
- **Threat(s) Addressed**: Safety-critical food data (allergens) sourced from free-text CMS content — wrong, unlocalizable, and an injection surface (CC-CAT-004; SECURITY.md, Input validation rules 1 and 5); regulatory-marking errors in IN (CC-CNT-006); reliance on client-supplied or client-computed prices (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
- **Trust Boundary**: Client–server edge: all product data crosses as typed, schema-validated responses (SECURITY.md, Input validation rule 1); the page itself performs no gating — a non-veg product URL in IN never reaches this page because the server returns 404 (CC-MKT-004, issue 026).
- **Zero Trust Consideration**: The page renders only structured fields the server supplies for the transacting market/locale; allergen/nutrition sections have no free-text fallback path; the veg/non-veg classification driving the marking comes from the server-side SKU classification (CC-CAT-001), never from client state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Web Frontend Security; V1 Encoding and Sanitization (platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A (compliance risk here is food-information law, not 800-53 scope)
- **NIST SP 800-207**: N/A
- **Regulatory**: FSSAI vegetarian marking for IN (CC-CNT-006; CC-CMP-004); EU FIC allergen/nutrition declaration, US FDA labeling, JP labeling (CC-CMP-004, all sourced from CC-CAT-004's structured fields); German Preisangabenverordnung unit pricing (CC-PRC-002)
- **Other**: WCAG 2.2 AA (DESIGN.md §13; CC-NFR-004)

## Acceptance Criteria
1. **AC-01**: Given any SKU in any market and locale, when the product detail page renders, then it contains gallery, weight and serving estimate, per-format reheat instructions (oven/sous-vide/steam per DESIGN.md §10), ingredients, allergens, and nutrition in the market's format — all sourced from the structured SKU fields of CC-CAT-001 (CC-CAT-004).
2. **AC-02**: Given allergen and nutrition sections in every locale, when their data source is audited, then they render exclusively from structured fields — free-text CMS content MUST NOT be a source for allergen or nutrition display (negative case; CC-CAT-004; structured data is the single source per CC-CMP-004).
3. **AC-03**: Given any product presentation in the IN market, when it renders, then the FSSAI green-in-square vegetarian regulation mark appears — the regulation mark itself, not a stylized leaf (CC-CNT-006; DESIGN.md §3.3).
4. **AC-04**: Given the IN market, when any surface of the page renders, then the non-veg mark appears nowhere (negative case; CC-CNT-006: "the non-veg mark MUST NOT appear anywhere in the IN market").
5. **AC-05**: Given a vegetarian SKU in a non-IN market, when the page renders, then the veg indicator is the cache.500 leaf-dot badge plus the word "Vegetarian" — shape plus label, never color alone (DESIGN.md §3.3, §13).
6. **AC-06**: Given the regional price, when displayed, then it uses issue 034's locale-aware formatting per DESIGN.md §4.4 and the market's tax convention per CC-PRC-002: tax-inclusive for DE/ES/MX/JP/IN, tax-exclusive for US (estimated tax computed at checkout, issue 069), with the market's tax-inclusion note (DESIGN.md §7 "Price display"); for DE, the unit price per kilogram renders alongside every price in Plex Mono (CC-PRC-002; DESIGN.md §8.4).
7. **AC-07**: Given the page's copy, when reviewed, then zero cache/tech puns appear inside allergen/nutrition content (DESIGN.md §5.4: comedy never touches safety information; negative case).
8. **AC-08**: Given the page components, when audited, then no price computation, no market/gating conditionals, and no raw-HTML sinks exist in client code; prices and food data render only from typed, validated responses (SECURITY.md, Input validation rules 1 and 5; ARCHITECTURE.md, Dependency rules 1–2).

## Failure Behavior
- **On Invalid Input**: A product response failing the typed schema at the client HTTP boundary is rejected and the page renders an error state, not partial unvalidated data (SECURITY.md, Input validation rule 1); users see generic messages with correlation IDs only (SECURITY.md, Logging rule 7).
- **On System Error**: Fail closed on compliance-bearing content: if structured allergen/nutrition data or the veg-classification/marking data for the transacting market cannot be resolved, the affected presentation MUST NOT render with free-text substitutes, without the required FSSAI mark in IN, or with a guessed classification — an error in a gating/compliance path is a denial, never a bypass (SECURITY.md, Logging rule 2). Non-veg product URLs in IN return HTTP 404 upstream of this page (CC-MKT-004, issue 026).
- **Alerting**: Render failures on compliance-bearing sections logged as structured events with alerting (SECURITY.md, Logging rule 3; CC-NFR-003); the IN-market synthetic gating probe (issue 096) exercises IN product presentations in production.

## Test Strategy
- **Unit Tests**: Angular component tests: structured-field-only rendering of allergen/nutrition sections (including rejection of any free-text source path), veg marking selection per market (FSSAI mark in IN, leaf-dot + label elsewhere), per-format reheat instruction rendering, DE unit-price-per-kg presence, tax-note rendering per market convention.
- **Integration Tests**: SSR renders per market x locale asserting AC-01–AC-06; IN renders asserting FSSAI mark present and non-veg mark absent across all page states; composes with issue 027's market-gating matrix (CC-QA-003) for the IN non-veg-URL 404 behavior owned by issue 026.
- **Security Tests**: Schema-rejection tests for malformed product payloads; raw-HTML-sink CI grep gate (SECURITY.md, Deployment rule 7); assertion that allergen/nutrition components have no CMS rich-text input binding.
- **Compliance Tests**: Automated per-market evidence that allergen/nutrition render from structured fields in every locale (CC-CAT-004, CC-CMP-004) and that IN presentations carry the FSSAI mark (CC-CNT-006); contrast and status-not-color-alone checks via issue 005's CI contrast gates (DESIGN.md §13); locale visual regression via issue 065.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CAT-004, CC-CNT-006, CC-PRC-002 (and CC-MKT-004 for the upstream 404 integration case) per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 029 "SKU domain model with structured food data" (CC-CAT-001 fields this page renders); 025 "Server-side gating enforcement API" and 026 "404 semantics for market-gated resources" (a non-veg PDP request in IN 404s before rendering, CC-MKT-004); 034 "Locale-aware price formatting and market tax-display conventions" (CC-PRC-002/004 display); 063 "Storefront SSR shell" (hosts the page); 064 "ICU MessageFormat resource pipeline" (all strings); 005 "Design token pipeline" (colors/type).
- **Downstream**: 068 "Cart with cross-market preservation rules" (add-to-cart entry point); 071 "SEO surfaces" (structured data for PDPs must match this page's gated content); 096 "Per-market synthetic probes including IN gating probe" (asserts IN behavior in production); 065 "Locale layout resilience" (PDP is plausibly a top-20 template).
- **External**: None directly (Contentful editorial content is out of scope here; allergen/nutrition never come from CMS free text per CC-CAT-004).

## Implementation Notes
- **Constraints**: Angular SSR page inside issue 063's shell; structured food data per CC-CAT-001 is the only source for safety content (CC-CAT-004, CC-CMP-004); FSSAI mark is the regulation mark on anything representing packaging or a menu of record (DESIGN.md §3.3); prices in Plex Mono per DESIGN.md §4.1/4.4 via issue 034 — never hand-formatted (CC-PRC-004); JP pages are information-dense by design (DESIGN.md §8.5); meat photography does not render in the IN market on any surface (DESIGN.md §8.1 — moot for PDPs since non-veg SKUs are absent, but applies to any editorial imagery slots on the page); vegetarian dishes shot with identical seriousness (DESIGN.md §8.6).
- **Anti-Patterns**: MUST NOT render allergen/nutrition from free-text CMS content (CC-CAT-004); MUST NOT use a stylized leaf where the FSSAI regulation mark is required, and MUST NOT let the non-veg mark reach any IN surface (CC-CNT-006; DESIGN.md §3.3); MUST NOT convey veg status by color alone (DESIGN.md §13); MUST NOT hand-format currency or compute prices client-side (CC-PRC-004/005); MUST NOT place puns in allergen/nutrition or price-adjacent safety content (DESIGN.md §5.4); MUST NOT use `bypassSecurityTrust*`/unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5); MUST NOT implement market conditionals in the client — marking selection derives from server-supplied market policy data (ARCHITECTURE.md, Dependency rule 1).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Compliance-bearing presentation (FSSAI marking, allergen rendering) additionally requires human review per the mandatory-review gate.

## Open Questions
- Behavior when a structured allergen/nutrition field is absent for a SKU/locale is unspecified: the specs prohibit free-text fallback (CC-CAT-004) but do not say whether the PDP may render at all without its allergen section; this issue fails closed on the section and records the page-level decision as open.
- The gallery's aspect ratio and image-count rules are unspecified (DESIGN.md §7 fixes 4:5 for the product card photo, not the PDP gallery); only photography direction (§8.6) constrains content.
- "Weight/serving calc" (DESIGN.md §10) is not defined beyond the phrase — whether it is an interactive calculator or a static display of CC-CAT-001's net weight and serving estimate is unspecified; acceptance criteria assert only display of the structured fields.
- Whether the non-IN veg leaf-dot badge and "Vegetarian" word must appear in the locale's language or as a fixed English label is unspecified (assumed localized through issue 064, not asserted).
