# 012 · Container image signing and admission verification

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-022, CC-SEC-009 (authoring control: SECURITY.md, Deployment rule 11)
- **Title**: Container image signing and admission verification
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Container images MUST be signed at build, and the AKS clusters MUST verify signature and provenance at admission, admitting only signed images from the reviewed internal registry.
- **Rationale**: SECURITY.md, Deployment rule 11 authors this control (CC-SEC-022, CC-SEC-009): image scanning (Deployment rule 6, issue 011) detects known CVEs but not a substituted or tampered image; admission-time signature and provenance verification closes the gap between "scanned" and "the artifact that actually runs". This was added by the 2026-07-15 threat model (REQUIREMENTS.md v1.3, CC-SEC-022). It extends the supply-chain posture of CC-SEC-009 (SCA gates, SBOM per release, no unaudited third-party supply chains) from source dependencies to the deployed artifact itself.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (every container image running platform workloads for storefront, portal, dashboard, and B2B API)
- **Components**: Image build/signing stage in CI, reviewed internal container registry, admission verification policy on all AKS clusters, signing-key management
- **Actors**: CI/CD pipeline identity (builds and signs), GitOps reconciler (deploys), cluster admission controller (verifies), platform engineers
- **Data Classification**: Internal (images/policies); signing keys are secrets (Confidential — SECURITY.md, Secret handling rule 1)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Supply-chain attack via substituted or tampered image (STRIDE Tampering/Spoofing of the deployment artifact); registry compromise or poisoned upstream image reaching the runtime; OWASP Top Ten A08:2021 Software and Data Integrity Failures; bypass of the merge gates by deploying an artifact CI never produced (CC-QA-002; SECURITY.md, Deployment rule 7)
- **Trust Boundary**: The registry-to-cluster admission boundary — the last control point before untrusted bytes become running workloads
- **Zero Trust Consideration**: The cluster does not trust the registry, the network path, or the deploy request: every image is cryptographically verified for signature and provenance at admission, every time, regardless of source reputation.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-7 (software, firmware, and information integrity), CM-14 (signed components), SC-13 (cryptographic protection), AC-6 (least privilege for signing identity)
- **NIST SP 800-207**: Artifact integrity verified per admission decision; no implicit trust in the delivery pipeline or registry
- **Regulatory**: N/A
- **Other**: SBOM generation per release accompanies the signed artifact (CC-SEC-009; SECURITY.md, Deployment rule 10)

## Acceptance Criteria
1. **AC-01**: Given a CI build that has passed the blocking merge gates, when the image is published, then it is pushed to the reviewed internal registry and signed by the pipeline's signing identity, with provenance metadata attached linking the image to its source revision and build (SECURITY.md, Deployment rule 11).
2. **AC-02**: Given any AKS cluster in any region, when a workload is admitted, then admission verifies the image's signature and provenance and confirms the image originates from the reviewed internal registry before the pod runs (SECURITY.md, Deployment rule 11; CC-SEC-022).
3. **AC-03** (negative): Given an unsigned image, an image with an invalid/mismatched signature, or an image from any registry other than the reviewed internal registry, when admission evaluates it, then the pod is rejected and a security event is logged — the image MUST NOT run (SECURITY.md, Deployment rule 11; Logging rule 3).
4. **AC-04** (negative): Given a signed image whose digest has been altered after signing (tampered artifact), when admission verifies it, then verification fails and the pod is rejected.
5. **AC-05**: Given the signing keys/identity, when their storage and access are reviewed, then signing material lives in Azure Key Vault under least-privilege data-plane RBAC, is never present in source, config, environment variables, or CI logs, and only the pipeline identity can sign (SECURITY.md, Secret handling rules 1–3).
6. **AC-06**: Given the admission verification policy itself, when it is created or changed, then the change flows through Git and the GitOps reconciliation path with the mandatory merge gates — the policy cannot be weakened by a manual cluster mutation (SECURITY.md, Deployment rule 5; issue 010).
7. **AC-07**: Given a release, when its artifacts are inventoried, then an SBOM exists for the released image set (SECURITY.md, Deployment rule 10; CC-SEC-009).

