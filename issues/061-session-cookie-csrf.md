# 061 · Session cookie policy and CSRF protection

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-006
- **Title**: Session cookie policy and CSRF protection
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: All session cookies MUST be HttpOnly, Secure, and SameSite with bounded expiry, server-side revocation, and session refresh on sign-in and privilege change, and all cookie-authenticated state-changing requests MUST enforce CSRF protection via `[AutoValidateAntiforgeryToken]` applied globally, with bearer-token API endpoints exempt (CC-SEC-006; SECURITY.md, Authentication and authorization rule 11).
- **Rationale**: Cookie-authenticated browser surfaces (storefront, wholesale portal, dashboard) are exposed to session theft via script access (mitigated by HttpOnly), plaintext interception (Secure, with the TLS-only posture of SECURITY.md HTTP boundary rule 1), cross-site request forgery (SameSite + antiforgery tokens), and session fixation/privilege carry-over (refresh on sign-in and privilege change). Server-side revocation ensures a compromised or signed-out session can be killed centrally rather than lingering until cookie expiry. Bearer-token B2B API endpoints carry no ambient cookie credential, so CSRF protection does not apply to them (SECURITY.md, Authentication rule 11).
- **Design**: N/A (transport/session control with no user-facing surface; sign-out and session-expiry messaging follows DESIGN.md §9 voice rules).

## Scope
- **Applies To**: Both (cookie policy on all three web apps; CSRF middleware in the ASP.NET Core host; explicit exemption scope for the bearer-token B2B API)
- **Components**: ASP.NET Core session/authentication middleware (modular monolith host); consumer storefront, wholesale portal, and internal dashboard session scopes (each isolated per SECURITY.md HTTP boundary rule 8); Identity & Access bounded context (revocation state)
- **Actors**: Consumers, wholesale portal buyers, internal staff (cookie-authenticated); B2B API clients (bearer tokens, exempt from CSRF)
- **Data Classification**: Confidential (session identifiers are credentials; sessions front Restricted/PII resources)

## Security Context
- **Defense Layer**: Architecture (session lifecycle) and Strict API (global antiforgery enforcement)
- **Threat(s) Addressed**: CSRF (CWE-352; OWASP Top Ten A01:2021 category); session cookie theft via XSS script access (CWE-1004 defense in depth); cleartext transmission of session tokens (CWE-614/CWE-319); session fixation (CWE-384); insufficient session expiration (CWE-613); STRIDE Spoofing/Tampering
- **Trust Boundary**: Client–server edge on every cookie-authenticated request: the browser is untrusted and any cross-origin page can attempt to ride the ambient cookie.
- **Zero Trust Consideration**: A cookie alone never authorizes a state change — the antiforgery token independently proves the request originated from the application's own pages; session validity is re-checked server-side against revocation state on every request rather than trusting cookie lifetime.

## Standards Alignment
- **OWASP ASVS**: V7 Session Management (cookie attributes, bounded lifetime, revocation, session refresh); CSRF defenses per the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-12 (session termination), SC-23 (session authenticity), SC-8 (transmission confidentiality — Secure flag with TLS-only transport)
- **NIST SP 800-207**: No implicit trust in a standing session; per-request revocation check
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any session cookie issued by storefront, portal, or dashboard, when the Set-Cookie header is inspected, then it carries HttpOnly, Secure, and SameSite attributes and a bounded expiry — no session cookie without all of them (CC-SEC-006).
2. **AC-02**: Given an authenticated session, when the server revokes it (sign-out, admin action, or security event), then the very next request bearing that cookie is treated as unauthenticated — revocation is server-side, not dependent on cookie deletion (CC-SEC-006).
3. **AC-03**: Given a user signs in, or a session's privileges change, when the flow completes, then a new session identifier is issued and the prior one is invalidated (session refresh on sign-in and privilege change; SECURITY.md, Authentication rule 11).
4. **AC-04**: Given any cookie-authenticated state-changing request (POST/PUT/PATCH/DELETE) on any of the three web apps, when it lacks a valid antiforgery token, then it is rejected before the action executes — enforced by `[AutoValidateAntiforgeryToken]` registered globally, not per-controller opt-in (CC-SEC-006).
5. **AC-05**: Given a forged cross-site request bearing a valid session cookie but no antiforgery token, when it targets a state-changing endpoint, then the state change MUST NOT occur and the rejection is logged as a validation rejection (SECURITY.md, Logging rule 3).
6. **AC-06**: Given a B2B API endpoint authenticated by bearer token (SECURITY.md, Authentication rules 5–6), when a request arrives without antiforgery material, then it is not subject to antiforgery validation — the exemption is scoped to bearer-token endpoints only, never to cookie-authenticated ones (SECURITY.md, Authentication rule 11).
7. **AC-07**: Given the dashboard, portal, and storefront, when their cookies are inspected, then session scopes are distinct per origin and no cookie is valid across surfaces (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md Dependency rule 4).
8. **AC-08**: Given any authenticated response, when its headers are inspected, then `Cache-Control: no-store` is present so session-bound content is never cached (SECURITY.md, HTTP boundary rules 3 and 10).

