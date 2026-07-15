# 081 · Append-only audit store with WORM retention

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Telemetry & backup residency (ARCHITECTURE.md, "Known unknowns"). The append-only audit tables, INSERT-only grants, and event schema can proceed; the region/residency zone in which the WORM-replicated audit stream is stored is entangled with the undecided residency topology for logs and backups (CC-CMP-003/006) and needs a human decision before the replication target is provisioned.

## Metadata
- **ID**: CC-DSH-004, CC-SEC-020
- **Title**: Append-only audit store with WORM retention
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every privileged dashboard action and every order state transition MUST be written as an audit event (actor, action, object, before/after, timestamp) to an audit store whose append-only property is enforced by database privilege — application roles hold INSERT-only rights, no UPDATE/DELETE — and whose financial audit stream is replicated to retention-locked (WORM) storage for the 7-year retention window.
- **Rationale**: CC-DSH-004 mandates audit of every privileged action with 7-year retention for financial actions (ratified 2026-07-15); CC-ORD-006 mandates every order state transition be appended to the audit log. CC-SEC-020 and SECURITY.md, Logging rule 6 (threat-model-derived, v1.1) require "append-only" to be enforced by database privilege, not convention, and the audit/financial stream replicated to WORM storage so no single compromised application or DBA credential can both write and erase history. ARCHITECTURE.md ("Cross-cutting", Data stores) confirms audit tables are INSERT-only at the database-privilege level with WORM retention on Azure Database for PostgreSQL Flexible Server. CC-CMP-003 reconciles this legal-hold retention against data-subject erasure: erasure requests cannot mutate the immutable financial records, and the retained fields are the documented erasure exception.
- **Design**: N/A (non-UI; storage and write-path infrastructure. Audit-consuming screens live in issues 082–087).

