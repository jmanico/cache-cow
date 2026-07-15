# 007 · Requirement traceability: PR references, test tagging, coverage report

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: N/A — no CC-* ID of its own; implements REQUIREMENTS.md §17 (Traceability)
- **Title**: Requirement traceability: PR references, test tagging, coverage report
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Operational

## Requirement
- **Description**: Every implemented feature MUST reference the CC-* requirement IDs it implements in its pull request description, the test suite MUST tag tests with the requirement IDs they verify, and CI MUST generate a requirements-to-tests coverage report from those tags (REQUIREMENTS.md §17).
- **Rationale**: REQUIREMENTS.md §17 makes traceability a platform requirement: PR references, test tagging, and a generated coverage report of requirements-to-tests. It also defines the enforcement stance — unreferenced code paths discovered in review are treated as scope creep and removed or ratified in REQUIREMENTS.md first. CLAUDE.md's working rules repeat the citation duty (work items, PRs, and tests reference the CC-* requirements they implement or verify). Traceability is what lets the audit-heavy controls elsewhere (e.g., CC-QA-003/004/005 suites) prove which requirement each test verifies.
- **Design**: N/A (non-UI).

## Scope
- **Applies To**: Both
- **Components**: (a) A PR-description check requiring at least one valid CC-* requirement ID on feature PRs; (b) a test-tagging convention usable from .NET tests and Angular tests, backed by the requirement-tagged test utilities the shared kernel reserves (ARCHITECTURE.md, Dependency rule 9); (c) a CI-generated requirements-to-tests coverage report mapping every CC-* ID in REQUIREMENTS.md to the tagged tests that verify it, flagging IDs with zero tests and tags citing unknown IDs. Excluded: the merge-gate pipeline itself (issue 006, which this composes with); the content of any specific test suite (027, 062, 099, etc.).
- **Actors**: Engineers and AI coding agents authoring PRs/tests; human reviewers; CI system
- **Data Classification**: Internal (traceability metadata and reports)

