# 086 · Dashboard invoice management module

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-003, CC-INV-001
- **Title**: Dashboard invoice management module
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The internal operations dashboard MUST provide an invoice management module offering invoice search and view plus credit-note issuance — corrections are new records, never mutations of issued invoices — role-gated, with every financial action audited, and with a print stylesheet in the consumer light theme (REQUIREMENTS.md CC-DSH-003, CC-INV-001, CC-DSH-004; ARCHITECTURE.md, Dependency rule 6; DESIGN.md §12).
- **Rationale**: Invoice management is one of the six launch dashboard modules (CC-DSH-003). Issued invoices are immutable legal documents with sequential numbering per legal entity and per-market tax content; corrections occur only via credit notes (CC-INV-001). Immutability is a threat-model-derived control enforced at database privilege, not convention: application roles hold INSERT-only rights on issued-invoice tables with WORM retention, so no single compromised credential can both write and erase financial history (CC-SEC-020; SECURITY.md, Logging rule 6). Financial audit events are retained 7 years (CC-DSH-004, ratified 2026-07-15). This module is the staff UI over the Invoicing bounded context (issues 046–048); it authors no invoice-format logic of its own.
- **Design**: DESIGN.md §12 — Pit theme for the working UI; invoices get a print stylesheet in the consumer light theme so paper invoices are Paper, not Pit; IBM Plex Mono for every number (invoice and order numbers, amounts, dates in tables), right-aligned numerals, units in column headers; compact 40px rows, sticky headers, keyboard-first filtering. Monetary display is locale-formatted per DESIGN.md §4.4, never hand-formatted (CC-PRC-004). Clearance naming ("Eviction Specials") never appears in invoice line-item legal descriptions (CC-PRC-007).

## Scope
- **Applies To**: Both (dashboard Angular client; Back Office + Invoicing server endpoints)
- **Components**: Internal dashboard (Back Office bounded context, ARCHITECTURE.md item 8); Invoicing bounded context (item 7; issues 046–048 own generation, tax content, and PDF rendering); append-only audit store (issue 081)
- **Actors**: Authenticated internal staff — finance role per CC-DSH-002 (exact matrix owned by issue 080)
- **Data Classification**: Regulated (legal financial records with market tax content); customer billing details are Restricted/PII

## Security Context
- **Defense Layer**: Strict API (server-side RBAC; database-privilege-enforced immutability)
- **Threat(s) Addressed**: Tampering with financial records (STRIDE: Tampering; mitigated by INSERT-only grants and WORM retention, CC-SEC-020); broken access control on financial data (OWASP Top Ten A01:2021); repudiation of financial actions (mitigated by the 7-year financial audit trail, CC-DSH-004)
- **Trust Boundary**: Dashboard origin (separate origin, VPN-restricted, distinct session scope — SECURITY.md, HTTP boundary rule 8); the database-privilege boundary making issued-invoice tables INSERT-only to application roles (SECURITY.md, Logging rule 6)
- **Zero Trust Consideration**: No client-supplied value can alter an issued invoice; credit-note content is recomputed and validated server-side from canonical invoice data, with server-controlled fields (numbering, actor, timestamps) set from server state only (SECURITY.md, Input validation rule 3); the UI is display-only over typed, validated responses (Input validation rule 1).

## Standards Alignment
- **OWASP ASVS**: V8 Authorization (role gating of financial functions); V16 Security Logging and Error Handling (append-only audit of financial actions) — under the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AU-9 (protection of audit information — INSERT-only/WORM)
- **NIST SP 800-207**: Per-request authorization on every financial endpoint
- **Regulatory**: Per-market invoice law as named in CC-INV-001 — US sales tax, EU VAT (rates, USt-IdNr.), JP consumption tax with qualified-invoice number, IN GST with GSTIN and HSN codes; GDPR for EU customer billing data (CC-CMP-001); erasure requests cannot mutate issued invoices — legal-hold exception per CC-CMP-003
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a staff user with the finance role (per the issue 080 matrix), when they use the invoice module, then they can search issued invoices and view any invoice rendered from the Invoicing context's structured data, including its market tax content and sequential number (CC-DSH-003, CC-INV-001; issues 046–047).
2. **AC-02**: Given a viewed invoice requiring correction, when the finance user issues a credit note, then a new sequentially numbered record is created referencing the original invoice, and the original invoice record remains byte-for-byte unchanged (CC-INV-001; ARCHITECTURE.md, Dependency rule 6).
3. **AC-03**: Given any module endpoint or UI action, then no path exists to UPDATE or DELETE an issued invoice; the application's database role holds INSERT-only privileges on issued-invoice tables, so an attempted mutation fails at the database even if application logic is bypassed (SECURITY.md, Logging rule 6; CC-SEC-020).
4. **AC-04**: Given a credit-note issuance (or any privileged action in the module), then an audit event with actor, action, object, before/after, and timestamp is written to the append-only store under the 7-year financial retention (CC-DSH-004; SECURITY.md, Logging rule 6; issue 081).
5. **AC-05**: Given an invoice open in the Pit-themed dashboard, when the user prints it, then the print stylesheet renders the consumer light theme — Paper background, Char text — not Pit (DESIGN.md §12).
6. **AC-06**: Given any displayed monetary value, then it renders locale-formatted from the canonical integer-minor-unit data in IBM Plex Mono — hand-formatted currency strings are a defect (CC-PRC-003, CC-PRC-004; DESIGN.md §4.4, §12).
7. **AC-07**: Given a staff user without the finance role, when they request any invoice-module endpoint, then the server returns 404 and logs the denial as a structured security event (SECURITY.md, Authentication rules 8–9; Logging rule 3; CC-DSH-002).
8. **AC-08**: Given any module response, then it carries `Cache-Control: no-store`, is served only on the dashboard origin, and errors return RFC 9457 problem details with no internal state (SECURITY.md, HTTP boundary rules 3 and 8; Logging rule 1).

