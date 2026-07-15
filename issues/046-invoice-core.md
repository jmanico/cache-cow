# 046 · Invoice core: sequential numbering, immutability, credit notes

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** ARCHITECTURE.md "Known unknowns" — *Data residency vs. "single primary write region"*. Issued invoices persist EU and IN personal data (buyer name/address), and ARCHITECTURE.md states this conflict "blocks any implementation that persists EU or India personal data". Domain model, numbering, immutability grants, and tests can proceed; the production persistence topology awaits a human decision. Not resolved here.

## Metadata
- **ID**: CC-INV-001, CC-SEC-020
- **Title**: Invoice core: sequential numbering, immutability, credit notes
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: Invoices MUST be issued with sequential numbering per legal entity and, once issued, MUST be immutable — corrections occur only via credit notes — with immutability enforced at the database-privilege level (INSERT-only application roles on issued-invoice tables) (REQUIREMENTS.md CC-INV-001; SECURITY.md, Logging rule 6).
- **Rationale**: Invoices are legal financial records generated "per market legal requirements" (CC-INV-001, formats accepted as drafted 2026-07-15 with later legal review against implemented behavior — ARCHITECTURE.md decision record). The 2026-07-15 threat model added database-enforced immutability (CC-SEC-020): "append-only" MUST be enforced by database privilege, not convention, so no single compromised application or DBA credential can both write and erase history (SECURITY.md, Logging rule 6). Audit and invoices are append-only sinks — corrections are new records, never mutations (ARCHITECTURE.md, Dependency rule 6). Issued invoices are also the documented erasure exception under legal hold (CC-CMP-003).
- **Design**: N/A — server-side invoice domain. Invoice document presentation is issue 048 (consumer PDF) and issue 086 (dashboard module; print stylesheet per DESIGN.md §12). Note only: invoice numbers render in IBM Plex Mono on UI surfaces (DESIGN.md §4.1).

## Scope
- **Applies To**: API
- **Components**: Invoicing bounded context (ARCHITECTURE.md, "Server bounded contexts" item 7): invoice issuance, per-legal-entity number sequences, credit-note issuance, issued-invoice tables with INSERT-only grants.
- **Actors**: Ordering & Payments context (triggers consumer invoice issuance), Wholesale context (wholesale invoices per CC-WHS-004), finance staff via dashboard (issue 086), application database roles.
- **Data Classification**: Restricted/PII and Regulated (legal financial records containing buyer identity/address)

## Security Context
- **Defense Layer**: Architecture (append-only sink enforced below the application layer)
- **Threat(s) Addressed**: STRIDE Tampering and Repudiation — mutation or erasure of issued financial records by a compromised application credential, injection flaw, or insider (SECURITY.md, Logging rule 6; Secret handling rule 10: a flaw in one module must not reach the audit store or, by the same schema-isolation mechanism, issued invoices).
- **Trust Boundary**: Application-to-database privilege boundary: the application role physically lacks UPDATE/DELETE on issued-invoice tables.
- **Zero Trust Consideration**: Immutability does not trust application code to behave — it is enforced by PostgreSQL grants; the Invoicing context connects with its own least-privilege role confined to its own schema over TLS, with no cross-context grants (SECURITY.md, Secret handling rule 10; CC-SEC-021).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); Security Logging/Data Protection chapters (protection of records against modification).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-9 (protection of audit information — applied here to the financial record stream per SECURITY.md, Logging rule 6), AC-6 (least privilege database roles)
- **NIST SP 800-207**: Least-privilege, per-context data-plane access; no implicit trust in application-layer discipline.
- **Regulatory**: Per-market invoicing law per CC-INV-001 (drafted formats accepted 2026-07-15; later legal review runs against implemented behavior — ARCHITECTURE.md decision record). Per-market tax content is issue 047.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a legal entity, when invoices are issued, then each receives the next number in that legal entity's sequence — unique and monotonically increasing per legal entity, with no duplicates under concurrent issuance (CC-INV-001).
2. **AC-02**: Given an issued invoice, when the application role attempts UPDATE or DELETE on its row(s), then PostgreSQL rejects the statement with a privilege error — the role holds INSERT-only rights on issued-invoice tables (SECURITY.md, Logging rule 6; CC-SEC-020).
3. **AC-03**: Given an issued invoice requiring correction, when a correction is processed, then a credit note is created as a new record referencing the original invoice, and the original record is byte-for-byte unchanged (CC-INV-001; ARCHITECTURE.md, Dependency rule 6).
4. **AC-04**: Given invoice monetary amounts, when stored or computed, then they use the shared integer-minor-unit Money type with overflow-checked arithmetic — never binary floating point (CC-PRC-003; ARCHITECTURE.md, Dependency rule 9; issue 002).
5. **AC-05**: Negative: invoice line-item legal descriptions MUST NOT contain presentation-only promotion naming — "Eviction Specials" never appears in a line-item legal description (CC-PRC-007).
6. **AC-06**: Negative: no code path exposes an invoice "edit" operation; the only post-issuance write operations are credit-note issuance and other new compensating records (CC-INV-001; ARCHITECTURE.md, Dependency rule 6).
7. **AC-07**: Given a data-subject erasure request touching an issued invoice (CC-CMP-003), when processed, then the invoice record is not mutated — it falls under the documented legal-hold erasure exception (CC-CMP-003) — while the exception's retained fields are documented (documentation itself tracked under issue 090).

