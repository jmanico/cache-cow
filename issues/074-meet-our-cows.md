# 074 · Meet our Cows page with IN navigation promotion

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-002, CC-MKT-005
- **Title**: Meet our Cows page with IN navigation promotion
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The storefront MUST render a "Meet our Cows" mascot page that is present in primary navigation in the IN market and under Our Story in all other markets, and the page MUST NOT contain links to non-veg product detail pages in any market.
- **Rationale**: CC-MKT-005 [P1] mandates the navigation placement: primary navigation in IN, Our Story elsewhere. CC-CNT-002 defines the content (mascot/herd content per DESIGN.md §8.1) and prohibits links to non-veg PDPs in any market. DESIGN.md §8.1 ("The India inversion") explains the driver: India is a fully vegetarian market where the cow is culturally revered, so the brand handles this by inversion — the herd is promoted sincerely as mascots and brand family — and imposes the separation rule that herd-mascot content and butchery content never appear in the same view in any market. Per CC-MKT-006, the placement rule is market-gating policy encoded as data, not scattered conditionals.
- **Design**: DESIGN.md §7 (Cow card: geometric mascot illustration with differentiating blaze shapes — database cylinder, lightning bolt, heart — name, "role", one-line bio; these illustrations are the only place the mascot style is permitted); §8.1 (India inversion, sincere framing, separation rule); §10 (page inventory); §2.3 (the icon/mascot is identical in every market, including India); §§3–6 (tokens, type, grid).

## Scope
- **Applies To**: Web App
- **Components**: Storefront Angular SSR page and cow-card component; navigation placement consumption of Market & Gating Policy data (ARCHITECTURE.md, bounded context 1); CMS-sourced herd content via the Content & Localization module (bounded context 10). Explicitly excluded: the gating-policy model and enforcement API (issues 023/025), the allowlist renderer (issue 072), the SSR shell and navigation frame (issue 63 — this issue supplies the placement rule consumption, not the nav component), the market-gating CI matrix (issue 027), the Cuts page side of the separation rule (issue 075).
- **Actors**: Consumer storefront visitors in all six markets; CMS editors (untrusted input)
- **Data Classification**: Public (published editorial content)

## Security Context
- **Defense Layer**: Architecture (server-side, policy-as-data placement and link gating) and Sanitization (CMS content via issue 072)
- **Threat(s) Addressed**: Compliance/brand-integrity failure in the IN market — a herd-mascot page linking to beef PDPs, or demoted navigation in IN, violates CC-MKT-005/CC-CNT-002; a cached response leaking another market's variant (CC-MKT-009); stored XSS via CMS herd content (CWE-79, via issue 072)
- **Trust Boundary**: Server-side rendering boundary — navigation placement and any product links on the page are determined server-side from Market & Gating Policy state, never client-side (ARCHITECTURE.md, Dependency rule 1: clients never gate)
- **Zero Trust Consideration**: Placement and link decisions key exclusively off server-side transacting-market state, never off geolocation, `Accept-Language`, or client-supplied hints (CC-SEC-012; SECURITY.md, Authentication rule 10); CMS content is untrusted and renders only through the allowlist renderer.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (server-side enforcement of market-scoped content rules); V1 Encoding and Sanitization (inherited via issue 072); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement — server-side market gating of content and links)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A (the IN veg-market regulatory marking itself is CC-CNT-006, owned by issue 067)
- **Other**: WCAG 2.2 AA (CC-NFR-004; DESIGN.md §13)

## Acceptance Criteria
1. **AC-01**: Given a session whose transacting market is IN, when any storefront page renders, then "Meet our Cows" is present in primary navigation (CC-MKT-005).
2. **AC-02**: Given a session in any of US, ES, MX, DE, JP, when navigation renders, then "Meet our Cows" appears under Our Story and not in primary navigation (CC-MKT-005).
3. **AC-03**: Given the rendered Meet our Cows page in each of the six markets, when every link in the response (including SSR transfer/hydration state) is enumerated, then none resolves to a non-veg PDP (CC-CNT-002; CC-MKT-009 for the embedded state). (Negative case.)
4. **AC-04**: Given the rendered page in any market, when its content is inspected, then it contains no butchery content and no link to the "Meet our Cuts" experience — herd-mascot content and butchery content never share a view (DESIGN.md §8.1 separation rule). (Negative case.)
5. **AC-05**: Given the herd content, when a cow card renders, then it uses the mascot illustration style (flat Char shapes on Butcher, differentiating blaze) with name, role, and one-line bio per DESIGN.md §7, and the mascot illustration style appears nowhere on the storefront outside cow cards (DESIGN.md §7). (Includes negative case.)
6. **AC-06**: Given the navigation-placement rule, when its implementation is reviewed, then the IN promotion is read from per-market policy configuration data (Market & Gating Policy, issues 023/025), not from hardcoded market conditionals in the client or page code (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1). (Negative case: no `if (market === 'IN')` scattered in UI code.)
7. **AC-07**: Given CMS-authored herd content containing markup outside the allowlist, when the page renders, then it never reaches the response — all content flows through issue 072's sanitizing allowlist renderer (CC-SEC-002). (Negative case.)
8. **AC-08**: Given any cacheable rendering of this page, when it is cached at SSR/edge/CDN, then the cache key derives from server-side transacting market + locale, so an IN response variant is never served to another market or vice versa (CC-MKT-009; SECURITY.md, HTTP boundary rule 10).