## Failure Behavior
- **On Invalid Input**: Missing/invalid antiforgery token → HTTP 400 (ASP.NET Core antiforgery failure) with an RFC 9457 generic body and correlation ID; revoked/expired session → 401 challenge; no internal state disclosed (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — an exception while validating the antiforgery token or session/revocation state is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Spikes in antiforgery validation rejections or in requests bearing revoked sessions are logged as structured security events and alerted via centralized monitoring (SECURITY.md, Logging rules 3 and 8).

## Test Strategy
- **Unit Tests**: Cookie-options configuration (HttpOnly/Secure/SameSite/bounded expiry asserted on the configured options); revocation-store logic; session-refresh-on-privilege-change logic. Tagged CC-SEC-006.
- **Integration Tests**: ASP.NET Core `WebApplicationFactory` tests: Set-Cookie attribute assertions per surface; state-changing POST without token → rejected, with token → accepted; revoked-session request → 401; sign-in issues a new session ID and the old one stops working; bearer-token B2B endpoint unaffected by antiforgery.
- **Security Tests**: CSRF test class in the authz suite — forged cross-site requests against every cookie-authenticated state-changing endpoint fail closed (CC-QA-005); DAST cookie-flag and CSRF checks against staging (CC-QA-007); SAST gate per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Automated header/cookie-attribute scan across the three surfaces as CI evidence; log presence for validation rejections.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-SEC-006 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 016 TLS/HSTS enforcement and security middleware ordering (Secure flag presumes TLS-only; middleware order per SECURITY.md HTTP boundary rule 5); 020 Deny-by-default authorization fallback policy; 021 RFC 9457 error handling; 022 Structured security logging.
- **Downstream**: 058 Consumer authentication (consumer sessions ride this policy); 060 Staff SSO (12-hour dashboard sessions and step-up refresh use this lifecycle); 051 Wholesale portal buyer authentication (tenant-scoped portal sessions); 068 Cart / 069 Checkout UI (cookie-authenticated state changes); 054 B2B OAuth2 authentication (defines the exempt bearer-token surface); 062 Object-level authorization and IDOR test suite.
- **External**: Microsoft Entra (External ID / Entra ID) as the upstream identity providers whose sign-ins trigger session issuance.

## Implementation Notes
- **Constraints**: ASP.NET Core: register antiforgery globally (e.g., `[AutoValidateAntiforgeryToken]` as a global filter per SECURITY.md Authentication rule 11); cookie configuration via the authentication/session cookie options; middleware ordering HTTPS/headers → static files → authentication → authorization (SECURITY.md, HTTP boundary rule 5). Server-side revocation requires a server-held session/ticket store the auth middleware consults per request. The Angular SPA surfaces must send the antiforgery token on state-changing XHR requests in a CSP-compatible way (no inline script; SECURITY.md, HTTP boundary rule 2). The specific SameSite mode (Lax vs. Strict) is not fixed by the specs — see Open Questions.
- **Anti-Patterns**: MUST NOT exempt any cookie-authenticated state-changing endpoint from antiforgery validation; MUST NOT rely on CORS or SameSite alone as the CSRF defense; MUST NOT issue session cookies lacking HttpOnly/Secure; MUST NOT keep the pre-sign-in session identifier after authentication (fixation); MUST NOT log session identifiers or tokens (SECURITY.md, Logging rule 4); MUST NOT share session cookies across the three origins (SECURITY.md, HTTP boundary rule 8).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret-scan, tests green, coverage, lint — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- SECURITY.md Authentication rule 11 mandates "SameSite" without specifying Lax or Strict; the mode (possibly differing between storefront and dashboard) needs a human decision. Note payment-processor redirect returns (Stripe/Razorpay hosted flows, CC-ORD-004) interact with SameSite=Strict on the storefront — flagged, not resolved.
- Bounded expiry values are specified only for the dashboard (12 hours, CC-DSH-001); consumer storefront and wholesale-portal session lifetimes are not specified anywhere and need a decision.
- The mechanics of the server-side revocation store (per-session ticket store vs. token revocation via the IdP) are not fixed by the specs.
