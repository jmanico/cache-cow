# 063 · Storefront SSR shell: hydration, switchers, lang/hreflang

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-I18N-001, CC-I18N-004, CC-MKT-002
- **Title**: Storefront SSR shell: hydration, switchers, lang/hreflang
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The consumer storefront MUST be an Angular SSR (`@angular/ssr`) application with hydration that server-renders every page with the correct `lang` attribute and `hreflang` alternates for the rendered locale, and whose header exposes two independent controls — market and language — such that changing one MUST NOT silently change the other.
- **Rationale**: ARCHITECTURE.md ("Clients", consumer storefront) states SSR is effectively required, not optional: CC-MKT-003/004 demand server-side exclusion of non-veg content from IN responses including sitemaps and structured data, and CC-I18N-004 needs correct `lang`/`hreflang` per rendered locale — neither is satisfiable by client-side rendering. The SSR mechanism is confirmed: Angular SSR (`@angular/ssr`) with hydration, running on AKS. CC-MKT-002 requires market selection proposed from geolocation but user-overridable and persisted across sessions; CC-I18N-001 makes locale a user selection independent of market (a user MAY shop the DE market in English, REQUIREMENTS.md §2). DESIGN.md §7 fixes the UI contract: two independent header controls — market drives catalog, currency, and compliance; language drives strings; "never infer one from the other silently."
- **Design**: DESIGN.md §6 (page anatomy: 12-column grid, 1200px max content width, spacing scale, breakpoints; Paper background default, Butcher bands for hero/featured sections, Char footer; card and shadow rules); DESIGN.md §7 ("Region and language switcher"); DESIGN.md §§3–4 via the generated `tokens.json` (ARCHITECTURE.md, Dependency rule 8); DESIGN.md §2.2 logo variants for header/footer; DESIGN.md §13 accessibility (visible focus, keyboard operability).

