# 016 · TLS/HSTS enforcement and security middleware ordering

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-003, CC-SEC-008 (transport-security clauses; authored in SECURITY.md, HTTP boundary rules 1 and 5)
- **Title**: TLS/HSTS enforcement and security middleware ordering
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every Cache Cow HTTP surface MUST be served exclusively over HTTPS with TLS 1.2+ (TLS 1.3 preferred), with the browser-facing applications (storefront, portal, dashboard) enforcing `UseHttpsRedirection()` and `UseHsts()` with preload, API hosts not listening on plaintext HTTP at all — rejecting plaintext connections outright, never redirecting — and the ASP.NET Core pipeline ordered so HTTPS and security headers run before static files, then authentication, then authorization (SECURITY.md, HTTP boundary rules 1 and 5; CC-SEC-003, CC-SEC-008).
- **Rationale**: Transport encryption is the baseline for CC-SEC-008 (encryption in transit) and CC-SEC-003. API hosts must reject rather than redirect plaintext because, per SECURITY.md HTTP boundary rule 1, a redirect arrives only *after* the client has already transmitted its credentials over plaintext — the leak has already happened — and HSTS is a browser-only control that offers no protection to non-browser API clients (B2B integrations, webhooks). Middleware ordering (rule 5) guarantees that no response — including static assets — escapes transport and header enforcement, and that authorization always evaluates an authenticated principal. OWASP ASVS 5.0 Level 2 applies in full as baseline (SECURITY.md, Baseline).
- **Design**: N/A (non-UI infrastructure; no DESIGN.md surface).

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core modular-monolith host (all bounded contexts, per ARCHITECTURE.md "Packaging"); storefront SSR host, wholesale portal host, internal dashboard host (Angular-served origins); B2B API gateway host (`/v1/...`); inbound webhook receiver endpoints
- **Actors**: Anonymous visitors, authenticated consumers, wholesale-portal buyers, B2B API service clients, staff/admins, network attackers (man-in-the-middle, protocol-downgrade)
- **Data Classification**: Restricted/PII (credentials, session cookies, capability tokens, and order PII all traverse these connections)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Credential and session-token interception in transit; protocol downgrade / SSL-stripping; plaintext credential leakage on API hosts prior to redirect; security-control bypass via misordered middleware (headers or authz skipped for static or early-pipeline responses). CWE-319 (cleartext transmission), OWASP Top Ten A02:2021 (Cryptographic Failures), A05:2021 (Security Misconfiguration); STRIDE: Information Disclosure, Tampering.
- **Trust Boundary**: Client–server edge on every public ingress (storefront, portal, and API gateways per ARCHITECTURE.md "Technology decisions" — gateway-only public ingress, everything else on private endpoints; SECURITY.md, HTTP boundary rule 9).
- **Zero Trust Consideration**: No connection is trusted by transport locality; every hop requires TLS (including intra-platform data-store connections, owned by issue 015 per SECURITY.md, Secret handling rule 10). Plaintext is treated as hostile and refused, not upgraded, on API hosts.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V12 Secure Communication; V13 Configuration (chapter-level; per SECURITY.md Baseline, ASVS 5.0 L2 applies in full)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-8 (Transmission Confidentiality and Integrity), SC-23 (Session Authenticity), CM-6 (Configuration Settings)
- **NIST SP 800-207**: Enforce security per-request at the resource edge; no implicit trust from network location
- **Regulatory**: N/A (PCI DSS card-data scope is delegated outward per CC-ORD-003; SAQ A scope owned by issues 039/040)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any browser-facing host (storefront, portal, dashboard), when a request arrives over plaintext HTTP, then it is redirected to HTTPS via `UseHttpsRedirection()`, and every HTTPS response carries a `Strict-Transport-Security` header configured with preload via `UseHsts()` (CC-SEC-003; SECURITY.md, HTTP boundary rule 1).
2. **AC-02**: Given the B2B API host (`/v1/...`) or any webhook-receiver endpoint, when a plaintext HTTP connection is attempted, then the connection is rejected outright — the host is not bound to any plaintext listener — and MUST NOT respond with a redirect (SECURITY.md, HTTP boundary rule 1; CC-API-001 HTTPS-only).
3. **AC-03**: Given any host, when a client attempts a TLS handshake with a protocol version below TLS 1.2, then the handshake is refused; TLS 1.3 is offered and preferred where the client supports it (CC-SEC-008).
4. **AC-04**: Given the ASP.NET Core pipeline, when a static file is served, then the response still carries the transport and security-header middleware output, proving HTTPS/header middleware is registered before static-file middleware (SECURITY.md, HTTP boundary rule 5).
5. **AC-05**: Given the pipeline, when an endpoint protected by an authorization policy is requested, then authentication middleware has executed before authorization middleware (authorization always evaluates the authenticated principal, never an anonymous default) (SECURITY.md, HTTP boundary rule 5; Authentication rule 1 interlock via issue 020).
6. **AC-06**: Negative — no endpoint of any host is reachable end-to-end over plaintext HTTP: a full request/response exchange over plaintext MUST NOT occur on API hosts (not even an error body), and MUST NOT return content (only a redirect) on browser-facing hosts.
7. **AC-07**: Negative — no middleware registration order in any host places authentication after authorization, or static files before the security-header middleware; an automated pipeline-order test fails the build if the order regresses.