## Failure Behavior
- **On Invalid Input**: N/A for visitor input (the page accepts none). CMS entries failing allowlist validation are stripped/rejected via issue 072 with structured logging.
- **On System Error**: Fail closed — any exception while resolving the market policy for placement or link gating is a denial, never a bypass: the request fails with a generic RFC 9457 error rather than rendering with guessed placement or ungated links (SECURITY.md, Logging rule 2; issue 021).
- **Alerting**: Gating-path exceptions log as structured security-relevant events to centralized monitoring (SECURITY.md, Logging rule 3); the per-market synthetic probes (issue 096, CC-NFR-003) exercise market-variant rendering in production.

## Test Strategy
- **Unit Tests**: Angular component tests for the cow card (fields, illustration slots, token-based styling); .NET unit tests for the placement-rule consumption (policy data in → placement out, both variants).
- **Integration Tests**: SSR integration tests rendering the page and navigation in all six markets, asserting AC-01–AC-04 including link enumeration of the full response body and transfer state.
- **Security Tests**: Hostile-CMS-payload tests (AC-07); the raw-HTML-sink grep gate (issue 006); cache-key assertions per issue 028's patterns for this route.
- **Compliance Tests**: The market-gating CI matrix (issue 027, CC-QA-003) includes this route × every market; accessibility gates (issue 098) include this page.
- **Coverage Target**: ≥ 80% (CC-QA-001); tests tagged CC-CNT-002, CC-MKT-005 (and CC-MKT-009 for AC-08) per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 023 "Market & Gating Policy: policy-as-data model" and 025 "Server-side gating enforcement API" (placement and content-availability policy); 024 "Transacting market/locale resolution" (the market state placement keys off); 072 "Contentful integration and sanitizing allowlist renderer"; 063 "Storefront SSR shell" (navigation frame); 064 "ICU MessageFormat resource pipeline"; 005 "Design token pipeline".
- **Downstream**: 027 "Market-gating CI test matrix" (asserts this page's per-market behavior); 028 "Cache-safe gating" (covers this route's cache keys); 075 "Meet our Cuts" (owns the other side of the separation rule: no mascot illustrations on the Cuts page).
- **External**: Contentful (herd content source).

## Implementation Notes
- **Constraints**: Placement is data-driven from the Market & Gating Policy context and enforced server-side in the SSR response (ARCHITECTURE.md, Dependency rule 1 — clients never gate; they display what the server already gated); the cow-mascot illustration set (7 illustrations, DESIGN.md §15) is a design asset dependency; the logo icon is identical in every market including IN (DESIGN.md §2.3 — only the menu changes); IN framing is sincere, mascots-and-brand-family tone (DESIGN.md §8.1, §9).
- **Anti-Patterns**: No market conditionals scattered in page/client code (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); no links to non-veg PDPs from this page in ANY market — not only IN (CC-CNT-002); no mixing of mascot and butchery content in one view (DESIGN.md §8.1); mascot illustration style used nowhere else (DESIGN.md §7); no raw-HTML sinks (SECURITY.md, Input validation rule 5); no caching keyed on client hints (SECURITY.md, HTTP boundary rule 10).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The herd roster (how many cows are presented vs. the 7-illustration asset set in DESIGN.md §15, their names, roles, bios) is a content decision not specified in the specs.
- Localization of cow names and pun-based "roles" (e.g., "Chief Cud Officer") per locale is governed only by the general principle in DESIGN.md §9 (native-written copy; untranslatable puns are cut); per-locale treatment needs content/editorial decision.
- Whether the page may link to veg PDPs (CC-CNT-002 prohibits only non-veg PDP links) is unspecified; no product links are asserted in acceptance criteria either way.