## Failure Behavior
- **On Invalid Input**: Invalid search parameters or credit-note requests rejected with 400 and RFC 9457 problem details plus correlation ID; a credit-note request against a non-existent or inaccessible invoice returns 404 (SECURITY.md, Authentication rule 9; Logging rule 1).
- **On System Error**: Fail closed — an exception in the authorization path is a denial (SECURITY.md, Logging rule 2); a credit-note issuance that fails mid-flight leaves no partial record and no unaudited state change (the action fails if its audit write fails, CC-DSH-004). The module never falls back to mutating an invoice when credit-note creation fails.
- **Alerting**: Authz denials and any database-level rejection of an attempted invoice mutation are logged as structured security events with alerting (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: Credit-note creation logic (new record, reference to original, server-set numbering and actor); search/filter logic; locale-aware money formatting from integer minor units in all five currencies including JPY zero-decimal and INR grouping (CC-QA-004). Tagged CC-INV-001, CC-DSH-003.
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL: issuing a credit note leaves the original invoice unchanged; direct UPDATE/DELETE attempts on issued-invoice tables under the application role fail with a privilege error; audit event emitted per financial action; role gating per role.
- **Security Tests**: AuthZ suite attempts cross-role and IDOR access to invoices (CC-QA-005; SECURITY.md, Authentication rules 8–9); mutation testing SHOULD run on this money-path code (CC-QA-001); SAST/SCA/secret-scan gates (Deployment rule 7).
- **Compliance Tests**: Automated evidence that financial actions produce 7-year-retained audit events (CC-DSH-004); database-privilege configuration check that issued-invoice tables are INSERT-only to application roles (CC-SEC-020).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); Angular component tests including the print stylesheet; tests tagged with CC-* IDs (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 046 Invoice core: sequential numbering, immutability, credit notes (the domain this UI drives); 047 Per-market invoice tax content; 048 Invoice PDF rendering; 079 Dashboard shell; 080 Dashboard RBAC (finance role); 081 Append-only audit store with WORM retention; 015 PostgreSQL per-context schemas/roles (INSERT-only grants); 020 Deny-by-default policy; 021 RFC 9457 error handling; 022 Structured security logging; 002 Shared kernel Money type; 005 Design token pipeline.
- **Downstream**: 090 Retention schedule and automated deletion jobs (must honor the issued-invoice/financial-audit legal-hold erasure exception, CC-CMP-003).
- **External**: Azure Database for PostgreSQL Flexible Server; Azure Monitor (ARCHITECTURE.md, Technology decisions).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith — the dashboard drives the Invoicing context through its module boundary; Invoicing owns its schema with its own least-privilege role over TLS (SECURITY.md, Secret handling rule 10). Named `[Authorize(Policy=...)]` policies; `[AutoValidateAntiforgeryToken]` on state-changing dashboard requests (Authentication rule 11). Credit notes are new INSERTs referencing the original — model them as compensating records, matching the INSERT-only grant (Logging rule 6). Money uses the shared-kernel integer-minor-unit type with overflow-checked arithmetic (CC-PRC-003); display formatting via `Intl.NumberFormat`/server equivalent only (CC-PRC-004). Allowlist sort/filter column names (Input validation rule 4).
- **Anti-Patterns**: MUST NOT expose any UPDATE/DELETE path for issued invoices (CC-INV-001; ARCHITECTURE.md, Dependency rule 6); MUST NOT grant the application role UPDATE/DELETE on issued-invoice or audit tables (SECURITY.md, Logging rule 6); MUST NOT hand-format currency or use binary floating point anywhere including tests (CC-PRC-003/004); MUST NOT let "Eviction Specials" naming appear in invoice line-item legal descriptions (CC-PRC-007); no puns anywhere in this module — comedy never touches money movement (DESIGN.md §5.4); MUST NOT log customer billing PII un-redacted (Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- SECURITY.md Authentication rule 2 lists step-up re-auth actions as "refunds, employee-record access, role changes"; whether credit-note issuance (financially analogous to a refund) also requires step-up re-auth is not stated. Not assumed either way.
- Whether the admin role, in addition to finance, may issue credit notes is not specified; the role–permission matrix is owned by issue 080.
- Search facets (invoice number, market, partner, customer, date range, order reference) are not enumerated in the canonical docs; drafted minimally, to be confirmed with operations.
- Whether refunds initiated in order management (issue 082, CC-DSH-003) automatically trigger credit-note creation, or credit notes are always manually issued here, is not specified.
