# 098 · Accessibility gates (WCAG 2.2 AA)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-NFR-004
- **Title**: Accessibility gates (WCAG 2.2 AA)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Compliance

## Requirement
- **Description**: All surfaces — storefront, wholesale portal, and internal dashboard — MUST meet WCAG 2.2 AA per DESIGN.md §13, enforced by automated accessibility checks in CI plus a manual audit per release train (REQUIREMENTS.md CC-NFR-004).
- **Rationale**: CC-NFR-004 sets WCAG 2.2 AA as the floor, "enforced by automated checks plus manual audit per release train", and DESIGN.md §13 makes the floor explicit across all surfaces "including the dashboard". DESIGN.md binds specific mechanisms: verified contrast pairs with CI contrast checks on token combinations (§3.2, §13); status never conveyed by color alone — every badge carries text and the veg mark is shape plus label (§5.2, §3.3, §13); full keyboard operability with visible focus (2px Ember outline on light, Cache on Pit); `prefers-reduced-motion` disabling arc-fill animation and reveals with content rendered in final state; correct language/direction per locale (CC-I18N-004); ARIA labels on icons and the cuts diagram with each region exposed as a named button.
- **Design**: DESIGN.md §13 (accessibility rules), §3.2 (verified contrast pairs), §3.3 (veg marking as shape plus label), §5.2 (badges always paired with a plain-language line).

## Scope
- **Applies To**: Web App
- **Components**: All three Angular clients (storefront SSR, wholesale portal, internal dashboard — ARCHITECTURE.md, "Clients"); CI accessibility-check jobs in the merge pipeline (issue 006); token contrast-check integration (issue 005); the manual-audit process artifact per release train.
- **Actors**: All end users including assistive-technology users (consumers, wholesale buyers, staff); CI pipeline; auditors performing the manual per-release-train audit.
- **Data Classification**: Public (accessibility properties of rendered UI; no data handling).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: N/A as a direct attack surface — this is a compliance/quality gate. Indirectly, the accessible list-based fallback for the cuts diagram (CC-CNT-003) keeps market-gated content reachable through one gated path rather than a divergent second implementation.
- **Trust Boundary**: N/A (no trust boundary crossed; CI-time and audit-time verification of rendered UI).
- **Zero Trust Consideration**: N/A for input trust. The gate itself follows verify-don't-trust: accessibility is proven by automated checks and manual audit each release train, never assumed from component-library claims.

## Standards Alignment
- **OWASP ASVS**: N/A (accessibility is outside ASVS scope; the CI gates themselves compose with the merge gates of SECURITY.md, Deployment rule 7).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A (no well-known control maps directly; not fabricating one per drafting rules).
- **NIST SP 800-207**: N/A
- **Regulatory**: WCAG 2.2 AA conformance target (REQUIREMENTS.md CC-NFR-004; DESIGN.md §13).
- **Other**: WCAG 2.2 Level AA (W3C Recommendation).

## Acceptance Criteria
1. **AC-01**: Given a pull request touching any of the three Angular surfaces, when CI runs, then automated WCAG 2.2 AA checks execute against the affected surface's templates/pages and a violation at AA level fails the merge gate (CC-NFR-004; CC-QA-002 merge-gate composition).
2. **AC-02**: Given the design-token pipeline's contrast checks (issue 005), when any token combination used for text or status fails its required contrast ratio per DESIGN.md §3.2, then CI fails — this issue consumes that gate and MUST NOT ship any surface using an unverified pair (DESIGN.md §3.2, §13).
3. **AC-03**: Given any status indicator — cache-status stock badges, veg marks, order-tracker stages — when rendered on any surface, then status is conveyed by text (badge plus plain-language line, shape plus label), and automated checks assert the accompanying text exists; status MUST NOT be conveyed by color alone (DESIGN.md §5.2, §3.3, §13; CC-NFR-004).
4. **AC-04**: Given keyboard-only operation, when a user tabs through any page on any surface, then all interactive elements are operable with a visible focus indicator (2px Ember outline on light surfaces, Cache on Pit) in a logical tab order, verified by automated checks plus the manual audit (DESIGN.md §13).
5. **AC-05**: Given a user agent with `prefers-reduced-motion`, when pages with arc-fill animation or reveal effects render, then all such animation is disabled and content renders in its final state (DESIGN.md §13).
6. **AC-06**: Given a release train, when it concludes, then a manual WCAG 2.2 AA audit covering all three surfaces (dashboard included) has been performed and its findings recorded; a release train without a recorded audit does not satisfy CC-NFR-004.
7. **AC-07** (negative): Given the internal dashboard, when accessibility gates run, then the dashboard MUST NOT be exempted — the AA floor applies to it explicitly (DESIGN.md §13; CC-NFR-004).