## Failure Behavior
- **On Invalid Input**: Admission rejects the pod (no partial admission, no warn-only mode in production namespaces); the rejection event records image reference, digest, and failure reason without leaking key material.
- **On System Error**: Fail closed — if the verifier cannot evaluate an image (verifier down, key material unavailable), admission is denied, never bypassed (SECURITY.md, Logging rule 2). Already-running verified workloads keep serving (CC-NFR-001); new admissions wait.
- **Alerting**: Every admission rejection for signature/provenance failure alerts to centralized security monitoring (a rejection in production implies attempted deployment of an unverified artifact — SECURITY.md, Logging rule 3); alert on Key Vault access denials for the signing identity (Logging rule 8).

## Test Strategy
- **Unit Tests**: Signing-stage tests: image built by CI carries a verifiable signature and provenance metadata; policy manifest validation in CI.
- **Integration Tests**: On a test cluster: admit a properly signed internal-registry image (succeeds); attempt unsigned, tampered-digest, wrongly-signed, and external-registry images (all rejected — AC-03/04); verifier-unavailable scenario asserting fail-closed denial.
- **Security Tests**: Verify no warn-only/audit-only bypass exists in production namespaces; secret scan asserts no signing material in source or pipeline configuration; pentest scope includes attempting to run an unsigned image (CC-QA-007).
- **Compliance Tests**: Automated evidence per release: signature verification results, provenance records, SBOM presence (Deployment rule 10), and admission-policy version in effect.
- **Coverage Target**: ≥ 80% for first-party signing/verification tooling code per CC-QA-001; tests tagged CC-SEC-022/CC-SEC-009 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 006 CI merge gates (only gate-passing builds get signed); 008 Terraform bootstrap (registry and admission infrastructure provisioned via Terraform); 010 GitOps application delivery to AKS (admission policy delivered declaratively); 011 AKS workload hardening (image scanning per Deployment rule 6 — this issue closes its substituted-image gap); 014 Azure Key Vault (signing-key custody).
- **Downstream**: Every issue that ships a deployable workload (001 solution scaffold, 004 Angular workspace, and all service issues) — their images must be signed to run.
- **External**: Microsoft Azure — AKS and Azure Key Vault (ARCHITECTURE.md, "Technology decisions").

## Implementation Notes
- **Constraints**: Verification must run at cluster admission on every cluster and namespace running platform workloads, in enforcing (not audit-only) mode for production; the "reviewed internal registry" is the only admissible image source, which also covers third-party base images — they enter only by being pulled, reviewed, scanned, re-signed, and hosted internally (consistent with the third-party supply-chain stance of SECURITY.md, Deployment rule 10 and the Dependency Rules). Registry access rides the private-endpoint topology (ARCHITECTURE.md, "Technology decisions").
- **Anti-Patterns**: MUST NOT admit images by tag trust, registry allowlist alone, or scan status alone — signature and provenance verification is required (Deployment rule 11); MUST NOT run admission in warn-only mode in production; MUST NOT store signing keys outside Key Vault or grant broad roles to the signing identity (Secret handling rules 1–3); MUST NOT pull workload images directly from public registries.
- **AI Development Guidance**: AI-generated pipeline and policy code passes the identical blocking gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Signing happens only after all gates pass — a signature attests the artifact went through the full gate set.

## Open Questions
- No signing/provenance toolchain is named in the specs (only "sign container images and verify signature and provenance at admission").
- The concrete registry service for the "reviewed internal registry" is not named.
- The admission-controller mechanism is not named.
- Signing-key rotation cadence and the re-verification/grace policy for images signed with a rotated (non-compromised) key are not specified.
- Whether verification should also be enforced for non-production clusters in the same mode is not stated (this draft assumes enforcing everywhere, audit-mode nowhere in production).
