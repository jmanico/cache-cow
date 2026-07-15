# 015 · PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** The data-residency vs. single-primary-write-region conflict (ARCHITECTURE.md, "Known unknowns" — reopened 2026-07-15 by the threat model) is an open decision. This issue is scoped strictly to the schema/role/TLS/encryption structure of the Flexible Server and explicitly excludes all region-placement, replica-placement, and backup/telemetry-residency decisions; those cannot proceed until a human resolves the residency topology (CC-CMP-001/002/006).

## Metadata
- **ID**: CC-SEC-021, CC-SEC-008 (authoring controls: SECURITY.md, Secret handling rules 6 and 10; ARCHITECTURE.md, "Cross-cutting: Data stores")
- **Title**: PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The single Azure Database for PostgreSQL Flexible Server hosting all ten bounded contexts MUST give each context its own least-privilege database role confined to its own schema with no cross-context grants, MUST require TLS on every data-store connection, and MUST be encrypted at rest.
- **Rationale**: SECURITY.md, Secret handling rule 10 authors the control (CC-SEC-021, added by the 2026-07-15 threat model): because one Flexible Server backs all ten bounded contexts (ARCHITECTURE.md, "Cross-cutting: Data stores"), the code-enforced module boundary of the modular monolith must not collapse at the connection string — a logic or injection flaw in one module (e.g., the contact form) must not reach employee compensation, partner terms, or the audit store. Encryption at rest is required by CC-SEC-008 (SECURITY.md, Secret handling rule 6), and backups/replicas/exports inherit encryption from their source (rule 6). ARCHITECTURE.md confirms the per-context role/schema/TLS pattern as the data-store architecture.
- **Design**: N/A (no user-facing surface).

## Scope
- **Applies To**: Both (data store for all ten bounded contexts serving storefront, portal, dashboard, and B2B API)
- **Components**: Azure Database for PostgreSQL Flexible Server; one schema per bounded context (Market & Gating Policy, Catalog & Inventory, Pricing & Promotions, Ordering & Payments, Fulfillment, Wholesale & B2B API, Invoicing, Back Office, Identity & Access, Content & Localization — ARCHITECTURE.md, "Server bounded contexts"); one least-privilege database role per context; connection configuration in each context module
- **Actors**: Bounded-context application modules (via their per-context roles), platform engineers/DBAs (administrative access), CI migration identity
- **Data Classification**: Restricted/PII (orders, addresses, employee records) and Regulated (financial audit and invoice data; GDPR/DPDP-scoped personal data)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: CWE-89 (SQL injection blast-radius containment — a compromised module reaches only its own schema); OWASP Top Ten A01:2021 Broken Access Control (cross-context data reach), A02:2021 Cryptographic Failures (plaintext at rest or in transit); STRIDE Information Disclosure and Tampering across module boundaries; insider/lateral movement via a shared over-privileged database credential
- **Trust Boundary**: The module-to-database boundary: each bounded context's connection is a distinct principal whose authority ends at its own schema, making the database enforce the same boundary the code declares
- **Zero Trust Consideration**: No module is trusted with the whole database because it is "inside": authority is per-context, verified by database privilege on every statement; every connection authenticates and encrypts regardless of the private network path (ARCHITECTURE.md private endpoints notwithstanding).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration; V11 Cryptography (at-rest encryption); V12 Secure Communication (TLS on data-store connections)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement at the database), AC-6 (least privilege per-context roles), SC-8 (transmission confidentiality — TLS), SC-28 (protection at rest)
- **NIST SP 800-207**: Per-workload database principals; no shared super-credential; encryption independent of network locality
- **Regulatory**: GDPR / DPDP Act 2023 protection-of-personal-data obligations depend on these controls (CC-CMP-001/002); residency aspects are excluded (see AT RISK)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the ten bounded contexts, when database principals are enumerated, then each context has exactly one dedicated least-privilege role whose privileges are confined to its own schema, and no application role holds superuser, database-owner, or cross-schema privileges (SECURITY.md, Secret handling rule 10; CC-SEC-021).
2. **AC-02** (negative): Given the Content & Localization context's role (the contact-form path of SECURITY.md, Secret handling rule 10's example), when it attempts any statement against the Back Office schema (employee compensation), the Wholesale schema (partner terms), or the audit tables, then the statement fails with a database privilege error — for SELECT, INSERT, UPDATE, and DELETE alike (CC-SEC-021).
3. **AC-03** (negative): Given any pair of distinct contexts A and B, when A's role attempts to access B's schema, then access is denied — verified by an automated full cross-context matrix test (10×9 pairs), which MUST fail closed (CC-QA-005 pattern applied at the database boundary).
4. **AC-04**: Given the Flexible Server configuration, when a client attempts a non-TLS connection, then the connection is rejected — TLS is required on every data-store connection, enforced server-side, not by client convention (SECURITY.md, Secret handling rule 10).
5. **AC-05**: Given the Flexible Server and any backup, snapshot, replica, or export derived from it, when encryption posture is audited, then data is encrypted at rest and derived copies inherit that encryption (SECURITY.md, Secret handling rule 6; CC-SEC-008; retention/residency inheritance beyond encryption is tracked under the open decisions, not verified here).
6. **AC-06**: Given each context's connection credentials, when their sourcing is audited, then they are consumed from Azure Key Vault via the issue-014 pattern — never embedded in source, config, environment variables, or manifests (SECURITY.md, Secret handling rules 1–2).
7. **AC-07** (negative): Given the network topology, when the Flexible Server's exposure is checked, then it is reachable only over private networking per the all-private-endpoints decision — no public database endpoint exists (ARCHITECTURE.md, "Technology decisions"; SECURITY.md, HTTP boundary rule 9).
8. **AC-08**: Given schema and grant definitions, when they change, then the change flows through migrations in the reviewed repository passing the mandatory merge gates — grants are code, not console actions (CC-QA-002; SECURITY.md, Deployment rules 5 and 7).