## Security Context
- **Defense Layer**: Architecture (process control ensuring implemented behavior maps to ratified requirements)
- **Threat(s) Addressed**: Scope creep — unreferenced code paths entering the system without ratification (REQUIREMENTS.md §17); silent loss of verification for security-, gating-, and money-path requirements (the report exposes CC-SEC-*/CC-MKT-*/CC-PRC-* IDs with no verifying tests)
- **Trust Boundary**: The merge boundary — a change without requirement references is not accepted (composes with issue 006's gates)
- **Zero Trust Consideration**: Claims of requirement coverage are not taken on trust: the report is generated from machine-readable test tags, and IDs are validated against the canonical list in REQUIREMENTS.md rather than free-typed prose.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V15 Secure Coding and Architecture (traceable, verifiable development process), under the platform-wide Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SA-11 (developer testing and evaluation), CM-3 (change control tied to documented requirements)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a feature pull request whose description contains no valid CC-* requirement ID, when the traceability check runs, then the merge is blocked with a message pointing to REQUIREMENTS.md §17 (negative case: unreferenced feature work MUST NOT merge).
2. **AC-02**: Given a PR description citing a CC-* ID that does not exist in REQUIREMENTS.md, when the check runs, then the merge is blocked (IDs validate against the canonical document, not free text).
3. **AC-03**: Given the test-tagging convention, when a .NET test or an Angular test is tagged with one or more CC-* IDs, then the CI report generator discovers the tag and attributes that test to those requirements (REQUIREMENTS.md §17).
4. **AC-04**: Given a full CI run, when the requirements-to-tests report is generated, then it lists every CC-* ID from REQUIREMENTS.md with its verifying tests, and separately lists (a) requirement IDs with zero tagged tests and (b) tags referencing unknown IDs.
5. **AC-05**: Given a test tagged with a CC-* ID that is absent from REQUIREMENTS.md, when the report generates, then the run flags it as an error — tests cannot "verify" unratified requirements (REQUIREMENTS.md §17: scope is ratified in REQUIREMENTS.md first).
6. **AC-06**: Given the shared kernel, when test projects reference the requirement-tagging utilities, then they come from the shared kernel's requirement-tagged test utilities (ARCHITECTURE.md, Dependency rule 9) — no per-context duplicate tagging mechanisms.
7. **AC-07**: Given the report artifact, when a release is prepared, then the report is retained as a CI artifact so review can enforce the §17 scope-creep rule (unreferenced code paths removed or ratified first).

## Failure Behavior
- **On Invalid Input**: A malformed tag or unparseable PR reference fails the check with a clear message; nothing is skipped or best-effort-matched.
- **On System Error**: Fail closed — if the traceability check or report generation errors, the merge is blocked (consistent with SECURITY.md, Deployment rule 7's blocking-gate posture via issue 006).
- **Alerting**: Failures surface on the merge request; a report showing previously-covered requirement IDs dropping to zero tests SHOULD be surfaced in review (mechanism per CI-platform decision — see Open Questions).

## Test Strategy
- **Unit Tests**: For the checker/report generator: PR-description parsing (valid ID, missing ID, unknown ID, multiple IDs); tag discovery in .NET and Angular test fixtures; report completeness against a fixture requirements list; ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: End-to-end CI run on fixture branches: a PR without references is blocked; a tagged test suite produces a report attributing tests to CC-* IDs; an unknown-ID tag fails the run.
- **Security Tests**: N/A beyond the standard merge gates applying to this tooling (SECURITY.md, Deployment rule 7); note the report is itself evidence infrastructure for security test suites (CC-QA-003/004/005).
- **Compliance Tests**: The generated requirements-to-tests report is the compliance artifact (REQUIREMENTS.md §17); CI asserts its presence per run.
- **Coverage Target**: ≥ 80% for the tooling per CC-QA-001; the tooling's own tests are tagged with the REQUIREMENTS.md §17 obligation they verify per the same convention.

## Dependencies
- **Upstream**: 001 "Solution scaffold" (shared kernel hosts the requirement-tagged test utilities, ARCHITECTURE.md Dependency rule 9); 006 "CI merge gates" (this check runs as one of the composed blocking gates).
- **Downstream**: Every test-bearing issue uses the tagging convention — notably 027 "Market-gating CI test matrix" (CC-QA-003), 062 "Object-level authorization test suite" (CC-QA-005), 099 "Money-path and mutation-testing suite" (CC-QA-004) — and all future PRs are subject to the reference check.
- **External**: None named by the specs (the CI platform is undecided — see Open Questions).

## Implementation Notes
- **Constraints**: Tagging must work across both stacks: .NET test frameworks support trait/category-style attributes and Angular tests need an equivalent convention the report generator can parse; the CC-* ID list is parsed from REQUIREMENTS.md as the single source of truth (ARCHITECTURE.md, Dependency rule 7's no-hand-maintained-parallel-definitions principle applies: do not keep a second, manually curated ID list); GitHub issues follow REQUIREMENT_TEMPLATE.md (CLAUDE.md), so PR references line up with issue metadata IDs.
- **Anti-Patterns**: MUST NOT maintain a parallel hand-edited requirements index that can drift from REQUIREMENTS.md; MUST NOT satisfy the check with decorative ID mentions on tests that assert nothing (composes with CC-QA-001's assertion-free-test guard in issue 006); MUST NOT let unreferenced code paths merge as "cleanup" — §17 says removed or ratified first.
- **AI Development Guidance**: AI-generated PRs carry the same CC-* reference obligation and pass the identical gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); requirement references give reviewers the spec anchor for judging AI-authored changes.

## Open Questions
- No CI platform is named in the specs (also recorded on issue 006); the PR-description check and report publication are specified platform-agnostically.
- REQUIREMENTS.md §17 does not define the report's format (markdown/HTML/JSON) or where it is published; only its existence and content are asserted here.
- The exact tagging syntax (e.g., .NET trait names, Angular test naming/annotation convention) is unspecified; the only spec constraint is that requirement-tagged test utilities live in the shared kernel (ARCHITECTURE.md, Dependency rule 9).
- Whether *every* PR (including pure refactors or doc changes) must cite a CC-* ID, or only "implemented feature" PRs (§17's wording), is ambiguous; AC-01 targets feature PRs and the boundary needs a human call.
