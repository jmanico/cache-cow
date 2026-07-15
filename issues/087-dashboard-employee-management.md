# 087 · Dashboard employee management (HR restriction, compensation encryption)

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Data residency vs. "single primary write region" (ARCHITECTURE.md, "Known unknowns"). This module persists employee personal data; for employees in the EU (ES/DE) and India, the unresolved conflict between the single-primary-write-region topology and the EU-in-EU / India-in-India residency rules (CC-CMP-001/002/006) affects where these records may be written. The module's authorization, encryption, and UI design can proceed; the persistence-region topology awaits a human decision.

## Metadata
- **ID**: CC-DSH-005, CC-DSH-003
- **Title**: Dashboard employee management (HR restriction, compensation encryption)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The dashboard employee management module MUST restrict employee records to the hr-admin role behind step-up re-authentication, MUST apply field-level encryption to compensation with Key Vault-managed keys, and MUST allow employee PII to leave the system only through the audited export function (REQUIREMENTS.md CC-DSH-005; SECURITY.md, Authentication and authorization rules 2, 8 and 12; Secret handling rule 6).
- **Rationale**: Employee management is one of the six launch dashboard modules (CC-DSH-003) and holds the platform's most sensitive internal PII. SECURITY.md restricts employee records to the hr-admin role and channels all PII egress through a single audited export function (Authentication rule 12); compensation gets field-level encryption with Key Vault-managed keys so a database-level compromise does not expose it in plaintext (Secret handling rule 6; CC-DSH-005, CC-SEC-008). Employee-record access is an enumerated sensitive action requiring re-authentication within the staff SSO/passkey session policy (Authentication rule 2; CC-DSH-001). Role-based views expose only the fields each role requires — "employee management especially" (Authentication rule 8; CC-DSH-002). A logic or injection flaw elsewhere (e.g., the contact form) must not reach employee compensation — per-context least-privilege database roles enforce this (Secret handling rule 10; CC-SEC-021).
- **Design**: DESIGN.md §12 — Pit theme, Archivo UI, IBM Plex Mono for every number, compact 40px rows, sticky headers, keyboard-first filtering; role-based views per SECURITY.md Authentication rule 8. Plain verbs, sentence case; zero puns in this module — employee data is not a comedy surface and the pun budget excludes safety/sensitive content by principle (DESIGN.md §5.4, §9).

## Scope
- **Applies To**: Both (dashboard Angular client; Back Office server endpoints)
- **Components**: Internal dashboard, Back Office bounded context — hr-restricted employee records (ARCHITECTURE.md, Server bounded contexts item 8); Azure Key Vault (encryption keys, issue 014); PostgreSQL field-level encryption (issue 015); append-only audit store (issue 081)
- **Actors**: hr-admin (sole role with employee-record access); all other staff roles (explicitly denied)
- **Data Classification**: Restricted/PII (employee records; compensation additionally field-level encrypted)

## Security Context
- **Defense Layer**: Architecture (role restriction + step-up re-auth + field-level encryption + single audited egress path)
- **Threat(s) Addressed**: Broken access control / insider access to HR data (OWASP Top Ten A01:2021); sensitive data exposure of compensation at rest (A02:2021 Cryptographic Failures); bulk PII exfiltration via unaudited exports; session riding into HR screens on an unattended staff session (mitigated by step-up re-auth); lateral movement from a flaw in another module into employee data (CC-SEC-021)
- **Trust Boundary**: Dashboard origin (separate origin, VPN-restricted, distinct session scope — SECURITY.md, HTTP boundary rule 8); the hr-admin role boundary inside the dashboard; the Back Office schema boundary (per-context least-privilege database role, Secret handling rule 10); the Key Vault key boundary for compensation fields
- **Zero Trust Consideration**: An authenticated 12-hour staff SSO session is not sufficient trust for employee records — access demands fresh re-authentication (SECURITY.md, Authentication rule 2); every request is re-authorized server-side against the hr-admin role (rule 8); compensation plaintext exists only transiently in the application after explicit decryption with Key Vault-managed keys, never at rest.

