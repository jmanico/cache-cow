# 034 · Locale-aware price formatting and market tax-display conventions

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-PRC-002, CC-PRC-004, CC-I18N-003
- **Title**: Locale-aware price formatting and market tax-display conventions
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional, Compliance

## Requirement
- **Description**: All price formatting MUST use locale-aware formatting (`Intl.NumberFormat` or server equivalent) and price display MUST follow market convention — DE/ES/MX/JP/IN tax-inclusive (MX IVA-inclusive; IN inclusive with a GST line on the invoice), US tax-exclusive with estimated tax computed at checkout, and DE additionally displaying unit price per kilogram alongside every price (REQUIREMENTS.md CC-PRC-002, CC-PRC-004, CC-I18N-003).
- **Rationale**: Tax-display convention is market law: DE's per-kg unit price is mandated by the Preisangabenverordnung (CC-PRC-002), and tax-inclusive display is the legal norm in the non-US markets. Hand-formatted currency strings are declared a defect outright (CC-PRC-004) because they break under locale rules — notably INR lakh/crore grouping and JPY zero-decimal — which must come from the locale, never hand-formatting (DESIGN.md §4.4). Formatting/display never computes money: computation authority stays with the order service (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
- **Design**: DESIGN.md §4.4 worked examples (en-US `$149.00`; es-ES `149,00 €`; es-MX `$149.00 MXN` where cross-currency ambiguity exists; de-DE `149,00 €`; ja-JP `￥14,900` no decimals; hi-IN/en-IN `₹12,49,000.00` lakh/crore grouping from the locale); DESIGN.md §7 "Price display" (always locale-formatted, always includes the market's tax-inclusion note, prices in IBM Plex Mono); DESIGN.md §8.4 (DE net weights and unit prices per kg next to every price, in Plex Mono).

## Scope
- **Applies To**: Both
- **Components**: A shared price-presentation capability used by the Angular storefront (SSR + hydrated client) and by server-rendered surfaces: locale-aware currency formatting, the market tax-inclusion note, US estimated-tax display slot at checkout, and DE €/kg unit-price display derived from net weight (CC-CAT-001) and price (issue 032). Excludes: tax *computation* (Stripe Tax on the order path, issues 036/039; IN GST via Razorpay/local rules, issue 040); the invoice GST line and invoice legal formats (issues 047/048); email price rendering (issue 043); page layouts consuming this capability (issues 066/067/069).
- **Actors**: Consumer (all locales/markets), storefront SSR, server rendering surfaces.
- **Data Classification**: Public (displayed consumer prices)

## Security Context
- **Defense Layer**: Encoding
- **Threat(s) Addressed**: Legally non-compliant or misleading price display (regulatory exposure, e.g. Preisangabenverordnung; STRIDE: Repudiation of advertised price); drift between displayed and charged amounts if display code ever computes money (business-logic abuse, OWASP Top Ten A04:2021 — prevented by keeping computation in the order service, CC-PRC-005).
- **Trust Boundary**: Client-server edge: clients render prices only from typed, validated responses (SECURITY.md, Input validation rule 1); formatted strings are presentation of server-computed minor-unit values, never inputs to any computation.
- **Zero Trust Consideration**: Locale selects formatting only; the transacting market (server-side state, CC-SEC-012) selects currency and tax convention — a client-forged locale or `Accept-Language` header can never change the amount, currency, or tax treatment displayed (SECURITY.md, Authentication rule 10).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic (typed boundary data feeding display).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A
- **NIST SP 800-207**: N/A
- **Regulatory**: Preisangabenverordnung (DE unit price per kg — named in CC-PRC-002); MX IVA-inclusive display; IN GST-inclusive display with GST line on the invoice (invoice side owned by CC-INV-001, issue 047).
- **Other**: CLDR-backed locale data via `Intl.NumberFormat` (client) and .NET globalization / ICU (server equivalent), per CC-PRC-004.

## Acceptance Criteria
1. **AC-01**: Given each launch locale, when a price is rendered, then output matches the DESIGN.md §4.4 worked examples — en-US `$149.00`, es-ES `149,00 €`, de-DE `149,00 €`, ja-JP `￥14,900` with no decimals, hi-IN/en-IN lakh/crore grouping (e.g. `₹12,49,000.00`) — all produced by `Intl.NumberFormat` or the server equivalent from the locale, never hand-formatted (CC-PRC-004, CC-I18N-003).
2. **AC-02**: Given the DE, ES, MX, JP, or IN transacting market, when a price is displayed, then it is the tax-inclusive amount with the market's tax-inclusion note (DESIGN.md §7); given the US market, then the price is tax-exclusive and checkout displays an estimated-tax amount computed by the order path (CC-PRC-002).
3. **AC-03**: Given the DE market, when any price is displayed (card, PDP, checkout), then a unit price per kilogram appears alongside it, derived from the SKU net weight and the market price (CC-PRC-002; DESIGN.md §8.4).
4. **AC-04**: Given SSR and the hydrated client render the same price, when both outputs are compared for every launch locale, then they are identical — no server/client formatting divergence (CC-PRC-004; ARCHITECTURE.md, SSR: Angular SSR with hydration).
5. **AC-05**: Given a client-supplied locale differing from the transacting market, when a price renders, then the locale changes only formatting — currency, amount, and tax convention still come from the server-side transacting market (CC-SEC-012 negative case; SECURITY.md, Authentication rule 10).
6. **AC-06**: Given the codebase, when reviewed/linted, then no hand-formatted currency string exists (no string concatenation of currency symbols with numbers, no hardcoded group/decimal separators) — hand-formatted currency strings are a defect (CC-PRC-004 negative case).
7. **AC-07**: Given US displays, when weights accompany prices (e.g., DE-equivalent unit context or spec sheets), then US shows imperial-primary and all other markets metric, via locale-aware unit formatting (CC-I18N-003).

## Failure Behavior
- **On Invalid Input**: A render request with an unknown locale or a non-minor-unit amount is rejected at the typed boundary (400 RFC 9457 on API surfaces; component-level error in Angular routed through the global ErrorHandler, SECURITY.md, Logging rule 7).
- **On System Error**: Fail closed for the money-adjacent path (SECURITY.md, Logging rule 2): if formatting or the tax-convention resolution fails, the surface omits/errors the price rather than showing an unformatted raw number, a wrong-convention price, or a DE price without its €/kg unit price.
- **Alerting**: Formatting-path errors logged server-side with correlation IDs (client sees generic message, SECURITY.md, Logging rules 1, 7); error-rate alerting via Azure Monitor (CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the server formatter and Angular component tests for the client: all seven locales × the DESIGN.md §4.4 examples, JPY zero-decimal, INR lakh/crore grouping, DE €/kg derivation (exact-decimal arithmetic per CC-PRC-003, no floats in tests), tax-convention selection per market.
- **Integration Tests**: SSR/hydration parity test asserting byte-identical formatted prices server vs client per locale; storefront integration test that market drives convention and locale drives formatting independently (CC-MKT-002/CC-I18N-001 interplay).
- **Security Tests**: Negative test that forged `Accept-Language`/locale cookies cannot alter amount, currency, or tax convention (CC-SEC-012); CI lint/grep gate candidate for hand-formatted currency patterns (see Open Questions); rounding cases in all five currencies per CC-QA-004.
- **Compliance Tests**: Automated evidence for DE: every price-bearing template renders the €/kg unit price (Preisangabenverordnung, CC-PRC-002); locale visual regression hooks per CC-QA-006.
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-PRC-002, CC-PRC-004, CC-I18N-003.

## Dependencies
- **Upstream**: 002 Shared kernel Money type (minor-unit inputs); 003 Shared kernel identity types; 024 Transacting market/locale resolution; 029 SKU domain model (net weight for €/kg); 032 Per-SKU per-market price model; 063 Storefront SSR shell (render host).
- **Downstream**: 043 Transactional order emails (localized price rendering); 047 Per-market invoice tax content (IN GST line lives there); 048 Invoice PDF rendering; 066 Menu page; 067 Product detail page; 069 Checkout UI per market (US estimated tax display); 099 Money-path testing suite.
- **External**: N/A (Stripe Tax computes US estimated tax on the order path, issue 039; this issue only displays the computed value).

## Implementation Notes
- **Constraints**: Client: `Intl.NumberFormat` with the transacting market's currency and the user's locale; server: the .NET/ICU equivalent, and both must agree for SSR hydration parity (ARCHITECTURE.md, SSR). Prices arrive as Money minor units (issue 002) and are converted to display-major units only inside the formatter. Plex Mono presentation and the tax-inclusion note wording come from design tokens and ICU MessageFormat resources (ARCHITECTURE.md, Dependency rule 8; CC-I18N-002 — no HTML in string resources, SECURITY.md, Input validation rule 7). DE €/kg uses exact arithmetic (CC-PRC-003).
- **Anti-Patterns**: MUST NOT hand-format currency (no `"$" + amount`, no hardcoded separators, no custom grouping logic for INR) (CC-PRC-004; DESIGN.md §4.4); MUST NOT compute tax, discounts, or totals in display code — display only what the order path computed (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2); MUST NOT derive currency or tax convention from locale or client hints (CC-SEC-012); MUST NOT animate price text (DESIGN.md §7); no puns in checkout/money surfaces (DESIGN.md §5.4).
- **AI Development Guidance**: Identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); money-adjacent formatting code is a mutation-testing candidate per CC-QA-001. PR cites CC-PRC-002/004, CC-I18N-003 (REQUIREMENTS.md §17).

## Open Questions
- CC-PRC-004 declares hand-formatted currency strings a defect, but no enforcement mechanism is specified (SECURITY.md Deployment rule 7 defines a CI grep gate only for raw-HTML sinks); an analogous lint/grep gate seems intended but is not mandated — flagging rather than inventing one as a hard gate.
- DESIGN.md §4.4 shows es-MX as `$149.00 MXN` "where cross-currency ambiguity exists"; the trigger condition for ambiguity (always in MX? only in mixed-market contexts?) is undefined.
- Rounding rules for the DE €/kg unit price (decimal places, rounding mode under Preisangabenverordnung) are not specified.
- The exact localized wording of the per-market tax-inclusion note (DESIGN.md §7) is not specified; assumed to be an ICU string resource per CC-I18N-002.
