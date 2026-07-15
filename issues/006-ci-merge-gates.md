# 006 · CI merge gates: tests, coverage, lint, SAST/SCA/secret-scan/raw-HTML-sink gate

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-QA-001, CC-QA-002 (with CC-SEC-009 pointers into SECURITY.md, Deployment rule 7)
- **Title**: CI merge gates: tests, coverage, lint, SAST/SCA/secret-scan/raw-HTML-sink gate
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every merge MUST be blocked unless all tests are green, line coverage is ≥ 80% per package (not met via assertion-free tests), lint is clean, mandatory human code review is recorded, and the security gates pass — SAST clean of high/critical findings, SCA clean of criticals, secret scan clean, and zero raw-HTML sinks (`bypassSecurityTrust*`, unsanitized `[innerHTML]`/`outerHTML`) in Angular code — with AI-generated code passing the identical gates plus mandatory human review and no auto-merge.
- **Rationale**: CC-QA-001/002 [P1] define the quality gates; SECURITY.md Deployment rule 7 makes the security gates mandatory and blocking and composes them with the QA gates. SECURITY.md's preamble makes these rules govern all code in the repository including AI-generated code, and Input validation rule 5 mandates the CI grep gate for raw-HTML sinks (CC-SEC-002). CC-SEC-009 routes the dependency policy's enforcement (SCA and secret scanning blocking on criticals) through these same gates. CLAUDE.md requires that once code exists, every change passes these merge gates.
- **Design**: N/A (non-UI).

## Scope
- **Applies To**: Both
- **Components**: CI pipeline definition running on every merge request: build; .NET and Angular test execution; per-package line-coverage enforcement (≥ 80%); assertion-free-test guard; lint (server and client); SAST; SCA; secret scan; raw-HTML-sink grep gate; required-human-review branch protection; no-auto-merge enforcement. Excluded (composed later, per their own issues): the market-gating matrix (027), money-path/mutation suite (099), authz cross-tenant suite (062), i18n CI (064/065), traceability report (007), IaC scanning/policy-as-code (009), DAST cadence (100), image scanning/signing (011/012), accessibility gates (098).
- **Actors**: Engineers submitting merge requests; AI coding agents (identical gates); CI system; human reviewers
- **Data Classification**: Internal (pipeline configuration; secret-scan findings are Confidential)

## Security Context
- **Defense Layer**: Architecture (a blocking control plane over all code entering the default branch)
- **Threat(s) Addressed**: Introduction of vulnerable code (SAST: injection, XSS classes — OWASP Top Ten A03:2021); vulnerable or malicious dependencies (SCA; CC-SEC-009); committed credentials (secret scan; SECURITY.md, Secret handling rule 1); DOM XSS via raw-HTML sinks (Input validation rule 5, CC-SEC-002); unreviewed AI-generated code reaching production (Deployment rule 7)
- **Trust Boundary**: The merge boundary into the default branch — all code, human- or AI-authored, is untrusted until it passes the gates plus human review
- **Zero Trust Consideration**: No code path is exempt: gates are blocking (no warn-only), apply to every change including AI-generated code, and cannot be satisfied by assertion-free coverage padding (CC-QA-001).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V15 Secure Coding and Architecture (secure development pipeline; dependency management), under the platform-wide Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SA-11 (developer testing and evaluation), RA-5 (vulnerability monitoring and scanning), CM-3 (configuration change control — mandatory review)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a merge request with any failing test, when the pipeline runs, then the merge is blocked (CC-QA-002).
2. **AC-02**: Given a package whose line coverage is below 80%, when the pipeline runs, then the merge is blocked; and given a test file containing tests with no assertions used to inflate coverage, when the assertion-free-test guard runs, then the merge is blocked (CC-QA-001).
3. **AC-03**: Given lint violations in server (C#) or client (Angular/TypeScript) code, when the pipeline runs, then the merge is blocked (CC-QA-002).
4. **AC-04**: Given a SAST finding of high or critical severity, an SCA finding of critical severity, or any secret-scan hit, when the pipeline runs, then the merge is blocked (SECURITY.md, Deployment rule 7; CC-SEC-009).
5. **AC-05**: Given Angular code containing `bypassSecurityTrust*` or an unsanitized `[innerHTML]`/`outerHTML` binding, when the raw-HTML-sink grep gate runs, then the merge is blocked (SECURITY.md, Input validation rule 5; CC-SEC-002) (negative case: such code MUST NOT reach the default branch).
6. **AC-06**: Given a merge request with all automated gates green but no human review approval, when merge is attempted, then it is refused — including for AI-generated changes, which MUST NOT auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).
7. **AC-07**: Given any attempt to bypass gates (e.g., a pipeline change marking a gate non-blocking), when the change is reviewed, then gate configuration itself is subject to the same mandatory review, and gates are enforced as required checks that cannot be skipped by the submitter (fail-closed posture, SECURITY.md, Logging rule 2 by analogy; Deployment rule 7 "mandatory and blocking").
8. **AC-08**: Given a new third-party dependency in a merge request, when SCA runs, then known unpatched critical CVEs block the merge, and the PR description carries the justification required by SECURITY.md Dependency Rules 2 (verified in human review).