## Standards Alignment
- **OWASP ASVS**: V6 Authentication (re-authentication for sensitive actions); V8 Authorization (role restriction, least privilege); V11 Cryptography / V14 Data Protection (field-level encryption of compensation at rest) — under the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AU-2 (event logging), SC-28 (protection of information at rest)
- **NIST SP 800-207**: Per-request evaluation; session age alone never grants access to the most sensitive resource class (step-up)
- **Regulatory**: GDPR for EU (ES/DE) employee personal data (CC-CMP-001); DPDP for India employees, APPI for Japan (CC-CMP-002); employee data class in the documented retention schedule with automated deletion (CC-CMP-003)
- **Other**: FIDO2/WebAuthn — the staff SSO and step-up factor is passkey-based with userVerification required (SECURITY.md, Authentication rule 2)

## Acceptance Criteria
1. **AC-01**: Given an authenticated hr-admin within a valid staff SSO session, when they open employee records, then a step-up re-authentication is required first, and access is granted only after it succeeds (SECURITY.md, Authentication rule 2; CC-DSH-001; issue 060).
2. **AC-02**: Given an authenticated staff user with any role other than hr-admin (sales-viewer, ops-agent, finance, admin), when they request any employee-record endpoint, then the server returns 404 and logs the denial as a structured security event — employee records are restricted to the hr-admin role (SECURITY.md, Authentication rules 9 and 12; Logging rule 3; CC-DSH-005).
3. **AC-03**: Given compensation data at rest, when the database, its backups, or replicas are inspected directly, then compensation fields are present only as ciphertext under Key Vault-managed keys — no plaintext compensation exists at rest anywhere, backups and replicas included (SECURITY.md, Secret handling rule 6; CC-DSH-005, CC-SEC-008).
4. **AC-04**: Given an hr-admin needing employee PII outside the system, when they use the export function, then the export executes and writes an audit event (actor, action, object, timestamp) to the append-only store (SECURITY.md, Authentication rule 12; CC-DSH-004; issue 081).
5. **AC-05**: Given any other endpoint, log stream, or telemetry pipeline, then employee PII MUST NOT leave the system through it — the audited export function is the only egress path, and employee PII is redacted from logs and telemetry (SECURITY.md, Authentication rule 12; Logging rules 4 and 8).
6. **AC-06**: Given the module's screens, then each exposes only the fields the viewing role requires per the documented role-based view design (SECURITY.md, Authentication rule 8; CC-DSH-002) — compensation renders only where the view explicitly requires it, never in list/search results by default.
7. **AC-07**: Given any privileged action in the module (record view, edit, export), then an audit event is written to the append-only store (CC-DSH-004; SECURITY.md, Logging rule 6).
8. **AC-08**: Given any module response, then it carries `Cache-Control: no-store`, is served only on the dashboard origin with distinct session scope, and the module shares no code, cookies, or tokens with storefront or portal (SECURITY.md, HTTP boundary rules 3 and 8; ARCHITECTURE.md, Dependency rule 4).

## Failure Behavior
- **On Invalid Input**: Malformed requests rejected with 400 and RFC 9457 problem details plus correlation ID; requests for records outside the caller's authorization return 404 without confirming existence (SECURITY.md, Authentication rule 9; Logging rule 1).
- **On System Error**: Fail closed — any exception in the authorization, step-up, or decryption path is a denial, never a bypass; a failed Key Vault key retrieval means compensation is not displayed, never a plaintext fallback; an export whose audit write fails does not complete (SECURITY.md, Logging rule 2; CC-DSH-004).
- **Alerting**: Authz denials on employee endpoints, Key Vault access denials, and export events are logged as structured security events with alerting; authentication-failure spikes alert (SECURITY.md, Logging rules 3 and 8).

