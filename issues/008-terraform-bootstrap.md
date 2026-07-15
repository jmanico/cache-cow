# 008 · Terraform bootstrap: encrypted remote state, pinned versions, golden modules

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-008, CC-SEC-009 (authoring controls: SECURITY.md, Deployment rules 1–3; Secret handling rule 1)
- **Title**: Terraform bootstrap: encrypted remote state, pinned versions, golden modules
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: All platform infrastructure MUST be provisioned exclusively through Terraform executed in CI/CD pipelines with short-lived, least-privilege provider credentials, with state stored in an encrypted, locked remote backend treated as a secret, all provider and module versions pinned, modules consumed only from reviewed internal or verified sources, and secure defaults (encryption on, no public access, no open security groups, least-privilege IAM) encoded in reusable golden modules — and no secret may ever appear in Terraform code, variables, or state.
- **Rationale**: SECURITY.md, Deployment rules 1–3 author these controls; ARCHITECTURE.md ("Delivery") confirms infrastructure is provisioned by Terraform. Terraform state can contain sensitive resource attributes, so it is a secret (Deployment rule 2), and SECURITY.md, Secret handling rule 1 prohibits secrets in Terraform code/variables/state — all secrets live in Azure Key Vault only (CC-SEC-008). Applies from developer machines with long-lived keys are an unauditable, phishable provisioning path; unpinned providers/modules are an unaudited supply chain (CC-SEC-009). Golden modules make the secure configuration the default rather than a per-resource decision.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (platform-wide infrastructure underpinning storefront, portal, dashboard, and B2B API)
- **Components**: Terraform root modules and golden module library, remote state backend, CI/CD provisioning pipeline, provider credential configuration
- **Actors**: Platform engineers (authors of Terraform changes), CI/CD pipeline identity (sole executor of plan/apply)
- **Data Classification**: Confidential (Terraform state files are treated as secrets — SECURITY.md, Deployment rule 2)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: OWASP Top Ten A05:2021 Security Misconfiguration; A02:2021 Cryptographic Failures (unencrypted state at rest); CWE-798 (hard-coded credentials in IaC), CWE-312 (cleartext storage of sensitive information in state); STRIDE Tampering/Elevation of Privilege (unaudited applies, over-privileged provider credentials); supply-chain compromise via unpinned or unvetted modules (CC-SEC-009)
- **Trust Boundary**: The CI/CD pipeline to Azure control plane boundary — the only path through which infrastructure may change
- **Zero Trust Consideration**: No standing credentials: provider credentials are short-lived and least-privilege, issued to the pipeline identity only (Deployment rule 1). State backend access is least-privilege and every module source is treated as untrusted until reviewed (Deployment rule 3).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration; V14 Data Protection (state confidentiality)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-6 (least privilege for pipeline and state-backend access), SC-28 (protection of state at rest), CM-6 (configuration settings via golden modules)
- **NIST SP 800-207**: Per-request, short-lived credentialing of the provisioning identity; no implicit trust for developer workstations
- **Regulatory**: N/A
- **Other**: Wiz Terraform security best practices (referenced by SECURITY.md, References)

## Acceptance Criteria
1. **AC-01**: Given a merged Terraform change, when provisioning runs, then `terraform plan` and `terraform apply` execute only inside the CI/CD pipeline using short-lived, least-privilege provider credentials (SECURITY.md, Deployment rule 1).
2. **AC-02**: Given the state backend configuration, when state is written or read, then it is stored in an encrypted remote backend with state locking enabled, and backend access is restricted to the pipeline identity and a documented break-glass group with least privilege (SECURITY.md, Deployment rule 2).
3. **AC-03**: Given any Terraform source file, when CI runs, then every provider and module declares an exact pinned version, and a plan referencing a floating version range or an unreviewed external module source fails the pipeline (SECURITY.md, Deployment rule 3; CC-SEC-009).
4. **AC-04**: Given the golden module library, when a resource type with security-relevant settings is provisioned through it, then encryption at rest is on, public network access is off, no open security groups are created, and IAM grants are least-privilege by default — overriding a secure default requires an explicit, reviewable input (SECURITY.md, Deployment rule 3).
5. **AC-05** (negative): Given a Terraform change containing a secret literal in code, a variable, or a value that would persist a secret into state, when the CI secret-scan gate runs, then the pipeline fails and the change MUST NOT be applied (SECURITY.md, Secret handling rule 1; Deployment rule 7).
6. **AC-06** (negative): Given a developer workstation without pipeline credentials, when `terraform apply` is attempted against the platform backend, then authentication fails — no long-lived provider keys exist outside the pipeline (SECURITY.md, Deployment rule 1).
7. **AC-07**: Given the private-endpoint topology in ARCHITECTURE.md ("Technology decisions"), when golden modules provision data stores or intra-platform services, then they default to private networking with public ingress possible only for the storefront, portal, and API gateways (SECURITY.md, HTTP boundary rule 9).