## Failure Behavior
- **On Invalid Input**: N/A in the HTTP sense; a malformed pipeline configuration fails the pipeline itself rather than skipping gates.
- **On System Error**: Fail closed — if any gate tool errors, times out, or cannot produce a result, the merge is blocked, never waved through (SECURITY.md, Deployment rule 7: mandatory and blocking; Logging rule 2 posture).
- **Alerting**: Gate failures surface on the merge request. Secret-scan hits are treated as security events: logged to centralized monitoring and alerting per SECURITY.md, Logging rule 3 (a committed credential requires rotation via Key Vault, Secret handling rule 1 — handled operationally).

## Test Strategy
- **Unit Tests**: For any first-party gate scripts (e.g., the raw-HTML-sink grep, assertion-free-test guard): fixture repositories/files that must pass and must fail, ≥ 80% coverage on the script code (CC-QA-001).
- **Integration Tests**: Pipeline self-test on fixture branches: one violating each gate (failing test, <80% coverage, assertion-free padding, lint error, seeded SAST/SCA/secret/sink findings) asserting each is blocked; one clean branch asserting merge is allowed only with review.
- **Security Tests**: The gates are themselves the security tests; seeded-finding fixtures prove SAST/SCA/secret-scan/sink-grep detection remains live (guarding against silently disabled scanners).
- **Compliance Tests**: Gate results retained per merge as evidence that CC-QA-001/002 and Deployment rule 7 are enforced; branch-protection configuration validated automatically.
- **Coverage Target**: ≥ 80% for first-party gate code (CC-QA-001); tests tagged CC-QA-001, CC-QA-002, CC-SEC-002, CC-SEC-009 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (.NET build/tests to gate) and 004 "Angular workspace scaffold" (client build, sink-grep target). Runs before code-bearing issues land in anger.
- **Downstream**: Every subsequent issue merges through these gates; specifically composed with: 007 "Requirement traceability" (PR/test tagging checks), 009 "IaC policy-as-code and scanning", 011/012 (image scanning/signing), 027 "Market-gating CI test matrix", 062 "Object-level authorization test suite", 064/065 (i18n CI), 098 "Accessibility gates", 099 "Money-path and mutation-testing suite", 100 "DAST and penetration-test cadence" (SECURITY.md, Deployment rule 8 composes these per-merge suites).
- **External**: None named by the specs (no CI platform, SAST, SCA, or secret-scan vendor is specified — see Open Questions).

## Implementation Notes
- **Constraints**: Gates must cover both the .NET 10 modular monolith and the Angular workspace; the sink grep targets the Angular sources (SECURITY.md, Input validation rule 5); mutation testing SHOULD run on money, gating, and authz code (CC-QA-001) — wired here as an optional stage, made mandatory for those paths by issues 027/062/099; specify all gates platform-agnostically since no CI platform is named in the specs.
- **Anti-Patterns**: No warn-only/advisory mode for any gate (Deployment rule 7: mandatory and blocking); no auto-merge for anything, AI-generated code especially (CC-QA-002); no coverage padding with assertion-free tests (CC-QA-001); no committing secrets anywhere — source, config, or pipeline variables (SECURITY.md, Secret handling rule 1); no gate exemptions per author, path, or "hotfix" label.
- **AI Development Guidance**: This issue *implements* the AI-code governance: AI-generated code passes the identical gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Review requirements are enforced by branch protection, not convention.

## Open Questions
- **No CI platform is named anywhere in the specs.** ARCHITECTURE.md confirms Terraform + GitOps for delivery and says "CI enforces the CC-QA gates and the SECURITY.md deployment rules", but the CI system itself is undecided/unstated. The gates above are specified platform-agnostically; the platform choice needs a human decision before implementation.
- No SAST, SCA, secret-scan, or lint tools are named in the specs; tool selection is open (and each added tool/dependency falls under SECURITY.md Dependency Rules).
- CC-QA-001 measures "line coverage ... per package"; the precise package granularity for the Angular workspace (per app vs. per library) is not defined.
- "Mandatory human code review" (CC-QA-002) does not specify reviewer count or CODEOWNERS-style routing; left to the platform-configuration decision above.
