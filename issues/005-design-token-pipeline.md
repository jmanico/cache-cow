# 005 · Design token pipeline: tokens.json from DESIGN.md with CI contrast checks

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-NFR-004 (contrast-check aspect); primary sources DESIGN.md §§3–4, §13, §15 and ARCHITECTURE.md, Dependency rule 8
- **Title**: Design token pipeline: tokens.json from DESIGN.md with CI contrast checks
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Operational

## Requirement
- **Description**: The repository MUST generate a `tokens.json` design-token file from the color and typography definitions in DESIGN.md §§3–4, consumed by all three Angular clients as the sole source of brand colors, type, and status vocabulary, with automated CI contrast checks verifying token combinations against the DESIGN.md §3.2 pairs and WCAG 2.2 AA.
- **Rationale**: DESIGN.md §15 names `tokens.json` generated from sections 3 and 4 for design-tool and code consumption. ARCHITECTURE.md Dependency rule 8 makes tokens flow from DESIGN.md outward: all three clients consume the generated `tokens.json`; no client hardcodes brand colors, type, or status vocabulary. DESIGN.md §3.2 requires verifying every new color pair at implementation with automated contrast checks in CI, and §13 mandates CI contrast checks on token combinations under the WCAG 2.2 AA floor enforced by CC-NFR-004.
- **Design**: DESIGN.md §3.1 core color tokens (`color.char.900` `#221812` through `color.pitpaper.100` `#E9DED4`); §3.2 verified contrast pairs (Char on Paper 15.8:1, Char on Butcher 10.4:1, Ember on Paper 3.7:1 large-text-only, `ember.700` 5.9:1 on Paper for colored body-size text, Cache green on Pit 6.7:1); §4 typography roles, per-script stacks, modular scale (1.250 from 17px: 17/21/27/34/42/53/66) and spacing/breakpoints from §6 as applicable to tokens; §5.2 cache-status vocabulary colors.

## Scope
- **Applies To**: Web App
- **Components**: Token source-of-truth file(s) and generation step producing `tokens.json`; consumption wiring in storefront, portal, and dashboard (via the stub from issue 004); CI contrast-check job over token combinations. Excluded: any styled pages/components (issues 052, 063+, 079); the full accessibility gate suite (issue 098); font hosting/subsetting beyond token references (CC-NFR-005, handled with client builds).
- **Actors**: Frontend engineers; designers (design-tool consumption per DESIGN.md §15); CI system
- **Data Classification**: Public (brand token values)

## Security Context
- **Defense Layer**: Architecture (single source of truth; no scattered hardcoded values)
- **Threat(s) Addressed**: Not threat-driven; the pipeline's CI gate prevents drift that would silently break the verified contrast pairs (DESIGN.md §3.2) and the accessibility floor (CC-NFR-004). Token files are first-party build inputs, not user input.
- **Trust Boundary**: N/A (build-time asset pipeline; no runtime trust boundary)
- **Zero Trust Consideration**: N/A beyond CI validating the token file's schema and contrast invariants before any client build consumes it.

## Standards Alignment
- **OWASP ASVS**: N/A (no security control; platform baseline unaffected)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA (DESIGN.md §13; CC-NFR-004 — contrast checks on token combinations)

