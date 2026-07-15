# 075 · Meet our Cuts interactive diagram with accessible fallback

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-003, CC-MKT-005
- **Title**: Meet our Cuts interactive diagram with accessible fallback
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The storefront MUST provide a "Meet our Cuts" interactive butcher-diagram experience that filters the menu by cut region and has an accessible list-based equivalent, and the experience MUST NOT render, link, or be reachable in the IN market.
- **Rationale**: CC-CNT-003 defines the feature (interactive cut diagram filtering the menu, with an accessible list-based equivalent) and CC-MKT-005 [P1] mandates its complete exclusion from the IN market — India is a fully vegetarian market where the cow is culturally revered (DESIGN.md §8.1), so butchery content must be unreachable there. DESIGN.md §8.1's separation rule additionally requires that butchery content and herd-mascot content never appear in the same view in any market, and DESIGN.md §7 requires the cut-card visual language to share zero DNA with the cow-mascot illustrations. DESIGN.md §13 requires full keyboard operability, each diagram region exposed as a named button, and a logical tab order with the list fallback.
- **Design**: DESIGN.md §7 (Cut card: line-art side-profile steer diagram, numbered cut regions, Char linework on Paper, Plex Mono numbering; deliberately technical — a diagram, not a character); §8.1 (separation rule; IN exclusion context); §10 (page inventory); §13 (accessibility: ARIA-labeled diagram, each region a named button, list fallback, visible focus, `prefers-reduced-motion`); §15 (butcher-diagram base art asset).

## Scope
- **Applies To**: Web App
- **Components**: Storefront Angular SSR page, interactive diagram component, list-based equivalent component, and menu-filter integration (the filtered menu itself is issue 066); market gating consumed from the Market & Gating Policy service (ARCHITECTURE.md, bounded context 1). Explicitly excluded: gating-policy model and enforcement API (issues 023/025), 404 semantics implementation for gated routes (issue 026), the menu page and its filter machinery (issue 066), the gating CI matrix (issue 027), cache-key enforcement machinery (issue 028), the Cows page side of the separation rule (issue 074).
- **Actors**: Consumer storefront visitors in US, ES, MX, DE, JP markets; visitors in the IN market (must never reach it); assistive-technology users
- **Data Classification**: Public (catalog-derived editorial content)

## Security Context
- **Defense Layer**: Architecture (server-side market gating of an entire experience)
- **Threat(s) Addressed**: Compliance/gating failure — butchery content rendered, linked, or reachable in the IN market (CC-MKT-005); gating bypass via cached responses served across markets (CC-MKT-009, CC-SEC-013); resource-existence disclosure in the gated market (404-not-403 hardening, SECURITY.md, Authentication rule 9)
- **Trust Boundary**: Server-side rendering and routing boundary — the exclusion is enforced by the Market & Gating Policy service in every server response (HTML, navigation, sitemap, SSR transfer state), never by client-side hiding (ARCHITECTURE.md, Dependency rule 1)
- **Zero Trust Consideration**: The gating decision keys exclusively off server-side transacting-market state, never geolocation, `Accept-Language`, or client-supplied hints (CC-SEC-012; SECURITY.md, Authentication rule 10); client-side hiding is non-compliant (per the CC-MKT-003 principle applied to this gated experience).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (server-side enforcement, deny by default for the gated market); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement — server-side market gating)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A (the exclusion is a market-compliance rule authored in REQUIREMENTS.md CC-MKT-005)
- **Other**: WCAG 2.2 AA (CC-NFR-004; DESIGN.md §13)

## Acceptance Criteria
1. **AC-01**: Given the Cuts page in a non-IN market, when a user activates a diagram cut region, then the menu is filtered to that cut (CC-CNT-003; DESIGN.md §7), and the same filtering is reachable through the list-based equivalent.
2. **AC-02**: Given the diagram, when it is inspected with assistive technology, then each cut region is exposed as a named button with an ARIA label, the page is fully keyboard operable with visible focus and a logical tab order through the interactive with the list fallback (DESIGN.md §13; CC-NFR-004).
3. **AC-03**: Given a session whose transacting market is IN, when any storefront surface renders (navigation, pages, sitemap, SSR transfer/hydration state), then no link or reference to the Cuts experience appears anywhere (CC-MKT-005; CC-MKT-009). (Negative case.)
4. **AC-04**: Given a direct request for the Cuts URL with transacting market IN, when the server responds, then it returns HTTP 404 — not 403 and not a redirect to another market (CC-MKT-005; 404 semantics per issue 026 and SECURITY.md, Authentication rule 9). (Negative case.)
5. **AC-05**: Given the gating decision, when its implementation is reviewed, then the IN exclusion is read from per-market policy configuration data via the Market & Gating Policy service (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1), and no client-side hiding is the enforcement mechanism. (Negative case: CSS/JS hiding of Cuts content in IN is non-compliant.)
6. **AC-06**: Given any cacheable rendering of the Cuts page or of navigation containing its link, when cached at SSR/edge/CDN, then the cache key derives from server-side transacting market + locale so a non-IN response is never served into IN (CC-MKT-009; SECURITY.md, HTTP boundary rule 10). (Negative case.)
7. **AC-07**: Given the rendered Cuts page in any market, when its content is inspected, then it contains no cow-mascot illustrations and no herd-mascot content (DESIGN.md §7, §8.1 separation rule), and the diagram uses the technical line-art language (Char linework on Paper, Plex Mono numbering). (Negative case.)
8. **AC-08**: Given a user with `prefers-reduced-motion`, when the page renders, then animated reveals are disabled and content renders in its final state (DESIGN.md §13).