## Failure Behavior
- **On Invalid Input**: A statement exceeding a role's privileges fails with a database privilege error; the application surfaces only a generic RFC 9457 problem response with no SQL, schema names, or internal identifiers (SECURITY.md, Logging rule 1), while the denial is logged server-side with a correlation ID.
- **On System Error**: Fail closed — connection or privilege errors never trigger fallback to a broader-privileged or shared role; an exception in a data-access path is a denial, never a bypass (SECURITY.md, Logging rule 2). Non-TLS connections are refused outright.
- **Alerting**: Database privilege-denial events from application roles are logged as structured security events and alerted on (a cross-schema denial from an application role indicates a coding flaw or active exploitation — SECURITY.md, Logging rule 3); alert on any grant drift detected against the migration-declared state (composes with issue 009 drift detection).

## Test Strategy
- **Unit Tests**: Migration tests asserting the declared grant matrix (each role's expected privilege set per schema) matches the migration output.
- **Integration Tests**: .NET integration tests against a provisioned test server: per-context CRUD succeeds within its own schema; the full 10×9 cross-context denial matrix (AC-03); non-TLS connection rejection (AC-04); credential sourcing via the Key Vault pattern (AC-06).
- **Security Tests**: SQL-injection blast-radius test: a simulated injection through one context's data path cannot read another schema (validates CC-SEC-021's stated threat); automated audit comparing live database grants to the migration-declared matrix; secret scan asserting no embedded connection strings (Deployment rule 7).
- **Compliance Tests**: Automated evidence: encryption-at-rest configuration, `require TLS` server parameter state, role/grant inventory per release; backup-encryption inheritance check (Secret handling rule 6).
- **Coverage Target**: ≥ 80% per package for data-access and migration tooling per CC-QA-001; tests tagged CC-SEC-021/CC-SEC-008 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (Flexible Server provisioned via golden modules with secure defaults); 014 Azure Key Vault (per-context credential custody and rotation); 001 Solution scaffold (the bounded-context module boundaries these roles mirror).
- **Downstream**: Every bounded-context service issue (023–090 ranges) connects through its context's role; 081 Append-only audit store with WORM retention (adds the INSERT-only grants of SECURITY.md, Logging rule 6 / CC-SEC-020 on top of this structure — not absorbed here); 046 Invoice core (issued-invoice immutability grants likewise layered in 081/046); 087 Employee management (field-level compensation encryption per Secret handling rule 6 — not absorbed here); 090 Retention schedule and automated deletion jobs (CC-CMP-003).
- **External**: Microsoft Azure — Azure Database for PostgreSQL Flexible Server, Azure Key Vault (ARCHITECTURE.md, "Technology decisions").
- **Blocked/At-risk**: **AT RISK** — data residency vs. single primary write region, and telemetry & backup residency (ARCHITECTURE.md, "Known unknowns"). Region/replica/backup placement is excluded from this issue and awaits a human decision (CC-CMP-003/006).

## Implementation Notes
- **Constraints**: One Flexible Server, ten schemas, ten roles — the schema-per-context and role-per-context mapping mirrors ARCHITECTURE.md's bounded contexts exactly; the shared kernel stays minimal and owns no schema (ARCHITECTURE.md, Dependency rule 9). Money columns use exact-decimal/integer minor-unit types (CC-PRC-003); append-only audit table structure exists here but its INSERT-only privilege enforcement and WORM replication are issue 081. Each context module gets its own connection string (own role) resolved via Key Vault; TLS enforcement is a server-side parameter, complemented by verifying client connection settings.
- **Anti-Patterns**: MUST NOT share one database role or connection string across contexts (Secret handling rule 10); MUST NOT grant cross-schema privileges "temporarily" or for reporting shortcuts — cross-context reads happen through module APIs, not the database (ARCHITECTURE.md module boundaries); MUST NOT concatenate input into SQL — parameterized queries/safe ORM only (SECURITY.md, Input validation rule 4); MUST NOT expose a public database endpoint; MUST NOT apply grants or schema changes manually outside migrations (Deployment rule 5); MUST NOT decide or imply region/replica placement in this issue (open decision).
- **AI Development Guidance**: AI-generated migrations and data-access code pass the identical blocking gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Cross-schema grants in an AI-generated migration are a review-blocking defect.

## Open Questions
- **AT RISK (repeat):** primary-write-region vs. EU/India residency for personal-data writes, and backup/replica/telemetry residency placement, are open decisions (ARCHITECTURE.md, "Known unknowns"); this issue intentionally excludes them and no region choices may be inferred from it.
- The PostgreSQL TLS client verification level (channel encryption only vs. server-certificate verification) is not specified — SECURITY.md, Secret handling rule 10 says only "every data-store connection requires TLS"; confirm the required client `sslmode`-equivalent posture.
- Whether per-context database authentication should use Entra-integrated authentication for PostgreSQL versus vault-managed native roles is not specified (Secret handling rule 2 mandates managed identity for Azure SDK clients; database wire authentication is not an Azure SDK client call).
- The migration tooling/identity model (which principal runs DDL migrations, separate from runtime roles) is not specified.
- Customer-managed vs. service-managed keys for at-rest encryption is not specified (field-level encryption uses Key Vault-managed keys per Secret handling rule 6, but the server's at-rest key model is unstated).