## Test Strategy
- **Unit Tests**: Role-gate logic (hr-admin only); step-up requirement evaluation; field-level encrypt/decrypt round-trip against a Key Vault test double; role-based view field filtering. Tagged CC-DSH-005.
- **Integration Tests**: ASP.NET Core integration tests: every non-hr-admin role receives 404 on every employee endpoint; step-up is demanded before first record access in a session; export produces an audit record; direct database read shows ciphertext-only compensation.
- **Security Tests**: AuthZ suite attempts cross-role access to employee records for every role and MUST fail closed (CC-QA-005; SECURITY.md, Authentication rules 8–9); SAST/secret-scan gates; verify no PII in structured logs via log-output assertions (Logging rules 4–5); DAST against staging covers the dashboard surface (CC-QA-007).
- **Compliance Tests**: Automated evidence of audit events for record access and exports (CC-DSH-004); configuration check that compensation columns are encrypted with Key Vault-managed keys and that backups/replicas inherit encryption (SECURITY.md, Secret handling rule 6); retention-schedule coverage of the employee data class (CC-CMP-003, with issue 090).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on the authz gate (CC-QA-001 — authz code); tests tagged CC-DSH-005 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 060 Staff SSO with mandatory WebAuthn and step-up re-authentication (the step-up mechanism); 080 Dashboard RBAC (hr-admin role and role-based views); 079 Dashboard shell; 081 Append-only audit store; 014 Azure Key Vault (Workload Identity, key access, rotation); 015 PostgreSQL per-context schemas/roles and encryption at rest; 020 Deny-by-default policy; 021 RFC 9457 error handling; 022 Structured security logging (PII redaction). **AT RISK:** the data-residency vs. single-primary-write-region open decision (ARCHITECTURE.md, "Known unknowns") governs where EU/India employee records may be persisted.
- **Downstream**: 090 Retention schedule and automated deletion jobs (employee data class, CC-CMP-003).
- **External**: Azure Key Vault (compensation keys); Microsoft Entra ID (staff SSO); Azure Database for PostgreSQL Flexible Server; Azure Monitor.

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith — employee records live in the Back Office context's own schema, reached only via its least-privilege database role over TLS with no cross-context grants (SECURITY.md, Secret handling rule 10; CC-SEC-021). hr-admin gate via a named `[Authorize(Policy=...)]` policy plus a step-up requirement (Authentication rules 1–2); `[AutoValidateAntiforgeryToken]` on state-changing requests (rule 11). Key Vault access via Azure Workload Identity with the Secrets Store CSI driver, managed identity SDK auth, TTL-bounded caching honoring rotation (Secret handling rules 2, 4–5). Compensation encryption keys are Key Vault-managed (rule 6). Export is a single server-side function that both emits the artifact and writes the audit event atomically — no export without audit.
- **Anti-Patterns**: MUST NOT expose employee data to any role but hr-admin, including admin, absent a ratified spec change (SECURITY.md, Authentication rule 12); MUST NOT store or log compensation in plaintext, including in backups, replicas, telemetry, or test fixtures (Secret handling rule 6; Logging rule 4); MUST NOT provide ad-hoc CSV/query exports outside the audited export function; MUST NOT treat the 12-hour session as sufficient for record access (step-up required, Authentication rule 2); MUST NOT cache employee responses anywhere (`no-store`, HTTP boundary rules 3 and 10); MUST NOT bind requests to entity models — dedicated DTOs only (Input validation rule 3).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Employee-data code paths warrant particularly careful human review given the Restricted/PII classification.

## Open Questions
- SECURITY.md Authentication rule 12 restricts employee records "to the hr-admin role"; whether the admin role has any employee-management capability (e.g., role assignment without record access) is not stated. Drafted as hr-admin only.
- The employee-record field set (beyond compensation being singled out for encryption) is not defined in the canonical docs — CC-DSH-003 names the module without a data model. Fields need a human/HR decision; only compensation's encryption treatment is specified.
- Whether viewing a record and exporting demand separate step-up ceremonies, and the step-up freshness window, are not specified beyond "re-auth for sensitive actions" (Authentication rule 2).
- Export format and destination controls (file download vs. other channels) are not specified; drafted as a server-generated artifact through the single audited function.
- Employee-data retention periods per CC-CMP-003 are to be documented in the retention schedule (issue 090), not decided here.
