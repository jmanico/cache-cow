# 085 · Dashboard partner management module

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-003, CC-WHS-002
- **Title**: Dashboard partner management module
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The internal operations dashboard MUST provide a partner (wholesale) management module that executes the partner onboarding approval workflow — no self-service activation — over partner records carrying per-market business identity (e.g., USt-IdNr. in DE, GSTIN in IN) and per-partner wholesale terms (net-60 default, adjustable), with every action audited and role-gated (REQUIREMENTS.md CC-DSH-003, CC-WHS-002, CC-WHS-004, CC-DSH-004).
- **Rationale**: Partner (wholesale) management is one of the six launch dashboard modules (CC-DSH-003). Partner onboarding MUST be an approval workflow executed in the internal dashboard with no self-service activation (CC-WHS-002) — approval is the gate that grants a business access to wholesale price lists and case-quantity ordering (CC-WHS-001), data that must stay invisible to consumer sessions (CC-WHS-003). Wholesale terms default to net-60 (ratified 2026-07-15) and are adjustable per partner (CC-WHS-004). Every privileged dashboard action writes an audit event (CC-DSH-004; SECURITY.md, Logging rule 6).
- **Design**: DESIGN.md §12 — Pit background, Pitpaper text, Archivo UI, IBM Plex Mono for every number (partner IDs, terms, dates in tables), compact 40px rows, sticky headers, keyboard-first filtering; status colors one accent family per meaning (cache-green good states, Ember alerts, Smoke neutral). Tokens from `tokens.json` only (ARCHITECTURE.md, Dependency rule 8).

## Scope
- **Applies To**: Both (dashboard Angular client; Back Office + Wholesale & B2B server endpoints)
- **Components**: Internal dashboard (Back Office bounded context, ARCHITECTURE.md item 8); Wholesale & B2B API context's partner tenancy and onboarding workflow (item 6; issue 049 — this issue is its dashboard UI surface); append-only audit store (issue 081)
- **Actors**: Authenticated internal staff (role per the issue 080 matrix); wholesale partner records as the managed objects (partners themselves never act here — no self-service)
- **Data Classification**: Confidential (partner business identity, commercial terms); partner contact details are Restricted/PII

## Security Context
- **Defense Layer**: Strict API (server-side approval workflow, RBAC, tenancy boundary)
- **Threat(s) Addressed**: Broken access control / privilege escalation into wholesale terms (OWASP Top Ten A01:2021); unauthorized partner activation bypassing the approval gate; leakage of wholesale prices/terms to consumer surfaces (CC-WHS-003)
- **Trust Boundary**: Dashboard origin (separate origin, VPN-restricted, distinct session scope — SECURITY.md, HTTP boundary rule 8); the partner tenancy boundary between wholesale data and consumer session context (ARCHITECTURE.md, Dependency rule 3)
- **Zero Trust Consideration**: Activation state changes only via authenticated, role-authorized, audited dashboard actions validated server-side; no client-supplied field (including any partner-submitted onboarding data) is trusted — partner-supplied business identity is validated server-side against explicit schemas (SECURITY.md, Input validation rule 1) and server-controlled fields (approval state, actor, timestamps) are set from server state only (Input validation rule 3).

## Standards Alignment
- **OWASP ASVS**: V8 Authorization (RBAC and tenancy enforcement); V16 Security Logging and Error Handling (audit of privileged actions) — under the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AU-2 (event logging)
- **NIST SP 800-207**: Per-request authorization; approval authority is an explicit, audited human decision, never implicit
- **Regulatory**: GDPR applies to partner-contact personal data for ES/DE partners (CC-CMP-001); DPDP for IN (CC-CMP-002)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a staff user whose role grants partner management (per the issue 080 matrix), when they open the module, then they can search and view partner records including per-market business identity fields (USt-IdNr. for DE, GSTIN for IN) captured by the issue 049 onboarding workflow (CC-WHS-002, CC-DSH-003).
2. **AC-02**: Given a pending partner application, when an authorized staff user approves it in the dashboard, then and only then does the partner become active (able to authenticate to the portal and order per CC-WHS-001); rejection leaves the partner inactive (CC-WHS-002).
3. **AC-03**: Given any surface of the platform (portal, B2B API, storefront), when a prospective partner attempts to activate an account without dashboard approval, then no path exists — activation MUST NOT be self-service (CC-WHS-002).
4. **AC-04**: Given an active partner, when an authorized staff user views or adjusts wholesale payment terms, then the default is net-60 and the term is adjustable per partner, with the change taking effect for subsequent wholesale invoices (CC-WHS-004; invoice generation itself is issues 046–047).
5. **AC-05**: Given any state-changing action in the module (approve, reject, edit business identity, adjust terms), then an audit event is written to the append-only store with actor, action, object, before/after values, and timestamp (CC-DSH-004; SECURITY.md, Logging rule 6; issue 081).
6. **AC-06**: Given a staff user whose role does not include partner management, when they request any module endpoint, then the server returns 404 and logs the denial as a structured security event (SECURITY.md, Authentication rules 8–9; Logging rule 3; CC-DSH-002).
7. **AC-07**: Given any consumer or portal session, when it attempts to reach partner-management endpoints or data, then it cannot: the module exists only on the dashboard origin with distinct session scope, and wholesale terms are not derivable from consumer API responses (CC-WHS-003; SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rules 3–4).
8. **AC-08**: Given any module response, then it carries `Cache-Control: no-store` and returns RFC 9457 problem details on error with no internal state disclosed (SECURITY.md, HTTP boundary rule 3; Logging rule 1).