## Failure Behavior
- **On Invalid Input**: Invoice issuance requests failing schema validation are rejected with RFC 9457 problem details, logged with correlation ID, no internal state disclosed (SECURITY.md, Input validation rule 1; Logging rule 1; issue 021).
- **On System Error**: Fail closed — a failure while allocating a number or writing an invoice aborts issuance atomically; no partially issued invoice, no number allocated without a persisted invoice record where gaplessness could be legally required (see Open Questions).
- **Alerting**: Database privilege-denial events on issued-invoice tables (attempted UPDATE/DELETE) are logged and alerted as security events — they indicate a code defect or an attack (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for sequence allocation per legal entity, credit-note referencing, Money-type arithmetic on totals (overflow-checked), and the CC-PRC-007 description rule. Tagged CC-INV-001, CC-SEC-020 (REQUIREMENTS.md §17).
- **Integration Tests**: PostgreSQL-backed integration tests: concurrent issuance produces no duplicate numbers; UPDATE/DELETE by the application role fails at the database; credit-note flow leaves originals unchanged.
- **Security Tests**: SAST clean per merge gates; a dedicated test executes raw UPDATE/DELETE against issued-invoice tables under the application role and asserts privilege denial (CC-SEC-020). Mutation testing SHOULD run on this money-path code (CC-QA-001).
- **Compliance Tests**: Automated evidence that grants on issued-invoice tables are INSERT-only (schema/privilege assertion in CI against the migration output).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); money-path tests per CC-QA-004.
- **Note**: Tests MUST NOT use binary floating point for money either (CC-PRC-003 — "including tests").

## Dependencies
- **Upstream**: 001 (solution scaffold), 002 (shared Money type), 015 (PostgreSQL per-context schemas/roles, TLS, encryption at rest — the INSERT-only grants build on this), 035 (order state machine), 036 (order submission).
- **Downstream**: 047 (per-market invoice tax content renders onto this core), 048 (invoice PDF rendering and delivery), 052 (wholesale portal invoice history), 081 (WORM retention pipeline — replication of the financial stream to retention-locked storage per SECURITY.md, Logging rule 6, is implemented with the audit store there), 086 (dashboard invoice management), 090 (retention schedule documents the erasure exception).
- **External**: None (Stripe/Razorpay tax data enters via issue 047).
- **Open decision**: AT RISK on data residency vs. single primary write region (ARCHITECTURE.md, "Known unknowns") — see blockquote.

## Implementation Notes
- **Constraints**: Invoicing bounded context in the .NET 10 modular monolith; own PostgreSQL Flexible Server schema, own least-privilege role, TLS required (SECURITY.md, Secret handling rule 10). Enforce INSERT-only via `GRANT INSERT, SELECT` (no UPDATE/DELETE) to the application role on issued-invoice and credit-note tables in migrations. Sequential numbering per legal entity must be safe under concurrency (e.g., a per-entity allocation guarded by a transactional construct in PostgreSQL); do not use one global sequence across entities. Data access through parameterized queries/safe ORM APIs only (SECURITY.md, Input validation rule 4). Retention: issued invoices sit under the 7-year financial legal hold that overrides erasure (CC-CMP-003; CC-DSH-004).
- **Anti-Patterns**: MUST NOT enforce immutability by application convention alone (SECURITY.md, Logging rule 6); MUST NOT expose UPDATE/DELETE endpoints for issued invoices; MUST NOT store money as floats (CC-PRC-003); MUST NOT let presentation vocabulary leak into legal descriptions (CC-PRC-007); MUST NOT grant the Invoicing role access to any other context's schema (CC-SEC-021).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-INV-001 and CC-SEC-020 (REQUIREMENTS.md §17).

## Open Questions
- **The legal entities per market are not enumerated anywhere in the specs.** CC-INV-001 requires "sequential numbering per legal entity", but which legal entities exist (one per market? one per region? a single global entity?) is undefined. This is required configuration for the numbering model and must be supplied by a human — it is deliberately not invented here.
- Whether "sequential" means strictly *gapless* (a legal requirement in some markets) or merely unique-and-monotonic is not specified; gapless numbering materially changes the concurrency design (no discarded allocations).
- Whether numbering sequences reset (e.g., per fiscal year) per market law is not specified.
- The retention period for issued invoices themselves is implied by the 7-year financial audit window (CC-DSH-004) and the CC-CMP-003 legal hold, but is not explicitly stated per market; the documented retention schedule (issue 090) must confirm it.
