# 090 · Retention schedule and automated deletion jobs

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Backup and telemetry deletion/retention enforcement depends on open decisions — *Telemetry & backup residency* and *Data residency vs. "single primary write region"* (ARCHITECTURE.md, "Known unknowns"). The documented schedule, the primary-store/search-index deletion jobs, and the external-processor propagation can proceed; pinning backup/snapshot/replica and Log Analytics retention to a residency topology cannot be finalized until a human decides. Do not resolve these decisions in implementation.

## Metadata
- **ID**: CC-CMP-003
- **Title**: Retention schedule and automated deletion jobs
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The platform MUST document a data-minimization and retention schedule per data class (orders, marketing, employee, logs) and enforce it with automated deletion jobs whose deletion propagates to backups, read replicas, search indexes, telemetry, and external processors, reconciled against the legal-hold retention for immutable financial records (REQUIREMENTS.md CC-CMP-003).
- **Rationale**: CC-CMP-003 makes retention an enforced control, not a policy document: unenforced retention accumulates PII that enlarges breach impact and violates GDPR/DPDP minimization (CC-CMP-001/002). The 2026-07-15 threat model added the propagation clause — a backup or replica is never a residency, retention, or erasure loophole (SECURITY.md, Secret handling rule 6) — and the legal-hold reconciliation: erasure and retention jobs cannot mutate issued invoices or the 7-year financial audit (CC-INV-001, CC-DSH-004), and logs are explicitly in scope for retention (SECURITY.md, Logging rule 9).
- **Design**: N/A (non-UI issue).

## Scope
- **Applies To**: Both
- **Components**: All bounded contexts holding personal or retained data (Ordering & Payments, Invoicing, Back Office/employee records, Content & Localization/marketing, Identity & Access), PostgreSQL Flexible Server (per-context schemas, search indexes, backups/replicas), Azure Monitor/Log Analytics telemetry, external processors
- **Actors**: Scheduled system jobs (service identity), compliance/ops staff (schedule ownership, exception review), external processors
- **Data Classification**: Restricted/PII and Regulated (financial records under legal hold)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Data-minimization failure enlarging breach blast radius; residency/retention/erasure bypass via backups, replicas, or telemetry (SECURITY.md, Secret handling rule 6); destruction of legally required financial records (integrity threat against CC-INV-001/CC-DSH-004 records)
- **Trust Boundary**: Internal data plane (jobs run with per-context least-privilege roles); outbound boundary to external processors for propagation
- **Zero Trust Consideration**: Deletion jobs hold only the narrow per-context database privileges needed for their data class — no cross-schema grants (SECURITY.md, Secret handling rule 10); jobs authenticate to Azure with managed identity (SECURITY.md, Secret handling rule 2). Legal-hold protection is enforced by database privilege (INSERT-only on audit/invoice tables), not by job logic alone (SECURITY.md, Logging rule 6).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V14 Data Protection (data-at-rest lifecycle)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-9 (protection of audit information from deletion), AC-6 (least-privilege job roles), SI-12 (information management and retention)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR data minimization and storage limitation for ES/DE (CC-CMP-001); India DPDP and Japan APPI retention obligations per their assessment (CC-CMP-002, issue 091); 7-year financial audit retention (CC-DSH-004); immutable issued invoices (CC-INV-001); residency reconciliation (CC-CMP-006)
- **Other**: N/A

## Acceptance Criteria

1. **AC-01**: Given the documentation deliverable, when the issue closes, then a versioned retention schedule exists in the repository covering at minimum the data classes CC-CMP-003 names — orders, marketing, employee, logs — each with retention period, deletion method, propagation targets, and legal-hold exceptions (CC-CMP-003).
2. **AC-02**: Given a record whose retention period has elapsed and which is not under legal hold, when the scheduled deletion job runs, then the record is deleted from its primary PostgreSQL schema and its search-index entries, and the job emits a structured completion event with counts per data class (CC-CMP-003; CC-NFR-003).
3. **AC-03**: Given expired records exist at an external processor (payment: Stripe/Razorpay; email: Azure Communication Services; CMS: Contentful; carrier: EasyPost), when the deletion job runs, then propagation requests are issued to each processor and tracked to completion in a propagation ledger (CC-CMP-003).
4. **AC-04**: Given issued invoices or financial audit records inside the 7-year window, when any deletion job runs, then those records are NOT deleted or mutated (negative case) — the job's database role holds no DELETE/UPDATE privilege on audit and issued-invoice tables, and an attempted deletion is rejected by the database and logged as a security event (CC-INV-001, CC-DSH-004; SECURITY.md, Logging rule 6, CC-SEC-020).
5. **AC-05**: Given telemetry/log data past its documented retention, when the retention enforcement runs, then Log Analytics/Application Insights data ages out per the schedule (SECURITY.md, Logging rule 9) — with the residency pinning of that telemetry marked pending the open decision (AT RISK scope).
6. **AC-06**: Given a deletion job fails partway, when the run ends, then the job records exactly which targets remain undeleted, retries per policy, and MUST NOT report the data class as compliant while any leg is outstanding (negative case).
7. **AC-07**: Given backups, snapshots, and read replicas, when retention/deletion is enforced, then they inherit the same retention and deletion rules as their source (SECURITY.md, Secret handling rule 6) — with the concrete topology/mechanism explicitly deferred to the open residency decisions and represented as pending, not silently skipped (AT RISK scope; ARCHITECTURE.md, "Known unknowns").

