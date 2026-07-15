# 011 · AKS workload hardening: pod security, default-deny network policies, image scanning

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-009, CC-SEC-022 (authoring control: SECURITY.md, Deployment rule 6)
- **Title**: AKS workload hardening: pod security, default-deny network policies, image scanning
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Kubernetes workloads MUST run hardened: restricted pod security, default-deny ingress and egress network policies, and image scanning before deployment — default-deny network controls as defense in depth on top of identity-based auth.
- **Rationale**: SECURITY.md, Deployment rule 6 authors this control. The platform is a modular monolith plus supporting workloads on AKS with all private endpoints (ARCHITECTURE.md, "Technology decisions"); default-deny ingress/egress network policies ensure that a compromised pod cannot pivot laterally or exfiltrate even if an identity-based control fails (explicit defense in depth per the rule). Restricted pod security limits container breakout primitives. Pre-deployment image scanning blocks known-vulnerable images from running; Deployment rule 11 notes scanning detects known CVEs but not substituted images — signing/admission is the complementary control in issue 012.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (runtime platform for storefront SSR, portal, dashboard, and B2B API workloads)
- **Components**: AKS clusters (all regions), pod security configuration, Kubernetes NetworkPolicy set, image-scanning stage in the delivery pipeline
- **Actors**: Platform engineers, CI/CD pipeline identity (scanning), GitOps reconciler (policy delivery), workload service accounts
- **Data Classification**: Internal (cluster policy configuration); the workloads protected handle up to Restricted/PII data

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: STRIDE Elevation of Privilege (container breakout via privileged pods, hostPath, host namespaces); lateral movement and SSRF-adjacent egress from a compromised pod (complements SECURITY.md, Input validation rule 8); OWASP Top Ten A05:2021 Security Misconfiguration, A06:2021 Vulnerable and Outdated Components (unscanned images; CC-SEC-009); data exfiltration from workloads holding PII
- **Trust Boundary**: Pod-to-pod and pod-to-external network boundaries inside the cluster; the pipeline-to-cluster boundary for images entering the runtime
- **Zero Trust Consideration**: No pod is trusted by network position: all traffic is denied unless a policy explicitly allows the specific flow, layered on top of (never replacing) identity-based auth; every image is treated as potentially vulnerable until scanned.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-6 (least privilege for workload capabilities), SC-7 (boundary protection — default-deny network segmentation), SI-3/RA-5 (vulnerability scanning of images), CM-6 (configuration settings)
- **NIST SP 800-207**: Micro-segmentation with per-flow explicit allow; no implicit trust from network locality
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any namespace running platform workloads, when a pod is admitted, then it satisfies restricted pod security (no privileged containers, no host namespaces/hostPath, no privilege escalation), enforced by the cluster, not by convention (SECURITY.md, Deployment rule 6).
2. **AC-02** (negative): Given a manifest requesting a privileged container or host-level access, when it is applied to a platform namespace, then admission rejects it and the rejection is logged (SECURITY.md, Deployment rule 6; Logging rule 3).
3. **AC-03**: Given any platform namespace, when NetworkPolicies are enumerated, then a default-deny policy covers both ingress and egress for all pods, and every permitted flow is an explicit, specific allow rule (SECURITY.md, Deployment rule 6).
4. **AC-04** (negative): Given a pod with no explicit allow rule for a destination, when it attempts a connection (pod-to-pod or egress), then the connection fails — including from a deliberately misconfigured test pod, demonstrating defense in depth independent of identity-based auth (SECURITY.md, Deployment rule 6).
5. **AC-05**: Given a container image in the delivery pipeline, when it is promoted toward deployment, then it has been scanned, and images with critical findings are blocked from deployment consistent with the blocking SCA gate severity of SECURITY.md, Deployment rule 7 (CC-SEC-009).
6. **AC-06** (negative): Given an image that has not passed scanning, when deployment is attempted, then it does not reach the clusters through the delivery pipeline (SECURITY.md, Deployment rule 6; full signature/provenance admission enforcement is issue 012).
7. **AC-07**: Given the allow-rule set, when reviewed against ARCHITECTURE.md, then permitted flows match the declared topology: public ingress only via the storefront/portal/API gateways, private endpoints for intra-platform services and data stores (ARCHITECTURE.md "Technology decisions"; SECURITY.md, HTTP boundary rule 9), and dashboard workloads on a private network path (HTTP boundary rule 8).

