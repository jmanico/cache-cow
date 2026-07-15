# 049 · Partner tenancy and onboarding approval workflow

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-WHS-002
- **Title**: Partner tenancy and onboarding approval workflow
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Partner onboarding MUST be an approval workflow executed in the internal dashboard with no self-service activation, capturing business identity per market (e.g., USt-IdNr. in DE, GSTIN in IN) and establishing the partner tenancy record that scopes all subsequent wholesale data access (REQUIREMENTS.md CC-WHS-002).
- **Rationale**: Partner tenancy is the authorization boundary behind which wholesale prices, terms, orders, and invoices live (REQUIREMENTS.md CC-WHS-003, CC-API-004; ARCHITECTURE.md, Dependency rule 3). A human approval gate — never self-service activation — is what prevents an unvetted party from acquiring a tenant and reaching wholesale pricing; per-market business identity (tax registration numbers) is required for legally valid wholesale invoicing (REQUIREMENTS.md CC-INV-001, CC-WHS-004). ARCHITECTURE.md bounded context 6 (Wholesale & B2B API) owns partner tenancy with the "approval workflow driven from the dashboard".
- **Design**: N/A — this issue is the server-side tenancy model and workflow. The dashboard partner-management UI surface is issue 085 (DESIGN.md §12 governs it there).

## Scope
- **Applies To**: API (internal dashboard back end + wholesale bounded context)
- **Components**: Wholesale & B2B API bounded context (ARCHITECTURE.md, Server bounded contexts 6) — partner tenancy model, onboarding workflow endpoints consumed by the Back Office dashboard (context 8); append-only audit store (SECURITY.md, Logging rule 6).
- **Actors**: Internal dashboard staff (RBAC-authorized, CC-DSH-002); prospective and approved grocery partners (as subjects of the workflow, never as actors who can activate themselves).
- **Data Classification**: Confidential (partner business identity, tax registration numbers, tenancy state).

## Security Context
- **Defense Layer**: Architecture (tenancy boundary) + Strict API (approval state machine, no self-service path)
- **Threat(s) Addressed**: OWASP API Security Top 10 — broken object-level authorization / broken function-level authorization (unauthorized tenant activation); OWASP Top Ten A01:2021 Broken Access Control; STRIDE Elevation of Privilege (self-activated partner reaching wholesale prices, CC-WHS-003) and Repudiation (unaudited activation, CC-DSH-004).
- **Trust Boundary**: Internal dashboard boundary (separate origin, VPN-restricted, SECURITY.md, HTTP boundary rule 8). Approval is a privileged dashboard action; no public or partner-facing surface may cross this boundary.
- **Zero Trust Consideration**: Submitted partner business-identity data is untrusted input validated server-side against explicit schemas (SECURITY.md, Input validation rule 1); tenancy activation is granted only by an authenticated, RBAC-authorized staff action (SECURITY.md, Authentication rules 1, 8), never inferred from partner-supplied state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V4 Access Control (function- and object-level authorization on the approval action and tenant scoping); Input Validation chapter for the identity-capture schema.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege for the approving role), AU-2/AU-9 (audit of the privileged approval action, append-only), SI-10 (input validation of captured identity fields).
- **NIST SP 800-207**: Access to wholesale resources is granted per-tenant by explicit policy decision (human approval), never implicitly.
- **Regulatory**: Per-market tax-identity capture supports EU VAT (USt-IdNr.) and IN GST (GSTIN) invoicing obligations (REQUIREMENTS.md CC-INV-001).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a new partner record created via the dashboard workflow, when it is persisted, then its tenancy state is a non-active pending state, and no wholesale price list, order capability, portal access, or B2B API authorization is reachable for it (CC-WHS-002, CC-WHS-003).
2. **AC-02**: Given a pending partner, when an authenticated, RBAC-authorized dashboard staff member approves it, then the tenant becomes active and an audit event (actor, action, object, before/after, timestamp) is appended to the audit store (CC-WHS-002, CC-DSH-004; SECURITY.md, Logging rule 6).
3. **AC-03**: Given a partner being onboarded for the DE market, when business identity is captured, then a USt-IdNr. value is required and schema-validated server-side; given the IN market, a GSTIN is required and schema-validated (CC-WHS-002; SECURITY.md, Input validation rule 1).
4. **AC-04**: Given any request from outside the internal dashboard boundary (storefront, wholesale portal, B2B API, anonymous), when it attempts to create, approve, or activate a partner tenant, then the request is denied — 404 for inaccessible resources per SECURITY.md, Authentication rule 9 — and a security event is logged (CC-WHS-002; SECURITY.md, Logging rule 3). No self-service activation path exists.
5. **AC-05**: Given a partner in a pending (or deactivated) state, when credentials or sessions associated with it attempt wholesale portal or B2B API access, then access is denied and the denial is logged (CC-WHS-002, CC-API-004).
6. **AC-06**: Given any wholesale-context query (price lists, orders, invoices), when executed, then it is scoped to a single partner tenant identifier from server-side state — the tenancy record created here is the scoping key used by SECURITY.md, Authentication rules 8–9 (CC-API-004, CC-WHS-003).
7. **AC-07 (negative)**: Given partner-supplied onboarding input containing unknown fields or an attempt to set server-controlled fields (tenancy state, approval flags, timestamps), when submitted, then the request is rejected with 400 (RFC 9457 problem details) and no state change occurs (SECURITY.md, Input validation rules 2–3).

