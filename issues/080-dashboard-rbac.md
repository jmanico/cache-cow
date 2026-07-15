# 080 · Dashboard RBAC: role-permission matrix and enforcement

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-002
- **Title**: Dashboard RBAC: role-permission matrix and enforcement
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Dashboard authorization MUST be role-based with at minimum the roles sales-viewer, ops-agent, finance, hr-admin, and admin, enforcing least privilege on every dashboard endpoint through a documented, tested role–permission matrix, with role-based views exposing only the fields each role requires.
- **Rationale**: The dashboard concentrates privileged operations across sales, orders, invoices, inventory, partners, and employees (CC-DSH-003); without least-privilege RBAC, any staff account compromise yields full back-office reach. CC-DSH-002 mandates the role model; SECURITY.md, Authentication rule 8 mandates least-privilege enforcement on every dashboard endpoint with a documented, tested role–permission matrix and role-based views that expose only the fields the role requires — employee management especially (field-level restriction of employee records is CC-DSH-005, implemented in issue 087 on top of this matrix).
- **Design**: DESIGN.md §12 — role-based views render within the Pit-themed shell (issue 079); screens are designed per role so each exposes only required fields (SECURITY.md, Authentication rule 8).

## Scope
- **Applies To**: Both (dashboard Angular views and the Back Office ASP.NET Core endpoints they call)
- **Components**: Back Office bounded context (ARCHITECTURE.md, "Server bounded contexts" 8): role definitions, named authorization policies, role–permission matrix document, matrix-conformance test suite, role-shaped response DTOs; dashboard Angular role-aware view scaffolding. Explicitly excluded: the deny-by-default fallback policy (issue 020), staff SSO/passkey authentication and step-up (issue 060), the platform-wide cross-tenant/IDOR suite (issue 062), per-module business logic (issues 082–087), employee-record field encryption and HR restrictions beyond the matrix entries (issue 087).
- **Actors**: Internal staff in roles sales-viewer, ops-agent, finance, hr-admin, admin (CC-DSH-002)
- **Data Classification**: Restricted/PII (the matrix gates access to order, invoice, partner, and employee data)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: OWASP Top Ten A01:2021 Broken Access Control; vertical privilege escalation between dashboard roles; excessive data exposure through over-broad response shapes (CWE-284, CWE-269)
- **Trust Boundary**: Every dashboard endpoint at the Back Office context boundary — authorization is enforced server-side per request, never by the Angular client hiding controls
- **Zero Trust Consideration**: Every request is independently authorized against the caller's role from the authenticated session; UI state, client claims, and prior navigation are never trusted; unannotated endpoints are denied by the fallback policy (SECURITY.md, Authentication rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (role-based least privilege, deny by default) — platform baseline ASVS 5.0 Level 2 (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AC-2 (account/role management)
- **NIST SP 800-207**: Per-request authorization decisions based on authenticated identity and role, independent of network location (the VPN path of issue 079 grants no permission)
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the Back Office context, when roles are enumerated, then at minimum sales-viewer, ops-agent, finance, hr-admin, and admin exist and every dashboard endpoint requires a named authorization policy derived from the role–permission matrix (`[Authorize(Policy=...)]`/`RequireAuthorization()`), with unannotated endpoints denied by the issue 020 fallback policy (CC-DSH-002; SECURITY.md, Authentication rules 1 and 8).
2. **AC-02**: Given the role–permission matrix document in the repository, when the matrix-conformance test suite runs, then every dashboard endpoint's enforced policy matches the documented matrix exactly, and an endpoint present in code but absent from the matrix (or vice versa) fails the suite (SECURITY.md, Authentication rule 8 — "documented, tested role–permission matrix").
3. **AC-03**: Given an authenticated staff member whose role lacks a permission (e.g., sales-viewer), when they invoke an endpoint requiring that permission (e.g., an order state transition), then the request is denied server-side, no state changes, and the denial is logged as a structured authz-denial security event (negative case; CC-DSH-002; SECURITY.md, Logging rules 2–3).
4. **AC-04**: Given a role-based view or its backing endpoint, when a response is produced for a role, then the response DTO contains only the fields that role requires per the matrix — field restriction is applied server-side, not by client-side hiding of a fuller payload (SECURITY.md, Authentication rule 8).
5. **AC-05**: Given a staff member's role assignment changes, when their next request arrives, then authorization reflects the new role and the session is refreshed on privilege change (SECURITY.md, Authentication rule 11).
6. **AC-06**: Given any exception thrown inside a dashboard authorization check, when the request is processed, then the result is a denial — never a bypass — and the error returned to the client is a generic RFC 9457 problem with no internal detail (SECURITY.md, Logging rules 1–2).
7. **AC-07**: Given the cross-role portion of the authz test suite, when it attempts every sensitive dashboard endpoint with every role not granted that permission, then every attempt fails closed (CC-QA-005; SECURITY.md, Authentication rule 8; composes with the platform suite in issue 062).