## Failure Behavior
- **On Invalid Input**: N/A for visitor input beyond route/filter parameters; an unknown cut-region identifier in the filter route is rejected with 404/400 per issue 021's RFC 9457 handling, with no partial filtering.
- **On System Error**: Fail closed — any exception in the gating path is a denial: if the market policy cannot be resolved, the Cuts route returns 404/error rather than rendering (SECURITY.md, Logging rule 2). The experience MUST NOT render in IN under any failure mode.
- **Alerting**: Gating-path exceptions log as structured security events (SECURITY.md, Logging rule 3); the production IN-market synthetic gating probe (CC-NFR-003, issue 096) covers IN unreachability of gated content.

## Test Strategy
- **Unit Tests**: Angular component tests: region activation triggers the correct filter; ARIA names and roles on every region; list equivalent parity with the diagram's regions; reduced-motion behavior.
- **Integration Tests**: SSR integration tests per market: non-IN markets render and filter; IN receives 404 on direct request and zero references in navigation, sitemap, and transfer state (AC-03/04).
- **Security Tests**: Gating tests asserting the decision keys off server-side market state and ignores `Accept-Language`/geolocation headers (CC-SEC-012); cache-key assertions for this route per issue 028's patterns.
- **Compliance Tests**: The market-gating CI matrix (issue 027, CC-QA-003) includes the Cuts route × every market; automated WCAG 2.2 AA checks plus manual audit inclusion (CC-NFR-004, issue 098).
- **Coverage Target**: ≥ 80% (CC-QA-001); tests tagged CC-CNT-003, CC-MKT-005 (and CC-MKT-009, CC-SEC-012 where they verify those) per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 023 "Market & Gating Policy: policy-as-data model" and 025 "Server-side gating enforcement API" (the IN exclusion rule); 026 "404 semantics for market-gated resources"; 024 "Transacting market/locale resolution"; 066 "Menu page: product cards, cache-status badges, filters" (the menu the diagram filters); 063 "Storefront SSR shell"; 005 "Design token pipeline"; butcher-diagram base art (DESIGN.md §15 asset).
- **Downstream**: 027 "Market-gating CI test matrix" (asserts this route per market); 028 "Cache-safe gating" (covers this route); 071 "SEO surfaces" (must exclude the Cuts URL from IN sitemaps); 096 "Per-market synthetic probes" (production IN gating probe).
- **External**: None.

## Implementation Notes
- **Constraints**: Enforcement is server-side in the Angular SSR response via the Market & Gating Policy service (ARCHITECTURE.md, Dependency rule 1 — clients never gate); the filter integration reuses issue 066's menu filter model (cut/category per CC-CAT-001) rather than a parallel taxonomy; visual language is strictly the technical diagram style — zero shared DNA with mascot illustrations (DESIGN.md §7); focus outline 2px Ember on light surfaces (DESIGN.md §13).
- **Anti-Patterns**: Client-side hiding as the gating mechanism (non-compliant per the CC-MKT-003 principle; gating is server-side); 403 or cross-market redirect instead of 404 in IN (CC-MKT-005, CC-MKT-004 semantics); market conditionals scattered in UI code instead of policy data (CC-MKT-006); mascot illustrations anywhere on this page (DESIGN.md §8.1); a mouse-only diagram without the named-button ARIA surface and list equivalent (DESIGN.md §13).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The cut-region taxonomy (which numbered regions the diagram exposes and how each maps to the catalog's cut/category field, CC-CAT-001) is not defined in the specs; the mapping needs a content/catalog decision.
- Behavior of the diagram with respect to vegetarian SKUs (whether the vegetarian filter, CC-CAT-006, interacts with cut filtering, and what a cut region shows when only veg SKUs match) is unspecified.
- Whether the Cuts experience is a standalone page, a menu-page mode, or both is not fixed by the specs (DESIGN.md §10 lists it as a page; CC-CNT-003 says it filters the menu); routing shape is left to implementation review.
