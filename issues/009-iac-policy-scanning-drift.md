# 009 · IaC policy-as-code, scanning, and drift detection

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-QA-002, CC-SEC-009 (authoring control: SECURITY.md, Deployment rule 4)
- **Title**: IaC policy-as-code, scanning, and drift detection
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: CI MUST enforce policy-as-code (e.g., OPA) and IaC scanning so that noncompliant Terraform plans are blocked before merge and before apply, and automated drift detection MUST run against the GitOps-declared state.
- **Rationale**: SECURITY.md, Deployment rule 4 authors this control. Golden modules (issue 008) make secure configuration the default, but only a blocking policy gate guarantees that a plan violating the platform's secure defaults (public access, missing encryption, permissive IAM, open security groups) can never reach the clusters; drift detection closes the complementary gap where cloud state is mutated outside Git, silently diverging from the reviewed, declared configuration (SECURITY.md, Deployment rule 5). These gates compose with the mandatory merge gates of CC-QA-002 and govern AI-generated IaC identically (SECURITY.md, Deployment rule 7).
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (platform-wide infrastructure underpinning storefront, portal, dashboard, and B2B API)
- **Components**: CI pipeline policy gate, IaC scanner integration, drift-detection job comparing live state to GitOps-declared state
- **Actors**: Platform engineers (authors of IaC changes), CI/CD pipeline identity, security monitoring consumers of drift alerts
- **Data Classification**: Internal (policies and scan results); findings referencing state contents are Confidential (state is a secret — SECURITY.md, Deployment rule 2)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: OWASP Top Ten A05:2021 Security Misconfiguration (noncompliant plans reaching production); STRIDE Tampering (out-of-band mutation of cluster/cloud state defeating reviewed configuration); Elevation of Privilege via IAM misconfiguration shipped in a plan; erosion of the CC-QA-002 merge-gate guarantee for AI-generated IaC
- **Trust Boundary**: The merge/apply gate between proposed IaC and the Azure control plane; the reconciliation boundary between GitOps-declared state and live cluster/cloud state
- **Zero Trust Consideration**: Every plan — human- or AI-authored — is treated as untrusted until it passes the policy gate; live state is not trusted to match Git and is continuously re-verified by drift detection.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: CM-3 (configuration change control), CM-6 (configuration settings enforcement), SI-4 (system monitoring — drift detection)
- **NIST SP 800-207**: Continuous verification — declared state is re-validated against observed state rather than trusted after initial deployment
- **Regulatory**: N/A
- **Other**: Wiz Terraform security best practices (referenced by SECURITY.md, References)

## Acceptance Criteria
1. **AC-01**: Given a Terraform change, when CI runs, then policy-as-code evaluation and IaC scanning execute on the rendered plan, and both are blocking, mandatory gates before merge and before apply (SECURITY.md, Deployment rules 4 and 7; CC-QA-002).
2. **AC-02** (negative): Given a plan that violates a secure-default policy (e.g., public network access enabled, encryption at rest disabled, an open security group, or an over-privileged IAM grant — SECURITY.md, Deployment rule 3), when the policy gate evaluates it, then the pipeline fails, the plan is not applied, and the merge is blocked (SECURITY.md, Deployment rule 4).
3. **AC-03**: Given the policy set, when a plan is rejected, then the failure output identifies the violated policy rule and resource address without echoing secret values or full state contents into CI logs (SECURITY.md, Logging rule 4).
4. **AC-04**: Given the GitOps-declared state (issue 010), when the automated drift-detection job runs on its schedule, then any divergence between declared and live state is detected and reported as a structured event to centralized monitoring (SECURITY.md, Deployment rule 4; Logging rule 3).
5. **AC-05** (negative): Given a manual out-of-band mutation of provisioned infrastructure or cluster state, when the next drift-detection run executes, then the mutation is flagged and alerted — it MUST NOT persist silently as the new accepted baseline (SECURITY.md, Deployment rules 4–5).
6. **AC-06**: Given an AI-generated IaC change, when it enters CI, then it passes the identical policy and scanning gates plus mandatory human review, with no auto-merge path (CC-QA-002; SECURITY.md, Deployment rule 7).
7. **AC-07**: Given the policy definitions themselves, when they change, then the change flows through the same reviewed merge process as any other code — policies are versioned in the repository, not configured ad hoc in a console.

## Failure Behavior
- **On Invalid Input**: Noncompliant plans are rejected in CI with a failing, blocking check; nothing is applied. Policy-evaluation output discloses rule and resource, never internal secrets or state contents.
- **On System Error**: Fail closed — if the policy engine, scanner, or their configuration cannot be loaded or errors out, the gate fails and merge/apply is blocked (per the fail-closed principle of SECURITY.md, Logging rule 2); an unreachable drift detector raises an operational alert rather than silently skipping runs.
- **Alerting**: Drift-detection findings and policy-gate infrastructure failures alert to centralized security monitoring (SECURITY.md, Logging rule 3); repeated policy-gate failures on the same rule are visible for triage.

## Test Strategy
- **Unit Tests**: Policy tests: for each policy rule, a compliant and a violating plan fixture asserting pass/fail (the policy suite itself is code and is tested).
- **Integration Tests**: End-to-end CI run over a known-bad Terraform fixture asserting the pipeline blocks before merge and before apply; end-to-end drift scenario asserting an injected out-of-band change is detected and reported.
- **Security Tests**: Verify the gate cannot be skipped: the policy and scan checks are required, blocking checks in the merge configuration (composes with issue 006); negative test asserting a pipeline with a disabled gate cannot merge.
- **Compliance Tests**: Automated evidence per run: policy version evaluated, scanner version, findings, and drift-report artifacts retained per the documented log retention schedule (SECURITY.md, Logging rule 9; CC-CMP-003).
- **Coverage Target**: ≥ 80% for policy test suites and any first-party glue code per CC-QA-001; tests tagged CC-QA-002/CC-SEC-009 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 006 CI merge gates (this issue's gates compose with them); 008 Terraform bootstrap (plans and golden-module defaults these policies enforce); 010 GitOps application delivery to AKS (the GitOps-declared state drift detection compares against).
- **Downstream**: 011 AKS workload hardening (its network-policy/pod-security manifests are inside the policy/drift perimeter); 015 PostgreSQL Flexible Server (its provisioned configuration is policy-checked).
- **External**: Microsoft Azure (control plane whose live state is compared against declarations).

## Implementation Notes
- **Constraints**: Policies encode exactly the secure defaults SECURITY.md mandates — encryption on, no public access, no open security groups, least-privilege IAM (Deployment rule 3), private endpoints everywhere with public ingress only at the storefront/portal/API gateways (HTTP boundary rule 9; ARCHITECTURE.md "Technology decisions"). Drift detection targets the GitOps-declared state as the single source of truth (Deployment rules 4–5).
- **Anti-Patterns**: MUST NOT make policy or scan gates advisory/non-blocking (Deployment rules 4 and 7); MUST NOT resolve drift by adopting live state into Git without review; MUST NOT maintain policy exceptions outside the versioned repository; MUST NOT let scanner findings of high/critical severity merge (Deployment rule 7).
- **AI Development Guidance**: AI-generated policies and IaC pass the identical blocking gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The policy engine is not fixed — SECURITY.md, Deployment rule 4 says "e.g., OPA"; the concrete engine choice is unconfirmed.
- No specific IaC scanner is named in the specs.
- No drift-detection tool or schedule/frequency is specified — only "automated drift detection against GitOps-declared state".
- The alert destination/on-call routing for drift findings is not specified beyond "centralized monitoring with alerting" (SECURITY.md, Logging rule 3).
