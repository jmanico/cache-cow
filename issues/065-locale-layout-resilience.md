# 065 · Locale layout resilience: expansion budget, pseudo-localization, visual regression

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-I18N-005, CC-QA-006
- **Title**: Locale layout resilience: expansion budget, pseudo-localization, visual regression
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Storefront layouts MUST accommodate 130 percent of English string length without truncation or overflow, verified by a pseudo-localization build and automated locale visual regression across the top 20 templates running in CI.
- **Rationale**: CC-I18N-005 sets the 130 percent expansion budget with automated visual regression across locales for the top 20 templates; CC-QA-006 places the pseudo-localization build and locale visual regression in the i18n CI gates. DESIGN.md §4.2 supplies the empirical basis: Spanish runs roughly 20–25 percent longer than English and German compounds run wider still. §4.2 also fixes the per-script rendering rules layouts must survive: de-DE needs `hyphens: auto` with a correct `lang` attribute (German compounds otherwise break layouts) and avoids all-caps nav labels; ja-JP uses Noto Sans JP with body line-height 1.75, the weight-900-plus-Ember-slab-bar display treatment, and no italics; hi-IN uses Noto Sans Devanagari with line-height 1.7 minimum for matra clearance and the same slab-bar display treatment.
- **Design**: DESIGN.md §4.2 (per-script stacks and the expansion evidence), §4.3 (modular scale the layouts implement), §6 (grid, spacing, breakpoints the regression baselines capture), §13 (content must remain readable and operable — truncation that hides labels is also an accessibility defect).

## Scope
- **Applies To**: Web App
- **Components**: Pseudo-localization build target on the issue 064 resource pipeline (length-expanded, marker-wrapped strings); automated visual regression suite across the top 20 storefront templates in the launch locales; layout conformance for expansion (CSS rules, hyphenation, per-script line-heights) in the shared shell and shared components. Explicitly excluded: the resource pipeline itself and its schema/parity validation (issue 064), the template implementations being tested (issues 063, 066–078), and accessibility gates beyond overflow/truncation (issue 098).
- **Actors**: CI system; frontend engineers; indirectly every consumer in the seven launch locales.
- **Data Classification**: Public (UI layout only).

## Security Context
- **Defense Layer**: N/A (quality gate, not a security control); pseudo-localized strings still flow through the untrusted-input pipeline of issue 064 (SECURITY.md, Input validation rule 7)
- **Threat(s) Addressed**: N/A directly. Indirect: truncated legal, allergen, or price-context text is a compliance hazard (CC-CNT-005, CC-CAT-004, CC-PRC-002 all depend on text rendering completely).
- **Trust Boundary**: N/A
- **Zero Trust Consideration**: Pseudo-localization resources are generated, not hand-trusted; they pass the same schema validation as real translations (SECURITY.md, Input validation rule 1).

