# 054 · B2B OAuth2 client-credentials authentication and token validation

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-002, CC-API-003
- **Title**: B2B OAuth2 client-credentials authentication and token validation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: B2B API clients MUST authenticate via OAuth 2.0 client credentials per RFC 9700 using `private_key_jwt` or mutual TLS (RFC 8705) against Microsoft Entra ID, and the API MUST accept only fully validated, short-lived (≤ 15 minutes) access tokens that are sender-constrained (mTLS-bound per RFC 8705 or DPoP per RFC 9449) except for bearer-only client tiers, which MUST be scoped read-only (REQUIREMENTS.md CC-API-002/003; SECURITY.md, Authentication rules 5–7).
- **Rationale**: The B2B API exposes partner orders, invoices, and wholesale pricing; static shared secrets and long-lived bearer tokens are replayable credentials that turn a single leak into durable cross-tenant access. RFC 9700 (OAuth 2.0 Security BCP) governs B2B API authorization (SECURITY.md, Baseline), and sender-constraining makes a stolen token unusable off the legitimate client's key or TLS channel. Full JWT validation with pinned algorithms closes `alg=none` and audience-confusion forgeries.
- **Design**: N/A (machine-to-machine surface; no UI).

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context; Identity & Access bounded context (ARCHITECTURE.md, Server bounded contexts #6 and #9); Microsoft Entra ID as the OAuth2 authorization server (ARCHITECTURE.md, Authentication model)
- **Actors**: B2B API clients (partner service accounts); Microsoft Entra ID (authorization server)
- **Data Classification**: Confidential (credentials, tokens; gateway to partner order/invoice data)

## Security Context
- **Defense Layer**: Architecture | Strict API
- **Threat(s) Addressed**: Broken authentication (OWASP API Security Top 10), stolen/replayed bearer tokens (CWE-294), JWT validation bypass including `alg=none` and audience confusion (CWE-347), credential leakage via query strings (CWE-598), long-lived static secrets (CWE-798)
- **Trust Boundary**: API gateway / B2B API HTTP edge — every inbound token is untrusted until cryptographically validated against Entra ID's signing keys and this API's audience.
- **Zero Trust Consideration**: No request is trusted by network position (private endpoints notwithstanding); every request presents a token independently validated for issuer, audience, lifetime, signature, and algorithm, and sender-constrained tokens are additionally bound to the presenting client's key or TLS channel.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); the ASVS chapters on authentication and on OAuth/token-based session management (V6 and the token/OAuth controls)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: IA-2 (Identification and Authentication), IA-5 (Authenticator Management), SC-8 (Transmission Confidentiality)
- **NIST SP 800-207**: Per-request authentication decisions; no implicit trust from network locality.
- **Regulatory**: N/A
- **Other**: RFC 9700 (OAuth 2.0 Security BCP); RFC 8705 (OAuth 2.0 Mutual-TLS Client Authentication and Certificate-Bound Access Tokens); RFC 9449 (DPoP)

## Acceptance Criteria
1. **AC-01** (CC-API-002): Given a registered partner client, when it authenticates to Microsoft Entra ID, then only OAuth 2.0 client-credentials flow with `private_key_jwt` or mutual TLS (RFC 8705) succeeds; client registrations using static shared secrets are not provisioned.
2. **AC-02** (CC-API-002, SECURITY.md Authentication rule 5): Given any request presenting an API key or token in a query string, when it reaches the API, then it is rejected (401) and the credential value is never written to logs (SECURITY.md, Logging rule 4).
3. **AC-03** (CC-API-003): Given a token issued with a lifetime exceeding 15 minutes, or an expired token, when it is presented, then the API rejects it with 401; accepted tokens have lifetime ≤ 15 minutes with clock skew ≤ 2 minutes (ratified 2026-07-15; ARCHITECTURE.md, Decision record).
4. **AC-04** (CC-API-003, SECURITY.md Authentication rule 6): Given a sender-constrained token (mTLS-bound or DPoP), when it is replayed without the corresponding client TLS certificate or DPoP proof, then the request is rejected with 401.
5. **AC-05** (CC-API-003): Given a client on a bearer-only tier, when its token is inspected, then it carries read-only scopes exclusively; a bearer-only token presenting `orders:write` (or any write scope) MUST be rejected on any mutating endpoint.
6. **AC-06** (SECURITY.md Authentication rule 7): Given a JWT with `alg=none`, an algorithm outside the pinned `ValidAlgorithms` list, a wrong issuer, a wrong or missing audience, or an invalid signature, when it is presented, then validation fails closed with 401 — `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, and `ValidateIssuerSigningKey` are all enabled and the audience equals this API's specific resource identifier.
7. **AC-07** (negative, SECURITY.md Authentication rule 1): Given any B2B endpoint without an explicit authorization opt-out, when an unauthenticated request arrives, then the deny-by-default fallback policy rejects it with 401 — no endpoint is anonymously reachable by omission.

## Failure Behavior
- **On Invalid Input**: 401 with RFC 9457 problem details (generic, no token contents or validation internals disclosed); authn failures logged as structured security events with correlation IDs (SECURITY.md, Logging rules 1, 3); tokens and credentials never logged (Logging rule 4).
- **On System Error**: Fail closed — any exception during token validation or key retrieval is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authentication-failure spikes alert via centralized monitoring (SECURITY.md, Logging rules 3 and 8).

## Test Strategy
- **Unit Tests**: JwtBearer options tests asserting `ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`/`ValidateIssuerSigningKey` true, pinned `ValidAlgorithms`, clock skew ≤ 2 minutes, audience = this API's resource identifier. Token-rejection matrix: expired, wrong audience, wrong issuer, `alg=none`, unsigned, over-lifetime.
- **Integration Tests**: ASP.NET Core integration tests with a stub/test authorization server: valid `private_key_jwt` and mTLS-bound flows succeed; replay without sender constraint fails; bearer-only tier write attempts fail; query-string credentials rejected; deny-by-default fallback verified against an unmapped endpoint.
- **Security Tests**: SAST clean per SECURITY.md Deployment rule 7; DAST token-handling checks against staging (CC-QA-007); authz suite cross-checks in issue 062/055 build on this.
- **Compliance Tests**: Tests tagged CC-API-002/003 (REQUIREMENTS.md §17); CI evidence that no static client secrets exist in source or config (secret-scan gate, SECURITY.md Deployment rule 7).
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold; 014 Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation (client key material handling); 016 TLS/HSTS enforcement and security middleware ordering (authentication before authorization); 020 Deny-by-default authorization fallback policy; 053 B2B API scaffold: /v1 versioning, schema validation, docs from schemas.
- **Downstream**: 055 B2B scope and tenant enforcement with IN gating parity (scopes/tenancy enforce on the identity this issue establishes); 056 B2B per-client rate limits (per-client identity); 057 Outbound partner webhooks (partner identity/registration).
- **External**: Microsoft Entra ID as the OAuth2 authorization server (ARCHITECTURE.md, Authentication model — confirmed 2026-07-15).

## Implementation Notes
- **Constraints**: ASP.NET Core JwtBearer authentication configured per SECURITY.md Authentication rule 7 (all four validations on, pinned `ValidAlgorithms`, skew ≤ 2 minutes — tighter than the ASP.NET Core 5-minute default — audience set to this API's specific resource identifier). Middleware order: HTTPS/security headers, then authentication, then authorization (SECURITY.md, HTTP boundary rule 5). Entra ID app registrations for partner clients with certificate (`private_key_jwt`) or mTLS credentials only. Verification/signing key material via Key Vault + managed identity (SECURITY.md, Secret handling rules 1–4).
- **Anti-Patterns**: No static shared secrets, no API keys in query strings, no long-lived static keys (SECURITY.md, Authentication rule 5). No bearer tokens with write scopes (rule 6). No default clock skew. No `alg` acceptance beyond the pinned list; never accept `alg=none`. Never log tokens, credentials, or connection strings (SECURITY.md, Logging rule 4). CSRF middleware is not applied to these bearer-token endpoints (SECURITY.md, Authentication rule 11).
- **AI Development Guidance**: AI-generated code passes identical merge gates — SAST/SCA/secret scan, tests, coverage, lint, plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Which client tiers (if any) are permitted bearer-only tokens, and the criteria for assigning a partner to such a tier, are not defined in the specs (SECURITY.md Authentication rule 6 says "where permitted for a client tier"); the read-only constraint is specified, tier assignment is not.
- The specs do not state whether both `private_key_jwt` and mTLS are offered to all partners or one is preferred per tier; Entra ID capability details for RFC 8705 certificate-bound access tokens and DPoP need confirmation during implementation.
