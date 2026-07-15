# 014 · Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-008 (authoring controls: SECURITY.md, Secret handling rules 1–5)
- **Title**: Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: All platform secrets MUST live exclusively in Azure Key Vault, reached from Kubernetes via Azure Workload Identity with the Key Vault Secrets Store CSI driver, with every Azure SDK client authenticating via managed identity, app identities holding only narrow data-plane RBAC roles, secrets cached only with a TTL honoring expiry and refreshed on rotation — never stored in source, config files, client bundles, environment variables, Terraform code/variables/state, images, or manifests.
- **Rationale**: SECURITY.md, Secret handling rules 1–5 author these controls (CC-SEC-008); ARCHITECTURE.md confirms Key Vault as the secrets service and the Workload Identity + CSI driver pattern was confirmed 2026-07-15 (Secret handling rule 4). Embedded or long-lived credentials are the classic initial-access and lateral-movement vector; managed identity eliminates static credentials, narrow data-plane RBAC bounds the blast radius of any single compromised identity (never Contributor/Owner/Key Vault Administrator — rule 3), and TTL-bounded caching with rotation reaction ensures a rotated (possibly compromised) secret actually stops working platform-wide.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (every service in the modular monolith and every supporting workload for storefront, portal, dashboard, and B2B API)
- **Components**: Azure Key Vault instance(s), Azure Workload Identity federation for AKS workloads, Key Vault Secrets Store CSI driver deployment, in-process secret access/caching layer used by the ASP.NET Core application, Azure RBAC role assignments
- **Actors**: Workload service accounts (per-workload identities), platform engineers, CI/CD pipeline identity
- **Data Classification**: Restricted (secrets: processor webhook verification secrets, outbound HMAC secrets, database credentials, field-encryption keys — per SECURITY.md, Secret handling rules 6–10)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: CWE-798 (hard-coded credentials), CWE-522 (insufficiently protected credentials); OWASP Top Ten A02:2021 Cryptographic Failures, A05:2021 Security Misconfiguration, A07:2021 Identification and Authentication Failures; STRIDE Information Disclosure (secret leakage via source/config/logs/state) and Elevation of Privilege (over-broad vault roles); stale-secret persistence after rotation of a compromised credential
- **Trust Boundary**: The workload-to-Key Vault boundary — every secret retrieval is an authenticated, authorized, audited data-plane call by a federated workload identity
- **Zero Trust Consideration**: No standing embedded credentials anywhere; each workload proves its identity per token via Workload Identity federation, receives only the specific secrets its narrow role permits, and re-verifies by honoring TTL/expiry rather than trusting a cached value indefinitely.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V11 Cryptography (secret/key management); V13 Configuration
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-6 (least privilege role assignments), IA-5 (authenticator management — rotation, no embedded credentials), SC-28 (protection at rest in the vault), AU-9 (protection of audit information for vault access logs)
- **NIST SP 800-207**: Per-request workload authentication to the secret store; no implicit trust from cluster membership
- **Regulatory**: Supports PCI DSS SAQ A scoping indirectly — no card data or processor credentials outside the delegated model (CC-ORD-003; SECURITY.md, Secret handling rule 7)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any platform secret, when its storage is audited, then it exists only in Azure Key Vault — a repository-wide and artifact-wide scan finds no secrets in source, config files, client bundles, environment variables, Terraform code/variables/state, container images, or Kubernetes manifests (SECURITY.md, Secret handling rules 1 and 4; Deployment rule 7 secret-scan gate).
2. **AC-02**: Given any Azure SDK client in the ASP.NET Core application, when it authenticates, then it uses managed identity (`DefaultAzureCredential` or an explicit `TokenCredential`) — no keys or credentialed connection strings are embedded anywhere (SECURITY.md, Secret handling rule 2).
3. **AC-03**: Given AKS workloads needing secrets, when they retrieve them, then access goes through Azure Workload Identity federation with the Key Vault Secrets Store CSI driver (confirmed 2026-07-15), with a distinct identity per workload (SECURITY.md, Secret handling rule 4).
4. **AC-04** (negative): Given the Azure RBAC assignments for application identities, when they are enumerated, then each holds only narrow data-plane roles scoped to specific resources (e.g., Key Vault Secrets User on its vault) — no identity holds Contributor, Owner, or Key Vault Administrator, and an application identity attempting a management-plane or out-of-scope data-plane operation is denied (SECURITY.md, Secret handling rule 3).
5. **AC-05**: Given the in-process secret cache, when a cached secret's TTL elapses or its Key Vault expiry passes, then the cached value is discarded and re-fetched — no secret is cached indefinitely (SECURITY.md, Secret handling rule 5).
6. **AC-06**: Given a secret rotation event in Key Vault, when it occurs, then consuming workloads react and converge to the new secret value within the documented reaction window, without redeploy-by-hand (SECURITY.md, Secret handling rule 5).
7. **AC-07** (negative): Given application logs, telemetry, and error responses, when secrets are in use, then no secret value ever appears in them (SECURITY.md, Logging rule 4), and Key Vault access denials are alerted (Logging rule 8).