## Failure Behavior
- **On Invalid Input**: A noncompliant Terraform change (unpinned version, secret literal, unreviewed module source) fails CI before merge and before apply; the pipeline reports the failing check without echoing secret values into logs (SECURITY.md, Logging rule 4).
- **On System Error**: Fail closed — a failed or interrupted apply never bypasses gates; state locking prevents concurrent/partial mutations; no manual out-of-band fixes to cloud state (GitOps discipline, SECURITY.md, Deployment rule 5 — issue 010).
- **Alerting**: Alert on state-backend access denials and on any state access from a non-pipeline identity, routed to centralized security monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Static assertions over the Terraform tree in CI: version-pin lint (no floating constraints), backend configuration check (encryption + locking present), module-source allowlist check.
- **Integration Tests**: Pipeline-level test that a plan/apply succeeds end-to-end with pipeline credentials; golden-module plan tests asserting secure defaults (encryption on, no public access) in rendered plans.
- **Security Tests**: Secret scanning over Terraform code and variables in the mandatory merge gates (SECURITY.md, Deployment rule 7); negative test injecting a dummy secret literal and asserting pipeline failure. (Full IaC policy scanning is issue 009 — not absorbed here.)
- **Compliance Tests**: Automated evidence: backend encryption/locking configuration captured per pipeline run; audit log of who/what triggered each apply.
- **Coverage Target**: ≥ 80% for any first-party test/tooling code per CC-QA-001; HCL itself is exercised via the plan-based tests above. Tests tagged with CC-SEC-008/CC-SEC-009 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 006 CI merge gates: tests, coverage, lint, SAST/SCA/secret-scan/raw-HTML-sink gate (the secret-scan gate this issue relies on).
- **Downstream**: 009 IaC policy-as-code, scanning, and drift detection; 010 GitOps application delivery to AKS; 011 AKS workload hardening; 013 Ingress WAF and DDoS protection; 014 Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation; 015 PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest.
- **External**: Microsoft Azure (confirmed primary cloud, ARCHITECTURE.md "Technology decisions"); Azure Key Vault as the only home for secrets (CC-SEC-008).

## Implementation Notes
- **Constraints**: Azure is the confirmed cloud and Terraform the confirmed provisioning tool (ARCHITECTURE.md, "Technology decisions"/"Delivery"); the remote backend must therefore be an Azure-hosted encrypted backend with locking. Secrets consumed by infrastructure are referenced from Azure Key Vault at runtime (issue 014), never materialized into Terraform variables or state.
- **Anti-Patterns**: MUST NOT run applies from developer machines or with long-lived keys (Deployment rule 1); MUST NOT store secrets in source, config files, environment variables, or Terraform code/variables/state (Secret handling rule 1); MUST NOT use floating version ranges or unreviewed module sources (Deployment rule 3, Dependency Rules 7); MUST NOT create resources with public access or permissive IAM as defaults.
- **AI Development Guidance**: AI-generated Terraform passes the identical blocking merge gates (SAST/SCA/secret scan, lint, tests) plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specific Azure service for the remote state backend (e.g., which storage/locking mechanism) is not named in the specs; only "encrypted remote backend with state locking" is mandated (SECURITY.md, Deployment rule 2).
- The CI/CD platform that executes plan/apply is not named in any canonical doc.
- The mechanism for issuing short-lived provider credentials to the pipeline (e.g., federated workload identity vs. another broker) is not specified — only "short-lived, least-privilege provider credentials" (SECURITY.md, Deployment rule 1).
- The break-glass/emergency access model for the state backend is not specified.
