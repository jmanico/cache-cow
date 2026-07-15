# 010 · GitOps application delivery to AKS

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-QA-002, CC-NFR-001 (authoring controls: ARCHITECTURE.md "Delivery"; SECURITY.md, Deployment rule 5)
- **Title**: GitOps application delivery to AKS
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Security

## Requirement
- **Description**: Application delivery MUST be GitOps: declarative manifests, versioned in Git and passed through the mandatory merge gates, are continuously reconciled to the AKS clusters, and cluster state MUST NOT be mutated manually.
- **Rationale**: ARCHITECTURE.md ("Delivery") confirms application delivery via GitOps — declarative manifests reconciled to the clusters — with CI enforcing the CC-QA gates and the SECURITY.md deployment rules; SECURITY.md, Deployment rule 5 authors the control ("no manual mutations of cluster state"). Manual cluster mutations bypass review, the blocking security gates (Deployment rule 7), and the audit trail, and create drift the platform cannot reason about; the GitOps-declared state is also the reference that drift detection (Deployment rule 4, issue 009) verifies against. Reconciled, reviewable delivery underpins the availability targets (CC-NFR-001) by making every change reproducible and revertible.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (delivery path for the modular monolith serving storefront, portal, dashboard, and B2B API)
- **Components**: Git repository of declarative Kubernetes manifests, reconciliation mechanism on the AKS clusters (multi-region: Americas, Europe, Asia per ARCHITECTURE.md "Scale"), CI gate integration
- **Actors**: Platform/application engineers (authors of manifest changes), CI/CD pipeline identity, cluster reconciler identity
- **Data Classification**: Internal (manifests); manifests MUST NOT contain secrets (SECURITY.md, Secret handling rules 1 and 4 — secrets arrive via Key Vault/Workload Identity, issue 014)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: STRIDE Tampering and Repudiation (unaudited manual cluster mutations); OWASP Top Ten A05:2021 Security Misconfiguration (untracked ad-hoc changes); bypass of the mandatory merge gates for deployed artifacts (CC-QA-002; SECURITY.md, Deployment rule 7)
- **Trust Boundary**: The Git-to-cluster reconciliation boundary — Git is the only trusted source of desired cluster state; direct human/cluster-API mutation paths are outside the trust model
- **Zero Trust Consideration**: The clusters do not trust pushed, imperative changes; only declared state that has passed review and blocking gates is reconciled, and live state is continuously re-verified against it (with drift detection in issue 009).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: CM-3 (configuration change control), CM-5 (access restrictions for change), AC-6 (least privilege for the reconciler and cluster credentials)
- **NIST SP 800-207**: Desired state is authenticated and versioned; runtime state is continuously reconciled rather than trusted
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given an application change, when it is delivered to any AKS cluster, then the delivery occurs exclusively by the reconciler applying declarative manifests from Git that have passed the mandatory merge gates (ARCHITECTURE.md "Delivery"; CC-QA-002; SECURITY.md, Deployment rule 7).
2. **AC-02**: Given the multi-region topology (ARCHITECTURE.md "Scale"), when manifests change, then every target cluster converges to the declared state through the same reconciliation mechanism — no per-cluster imperative deployment path exists.
3. **AC-03** (negative): Given a manual mutation of reconciler-managed cluster state (e.g., an ad-hoc edit of a managed Deployment), when reconciliation next runs, then the mutation is reverted to the Git-declared state and the event is observable — manual state MUST NOT persist (SECURITY.md, Deployment rule 5).
4. **AC-04** (negative): Given the manifest repository, when any manifest contains a secret value, then the CI secret-scan gate fails and the change is blocked — secrets never live in manifests and are never baked into images (SECURITY.md, Secret handling rules 1 and 4; Deployment rule 7).
5. **AC-05**: Given human and service credentials on the clusters, when access is reviewed, then routine deploy-capable write access to reconciler-managed resources is held only by the reconciler identity, and any emergency human write access is auditable (SECURITY.md, Deployment rule 5; Logging rule 3).
6. **AC-06**: Given a bad deployment, when a revert commit lands in Git, then the clusters converge back to the previous declared state through the same reconciliation path — rollback is a Git operation, not a manual cluster operation.
7. **AC-07**: Given the reconciler's own configuration, when it is provisioned or changed, then that configuration is itself declared in Git and provisioned via the Terraform/GitOps pipeline (issues 008–009), not configured by hand.

