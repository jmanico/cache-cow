# 020 · Deny-by-default authorization fallback policy

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-005/006/007 and CC-DSH-001/002 (supporting platform control; authored in SECURITY.md, Authentication and authorization rule 1)
- **Title**: Deny-by-default authorization fallback policy
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every host MUST set a fallback authorization policy requiring authentication so that every endpoint is protected unless explicitly opted out, and access MUST be granted only via named policies (`[Authorize(Policy=...)]` / `RequireAuthorization()`) (SECURITY.md, Authentication and authorization rule 1).
- **Rationale**: Deny by default converts "a developer forgot the attribute" from a vulnerability into a 401. It is the structural precondition for every downstream authorization requirement — B2B scope/tenancy enforcement (SECURITY.md, Authentication rule 8; CC-API-004), object-level authorization (rule 9; CC-SEC-007), dashboard RBAC (CC-DSH-002), and HR-restricted employee records (CC-DSH-005) all assume no endpoint is accidentally anonymous. Named policies keep authorization declarative, reviewable, and testable against the documented role–permission matrix, rather than scattered imperative checks. Fail-closed behavior is mandated by SECURITY.md, Logging rule 2.
- **Design**: N/A (non-UI infrastructure).

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core authorization configuration of the modular-monolith host and every exposed surface: storefront SSR endpoints, wholesale portal, internal dashboard, B2B API (`/v1/...`), webhook receivers, health/infrastructure endpoints
- **Actors**: Anonymous visitors (legitimately, on public storefront surfaces via explicit opt-out), authenticated consumers, wholesale-portal buyers, B2B API service clients, staff/admins; attackers probing for unprotected endpoints
- **Data Classification**: Restricted/PII (the fallback protects order, invoice, partner, and employee surfaces by default)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Broken access control via missing authorization metadata — OWASP Top Ten A01:2021 (Broken Access Control), OWASP API Security Top 10 API5:2023 (Broken Function Level Authorization), CWE-306 (Missing Authentication for Critical Function), CWE-862 (Missing Authorization). STRIDE: Elevation of Privilege, Information Disclosure.
- **Trust Boundary**: Server-side endpoint dispatch on every host — the boundary holds even when an individual endpoint's author forgot to declare one.
- **Zero Trust Consideration**: No request is trusted implicitly: absent an explicit, reviewed opt-out, every endpoint demands a verified identity. Public availability is an explicit, enumerable decision, never a default.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V8 Authorization (deny-by-default, centralized enforcement) — chapter-level, per SECURITY.md's ASVS 5.0 L2 baseline
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (Access Enforcement), AC-6 (Least Privilege), CM-7 (Least Functionality)
- **NIST SP 800-207**: Access decided per-request by policy; default posture is deny
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given each host's authorization configuration, when it is inspected, then a fallback authorization policy requiring an authenticated user is set (e.g., `FallbackPolicy = RequireAuthenticatedUser`), applying to every endpoint that carries no explicit authorization metadata (SECURITY.md, Authentication rule 1).
2. **AC-02**: Given a newly added endpoint with no `[Authorize]`, `[AllowAnonymous]`, or `RequireAuthorization()` metadata, when an unauthenticated request hits it, then the request is denied (401 challenge for interactive/browser flows; 401 for token-authenticated API surfaces) and the endpoint's handler does not execute.
3. **AC-03**: Given protected endpoints, when access is granted, then it is granted through named policies (`[Authorize(Policy=...)]` / `RequireAuthorization("...")`) mapped to the platform's identity populations (consumers, portal buyers, B2B clients, staff roles per CC-DSH-002) — not through bare boolean checks inside handlers.
4. **AC-04**: Given the set of intentionally public endpoints (e.g., anonymous storefront browsing per CC-ORD-001 guest flows, health probes, CSP report endpoint), when the codebase is scanned by an automated endpoint-inventory test, then every anonymous endpoint carries an explicit opt-out and appears on a reviewed allowlist; the test fails if an anonymous endpoint is not on the list.
5. **AC-05**: Negative — no endpoint on any host is reachable anonymously without an explicit opt-out; the endpoint-inventory test enumerates all routes (including minimal-API endpoints, MVC actions, and webhook receivers) and MUST fail the build on any unlisted anonymous route.
6. **AC-06**: Negative — an exception thrown during authorization evaluation MUST result in denial, never in the handler executing (SECURITY.md, Logging rule 2; full error-path behavior in issue 021).