## Failure Behavior
- **On Invalid Input**: Malformed or unknown role/permission claims are rejected; the request is denied with a generic RFC 9457 problem body and logged with a correlation ID (SECURITY.md, Logging rule 1). Inaccessible resources return 404 as the derived hardening default (SECURITY.md, Authentication rule 9).
- **On System Error**: Fail closed — any exception in an authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authorization denials are logged as structured security events to centralized monitoring with alerting; spikes in denials alert (SECURITY.md, Logging rules 3 and 8; CC-SEC-010).

## Test Strategy
- **Unit Tests**: Policy-to-matrix mapping tests; DTO shaping tests per role (field allowlists); role-change session-refresh logic. ≥ 80% coverage of the RBAC package (CC-QA-001).
- **Integration Tests**: ASP.NET Core integration tests exercising every dashboard endpoint under every role — grant and deny paths — asserting server-side enforcement and 404-for-inaccessible-resource semantics (SECURITY.md, Authentication rule 9).
- **Security Tests**: Cross-role escalation attempts for every sensitive endpoint failing closed (CC-QA-005), run on every merge (SECURITY.md, Deployment rule 8); mutation testing SHOULD run on the authz code (CC-QA-001).
- **Compliance Tests**: The matrix-conformance suite output retained as CI evidence that the documented matrix and enforced policies agree (SECURITY.md, Authentication rule 8).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-DSH-002 and CC-QA-005 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (Back Office module); 020 "Deny-by-default authorization fallback policy"; 060 "Staff SSO with mandatory WebAuthn and step-up re-authentication" (roles ride on authenticated staff identity, CC-DSH-001); 061 "Session cookie policy and CSRF protection"; 079 "Dashboard shell: separate origin, VPN-restricted, Pit theme".
- **Downstream**: 062 "Object-level authorization and cross-tenant/IDOR test suite" (extends over dashboard endpoints); 082 "Dashboard order management module"; 083 "Dashboard sales analytics module"; 084 "Dashboard inventory-by-cold-store module"; 085 "Dashboard partner management module"; 086 "Dashboard invoice management module"; 087 "Dashboard employee management (HR restriction, compensation encryption)" (CC-DSH-005 builds on the hr-admin matrix entries); 081 "Append-only audit store" consumers (role changes are privileged actions and are audited, CC-DSH-004).
- **External**: Microsoft Entra ID (staff SSO identity source, per ARCHITECTURE.md "Authentication model").

## Implementation Notes
- **Constraints**: ASP.NET Core named authorization policies with a fallback policy requiring authentication (SECURITY.md, Authentication rule 1); role claims sourced from the Entra ID staff SSO session (ARCHITECTURE.md, "Authentication model"); role assignment and change actions write audit events (CC-DSH-004, issue 081); responses bound to dedicated role-shaped DTOs, never entity/domain models (SECURITY.md, Input validation rule 3).
- **Anti-Patterns**: MUST NOT rely on the Angular client hiding buttons or fields as enforcement (clients display what the server already authorized); MUST NOT return a full object and trim it client-side (violates SECURITY.md, Authentication rule 8's field-level requirement); MUST NOT create endpoints outside the documented matrix (unreferenced paths are scope creep, REQUIREMENTS.md §17); MUST NOT confirm existence of inaccessible resources — 404, not 403 (SECURITY.md, Authentication rule 9).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs name the five minimum roles but do not enumerate the permission set of each role (e.g., whether ops-agent or finance may issue refunds, who may manage partners). The role–permission matrix content itself must be authored and human-approved as part of this issue; acceptance criteria here assert only its existence, documentation, enforcement, and test conformance — not specific grants.
- Whether roles are mutually exclusive or combinable per staff member (one person holding finance + ops-agent) is not specified.
- Where the matrix document lives (repo path/format) is unspecified; it must be machine-checkable to satisfy AC-02.