## Failure Behavior
- **On Invalid Input**: N/A (no runtime input; gate failures are CI results).
- **On System Error**: Fail closed at the gate: if the automated accessibility check job errors or cannot produce results, the merge gate fails rather than passing by default (consistent with the mandatory-blocking gate model of SECURITY.md, Deployment rule 7).
- **Alerting**: CI gate failure blocks merge and is visible on the PR; no runtime alerting applies. Manual-audit findings are tracked as issues against the release train.

## Test Strategy
- **Unit Tests**: Angular component tests asserting accessibility contracts per component: ARIA labels present on icons and cuts-diagram regions (each region a named button), badge text accompanying status colors, focus-visible styles applied. Tagged CC-NFR-004 (REQUIREMENTS.md §17).
- **Integration Tests**: Automated WCAG 2.2 AA scans over the top rendered templates of each surface in CI, including SSR-rendered storefront output; `prefers-reduced-motion` emulation test asserting final-state rendering; keyboard-navigation test through the butcher-diagram interactive and its list fallback (DESIGN.md §13; CC-CNT-003).
- **Security Tests**: N/A beyond the standard merge gates (SECURITY.md, Deployment rule 7) that these accessibility gates compose with.
- **Compliance Tests**: CI evidence retained per run (automated check reports); manual-audit report per release train collected as audit evidence (CC-NFR-004).
- **Coverage Target**: ≥ 80% line coverage for any first-party gate/check code (CC-QA-001); automated a11y scans cover at minimum the top 20 templates already under locale visual regression (CC-I18N-005 alignment).

## Dependencies
- **Upstream**: 004 Angular workspace scaffold (surfaces to check); 005 Design token pipeline (CI contrast checks on token combinations — coordinate, do not duplicate); 006 CI merge gates (gate composition).
- **Downstream**: All UI issues are governed by this gate, notably 063 Storefront SSR shell, 066–070 (menu, PDP, cart, checkout, order tracking), 075 Meet our Cuts interactive diagram with accessible fallback, 079 Dashboard shell.
- **External**: None.

## Implementation Notes
- **Constraints**: Angular builds with strict TypeScript and strictTemplates (SECURITY.md, Deployment rule 9) — leverage template-level static checks where possible; checks must run against SSR output for the storefront since that is what users and crawlers receive (ARCHITECTURE.md, "Clients"); `lang`/`hreflang` correctness per locale is owned by issue 063 (CC-I18N-004) but is asserted by these gates as part of AA conformance; RTL is out of scope for v1 but MUST NOT be precluded (REQUIREMENTS.md §16; DESIGN.md §13 — Devanagari is LTR).
- **Anti-Patterns**: MUST NOT convey status by color alone (DESIGN.md §13); MUST NOT suppress or allowlist-away AA violations to make CI pass; MUST NOT exempt the dashboard from the AA floor (DESIGN.md §13); MUST NOT rely on the automated checks alone — the manual audit per release train is a distinct mandatory activity (CC-NFR-004); MUST NOT hardcode brand colors bypassing the token pipeline and its contrast verification (ARCHITECTURE.md, Dependency rule 8).
- **AI Development Guidance**: AI-generated components pass the identical merge gates — including these accessibility gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- No automated accessibility-check tool is named in the specs; the tool choice (and whether it runs against live rendered pages, Storybook-style component harnesses, or both) is an implementation decision to be ratified.
- "Release train" cadence is not defined anywhere in the canonical docs, so the required frequency of the manual audit is not quantifiable yet (CC-NFR-004 ties the audit to it).
- The manual audit's required scope depth (full-page inventory vs. representative flows, assistive-technology matrix such as screen-reader/browser pairs) is unspecified.
