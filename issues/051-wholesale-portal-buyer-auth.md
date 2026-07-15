# 051 · Wholesale portal buyer authentication

Part of the Cache Cow v1 build-out epic.

> **BLOCKED:** The wholesale-portal identity provider / federation model is an open decision (ARCHITECTURE.md, "Known unknowns": "Wholesale-portal identity provider — Entra External ID (as consumers) vs. per-partner federation is undecided (CC-SEC-019)"). This issue cannot be implemented until a human decides it. This issue specs only what is already decided and does not propose a resolution.

## Metadata
- **ID**: CC-WHS-005, CC-SEC-019
- **Title**: Wholesale portal buyer authentication
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Human wholesale-portal buyers MUST authenticate with phishing-resistant MFA (WebAuthn passkeys) — password-only access and SMS-based MFA are prohibited — and every portal session MUST be tenant-scoped to the buyer's partner (REQUIREMENTS.md CC-WHS-005; SECURITY.md, Authentication rule 15).
- **Rationale**: The 2026-07-15 threat model (THREAT_MODEL.md) found that the authentication model covered consumers, staff, and B2B API clients but not the human partner buyers who see wholesale prices and place orders; CC-WHS-005 closes that gap (REQUIREMENTS.md CC-WHS-005, CC-SEC-019). Portal buyers access commercially sensitive wholesale prices/terms (CC-WHS-003) and can place case-quantity orders (CC-WHS-001), so their credentials must be phishing-resistant, and their sessions must be confined to their own partner's data (SECURITY.md, Authentication rules 9 and 15).
- **Design**: N/A — authentication policy and session scoping; the portal UI surface is issue 052 (DESIGN.md §11).

## Scope
- **Applies To**: Both (wholesale portal Angular client + its ASP.NET Core back end)
- **Components**: Wholesale portal (ARCHITECTURE.md, Clients); Identity & Access bounded context (ARCHITECTURE.md, Server bounded contexts 9); Wholesale & B2B API bounded context (tenant scoping, context 6).
- **Actors**: Human buyers employed by approved grocery partners — a distinct identity population from consumers, staff, and B2B API clients (SECURITY.md, Authentication rule 15).
- **Data Classification**: Confidential (wholesale prices/terms, partner orders and invoices behind the session).