## Failure Behavior
- **On Invalid Input**: N/A for public input (no public endpoint); job configuration is schema-validated at load — an invalid schedule configuration halts the job before any deletion occurs, logged with correlation ID.
- **On System Error**: Fail closed in both directions: on error the job neither deletes outside its validated scope nor marks incomplete work compliant. Any exception in legal-hold evaluation halts deletion for that batch (SECURITY.md, Logging rule 2 analogue: an exception in a protective path is never a bypass).
- **Alerting**: Alert on job failure, on propagation-ledger entries exceeding retry policy, and on any database-privilege violation from a deletion job touching audit/invoice tables (SECURITY.md, Logging rules 3, 8; Azure Monitor per ARCHITECTURE.md, Cross-cutting Observability).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit tests for retention-period evaluation per data class, legal-hold exclusion logic, propagation-ledger state machine, and schedule-configuration validation.
- **Integration Tests**: ASP.NET Core + PostgreSQL integration tests: seeded expired/unexpired/legal-hold records per data class, job run asserts correct deletions, search-index cleanup, and rejection of audit/invoice deletion at the database-privilege level (attempted DELETE as the job role fails).
- **Security Tests**: Privilege verification that each job role is confined to its context schema with no DELETE/UPDATE on audit/issued-invoice tables (SECURITY.md, Secret handling rule 10, Logging rule 6); secret-scan and SAST gates on job code (SECURITY.md, Deployment rule 7).
- **Compliance Tests**: Automated evidence collection: schedule document present and schema-valid in CI; per-run job reports archived; propagation-ledger completeness report; log-retention configuration validated against the documented schedule (SECURITY.md, Logging rule 9).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CMP-003 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 015 PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest; 081 Append-only audit store with WORM retention (the legal-hold surface deletion must respect); 046 Invoice core (immutability); 095 OpenTelemetry observability to Azure Monitor (telemetry stores in scope, SECURITY.md Logging rule 9); 091 DPDP/APPI/CCPA obligations assessment (may add per-market retention obligations to the schedule).
  **AT RISK (repeat):** backup/snapshot/replica and telemetry residency-and-retention topology depends on the open decisions *Telemetry & backup residency* and *Data residency vs. single primary write region* (ARCHITECTURE.md, "Known unknowns") — pending human decision.
- **Downstream**: 089 Data-subject rights endpoints (erasure reuses the deletion/propagation mechanics and the documented legal-hold exception); 094 Cross-border transfer mechanism documentation (residency reconciliation, CC-CMP-006); 088 EU consent management (consent-record retention entry in the schedule).
- **External**: Stripe, Razorpay, Azure Communication Services, Contentful, EasyPost (the processor classes CC-CMP-003 names: payment, email, CMS, carrier); Azure Database for PostgreSQL Flexible Server; Azure Monitor/Log Analytics.

## Implementation Notes
- **Constraints**: Jobs run in-cluster under the modular monolith's scheduling with managed identity (`DefaultAzureCredential`) — no embedded credentials (SECURITY.md, Secret handling rules 1–2); each data class's deletion executes under its owning context's least-privilege PostgreSQL role over TLS (SECURITY.md, Secret handling rule 10). Audit/invoice protection is a database-privilege fact (INSERT-only application roles, WORM replication) that the jobs inherit rather than re-implement (SECURITY.md, Logging rule 6, CC-SEC-020). Deletion events themselves are logged without reproducing the deleted PII (SECURITY.md, Logging rule 4).
- **Anti-Patterns**: MUST NOT enforce retention by convention or manual runbook — CC-CMP-003 requires automated jobs. MUST NOT grant a single "cleanup superuser" role across schemas. MUST NOT treat backups/replicas as out of scope — they are explicitly named propagation targets (a backup is never a retention or erasure loophole, SECURITY.md Secret handling rule 6) — but MUST NOT improvise their residency topology (open decision). MUST NOT delete or mutate issued invoices or 7-year financial audit records (CC-INV-001, CC-DSH-004).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must reference CC-CMP-003 (REQUIREMENTS.md §17).

## Open Questions
- Concrete retention periods per data class are not stated in the specs (only the 7-year financial audit window is ratified, CC-DSH-004); the schedule's numbers need human ratification.
- The mechanism for enforcing deletion within PostgreSQL Flexible Server backups/snapshots (age-out vs. selective techniques) is unspecified and entangled with the open residency decision — flagged AT RISK.
- Whether each external processor exposes a deletion API adequate for automated propagation (vs. documented manual process) is not established in the specs; per-processor capability needs confirmation alongside issue 094.
- CC-CMP-003 lists "orders, marketing, employee, logs" as the documented classes; whether consent records, contact-form submissions, and B2B partner data are additional classes needs human confirmation.