## Acceptance Criteria
1. **AC-01**: Given the token source, when the generation step runs, then it emits `tokens.json` containing all ten DESIGN.md §3.1 core color tokens with their exact hex values, the §4 typography roles/stacks/scale, and the §5.2 status vocabulary mappings (CACHE HIT → `cache.500`, WARMING → `ember.500`, CACHE MISS → `smoke.400`).
2. **AC-02**: Given `tokens.json`, when each of the three Angular clients builds, then brand colors, type, and status vocabulary resolve only from the generated tokens (ARCHITECTURE.md, Dependency rule 8; DESIGN.md §15).
3. **AC-03**: Given the CI contrast check, when it runs against the declared token usage pairs, then it verifies the DESIGN.md §3.2 pairs at their required ratios (AA thresholds; Ember on Paper permitted for large text/graphics only, `ember.700` required for colored body-size text on Paper) and fails the build if any pair regresses (DESIGN.md §3.2, §13; CC-NFR-004).
4. **AC-04**: Given a token change that breaks a verified contrast pair (e.g., lightening `color.char.900` until Char on Paper drops below AA), when CI runs, then the merge is blocked (negative case; DESIGN.md §13).
5. **AC-05**: Given a client source tree, when a hardcoded brand hex value, font family, or status label duplicating a token is introduced, then a CI check fails the build — no client hardcodes brand values (ARCHITECTURE.md, Dependency rule 8) (negative case).
6. **AC-06**: Given `tokens.json`, when validated in CI, then it conforms to a published schema (single source of truth for validation, consistent with ARCHITECTURE.md, Dependency rule 7's schema discipline), and malformed token files fail the build.

## Failure Behavior
- **On Invalid Input**: A malformed or schema-invalid token source fails generation with a clear build error; no partial `tokens.json` is emitted.
- **On System Error**: Fail closed at build time — if generation or the contrast check cannot run, the merge gate blocks (SECURITY.md, Deployment rule 7 gate posture); clients never build against a stale or unvalidated token file.
- **Alerting**: CI failure on the merge request; no runtime alerting applies.

## Test Strategy
- **Unit Tests**: Generator tests: token completeness against DESIGN.md §3.1/§4 values, deterministic output, schema conformance; contrast-computation tests against the §3.2 published ratios as fixtures.
- **Integration Tests**: Each Angular client builds successfully consuming `tokens.json`; a build with a deliberately broken token file fails.
- **Security Tests**: N/A beyond standard merge gates (SECURITY.md, Deployment rule 7) applying to the pipeline code itself.
- **Compliance Tests**: CI contrast-check output retained as automated evidence for the WCAG 2.2 AA checks (CC-NFR-004; DESIGN.md §13).
- **Coverage Target**: ≥ 80% for the generator/check code per CC-QA-001; tests tagged CC-NFR-004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 004 "Angular workspace scaffold" (clients exist with token-consumption wiring). DESIGN.md §§3–4 values are the content source.
- **Downstream**: 052 "Wholesale portal UI"; 063 "Storefront SSR shell"; 066 "Menu page"; 067 "Product detail page"; 079 "Dashboard shell (Pit theme)"; 098 "Accessibility gates (WCAG 2.2 AA)".
- **External**: None (fonts named in DESIGN.md §15 are all open-license and self-hosted per SECURITY.md, Deployment rule 10 / CC-NFR-005).

## Implementation Notes
- **Constraints**: Token values are authored by DESIGN.md §§3–4 — the pipeline transcribes, it does not restate or fork them (CLAUDE.md: rules live in the owning canonical document); output must serve both design tools and code (DESIGN.md §15); all three clients consume the same generated artifact (ARCHITECTURE.md, Dependency rule 8); any new dependency for generation/contrast math follows SECURITY.md Dependency Rules (prefer zero new dependencies; a contrast-ratio computation is a few lines of first-party code, Dependency rule 1).
- **Anti-Patterns**: MUST NOT hardcode brand colors, type, or status vocabulary in any client (ARCHITECTURE.md, Dependency rule 8); MUST NOT recolor the arc motif or introduce unapproved palette values (DESIGN.md §2.3, §3 — e.g., no blue in the consumer brand); MUST NOT use Ember (`ember.500`) for body-size text on Paper — `ember.700` is the compliant pair (DESIGN.md §3.2); MUST NOT convey status by color alone — the §5.2 vocabulary pairs badges with plain text (DESIGN.md §13), so status tokens carry label text, not just color.
- **AI Development Guidance**: AI-generated pipeline code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs name no token-pipeline tooling and no `tokens.json` schema/format (e.g., W3C design-tokens draft vs. custom); the schema is left to implementation and published per ARCHITECTURE.md Dependency rule 7's single-source discipline.
- The exact set of token *combinations* CI must check is open-ended: DESIGN.md §3.2 lists verified pairs and requires verifying "every new pair at implementation", but the mechanism for declaring which pairs are in use (an explicit pairs manifest vs. static analysis of usage) is unspecified.
- Whether spacing/grid/breakpoint values from DESIGN.md §6 belong in `tokens.json` is unspecified — §15 says the file is generated from sections 3 and 4 only; §6 values are excluded from acceptance criteria accordingly.
- The mechanism for AC-05's "no hardcoded brand values" check (lint rule vs. grep gate) is not specified in the specs.