## Security Context
- **Defense Layer**: Architecture (identity population and tenancy boundary) + Strict API (authorization on every portal endpoint)
- **Threat(s) Addressed**: Phishing and credential theft against buyers (the threat-model gap behind CC-SEC-019); SIM-swap / SMS interception (why SMS MFA is prohibited, SECURITY.md, Authentication rule 3); OWASP Top Ten A07:2021 Identification and Authentication Failures; A01:2021 Broken Access Control / cross-tenant access (CC-WHS-003); STRIDE Spoofing and Elevation of Privilege.
- **Trust Boundary**: The wholesale portal's public client-server edge. Every portal request is authenticated and tenant-scoped server-side; the portal shares no cookies, tokens, or modules with the dashboard (SECURITY.md, HTTP boundary rule 8) and portal buyer identity is distinct from B2B API client credentials (OAuth2 client credentials, Authentication rule 5).
- **Zero Trust Consideration**: No trust from network position or partner affiliation claims in the request: tenant scope derives from the server-side session identity established by the WebAuthn ceremony, and every resource access re-checks object-level authorization against that tenant (SECURITY.md, Authentication rule 9).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V6 Authentication (SECURITY.md, Authentication rule 3 cites ASVS 5.0 V6); Session Management and V4 Access Control chapters for session policy and tenant scoping.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: IA-2 (identification and authentication, multifactor), AC-3 (access enforcement), AC-6 (least privilege — buyer sees only own partner's data).
- **NIST SP 800-207**: Per-session, per-resource authorization decisions; authentication strength independent of network location.
- **Regulatory**: N/A
- **Other**: FIDO2/WebAuthn (FIDO Alliance passkeys guidance, SECURITY.md References).

## Acceptance Criteria
(All criteria are conditional on the blocked IdP decision; none may be implemented until it is resolved. They spec only the decided policy.)

1. **AC-01**: Given a provisioned portal buyer, when they authenticate to the wholesale portal, then the ceremony is a phishing-resistant WebAuthn passkey authentication; no session is issued without it (CC-WHS-005, CC-SEC-019; SECURITY.md, Authentication rule 15).
2. **AC-02 (negative)**: Given any portal authentication surface, when inspected or exercised, then no password-only sign-in path exists and no SMS-based MFA option is offered or accepted — SMS MFA is not offered on any surface (CC-WHS-005; SECURITY.md, Authentication rules 3 and 15).
3. **AC-03 (negative)**: Given account recovery for a portal buyer, when invoked, then it never falls back to phishable credentials (SMS codes, security questions); buyers can register multiple passkeys including a hardware security key as recovery (SECURITY.md, Authentication rule 4).
4. **AC-04**: Given an authenticated buyer session for partner A, when the buyer requests price lists, orders, or invoices, then only partner A's data is returned; a request addressing partner B's resources is denied with 404 (CC-WHS-005, CC-WHS-003; SECURITY.md, Authentication rules 9 and 15).
5. **AC-05**: Given portal session issuance, when cookies are set, then they are HttpOnly, Secure, SameSite, with bounded expiry, server-side revocation, session refresh on sign-in, and CSRF protection on all cookie-authenticated state-changing requests (SECURITY.md, Authentication rule 11; CC-SEC-006).
6. **AC-06**: Given any portal endpoint, when reached without an authenticated, tenant-scoped session, then the deny-by-default fallback authorization policy rejects it (SECURITY.md, Authentication rule 1), and authentication failures are logged as structured security events (SECURITY.md, Logging rule 3).
7. **AC-07 (negative)**: Given the open IdP decision, when this issue is worked, then no identity-provider or federation integration is merged — the decision in ARCHITECTURE.md "Known unknowns" must be resolved by a human first (CLAUDE.md working rules).

## Failure Behavior
- **On Invalid Input**: Failed or malformed WebAuthn assertions are rejected with a generic error (RFC 9457 problem details, no internal state disclosed — SECURITY.md, Logging rule 1); no session issued.
- **On System Error**: Fail closed — any exception in the authentication or tenant-scoping path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authentication failure spikes alert (SECURITY.md, Logging rule 8); authn successes/failures and authz denials logged as structured security events to centralized monitoring (Logging rule 3; CC-SEC-010).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on session/tenant-scope resolution and on rejection paths (no password path, no SMS path reachable in configuration). Angular component tests asserting the sign-in surface offers passkey-only flows.
- **Integration Tests**: ASP.NET Core integration tests: portal endpoints deny unauthenticated requests (deny-by-default); session cookie attributes and CSRF enforcement (`[AutoValidateAntiforgeryToken]`) verified; cross-tenant requests (buyer A → partner B resources) return 404 and fail closed.
- **Security Tests**: AuthZ cross-tenant suite over price lists, orders, invoices (CC-QA-005); DAST against staging exercising the portal auth boundary (CC-QA-007); merge gates per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: Automated check that security events (authn success/failure, authz denial) are emitted with correlation IDs (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-WHS-005, CC-SEC-019 (REQUIREMENTS.md §17).

## Dependencies
- **BLOCKED**: Wholesale-portal identity provider / federation model — open decision in ARCHITECTURE.md, "Known unknowns" (CC-SEC-019). No implementation until a human resolves it.
- **Upstream**: 004 Angular workspace scaffold (portal app); 020 Deny-by-default authorization fallback policy; 022 Structured security logging; 049 Partner tenancy and onboarding approval workflow (the tenant that sessions scope to); 061 Session cookie policy and CSRF protection.
- **Downstream**: 052 Wholesale portal UI (all portal screens sit behind this authentication); 062 Object-level authorization and cross-tenant/IDOR test suite (portal buyer population).
- **External**: Identity provider — undecided (see BLOCKED); Microsoft Entra is the confirmed vendor family elsewhere (ARCHITECTURE.md, Authentication model) but MUST NOT be assumed for portal buyers.

## Implementation Notes
- **Constraints**: Portal buyers are a fourth identity population, distinct from consumers (Entra External ID), staff (Entra ID SSO), and B2B API clients (OAuth2 client credentials) — do not fold them into an existing population without the IdP decision (SECURITY.md, Authentication rule 15; ARCHITECTURE.md, Authentication model). Tenant scope must be carried in server-side session state and enforced by scoping every query to the buyer's partner (SECURITY.md, Authentication rule 9). Portal sessions never share cookie scope with the dashboard or storefront (HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4).
- **Anti-Patterns**: MUST NOT ship password-only authentication, even temporarily; MUST NOT offer SMS-based MFA or SMS/security-question recovery (SECURITY.md, Authentication rules 3–4); MUST NOT derive tenant scope from client-supplied identifiers; MUST NOT log credentials or tokens (Logging rule 4); MUST NOT select or integrate an IdP to unblock this issue.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests, ≥ 80% coverage, lint, SAST/SCA/secret scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-WHS-005, CC-SEC-019 (REQUIREMENTS.md §17).

## Open Questions
- The blocker itself: identity provider / federation model for portal buyers (Entra External ID vs. per-partner federation) — ARCHITECTURE.md "Known unknowns"; human decision required.
- Buyer lifecycle is not authored: who provisions and deprovisions buyer accounts within a partner (Cache Cow staff via the dashboard partner-management module, or a partner-side administrator) is unspecified.
- Whether the staff-rule WebAuthn parameters (user verification `required`, UV-flag rejection — SECURITY.md, Authentication rule 2) apply verbatim to portal buyers is not stated; rule 15 requires passkeys but does not restate ceremony parameters.
- Portal session lifetime is not specified (the 12-hour maximum in Authentication rule 2 is authored for staff/dashboard sessions only).