## Failure Behavior
- **On Invalid Input**: Manifests failing schema validation or merge gates never merge and are never reconciled; the reconciler reports invalid declared state as a failed sync without partially applying it.
- **On System Error**: Fail closed for change delivery — if the reconciler is unavailable, no alternative manual deployment path is used; already-running workloads continue serving (availability per CC-NFR-001) while delivery pauses until reconciliation recovers.
- **Alerting**: Alert on sustained reconciliation failure, on reverted manual-mutation events, and on any write to reconciler-managed resources by a non-reconciler identity, as structured events to centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Manifest validation in CI (schema/lint of all declarative manifests); repository-structure tests asserting every deployable workload is covered by declared manifests.
- **Integration Tests**: Against a test cluster: commit-to-convergence test (change lands in Git, cluster converges); revert test (Git revert converges the cluster back); manual-mutation test asserting reconciliation restores declared state (AC-03).
- **Security Tests**: Secret scan over the manifest repository (Deployment rule 7); RBAC review test asserting no standing human write access to reconciler-managed resources (AC-05).
- **Compliance Tests**: Automated evidence: reconciliation history correlating each cluster change to a Git commit and its passed gates, retained per the log retention schedule (SECURITY.md, Logging rules 3 and 9).
- **Coverage Target**: ≥ 80% for any first-party tooling/test code per CC-QA-001; tests tagged CC-QA-002/CC-NFR-001 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 006 CI merge gates (gates that manifests must pass); 008 Terraform bootstrap (clusters and reconciler infrastructure provisioned via Terraform).
- **Downstream**: 009 IaC policy-as-code, scanning, and drift detection (drift detection runs against the GitOps-declared state defined here); 011 AKS workload hardening (pod-security/network-policy manifests delivered via this pipeline); 012 Container image signing and admission verification (admission policy manifests delivered via this pipeline); 014 Azure Key Vault (CSI driver/Workload Identity manifests delivered via this pipeline).
- **External**: Microsoft Azure — AKS (confirmed Kubernetes platform, ARCHITECTURE.md "Technology decisions").

## Implementation Notes
- **Constraints**: Tool-agnostic by necessity: the specs mandate GitOps semantics (declarative manifests, continuous reconciliation, no manual mutation) but name no GitOps tool — implement against those semantics and isolate the tool choice so it can be ratified without rework. Deployment targets are the confirmed AKS clusters in at least Americas, Europe, and Asia (ARCHITECTURE.md "Scale"). Workload manifests must be compatible with the hardening baseline of issue 011 (restricted pod security, default-deny network policies) and the admission requirements of issue 012 (signed images only).
- **Anti-Patterns**: MUST NOT mutate cluster state manually or via imperative CLI paths (SECURITY.md, Deployment rule 5); MUST NOT bake secrets into images or manifests (Secret handling rule 4); MUST NOT create side channels that deploy artifacts which skipped the blocking merge gates (Deployment rule 7); MUST NOT resolve reconciliation failures by hand-editing live state.
- **AI Development Guidance**: AI-generated manifests and delivery tooling pass the identical blocking gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- **No GitOps tool is named in the specs.** ARCHITECTURE.md confirms only "application delivery via GitOps (declarative manifests reconciled to the clusters)"; the concrete reconciler is an open implementation choice requiring human ratification.
- Repository layout (single manifest repo vs. per-context, environment promotion model dev→staging→prod) is not specified.
- The emergency ("break-glass") change procedure and its audit workflow are not specified beyond the prohibition on manual mutations.
- Multi-region rollout ordering/progressive-delivery strategy across the Americas/Europe/Asia clusters is not specified.