## Scope
- **Applies To**: Web App
- **Components**: Storefront Angular application shell within the Angular workspace (issue 004): SSR bootstrap and hydration configuration, root layout (header, footer, page anatomy), market switcher control, language switcher control, `lang`/`hreflang` emission per rendered locale. Explicitly excluded: server-side transacting market/locale resolution and persistence logic (issue 024), SSR transfer-state/cache gating discipline (issue 028), sitemap/structured-data/feed generation (issue 071), the string-resource pipeline the shell consumes (issue 064), and all individual pages (issues 066–078).
- **Actors**: Anonymous and authenticated consumers in all six markets and seven locales.
- **Data Classification**: Public (rendered catalog-facing shell); market/locale selection state is Internal personalization data.

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Client-side-only rendering defeating server-side market gating and locale declaration (ARCHITECTURE.md "Clients": clients never gate, they display what the server already gated — Dependency rule 1); gating keyed off client-supplied hints (CC-SEC-012; SECURITY.md, Authentication and authorization rule 10).
- **Trust Boundary**: Client–server edge: the shell renders only what the server already gated for the transacting market/locale; the browser is untrusted.
- **Zero Trust Consideration**: The shell performs no gating and trusts no client hint: `lang`/`hreflang` and all gated content derive from the server-side transacting market/locale state (issue 024), never from `Accept-Language`, geolocation, or a client-forgeable cookie (SECURITY.md, HTTP boundary rule 10; Authentication and authorization rule 10). Prices and inventory render only from typed, validated responses (SECURITY.md, Input validation rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Web Frontend Security; V15 Secure Coding and Architecture (platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A (no specific control; UI shell)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A (per-market legal content itself is issues 067/077; this issue only carries the locale declaration that SEO/compliance surfaces build on)
- **Other**: BCP 47 locale identifiers (REQUIREMENTS.md §2, "Locale")

## Acceptance Criteria
1. **AC-01**: Given any storefront route requested for a rendered locale, when the server responds, then the initial HTML is fully server-rendered by Angular SSR (`@angular/ssr`) and Angular hydration attaches on the client without destroying and re-rendering the server DOM (ARCHITECTURE.md, "Clients" / "SSR").
2. **AC-02**: Given a page rendered in any of the seven launch locales (en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN), when the HTML is inspected, then the document declares the correct `lang` for the rendered locale and `hreflang` alternate links for the locale variants of that page (CC-I18N-004, CC-I18N-001).
3. **AC-03**: Given the storefront header, when it renders in any market/locale combination, then it contains two separate controls — a market switcher and a language switcher — per DESIGN.md §7, with the full set of launch markets (CC-MKT-001) and launch locales (CC-I18N-001) available.
4. **AC-04**: Given a user who changes only the language control, when the page re-renders, then the market — and therefore catalog, currency, and compliance content — is unchanged; and given a user who changes only the market control, then the language is unchanged (negative: the shell MUST NOT silently infer market from language or language from market; DESIGN.md §7, CC-MKT-002, CC-I18N-001).
5. **AC-05**: Given a user who has explicitly chosen a market, when they return in a later session, then the storefront renders that market via the persisted server-side transacting-market state of issue 024, and the geolocation-based proposal MUST NOT override the explicit choice (CC-MKT-002; SECURITY.md, Authentication and authorization rule 10).
6. **AC-06**: Given the rendered shell, when its layout and styling are inspected, then page anatomy follows DESIGN.md §6 (Paper default background, Butcher bands, Char footer, 12-column/1200px grid, stated breakpoints) and every brand color, type role, and status token is consumed from the generated `tokens.json` — no hardcoded brand values (ARCHITECTURE.md, Dependency rule 8; negative: a hardcoded hex from DESIGN.md §3.1 in component styles is a defect).
7. **AC-07**: Given the shell's components, when audited, then no market or gating conditionals exist in client code — the shell renders only server-gated data (ARCHITECTURE.md, Dependency rule 1) — and no `bypassSecurityTrust*` or unsanitized `[innerHTML]` sinks are present (SECURITY.md, Input validation rule 5).
8. **AC-08**: Given both switcher controls, when operated by keyboard only, then they are fully operable with visible focus (2px Ember outline on light surfaces) per DESIGN.md §13 (CC-NFR-004).

## Failure Behavior
- **On Invalid Input**: An unknown market or locale value submitted through a switcher is rejected server-side by the transacting-state resolution of issue 024; the shell renders the current valid state and never falls back to a client-hint-derived market/locale (CC-SEC-012).
- **On System Error**: Fail closed: SSR render errors return a generic error page with correlation ID only — no stack traces, exception messages, or internal identifiers (SECURITY.md, Logging rules 1 and 7); any exception in a gating-dependent render path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: SSR render failures and client-side errors routed through the Angular global ErrorHandler/interceptors to server-side structured logging with alerting (SECURITY.md, Logging rules 3 and 7; CC-NFR-003).

## Test Strategy
- **Unit Tests**: Angular component tests for both switchers (independence of the two controls, full market/locale option sets, keyboard operability); `lang`/`hreflang` emission logic per rendered locale.
- **Integration Tests**: SSR integration tests asserting server-rendered HTML for each of the seven locales carries correct `lang`/`hreflang`; hydration smoke test (no client re-render); persistence round-trip with issue 024's transacting-market state.
- **Security Tests**: CI grep gate for `bypassSecurityTrust*`/unsanitized `[innerHTML]` (SECURITY.md, Deployment rule 7); static check that no gating conditionals live in client code; assertion that rendered market never derives from `Accept-Language` (CC-SEC-012).
- **Compliance Tests**: Automated per-locale check of `lang`/`hreflang` correctness across launch locales retained as CI evidence (CC-I18N-004); token-sourcing lint evidence (Dependency rule 8).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-I18N-001, CC-I18N-004, CC-MKT-002 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 004 "Angular workspace scaffold: storefront (SSR), portal, dashboard with hardened builds"; 005 "Design token pipeline: tokens.json"; 024 "Transacting market/locale resolution: geo proposal, user override persistence" (CC-MKT-002 server state this shell displays); 028 "Cache-safe gating: cache keys, no-store personalized responses, gated SSR transfer state" (CC-MKT-009/CC-SEC-013 — the SSR transfer-state and cache-key discipline for everything this shell serves lives there, not here); 064 "ICU MessageFormat resource pipeline" (all shell strings).
- **Downstream**: 066 "Menu page", 067 "Product detail page", 068 "Cart", 069 "Checkout UI", 070 "Order tracking UI", 071 "SEO surfaces" (extends hreflang to sitemaps), 073–078 (content pages, contact, legal, store locator), 065 "Locale layout resilience" (validates this shell's templates), 097 "Real-user monitoring and performance budgets" (CC-NFR-002 LCP applies to this shell).
- **External**: None (no third-party runtime CDNs — SECURITY.md, Deployment rule 10; fonts self-hosted per CC-NFR-005).

## Implementation Notes
- **Constraints**: Angular SSR (`@angular/ssr`) with hydration on AKS (ARCHITECTURE.md, confirmed 2026-07-15); AOT builds, strict TypeScript and strictTemplates, subresource integrity, no production source maps (SECURITY.md, Deployment rule 9); CSP-compatible construction — no inline event handlers or inline styles (SECURITY.md, HTTP boundary rule 2); self-hosted, per-locale-subset fonts (CC-NFR-005; DESIGN.md §15); LCP < 2.5s at p75 per market (CC-NFR-002).
- **Anti-Patterns**: MUST NOT gate in the client or scatter market conditionals (ARCHITECTURE.md, Dependency rule 1; CC-MKT-006); MUST NOT infer market from language, `Accept-Language`, or geolocation, or language from market (DESIGN.md §7; CC-SEC-012); MUST NOT hardcode brand colors/type/status vocabulary (Dependency rule 8); MUST NOT use `bypassSecurityTrust*` or unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5); MUST NOT embed ungated data in SSR transfer state (that discipline is issue 028; SECURITY.md, HTTP boundary rule 10).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The persistence mechanism for the user's explicit market choice (cookie vs. account profile vs. both) is not specified by CC-MKT-002; this issue defers to issue 024's resolution and asserts only round-trip behavior.
- The specs do not define the visual form of the switcher controls (dropdown vs. dialog) beyond "two independent controls in the header" (DESIGN.md §7).
- Whether `hreflang` alternates on a page must include only locales valid for the transacting market or all seven launch locales is not specified (interacts with issue 071's SEO surface; recorded there too if needed).
