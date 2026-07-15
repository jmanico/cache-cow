# 073 · Meet our Chefs page

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-001
- **Title**: Meet our Chefs page
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: The storefront MUST render a "Meet our Chefs" page presenting structured chef profiles from a single roster shared across all markets, with bios localized per locale, rendered from the CMS exclusively through the sanitizing allowlist renderer.
- **Rationale**: CC-CNT-001 defines the page: structured profiles (shared roster, localized bios) rendered from the CMS through the sanitizing allowlist renderer (SECURITY.md, Input validation rule 5). DESIGN.md §10 lists "Meet our Chefs — Shared roster, localized bios" in the consumer page inventory, and §7 specifies the chef card ("Chefs are shared across markets; their bios localize"). Rendering through the allowlist renderer keeps CMS-authored bios from becoming a stored-XSS vector (CC-SEC-002 — controls authored in issue 072).
- **Design**: DESIGN.md §7 (Chef card: portrait, name, pit specialty, market flag(s)); §10 (page inventory); §§3–4 (color and typography tokens via the generated `tokens.json`, ARCHITECTURE.md Dependency rule 8); §6 (grid, card construction: 12px radius, 1px Smoke border); §9 (voice); §5.4 (pun budget: at most one cache/tech pun per viewport).

## Scope
- **Applies To**: Web App
- **Components**: Storefront Angular SSR page and chef-card component; Content & Localization module query for chef profiles (ARCHITECTURE.md, bounded context 10). Explicitly excluded: the Contentful integration and allowlist renderer itself (issue 072), the ICU string pipeline (issue 064), the SSR shell/navigation/`hreflang` machinery (issue 063), locale visual-regression gates (issue 065).
- **Actors**: Consumer storefront visitors (all markets, all locales); CMS editors authoring profiles (untrusted input at the platform boundary)
- **Data Classification**: Public (published editorial content)

## Security Context
- **Defense Layer**: Sanitization (inherited — all CMS content on this page flows through issue 072's allowlist renderer)
- **Threat(s) Addressed**: Stored XSS via CMS-authored bios (OWASP Top Ten A03:2021, CWE-79), mitigated by rendering only through the allowlist renderer (CC-SEC-002; SECURITY.md, Input validation rule 5)
- **Trust Boundary**: CMS content → rendered storefront page (server-side, via issue 072)
- **Zero Trust Consideration**: Chef profile fields, including localized bios, are untrusted CMS input; the page binds only sanitized renderer output and typed, validated fields (SECURITY.md, Input validation rule 1), never raw CMS payloads.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V1 Encoding and Sanitization (inherited via issue 072); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation, inherited via issue 072)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA (CC-NFR-004; DESIGN.md §13)

## Acceptance Criteria
1. **AC-01**: Given the Meet our Chefs page in any market, when it renders, then each chef appears as a chef card containing portrait, name, pit specialty, and market flag(s) per DESIGN.md §7 (CC-CNT-001).
2. **AC-02**: Given any two markets, when the page is rendered in each, then the chef roster is identical (shared roster) while bios render in the active locale (CC-CNT-001; DESIGN.md §7, §10).
3. **AC-03**: Given a chef bio authored in the CMS containing markup outside the allowlist (e.g., a `<script>` tag or inline event handler), when the page renders, then that markup never reaches the response — the bio renders only through issue 072's sanitizing allowlist renderer (CC-CNT-001, CC-SEC-002; SECURITY.md, Input validation rule 5). (Negative case.)
4. **AC-04**: Given the page in each of the seven launch locales (CC-I18N-001), when it renders, then all page chrome strings come from ICU MessageFormat resources (CC-I18N-002, via issue 064) and the response carries the correct `lang` and `hreflang` alternates via the SSR shell (CC-I18N-004, via issue 063).
5. **AC-05**: Given locale text up to 130 percent of the English string length, when a chef card renders, then no truncation or overflow occurs (CC-I18N-005; DESIGN.md §4.2 — verified in the issue 065 visual-regression gates).
6. **AC-06**: Given the page markup, when it is audited, then chef portraits carry text alternatives and the page meets WCAG 2.2 AA per DESIGN.md §13 (CC-NFR-004), and no brand colors, type, or status vocabulary are hardcoded — all styling consumes generated design tokens (ARCHITECTURE.md, Dependency rule 8). (Includes negative case: no hardcoded token values.)

