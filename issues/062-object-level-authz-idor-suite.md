# 062 · Object-level authorization and cross-tenant/IDOR test suite

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-007, CC-QA-005
- **Title**: Object-level authorization and cross-tenant/IDOR test suite
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every resource access MUST enforce object-level authorization server-side by scoping every query to the caller's identity/tenant, returning 404 for inaccessible resources, and an authorization test suite MUST attempt cross-tenant and cross-role access against every sensitive endpoint — with mandatory IDOR coverage on orders, invoices, addresses, and partner data — failing closed and running on every merge (CC-SEC-007; CC-QA-005; SECURITY.md, Authentication and authorization rules 8–9; Deployment rule 8).
- **Rationale**: Broken object-level authorization is the top API risk (OWASP API Security Top 10 API1): a consumer must not read another consumer's orders or addresses, and a B2B partner must not read or mutate another partner's orders or invoices (CC-API-004); wholesale prices and terms must be unreachable from consumer session context (CC-WHS-003; ARCHITECTURE.md, Dependency rule 3). Returning 404 rather than 403 avoids confirming resource existence and is additionally a hard requirement for non-veg product URLs in the IN market (CC-MKT-004; SECURITY.md, Authentication rule 9). A per-merge adversarial suite (Deployment rule 8) keeps the guarantee continuous rather than point-in-time.
- **Design**: N/A (server-side control; no user-facing surface beyond generic 404 responses).

## Scope
- **Applies To**: Both (all cookie-authenticated web surfaces, the B2B API, and guest capability-token access paths)
- **Components**: All bounded contexts serving caller-owned resources — Ordering & Payments (orders, addresses), Invoicing (invoices), Wholesale & B2B API (partner data, tenancy), Back Office (dashboard endpoints under RBAC per CC-DSH-002); shared authorization enforcement in the ASP.NET Core host; the CI authz test suite
- **Actors**: Consumers (account and guest), B2B API clients (per-partner tenants), wholesale portal buyers (tenant-scoped), internal staff (per-role), and the hostile counterparts of each attempting cross-tenant/cross-role access
- **Data Classification**: Restricted/PII (orders, addresses, employee data) and Confidential (invoices, partner terms, wholesale price lists)

## Security Context
- **Defense Layer**: Strict API (server-side object-level authorization on every resource access)
- **Threat(s) Addressed**: IDOR / broken object-level authorization (OWASP API Security Top 10 API1; CWE-639 Authorization Bypass Through User-Controlled Key; OWASP Top Ten A01:2021 Broken Access Control); resource-existence disclosure via 403-vs-404 discrepancies; cross-tenant data exposure (CC-API-004, CC-WHS-003); STRIDE Information Disclosure and Elevation of Privilege
- **Trust Boundary**: Every API/controller entry point where a client-supplied identifier (order ID, invoice number, address ID, partner ID) selects a resource — the identifier is attacker-controlled input (SECURITY.md, Input validation rule 1).
- **Zero Trust Consideration**: Possession of an identifier is never authorization: ownership is re-derived server-side on each request by scoping the data query to the authenticated caller's identity/tenant taken from server-side session/token state — never from route, query, or body values (SECURITY.md, Input validation rule 3: server-controlled fields from server state only).

## Standards Alignment
- **OWASP ASVS**: V4 Access Control (object-level/field-level authorization, deny by default), under the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AC-4 (information flow between tenants)
- **NIST SP 800-207**: Per-request, per-resource authorization decisions; no trust from prior access
- **Regulatory**: GDPR/DPDP/APPI/CCPA data-protection obligations (CC-CMP-001/002) — cross-tenant PII exposure is a reportable breach
- **Other**: OWASP API Security Top 10 (SECURITY.md, References)

## Acceptance Criteria
1. **AC-01**: Given any endpoint that returns or mutates a caller-owned resource, when the handler queries the data store, then the query is scoped to the caller's identity/tenant from server-side auth state — an unscoped lookup by raw client-supplied ID does not exist in the code path (CC-SEC-007; SECURITY.md, Authentication rule 9).
2. **AC-02**: Given authenticated consumer A and an order, invoice, or address belonging to consumer B, when A requests B's resource by its identifier, then the response is HTTP 404 — not 403, and the resource body MUST NOT be returned (CC-SEC-007; SECURITY.md, Authentication rule 9).
3. **AC-03**: Given B2B partner A's credentials with any scope combination, when A calls any `/v1` endpoint with partner B's order or invoice identifiers, then reads and mutations are refused with 404 and no cross-partner data is disclosed (CC-API-004; SECURITY.md, Authentication rules 8–9).
4. **AC-04**: Given a consumer session, when it requests wholesale resources (price lists, partner orders, terms), then the response is 404 and wholesale data is not derivable from any consumer API response (CC-WHS-003; ARCHITECTURE.md, Dependency rule 3).
5. **AC-05**: Given a dashboard user of each role in the CC-DSH-002 matrix, when they call an endpoint their role does not permit, then the request is denied per the documented role–permission matrix and logged as an authz denial (SECURITY.md, Authentication rule 8; Logging rule 3).
6. **AC-06**: Given the CI pipeline, when any merge builds, then the authz suite runs attempts of cross-tenant and cross-role access for every sensitive endpoint — with orders, invoices, addresses, and partner data covered as mandatory IDOR classes — and the merge is blocked on any failure (CC-QA-005; SECURITY.md, Deployment rule 8).
7. **AC-07**: Given an exception thrown inside an authorization check, when the request is processed, then the outcome is a denial — the suite includes a fault-injection case proving fail-closed behavior (SECURITY.md, Logging rule 2; CC-QA-005).
8. **AC-08**: Given a sensitive endpoint added without cross-tenant/cross-role coverage, when the suite's endpoint inventory check runs in CI, then the build fails — coverage of "every sensitive endpoint" is enforced mechanically, not by convention (CC-QA-005).