## Failure Behavior
- **On Invalid Input**: Invalid business-identity or terms input rejected server-side with 400 and RFC 9457 problem details plus correlation ID; unknown fields rejected; no partial state change (SECURITY.md, Input validation rules 1–3; Logging rule 1).
- **On System Error**: Fail closed — an exception in the authorization or approval path is a denial; a partner is never activated by a failed or ambiguous workflow step (SECURITY.md, Logging rule 2). If the audit write fails, the action fails — no unaudited privileged action completes (CC-DSH-004).
- **Alerting**: Authz denials and validation rejections logged as structured security events with alerting via Azure Monitor (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: Approval-workflow state transitions (pending → approved/rejected, no transition without authorized actor); terms default (net-60) and per-partner override logic; per-market business-identity validation. Tagged CC-WHS-002, CC-WHS-004, CC-DSH-003.
- **Integration Tests**: ASP.NET Core integration tests: approval activates a partner end-to-end against issue 049's workflow; rejection does not; every state change produces an audit record with the required fields; role gating per role.
- **Security Tests**: AuthZ suite attempts cross-role access to partner endpoints and consumer/portal-session access to wholesale data — both MUST fail closed (CC-QA-005; SECURITY.md, Authentication rules 8–9); IDOR attempts across partner records (rule 9); SAST/SCA/secret-scan gates (Deployment rule 7).
- **Compliance Tests**: Automated evidence that each privileged action emitted an audit event (CC-DSH-004); assertion that no consumer API response contains wholesale terms (CC-WHS-003).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); Angular component tests for the approval and terms UI; tests tagged with CC-* IDs (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 049 Partner tenancy and onboarding approval workflow (this module is its UI surface); 079 Dashboard shell; 080 Dashboard RBAC; 081 Append-only audit store; 020 Deny-by-default policy; 021 RFC 9457 error handling; 022 Structured security logging; 060 Staff SSO (CC-DSH-001); 005 Design token pipeline.
- **Downstream**: 051 Wholesale portal buyer authentication (buyers exist only for approved partners; that issue is BLOCKED on the portal IdP open decision — this module does not depend on it, but partner activation feeds it); 052 Wholesale portal UI; 050 Wholesale price lists; 046 Invoice core / 047 per-market tax content (consume per-partner terms per CC-WHS-004).
- **External**: Microsoft Entra ID (staff SSO, ARCHITECTURE.md Authentication model); Azure Monitor (observability).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith — the dashboard drives the Wholesale & B2B context's workflow through its module boundary; the Wholesale context owns partner data in its own schema with its own least-privilege database role (SECURITY.md, Secret handling rule 10). Named `[Authorize(Policy=...)]` policies per action (Authentication rule 1); `[AutoValidateAntiforgeryToken]` applies to the cookie-authenticated dashboard's state-changing requests (Authentication rule 11). Bind requests only to dedicated DTOs with explicit source attributes; approval state and actor are server-controlled fields (Input validation rule 3). Audit writes go through the INSERT-only audit role (Logging rule 6; issue 081).
- **Anti-Patterns**: MUST NOT expose any self-service activation path (CC-WHS-002); MUST NOT let wholesale terms or partner data reach consumer session context or consumer API responses (CC-WHS-003; ARCHITECTURE.md, Dependency rule 3); MUST NOT share modules/cookies/tokens with storefront or portal (Dependency rule 4); MUST NOT mutate or delete audit records (Logging rule 6); MUST NOT log partner contact PII un-redacted (Logging rule 4); no hardcoded brand tokens (Dependency rule 8).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Which dashboard role(s) may approve partners and adjust terms is not specified (CC-DSH-002 lists minimum roles but no module mapping); the role–permission matrix is owned by issue 080 and needs a human decision.
- SECURITY.md Authentication rule 2 enumerates step-up re-auth actions as "refunds, employee-record access, role changes" — whether partner approval or wholesale-term changes also require step-up re-auth is not stated. Not assumed either way.
- CC-WHS-002 gives business-identity examples ("e.g., USt-IdNr. in DE, GSTIN in IN"); the required identity fields for US, ES, MX, and JP partners are not enumerated in the canonical docs.
- The specs define onboarding approval only; whether partner suspension/deactivation/offboarding is in v1 scope is not stated.