## Failure Behavior
- **On Invalid Input**: Unauthenticated request to a protected endpoint → 401 (with authentication challenge appropriate to the surface); authenticated but unauthorized → 403, or 404 where object-level resource-existence hardening applies (SECURITY.md, Authentication rule 9 — owned by issue 062); RFC 9457 bodies via issue 021; authz denials logged as structured security events with correlation IDs (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — any exception or misconfiguration in the authorization path is a denial (SECURITY.md, Logging rule 2); if the fallback policy cannot be established at startup, the host fails to start.
- **Alerting**: Authorization denials stream to centralized monitoring; denial spikes alert per SECURITY.md Logging rules 3 and 8 (wired in issue 022).

## Test Strategy
- **Unit Tests**: Policy-registration tests asserting the fallback policy is present and requires authentication on every host; named-policy resolution tests. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: `WebApplicationFactory` tests: an intentionally attribute-less test endpoint returns 401 anonymously; opted-out endpoint serves anonymously; named-policy endpoint admits/denies per principal. Endpoint-inventory test (AC-04/05) run as a CI gate on every merge.
- **Security Tests**: The cross-tenant/cross-role authz suite (CC-QA-005, issue 062) builds on this default; DAST unauthenticated crawl (CC-QA-007) asserting no protected surface responds 200 anonymously.
- **Compliance Tests**: The reviewed anonymous-endpoint allowlist is committed and diffed in CI — any change requires human review; evidence of authz-denial logging per SECURITY.md Logging rule 3.
- **Coverage Target**: ≥ 80% branch coverage for the authorization-configuration module; tests tagged `CC-SEC-007`, `CC-DSH-002` as applicable (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 016 TLS/HSTS enforcement and security middleware ordering (authentication before authorization in the pipeline)
- **Downstream**: 054 B2B OAuth2 client-credentials authentication (JWT-bearer principals feed the fallback policy); 055 B2B scope and tenant enforcement; 058 Consumer authentication; 060 Staff SSO with mandatory WebAuthn; 061 Session cookie policy and CSRF; 062 Object-level authorization and IDOR test suite; 080 Dashboard RBAC role–permission matrix; 041 Inbound processor webhook verification (webhook receivers are anonymous-by-opt-out with signature verification as their authentication)
- **External**: Microsoft Entra (External ID for consumers, Entra ID for staff SSO and the B2B OAuth2 authorization server, per ARCHITECTURE.md "Authentication model") — consumed by downstream issues; this issue is provider-agnostic

## Implementation Notes
- **Constraints**: ASP.NET Core `AuthorizationOptions.FallbackPolicy` (and `DefaultPolicy` for bare `[Authorize]`) in the .NET 10 modular monolith; named policies registered centrally so bounded contexts attach policies, never re-implement checks (ARCHITECTURE.md module boundaries); webhook receivers (Stripe/Razorpay/EasyPost/Contentful) are explicitly opted out of interactive authentication but MUST be listed on the anonymous allowlist and are authenticated by raw-body signature verification instead (SECURITY.md, Input validation rule 11; issue 041).
- **Anti-Patterns**: MUST NOT rely on remembering to add `[Authorize]` per endpoint (the fallback exists precisely because that fails); MUST NOT scatter `User.IsInRole(...)` string checks through handlers instead of named policies; MUST NOT opt out endpoint groups wholesale (e.g., a whole area marked `[AllowAnonymous]`) for convenience; MUST NOT treat a 401→200 flip in tests as acceptable collateral of refactoring — the inventory allowlist is the single review point.
- **AI Development Guidance**: AI-generated endpoints are a prime source of missing-authorization defects; the fallback policy plus the CI endpoint-inventory gate are the structural backstop. All AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- SECURITY.md Authentication rule 1 has no dedicated CC-SEC-* pointer in REQUIREMENTS.md §12; this issue anchors it as a supporting control for CC-SEC-005/006/007 and CC-DSH-001/002 — flagging the traceability gap rather than inventing an ID.
- The definitive list of intentionally anonymous endpoints (public storefront routes, guest checkout steps per CC-ORD-001, health probes, CSP report endpoint, webhook receivers) is not enumerated in the specs; it accretes via the reviewed allowlist in AC-04 as surfaces land.
- Guest checkout (CC-ORD-001) and guest capability-token access (CC-ORD-010) are anonymous-with-compensating-control flows; exactly which policy construct represents "capability-token-authenticated" (issue 042) versus plain anonymous opt-out is an implementation decision for that issue.