## Standards Alignment
- **OWASP ASVS**: No specific control; the platform-wide ASVS 5.0 Level 2 baseline applies (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA floor (DESIGN.md §13, CC-NFR-004) — truncation/overflow failures frequently double as WCAG failures

## Acceptance Criteria
1. **AC-01**: Given the pseudo-localization build, when any top-20 template renders with strings expanded to 130 percent of English length, then no text is truncated and no container overflows (CC-I18N-005, CC-QA-006).
2. **AC-02**: Given the automated visual regression suite, when it runs in CI, then it covers the designated top 20 templates across the launch locales and fails the build on truncation, overflow, or unapproved layout diffs (CC-I18N-005: "automated visual regression across locales for the top 20 templates"; CC-QA-006).
3. **AC-03**: Given a de-DE render of any covered template, when long German compounds occur, then `hyphens: auto` is active under a correct `lang="de-DE"` attribute and navigation labels are not all-caps (DESIGN.md §4.2).
4. **AC-04**: Given a ja-JP render, when body text displays, then line-height is 1.75 with Noto Sans JP, no italics are used, and display headings use the weight-900 treatment with the 6px Ember slab underline bar (DESIGN.md §4.2).
5. **AC-05**: Given a hi-IN render, when Devanagari body text displays, then line-height is at least 1.7 (matra clearance) with Noto Sans Devanagari, and en-IN pages use the Latin stack (DESIGN.md §4.2).
6. **AC-06**: Given a change that introduces truncation or overflow at 130 percent expansion in a covered template, when CI runs, then the merge is blocked (negative: a layout regression MUST NOT reach main via a green build; CC-QA-006, CC-QA-002).
7. **AC-07**: Given the visual regression suite, when a diff is approved, then baseline updates are explicit and reviewed — a failing comparison MUST NOT auto-accept its own new baseline (CC-QA-002 mandatory human review).

## Failure Behavior
- **On Invalid Input**: N/A at runtime (CI-time gate). A pseudo-localization resource that fails issue 064's schema validation fails the build there first.
- **On System Error**: Fail closed at build time — a visual-regression job error (renderer crash, missing baseline) is a red build, never a skipped check (consistent with SECURITY.md, Deployment rule 7 blocking-gate posture).
- **Alerting**: CI failure on the merge request; no runtime alerting applies.

## Test Strategy
- **Unit Tests**: Pseudo-localization string generator (130 percent expansion factor, marker wrapping, placeholder preservation so issue 064's placeholder-consistency validation still passes).
- **Integration Tests**: Full pseudo-locale build of the storefront; per-locale render of the top 20 templates at the DESIGN.md §6 breakpoints (480/768/1024/1280) feeding the visual comparison.
- **Security Tests**: N/A beyond confirming pseudo-locale resources pass the issue 064 untrusted-input validation unchanged.
- **Compliance Tests**: CI artifacts (screenshots, diff reports) retained as evidence the CC-I18N-005 budget is enforced per merge (CC-QA-006).
- **Coverage Target**: ≥ 80% per package for the generator/tooling code (CC-QA-001); tests tagged CC-I18N-005, CC-QA-006 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 064 "ICU MessageFormat resource pipeline with CI validation" (pseudo-locale resources are generated into and validated by that pipeline); 063 "Storefront SSR shell" (shell templates are among the covered set); 005 "Design token pipeline" (type/line-height values under test); 006 "CI merge gates" (hosts the blocking jobs).
- **Downstream**: 066 "Menu page", 067 "Product detail page", 068 "Cart", 069 "Checkout UI", 070 "Order tracking UI" and other top-20 templates (each must stay within the budget this issue enforces); 098 "Accessibility gates (WCAG 2.2 AA)" (shares rendering infrastructure concerns).
- **External**: None (fonts self-hosted and subset per locale — CC-NFR-005; SECURITY.md, Deployment rule 10).

## Implementation Notes
- **Constraints**: Angular workspace with AOT/strictTemplates builds (SECURITY.md, Deployment rule 9); breakpoints and grid per DESIGN.md §6; per-script stacks and line-heights per DESIGN.md §4.2; the 130 percent budget applies to every component ("the empirical basis for the 130-percent expansion budget every component must meet", DESIGN.md §4.2).
- **Anti-Patterns**: MUST NOT fix overflow by truncating with ellipsis on labels, legal, allergen, or price text (defeats the requirement and DESIGN.md §13); MUST NOT hand-tune per-locale font sizes outside the token scale (DESIGN.md §4.3; Dependency rule 8); MUST NOT exempt a template from regression instead of fixing it; MUST NOT rely on all-caps German labels (DESIGN.md §4.2).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Visual-regression baseline updates always require human approval.

## Open Questions
- The specs do not enumerate which 20 templates are "the top 20" (CC-I18N-005); the covered set needs a human-ratified list (presumably drawn from DESIGN.md §10's page inventory) before the suite is authoritative.
- Whether visual regression must run across all seven locales or a representative per-script subset (Latin/ja/hi) per template is not specified; CC-I18N-005 says "across locales" without enumerating.
- The specs name no visual-regression or pseudo-localization tooling; SECURITY.md Dependency Rules govern any library choice.