## Scope
- **Applies To**: Both (written by dashboard endpoints and by the Ordering context's state machine)
- **Components**: Back Office bounded context's append-only audit store (ARCHITECTURE.md, "Server bounded contexts" 8): audit table schema and event-writing API for other contexts; PostgreSQL privilege configuration (INSERT-only grants for application roles); WORM replication of the financial audit stream; retention configuration (7 years, financial actions). Explicitly excluded: issued-invoice table immutability and credit notes (issue 046 applies the same Logging rule 6 mechanism to invoices); the order state machine itself (issue 035 — it calls this store); dashboard modules that emit events (issues 082–087); general security/application logging (issue 022); retention/deletion jobs for non-audit data classes (issue 090).
- **Actors**: Bounded-context services as writers (Back Office, Ordering & Payments, Fulfillment); internal staff and auditors as readers via role-gated dashboard views (issue 080); no actor holds UPDATE/DELETE on audit tables.
- **Data Classification**: Regulated (financial audit trail; contains actor identity and object references — Restricted/PII handling applies to event content)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Repudiation and audit-trail tampering (STRIDE: Repudiation, Tampering); an attacker or malicious insider with application or DBA credentials erasing evidence after fraudulent refunds, role changes, or order manipulation (CC-SEC-020; CWE-778 insufficient logging, AU-9-class audit-protection failures)
- **Trust Boundary**: The database-privilege boundary between application roles and the audit tables, and the replication boundary between the primary store and retention-locked WORM storage — two independent controls so one compromised credential cannot both write and erase (SECURITY.md, Logging rule 6)
- **Zero Trust Consideration**: The application is not trusted to preserve history by convention; immutability is enforced by PostgreSQL grants (INSERT-only) and by storage-level retention lock, both independent of application code correctness.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V16 Security Logging and Error Handling (protected, complete audit trails) — platform baseline ASVS 5.0 Level 2 (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-2 (event logging), AU-9 (protection of audit information), AU-11 (audit record retention), AC-6 (least privilege on audit tables)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR reconciliation — data-subject erasure cannot mutate immutable financial records; retained fields documented as the erasure exception (CC-CMP-003)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any privileged dashboard action or order state transition, when it executes, then an audit event containing actor, action, object, before/after state, and timestamp is appended to the audit store within the same unit of work, so no covered action completes without its audit record (CC-DSH-004; CC-ORD-006; SECURITY.md, Logging rule 6).
2. **AC-02**: Given the PostgreSQL privilege configuration, when the grants on audit tables are inspected for every application role, then each holds INSERT (and read where the matrix permits) only — no UPDATE, DELETE, or TRUNCATE — and no application role can alter the grants (CC-SEC-020; SECURITY.md, Logging rule 6; ARCHITECTURE.md, "Cross-cutting", Data stores).
3. **AC-03**: Given any application role's connection, when an UPDATE, DELETE, or TRUNCATE is attempted against an audit table, then PostgreSQL rejects it with a privilege error, the attempt is logged as a security event, and the stored events are unchanged (negative case; CC-SEC-020; SECURITY.md, Logging rule 3).
4. **AC-04**: Given the financial audit stream, when events are written, then they are replicated to retention-locked (WORM) storage configured for the 7-year retention window, and the retention lock cannot be shortened or removed by application or DBA credentials (CC-DSH-004, ratified 2026-07-15; CC-SEC-020; SECURITY.md, Logging rule 6).
5. **AC-05**: Given a data-subject erasure request touching data referenced by immutable financial audit records, when erasure executes, then the audit records are not mutated and the retained fields match the documented erasure exception (CC-CMP-003).
6. **AC-06**: Given the audit write path, when an event is composed, then it contains no credentials, tokens, or PANs, user-supplied values are encoded/sanitized against log injection, and events are structured (no string interpolation into messages) (SECURITY.md, Logging rules 4–5).
7. **AC-07**: Given a correction to an audited fact (e.g., a reversed transition), when it is recorded, then it is a new compensating event, never a mutation of an existing record (ARCHITECTURE.md, Dependency rule 6; SECURITY.md, Logging rule 6).

## Failure Behavior
- **On Invalid Input**: An audit event failing schema validation is rejected and the invoking action fails with a generic RFC 9457 error and correlation ID; no partial or malformed event is stored (SECURITY.md, Input validation rule 1; Logging rule 1).
- **On System Error**: Fail closed — if the audit append cannot be committed, the covered privileged action or state transition does not commit either (audit is on authorization/money paths; SECURITY.md, Logging rule 2). Replication lag to WORM storage does not block the primary write but is monitored and alerted.
- **Alerting**: Alerts on: rejected UPDATE/DELETE attempts against audit tables, audit-write failures, and WORM replication failures or lag beyond threshold — structured security events to centralized monitoring (SECURITY.md, Logging rule 3; CC-SEC-010, CC-NFR-003).

## Test Strategy
- **Unit Tests**: Event schema composition (actor/action/object/before-after/timestamp), log-injection encoding, compensating-event semantics. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: Against PostgreSQL: privileged action + audit append commit atomically; audit append failure rolls back the action; UPDATE/DELETE/TRUNCATE under each application role fails with a privilege error (AC-03); erasure-exception behavior (AC-05).
- **Security Tests**: Privilege-matrix verification test enumerating grants on audit tables for every context role (composes with issue 015); SAST/CI gates per SECURITY.md, Deployment rule 7; audit coverage assertions inside the authz and money-path suites (issues 062, 099).
- **Compliance Tests**: Automated evidence: grant configuration snapshot, WORM retention-lock configuration, presence of audit events for each covered action class — retained as CI/audit evidence (CC-DSH-004).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-DSH-004, CC-SEC-020, CC-ORD-006 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (Back Office module); 015 "PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest" (the roles whose grants this issue narrows, CC-SEC-021); 022 "Structured security logging, PII redaction, log-injection prevention, alerting" (shared logging discipline). **AT RISK:** Telemetry & backup residency decision (ARCHITECTURE.md, "Known unknowns") gates the WORM replication target's region.
- **Downstream**: 035 "Order state machine with audited transitions" (CC-ORD-006 writes here); 044 "Regional cold-store order routing with audited cross-region override" (CC-FUL-001); 046 "Invoice core: sequential numbering, immutability, credit notes" (same Logging rule 6 mechanism on invoice tables); 060 "Staff SSO" (re-auth events); 080 "Dashboard RBAC" (role changes audited); 082–087 dashboard modules (privileged actions write here); 087 "Dashboard employee management" (audited PII export, CC-DSH-005).
- **External**: Microsoft Azure — Azure Database for PostgreSQL Flexible Server (ARCHITECTURE.md, "Technology decisions"); the WORM storage target is an Azure service to be named (see Open Questions).

## Implementation Notes
- **Constraints**: Azure Database for PostgreSQL Flexible Server hosts the audit tables in the Back Office schema; each writing context connects with its own least-privilege TLS role (SECURITY.md, Secret handling rule 10; ARCHITECTURE.md, "Cross-cutting"); INSERT-only via PostgreSQL `GRANT INSERT` (no UPDATE/DELETE/TRUNCATE) enforced in the issue 015 role provisioning; timestamps and actor identity set from server state only (SECURITY.md, Input validation rule 3); audit data is in scope for the retention schedule — 7 years financial, per-class otherwise (CC-CMP-003, issue 090).
- **Anti-Patterns**: MUST NOT enforce append-only by application convention, triggers alone, or code review — database privilege is the control (CC-SEC-020); MUST NOT mutate or delete audit records for corrections — compensating events only (ARCHITECTURE.md, Dependency rule 6); MUST NOT log credentials, tokens, capability tokens, or PANs in audit events (SECURITY.md, Logging rule 4; Authentication rule 14); MUST NOT give any single credential both write access and retention-lock control (SECURITY.md, Logging rule 6); MUST NOT interpolate user input into event text (SECURITY.md, Logging rules 4–5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- **The WORM storage service is not named in the specs.** SECURITY.md, Logging rule 6 requires replication to "retention-locked (WORM) storage" and ARCHITECTURE.md confirms Azure, but no confirmed Azure service is designated for the retention-locked store. Naming it requires a human decision; acceptance criteria therefore assert the retention-lock property, not a product.
- The residency zone for the WORM replica (and whether it follows the EU-in-EU / India-in-India rules for audit events referencing EU/IN subjects) is entangled with the open "Telemetry & backup residency" decision (ARCHITECTURE.md, "Known unknowns") — see the AT RISK banner.
- The specs define the financial-stream retention (7 years) but not the retention period for non-financial audit events; that belongs to the CC-CMP-003 retention schedule (issue 090) and is not asserted here.
- The exact catalog of "privileged" dashboard actions is implied by the dashboard modules and the role–permission matrix (issue 080) rather than enumerated in the specs; each module issue must declare its audited actions against this store.