## Failure Behavior
- **On Invalid Input**: Noncompliant pod specs are rejected at admission; disallowed network flows are dropped. Neither results in a degraded-but-permissive mode.
- **On System Error**: Fail closed — if policy enforcement or the scanning stage is unavailable, workloads are not admitted/promoted without it; an exception in an enforcement path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Admission rejections, denied-flow anomalies (sustained unexpected denials indicating misconfiguration or compromise), and scan-gate failures emit structured security events to centralized monitoring with alerting (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Policy manifests validated in CI (schema/lint); per-namespace assertion tests that default-deny ingress and egress policies exist and select all pods.
- **Integration Tests**: On a test cluster: admission tests submitting privileged/host-access pod specs and asserting rejection (AC-02); connectivity matrix tests asserting allowed flows succeed and all others fail (AC-04); end-to-end test that an unscanned or critical-CVE image is blocked (AC-05/06).
- **Security Tests**: Scheduled connectivity probes in staging asserting the deny matrix holds; image-scan gate verified as a required blocking check; DAST/pentest scope includes lateral-movement attempts per CC-QA-007.
- **Compliance Tests**: Automated evidence: cluster policy inventory (pod security + NetworkPolicy coverage per namespace) and per-image scan results archived per release.
- **Coverage Target**: ≥ 80% for first-party test/tooling code per CC-QA-001; tests tagged CC-SEC-009/CC-SEC-022 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (AKS clusters provisioned with enforcement enabled); 010 GitOps application delivery to AKS (policies delivered as declarative manifests); 006 CI merge gates (scan gate composes with Deployment rule 7 gates).
- **Downstream**: 012 Container image signing and admission verification (closes the scanned-vs-substituted-image gap per Deployment rule 11); 014 Azure Key Vault (CSI driver/Workload Identity pods must run within this hardening baseline); all application workload issues (001, 004, and the service issues) deploy inside these constraints.
- **External**: Microsoft Azure — AKS (ARCHITECTURE.md, "Technology decisions").

## Implementation Notes
- **Constraints**: Enforcement must be cluster-side (admission-enforced pod security, CNI-enforced NetworkPolicy), not convention; policies are declarative manifests under GitOps (issue 010) and inside the drift-detection perimeter (issue 009). Allow rules must encode the confirmed topology: gateway-only public ingress, private endpoints for services and data stores, VPN-only private path for the dashboard origin (ARCHITECTURE.md; SECURITY.md, HTTP boundary rules 8–9). Egress allows must cover only the confirmed external dependencies (Stripe, Razorpay, EasyPost, Contentful, Microsoft Entra, Azure services) plus cluster infrastructure.
- **Anti-Patterns**: MUST NOT run privileged pods or grant host access to application workloads; MUST NOT deploy namespaces without default-deny ingress and egress; MUST NOT use broad "allow-all within namespace" rules as a substitute for specific flows; MUST NOT treat network policy as a replacement for identity-based auth (it is defense in depth on top — SECURITY.md, Deployment rule 6); MUST NOT deploy unscanned images.
- **AI Development Guidance**: AI-generated policy manifests and tooling pass the identical blocking merge gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- No image-scanning tool is named in the specs.
- The exact blocking severity threshold for image scans is not stated in Deployment rule 6; this draft aligns it with the "SCA clean of critical" gate of Deployment rule 7 — confirm.
- The pod-security enforcement mechanism (e.g., Pod Security Admission "restricted" profile vs. a policy engine) is not named — the specs say only "restricted pod security".
- Whether any workload requires a documented exception to restricted pod security (none is anticipated for the modular monolith) is unconfirmed.