## Failure Behavior
- **On Invalid Input**: Requests for resources outside the caller's scope return 404 with an RFC 9457 generic body and correlation ID — identical in shape and timing to a genuinely nonexistent resource; no existence confirmation, no internal identifiers (SECURITY.md, Authentication rule 9; Logging rule 1).
- **On System Error**: Fail closed — any exception in an authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authorization denials are logged as structured security events; spikes (enumeration/probing patterns) alert via centralized monitoring (SECURITY.md, Logging rules 3 and 8; CC-SEC-010).

## Test Strategy
- **Unit Tests**: Ownership/tenancy-scoping predicates per bounded context; the 404-mapping of authorization failures; fail-closed exception handling in authorization handlers. Tagged CC-SEC-007.
- **Integration Tests**: ASP.NET Core `WebApplicationFactory` suite seeding at least two consumers, two B2B partners, and one dashboard user per role, then executing the full cross-tenant/cross-role matrix over orders, invoices, addresses, and partner data endpoints; asserts 404 (not 403, not 200) and empty bodies for every out-of-scope access. Tagged CC-QA-005.
- **Security Tests**: This issue IS the platform's IDOR/authz security suite, wired into CI on every merge (SECURITY.md, Deployment rule 8); DAST access-control checks against staging complement it per release (CC-QA-007); mutation testing SHOULD run on authz code (CC-QA-001).
- **Compliance Tests**: Automated evidence: authz-denial log entries produced during suite runs; generated requirements-to-tests coverage report includes CC-SEC-007/CC-QA-005 tagging (REQUIREMENTS.md §17).
- **Coverage Target**: ≥ 80% per package (CC-QA-001), with mutation testing on authz code per CC-QA-001 SHOULD.

## Dependencies
- **Upstream**: 001 Solution scaffold; 006 CI merge gates (this suite becomes a blocking gate); 020 Deny-by-default authorization fallback policy; 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging; 054 B2B OAuth2 authentication and 055 B2B scope and tenant enforcement (supply the tenant identity this scopes by); 058 Consumer authentication; 060 Staff SSO.
- **Downstream**: 035–036 Order services, 042 Guest order capability tokens (guest-path resource access under CC-ORD-010 pairs with this object-level model), 046–048 Invoicing, 049–050 Wholesale tenancy and price-list isolation, 080 Dashboard RBAC, 082–087 dashboard modules — all rely on this enforcement pattern and are covered by this suite as their endpoints land; 026 404 semantics for market-gated resources (shares the 404-not-403 posture of CC-MKT-004).
- **External**: Microsoft Entra (identity/tenant claims consumed for scoping).

## Implementation Notes
- **Constraints**: ASP.NET Core resource-based authorization (e.g., `IAuthorizationService` with resource handlers) layered over the deny-by-default fallback and named policies (SECURITY.md, Authentication rule 1); caller identity/tenant read only from validated token/session claims; data access through parameterized/LINQ queries with the tenant filter applied at the query root (SECURITY.md, Input validation rules 3–4). Defense in depth: each bounded context's least-privilege database role confined to its own schema means a missed check in one module still cannot reach another context's data (SECURITY.md, Secret handling rule 10). The suite runs on every merge alongside the market-gating matrix and money-path tests (SECURITY.md, Deployment rule 8).
- **Anti-Patterns**: MUST NOT return 403 for out-of-scope resources (existence disclosure; 404 is the derived hardening default and hard-required for IN non-veg URLs per CC-MKT-004); MUST NOT bind ownership fields (user ID, partner ID) from request bodies or routes (SECURITY.md, Input validation rule 3); MUST NOT rely on client-side filtering or unguessable IDs as the authorization mechanism; MUST NOT let any authorization exception fall through to the resource (fail open); MUST NOT satisfy CC-QA-001 coverage with assertion-free tests.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret-scan, tests green, coverage, lint, this authz suite — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- "Every sensitive endpoint" (CC-QA-005) has no authoritative enumeration in the specs; AC-08 requires a mechanical inventory (e.g., deriving the endpoint list from route metadata and requiring an explicit non-sensitive annotation), but the classification rule for "sensitive" needs human ratification.
- Whether guest capability-token access paths (CC-ORD-010, issue 042) are in this suite's mandatory matrix or tested solely in issue 042 is not specified; SECURITY.md Authentication rule 14 keeps authenticated-account access under rule 9 but does not assign the guest path's adversarial coverage.