## Failure Behavior
- **On Invalid Input**: N/A for visitor input (the page accepts none). CMS entries failing allowlist validation are stripped/rejected by issue 072's renderer, with structured validation-rejection logging (SECURITY.md, Input validation rule 1; Logging rule 3).
- **On System Error**: Fail closed on the content pipeline — a renderer or CMS-fetch failure produces a generic RFC 9457 error or omits the affected content (issue 021; SECURITY.md, Logging rule 1); raw CMS content is never emitted as a fallback.
- **Alerting**: Rendering-pipeline errors surface through structured logs and centralized monitoring with correlation IDs (SECURITY.md, Logging rules 3 and 7); no page-specific alerting beyond that.

## Test Strategy
- **Unit Tests**: Angular component tests for the chef card (all §7 fields render; long-string behavior; token-based styling; alt text present).
- **Integration Tests**: ASP.NET Core + SSR integration tests rendering the page from stubbed CMS payloads in multiple markets/locales, asserting shared roster, localized bios, and correct `lang`/`hreflang`.
- **Security Tests**: Payload tests asserting AC-03 (hostile bio markup never reaches output); the raw-HTML-sink CI grep gate (issue 006) covers the page's bindings.
- **Compliance Tests**: Automated accessibility checks (issue 098) and locale visual regression across the top templates (CC-QA-006, issue 065) include this page.
- **Coverage Target**: ≥ 80% for the page and component code (CC-QA-001); tests tagged CC-CNT-001 (and CC-SEC-002 for AC-03) per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 072 "Contentful integration and sanitizing allowlist renderer" (all CMS rendering); 063 "Storefront SSR shell" (navigation, `lang`/`hreflang`); 064 "ICU MessageFormat resource pipeline" (chrome strings); 005 "Design token pipeline" (tokens.json); 004 "Angular workspace scaffold".
- **Downstream**: 065 "Locale layout resilience" and 098 "Accessibility gates" exercise this page in their gates.
- **External**: Contentful (profile content source, per ARCHITECTURE.md "Technology decisions").

## Implementation Notes
- **Constraints**: Angular SSR page (ARCHITECTURE.md, "Clients"); chef data is CMS-sourced structured content flowing through the Content & Localization bounded context (ARCHITECTURE.md item 10); typography and card construction per DESIGN.md §§4, 6–7 (Archivo 700 names, localized body text per the per-script stacks in §4.2); page content is public and market-independent, so any caching still keys on transacting market + locale per SECURITY.md, HTTP boundary rule 10.
- **Anti-Patterns**: No raw-HTML sinks or `bypassSecurityTrust*` (SECURITY.md, Input validation rule 5); no per-market roster forks (the roster is shared; only bios localize — CC-CNT-001); no hardcoded colors/type (ARCHITECTURE.md, Dependency rule 8); no translated puns — localized copy is written per market, and a pun that does not survive a locale is cut in that locale (DESIGN.md §9).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The chef-profile content model beyond DESIGN.md §7's card fields (portrait, name, pit specialty, market flags, bio) is not specified — e.g., ordering of the roster, number of chefs, additional fields.
- Fallback behavior when a bio lacks a translation for the active locale is unspecified (CC-I18N-006 defines fallback for transactional email only, not editorial content).
- Whether the "market flag(s)" on a chef card have any market-gating interaction (e.g., filtering chefs by market) is unspecified; DESIGN.md §7 says chefs are shared across markets, so no filtering is asserted here.
