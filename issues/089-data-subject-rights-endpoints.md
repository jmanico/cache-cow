# 089 · Data-subject rights endpoints

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Deletion propagation touches backups, read replicas, and telemetry whose residency topology is an open decision (ARCHITECTURE.md, "Known unknowns": *Data residency vs. "single primary write region"* and *Telemetry & backup residency*). Access/portability endpoints and the primary-store deletion path can proceed; the backup/replica/telemetry propagation legs cannot be finalized until a human decides the topology. Do not resolve these decisions in implementation.

## Metadata
- **ID**: CC-CMP-001
- **Title**: Data-subject rights endpoints
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The platform MUST provide data-subject rights endpoints for access, deletion, and portability with identity verification, and erasure MUST reconcile with the legal-hold retention that overrides erasure for immutable financial records (REQUIREMENTS.md CC-CMP-001, CC-CMP-003).
- **Rationale**: GDPR applies to ES/DE and EU visitors and mandates data-subject rights with identity verification (REQUIREMENTS.md CC-CMP-001). CC-CMP-003 requires that deletion propagate to backups, read replicas, search indexes, telemetry, and external processors, and that erasure be reconciled against legal hold: a data-subject erasure request cannot mutate issued invoices or the 7-year financial audit (CC-INV-001, CC-DSH-004), and the retained fields must be documented as the erasure exception. Without identity verification, the rights endpoints themselves become an exfiltration or malicious-deletion vector.
- **Design**: N/A (non-UI issue; any account-settings entry point that surfaces these requests follows DESIGN.md §9 voice and §13 accessibility, but the UI surface is not in this issue's scope).

## Scope
- **Applies To**: Both
- **Components**: A data-subject-rights capability spanning bounded contexts that hold consumer personal data — Ordering & Payments, Invoicing, Identity & Access, Content & Localization (consent/marketing records), Catalog-adjacent behavioral data — plus orchestration of external-processor requests (ARCHITECTURE.md, Server bounded contexts)
- **Actors**: Authenticated consumer, guest data subject (identity-verified), internal ops/compliance staff (audited handling), external processors (Stripe, Razorpay, EasyPost, Contentful, Azure Communication Services)
- **Data Classification**: Restricted/PII

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: OWASP API Security Top 10 — broken object-level authorization (a rights endpoint that returns or deletes another subject's data is a mass-exfiltration/denial primitive); user enumeration via the request flow; malicious erasure of financial records required for legal hold (integrity of CC-INV-001/CC-DSH-004 records)
- **Trust Boundary**: Public gateway edge for request intake; internal context boundaries for fan-out; outbound boundary to external processors
- **Zero Trust Consideration**: Every rights request is unauthenticated-hostile until identity is verified; the subject's identity, not client-supplied identifiers, scopes every query (SECURITY.md, Authentication rule 9). Export and deletion actions are audited (SECURITY.md, Logging rule 6).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V4 Access Control (object-level authorization on subject data); V6 Authentication (identity verification before disclosure or destruction)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3, AC-6 (least-privilege access to subject data), AU-9 (protection of audit information from erasure)
- **NIST SP 800-207**: Per-request identity verification before resource access
- **Regulatory**: GDPR data-subject rights — access, deletion, portability with identity verification (REQUIREMENTS.md CC-CMP-001); legal-hold erasure exception for immutable financial records (CC-CMP-003, CC-INV-001, CC-DSH-004)
- **Other**: N/A

## Acceptance Criteria

1. **AC-01**: Given a data subject with verified identity, when they submit an access or portability request, then the response compiles that subject's personal data from every bounded context that holds it, in a structured machine-readable export for portability, and the fulfillment event is written to the audit log (CC-CMP-001; SECURITY.md, Logging rule 6).
2. **AC-02**: Given a rights request whose identity verification has not completed, when access, deletion, or portability is attempted, then the request is refused (HTTP 403 or the flow's pending state), no personal data is disclosed or deleted, and responses do not reveal whether the identifier maps to a data subject (CC-CMP-001; enumeration-safety consistent with SECURITY.md, Authentication rule 13).
3. **AC-03**: Given a verified erasure request, when it executes, then personal data in primary stores is deleted or anonymized for every in-scope data class, and propagation actions are initiated for search indexes and external processors (payment, email, CMS, carrier) with per-target completion tracked (CC-CMP-003).
4. **AC-04**: Given a verified erasure request from a subject with issued invoices or 7-year financial audit records, when erasure executes, then those records are NOT mutated or deleted (negative case), the retained fields match the documented erasure-exception record, and the subject-facing outcome states that legal-hold data was retained (CC-CMP-003, CC-INV-001, CC-DSH-004; SECURITY.md, Logging rule 6 — INSERT-only audit/invoice tables make mutation impossible at the database-privilege level).
5. **AC-05**: Given any rights request, when a caller attempts to target a different subject's data (parameter tampering, IDOR), then the attempt fails closed with 404/403, is logged as an authz denial, and no cross-subject data is returned or deleted (negative case; SECURITY.md, Authentication rule 9; CC-QA-005).
6. **AC-06**: Given an erasure has completed its primary-store leg, when the propagation ledger is inspected, then backup/replica/telemetry legs are represented as explicitly pending-on-open-decision states — not silently marked complete (AT RISK scope; ARCHITECTURE.md, "Known unknowns").
7. **AC-07**: Given the erasure-exception documentation deliverable, when the issue closes, then the retained-field inventory (which fields, which record types, which retention authority: CC-INV-001 invoices, CC-DSH-004 financial audit) exists in the repository and is versioned (CC-CMP-003).

## Failure Behavior
- **On Invalid Input**: HTTP 400 with RFC 9457 problem details; schema-validated DTO binding with explicit source attributes; no internal identifiers or state disclosed (SECURITY.md, Input validation rules 1–3; Logging rule 1).
- **On System Error**: Fail closed — any exception in identity verification or authorization is a denial, never a bypass (SECURITY.md, Logging rule 2). A partially failed erasure fan-out records per-target failure and retries; it never reports success for an incomplete propagation.
- **Alerting**: Authz denials and verification failures logged as structured security events with alerting on spikes (SECURITY.md, Logging rules 3, 8); erasure fan-out failures to external processors alert compliance/ops via centralized monitoring (Azure Monitor per ARCHITECTURE.md).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit tests for identity-verification gating, per-context data-collection assembly, erasure-scope computation including the legal-hold carve-out (invoice/audit fields excluded), and propagation-ledger state transitions.
- **Integration Tests**: ASP.NET Core integration tests spanning contexts: end-to-end access/portability export; erasure against a subject with issued invoices asserting invoices and audit rows are untouched (database INSERT-only privileges verified to reject UPDATE/DELETE, SECURITY.md Logging rule 6); search-index deletion propagation against PostgreSQL full-text.
- **Security Tests**: Cross-tenant/IDOR suite attempts on the rights endpoints (CC-QA-005, issue 062); enumeration-safety timing/response-parity checks on the request-intake flow; DAST coverage of the endpoints (CC-QA-007).
- **Compliance Tests**: Automated evidence that every fulfillment writes an audit event; presence and schema-validity of the erasure-exception documentation; propagation-ledger completeness report per request.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CMP-001, CC-CMP-003 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 058 Consumer authentication (identity verification for account holders); 062 Object-level authorization and cross-tenant/IDOR test suite; 081 Append-only audit store with WORM retention (legal-hold enforcement surface); 046 Invoice core (immutability the erasure must not violate); 090 Retention schedule and automated deletion jobs (shared deletion mechanics and per-data-class inventory); 015 PostgreSQL Flexible Server (per-context schemas/roles).
  **AT RISK (repeat):** backup/replica/telemetry propagation legs depend on the open decisions *Data residency vs. single primary write region* and *Telemetry & backup residency* (ARCHITECTURE.md, "Known unknowns") — blocked pending human decision.
- **Downstream**: 094 Cross-border transfer mechanism documentation (processor erasure propagation intersects transfer documentation, CC-CMP-006).
- **External**: Stripe, Razorpay (payment), Azure Communication Services (email), Contentful (CMS), EasyPost (carrier) — the external processors CC-CMP-003 names for deletion propagation; Microsoft Entra External ID (consumer identity).

## Implementation Notes
- **Constraints**: Endpoints are deny-by-default under the fallback authorization policy with named `[Authorize(Policy=...)]` policies (SECURITY.md, Authentication rule 1); guest-subject verification cannot reuse the consumer session and must not weaken to guessable identifiers (cf. the CC-ORD-010 prohibition on order-number-plus-email as an access credential). Exports leave the system only through audited paths (SECURITY.md, Authentication rule 12 pattern). Portability exports and access responses are `Cache-Control: no-store` (SECURITY.md, HTTP boundary rule 3). Erasure uses each context's own least-privilege database role — the rights capability gets no cross-schema superrole (SECURITY.md, Secret handling rule 10).
- **Anti-Patterns**: MUST NOT mutate or delete issued invoices or financial audit records in any erasure path (CC-CMP-003, CC-INV-001; SECURITY.md, Logging rule 6). MUST NOT verify identity via knowledge of enumerable order data. MUST NOT report erasure complete while external-processor or (once unblocked) backup/replica/telemetry legs are outstanding. MUST NOT log exported personal data or tokens (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must reference CC-CMP-001/CC-CMP-003 (REQUIREMENTS.md §17).

## Open Questions
- Identity-verification method and strength for data subjects without accounts (guests) is not specified in CC-CMP-001 — needs human definition.
- Statutory response deadlines/SLAs for fulfilling requests are not stated in the specs; the workflow needs human-ratified timelines.
- Whether rights requests are intake-only via API endpoints or also via a storefront UI surface (UI not specced here) — the endpoint contract is in scope; the UI owner is unassigned.
- The exact anonymize-vs-delete treatment per data class is not defined; it depends on the CC-CMP-003 retention schedule (issue 090) and must not be improvised.
- Backup/replica/telemetry propagation mechanics are blocked on the residency-topology decisions (ARCHITECTURE.md, "Known unknowns") — flagged AT RISK above.