## Failure Behavior
- **On Invalid Input**: Plaintext connection to an API host: rejected at the connection/listener level (no listener bound); no application response, no redirect. Sub-TLS-1.2 handshake: refused at TLS negotiation. Browser-facing plaintext request: redirect only, never content.
- **On System Error**: Fail closed — if HTTPS/certificate configuration is invalid at startup, the host MUST fail to start rather than fall back to plaintext; any exception in the auth middleware chain is a denial (SECURITY.md, Logging rule 2).
- **Alerting**: TLS handshake failures and certificate errors surface as structured security events to centralized monitoring (SECURITY.md, Logging rule 3; CC-NFR-003); host startup failure alerts through Azure Monitor availability tests.

## Test Strategy
- **Unit Tests**: Pipeline-composition tests asserting middleware registration order (HTTPS redirection/HSTS → security headers → static files → authentication → authorization) per host; HSTS options include preload; ≥ 80% coverage of the hosting/composition module (CC-QA-001).
- **Integration Tests**: ASP.NET Core integration tests (`WebApplicationFactory`/Kestrel) asserting: HTTP→HTTPS redirect plus HSTS header on browser hosts; connection refusal (no plaintext listener) on API hosts; security headers present on static-file responses; authenticated principal visible to authorization middleware.
- **Security Tests**: DAST scan (CC-QA-007) asserting no plaintext endpoint responds with content and HSTS is present; TLS configuration scan asserting TLS < 1.2 refused; CI config check that no Kestrel plaintext binding exists for API hosts.
- **Compliance Tests**: Automated evidence: captured response headers per host per release, TLS-version scan report retained per SECURITY.md Logging rule 3 pipeline.
- **Coverage Target**: ≥ 80% branch coverage for the hosting/middleware composition module; tests tagged `CC-SEC-003`, `CC-SEC-008` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold: .NET 10 modular monolith (hosts to configure); 004 Angular workspace scaffold (browser-facing hosts exist); 013 Ingress WAF and DDoS protection (public ingress path; SECURITY.md, HTTP boundary rules 9 and 11)
- **Downstream**: 017 CSP and security headers (registered in the header slot this issue orders); 018 CORS/method/media-type/size limits; 019 Baseline rate-limiting middleware; 020 Deny-by-default authorization fallback policy; 053 B2B API scaffold; 063 Storefront SSR shell; 079 Dashboard shell
- **External**: Azure (AKS ingress per ARCHITECTURE.md); no other third parties

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core on AKS (ARCHITECTURE.md, Technology decisions); public ingress limited to storefront, portal, and API gateways — everything else private endpoints (SECURITY.md, HTTP boundary rule 9). The dashboard host is additionally VPN-restricted on a separate origin (HTTP boundary rule 8, owned by issue 079) but still enforces this issue's transport rules. Latency budget CC-NFR-002 applies; TLS termination must not push API p95 reads over 300 ms.
- **Anti-Patterns**: MUST NOT bind API hosts to any plaintext listener "for health checks" or otherwise; MUST NOT redirect plaintext API traffic (credentials have already leaked by the time the redirect is issued — SECURITY.md, HTTP boundary rule 1); MUST NOT rely on HSTS for non-browser clients; MUST NOT register static-file middleware before security headers, or authorization before authentication.
- **AI Development Guidance**: AI-generated middleware/hosting code passes the identical merge gates — tests green, ≥ 80% coverage, lint, SAST/SCA/secret-scan clean — plus mandatory human review with no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- HSTS `max-age` value and whether/when the domains are submitted to the browser preload list are not specified in the specs; the rule requires "preload" configuration only.
- The exact TLS termination point (AKS ingress controller vs. Kestrel in-pod, or both/end-to-end) is not fixed by ARCHITECTURE.md; the acceptance criteria are written observable-behavior-first so either topology can satisfy them, but the choice needs an engineering decision at implementation.