## Failure Behavior
- **On Invalid Input**: Reject with 400 and an RFC 9457 problem-details body containing no internal state (SECURITY.md, Logging rule 1); log the validation rejection as a structured security event (Logging rule 3).
- **On System Error**: Fail closed — any exception in the approval/authorization path is a denial, never an activation (SECURITY.md, Logging rule 2). A partner whose tenancy state cannot be resolved is treated as not approved.
- **Alerting**: Authorization denials on the approval endpoints and any activation attempt from outside the dashboard boundary alert via centralized monitoring (SECURITY.md, Logging rule 3; Azure Monitor per ARCHITECTURE.md).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit-style unit tests on the tenancy state machine (pending → active, deactivation), per-market identity-field validation rules (DE USt-IdNr. required, IN GSTIN required), and rejection of server-controlled field binding. Tagged CC-WHS-002.
- **Integration Tests**: ASP.NET Core integration tests: approval endpoint requires authenticated staff with the authorized role (deny-by-default fallback policy, SECURITY.md, Authentication rule 1); audit event appended on approval; pending partner denied on portal/B2B surfaces.
- **Security Tests**: AuthZ suite attempts cross-role approval (e.g., sales-viewer approving a partner) and external-surface activation; MUST fail closed (CC-QA-005). SAST/SCA/secret-scan merge gates per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: Automated assertion that every approval/deactivation writes an audit record with the fields required by SECURITY.md, Logging rule 6 (CC-DSH-004).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged with CC-WHS-002 and related IDs (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold (bounded-context boundaries); 015 PostgreSQL per-context schemas/roles (wholesale context role, SECURITY.md, Secret handling rule 10); 020 Deny-by-default authorization fallback policy; 080 Dashboard RBAC (role–permission matrix for the approval permission, CC-DSH-002); 081 Append-only audit store (CC-DSH-004).
- **Downstream**: 050 Wholesale price lists isolated from consumer sessions (tenant scoping key); 051 Wholesale portal buyer authentication (tenant-scoped sessions); 052 Wholesale portal UI; 054/055 B2B OAuth2 authentication and scope/tenant enforcement (partner tenancy claims); 085 Dashboard partner management module (the UI for this workflow); 046–048 Invoicing (partner tax identity on invoices).
- **External**: None new. (Identity for the humans who later use the tenant is issue 051 and is blocked on the ARCHITECTURE.md "Known unknowns" portal-IdP decision; this issue does not depend on it — tenancy exists independent of the buyer IdP.)

## Implementation Notes
- **Constraints**: Implement inside the Wholesale & B2B API bounded context (ARCHITECTURE.md, Server bounded contexts 6) in its own PostgreSQL schema with a least-privilege role over TLS (SECURITY.md, Secret handling rule 10). Approval endpoints protected by named `[Authorize(Policy=...)]` policies under the deny-by-default fallback (SECURITY.md, Authentication rule 1). Bind requests only to dedicated DTOs with explicit `[FromBody]`/`[FromRoute]` sources; tenancy state, approver identity, and timestamps set from server state only (SECURITY.md, Input validation rule 3). Audit writes go to the INSERT-only audit tables (SECURITY.md, Logging rule 6).
- **Anti-Patterns**: MUST NOT expose any self-service activation endpoint on storefront, portal, or B2B API; MUST NOT let partner-supplied payloads set tenancy state; MUST NOT implement per-surface partner conditionals outside the tenancy model (tenancy is the single scoping mechanism, ARCHITECTURE.md, Dependency rule 3); MUST NOT mutate audit records (corrections are new records).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, ≥ 80% coverage, lint, SAST/SCA/secret scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-WHS-002 (REQUIREMENTS.md §17).

## Open Questions
- Which dashboard role(s) hold the partner-approval permission is not specified — CC-DSH-002 lists sales-viewer, ops-agent, finance, hr-admin, admin, but none is assigned partner approval; the role–permission matrix (issue 080) must decide with a human.
- Required business-identity fields for the US, ES, MX, and JP markets are not enumerated — CC-WHS-002 gives only examples ("e.g., USt-IdNr. in DE, GSTIN in IN").
- Whether one partner tenant may be authorized for multiple markets, or tenancy is strictly per market, is ambiguous (CC-WHS-002 captures identity "per market"; CC-API-007 speaks of "a partner authorized for the IN market").
- Whether approval requires step-up re-authentication as a "sensitive action" (SECURITY.md, Authentication rule 2 lists refunds, employee-record access, role changes — partner approval is not listed) is undecided.