## Failure Behavior
- **On Invalid Input**: N/A for classic input; a workload requesting a secret outside its role receives an authorization denial from Key Vault, which is logged and alerted (SECURITY.md, Logging rule 8).
- **On System Error**: Fail closed — if Key Vault is unreachable and no valid-TTL cached value exists, the dependent operation fails (no fallback to embedded defaults, no empty-credential connection attempts); an exception in a secret-retrieval path never degrades into anonymous or default-credential access (SECURITY.md, Logging rule 2). Valid-TTL cached values may bridge transient outages (rule 5's TTL bound still applies).
- **Alerting**: Alert on Key Vault access denials and authentication failure spikes (SECURITY.md, Logging rule 8), on rotation events that fail to propagate within the reaction window, and on any secret-scan gate hit in CI.

## Test Strategy
- **Unit Tests**: .NET unit tests for the secret access/caching layer: TTL expiry discards values, expiry metadata honored, rotation callback refreshes, no-cache-hit path fails closed. Target ≥ 80%.
- **Integration Tests**: On a test cluster: workload retrieves a secret via Workload Identity + CSI driver; per-workload identity isolation (workload A cannot read workload B's secrets); rotation propagation test asserting convergence to the new value; Key Vault-unreachable test asserting fail-closed behavior.
- **Security Tests**: Secret-scan CI gate over source, config, IaC, and images (SECURITY.md, Deployment rule 7); automated RBAC audit asserting no broad roles (AC-04); log-scrub test asserting secret values never reach logs/telemetry (Logging rule 4).
- **Compliance Tests**: Automated evidence: RBAC assignment inventory per release, Key Vault diagnostic/access logs flowing to centralized monitoring (Logging rule 3), rotation-event propagation records.
- **Coverage Target**: ≥ 80% per package for the secret access layer per CC-QA-001; tests tagged CC-SEC-008 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (Key Vault, identities, and RBAC provisioned via golden modules; no secrets in Terraform); 010 GitOps application delivery to AKS (CSI driver and workload manifests delivered declaratively); 011 AKS workload hardening (driver and workloads run within the hardened baseline).
- **Downstream**: 015 PostgreSQL Flexible Server (per-context database credentials and TLS materials consumed via this pattern); 012 Container image signing (signing-key custody); 039 Stripe / 040 Razorpay integrations and 041 inbound webhook verification (processor secrets per Secret handling rule 9); 057 outbound partner webhooks (per-partner HMAC secrets per rule 8); 087 employee compensation field-level encryption keys (rule 6); 022 structured security logging (alerting integration).
- **External**: Microsoft Azure — Key Vault, AKS, Azure Workload Identity, Key Vault Secrets Store CSI driver (ARCHITECTURE.md, "Technology decisions"; confirmed 2026-07-15).

## Implementation Notes
- **Constraints**: Confirmed stack: .NET 10 / ASP.NET Core on AKS; use `DefaultAzureCredential`/`TokenCredential` in every Azure SDK client (Secret handling rule 2); Key Vault reached over the private-endpoint topology (ARCHITECTURE.md, "Technology decisions"). Secret-consuming configuration binds from CSI-mounted sources, not environment variables (rule 1 prohibits env-var secrets). Vault access logs are part of the observability pipeline (Azure Monitor / Log Analytics, ARCHITECTURE.md "Cross-cutting").
- **Anti-Patterns**: MUST NOT store secrets in source, config, client bundles, env vars, Terraform, images, or manifests (Secret handling rules 1 and 4); MUST NOT grant Contributor/Owner/Key Vault Administrator to app identities (rule 3); MUST NOT cache secrets indefinitely or ignore expiry (rule 5); MUST NOT log secret values or credentialed connection strings (Logging rule 4); MUST NOT share one workload identity across bounded contexts (least privilege, rule 3; composes with per-context database roles in issue 015).
- **AI Development Guidance**: AI-generated secret-handling code passes the identical blocking gates — including the secret scan — plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The concrete rotation-event mechanism (how workloads learn of a rotation: CSI driver re-mount polling interval vs. an event subscription) is not specified — only "react to rotation events" (Secret handling rule 5).
- Numeric TTL values for the secret cache and the maximum acceptable rotation-propagation window are not specified.
- Vault topology (single vault vs. per-environment/per-region vaults) is not specified; regional placement is entangled with the open telemetry/backup-residency and residency-topology decisions (ARCHITECTURE.md, "Known unknowns") and is not resolved here.
