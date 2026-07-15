# 019 · Baseline rate-limiting middleware (429 + Retry-After)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-008 (enforcement-behavior clause; authored in SECURITY.md, HTTP boundary rule 7), CC-CNT-004 (rate-limiting clause)
- **Title**: Baseline rate-limiting middleware (429 + Retry-After)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The platform MUST provide shared per-client rate-limiting middleware that rejects over-limit requests with 429 plus a `Retry-After` header and supports stricter named policies on authentication and order-creation endpoints (SECURITY.md, HTTP boundary rule 7; CC-API-008 enforcement behavior; CC-CNT-004).
- **Rationale**: Rate limiting is the application-layer brake on brute force (login/OTP), order-spam, contact-form abuse (CC-CNT-004), and per-client resource exhaustion; SECURITY.md HTTP boundary rule 7 requires it per client with stricter limits on auth and order-creation endpoints. It is defense in depth beneath the ingress WAF/DDoS layer, which explicitly does not absorb application-layer per-client abuse (SECURITY.md, HTTP boundary rule 11; CC-SEC-022, issue 013). Scope boundary: this issue delivers the shared middleware/policy infrastructure only — the ratified B2B numeric limits (600 requests/minute, 60 order-creations/minute, tuned per partner tier, CC-API-008) are applied by issue 056, and endpoint-specific OTP issuance/verification throttles with backoff/lockout (SECURITY.md, Authentication rule 13; CC-SEC-016) by issue 059.
- **Design**: N/A (non-UI infrastructure; user-facing throttle errors render through the generic error voice of DESIGN.md §9 via issue 021's problem-details handling).

## Scope
- **Applies To**: Both
- **Components**: Shared ASP.NET Core rate-limiting middleware and named-policy registry in the modular-monolith host; per-endpoint policy attachment surface for all bounded contexts; storefront, portal, dashboard, and B2B API hosts
- **Actors**: Anonymous visitors, authenticated consumers, wholesale-portal buyers, B2B API service clients (numeric tiers in issue 056), staff; attackers performing brute force, enumeration, scraping, or application-layer flooding
- **Data Classification**: Internal (control-plane); protects auth and money-path surfaces up to Restricted/PII

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Credential/OTP brute force (CWE-307); application-layer resource exhaustion (CWE-400; OWASP API Security Top 10 API4:2023 Unrestricted Resource Consumption); order-creation spam against inventory and payment flows; contact-form abuse (CC-CNT-004). STRIDE: Denial of Service, Spoofing (brute-force credential guessing).
- **Trust Boundary**: Client–server edge at every public gateway (SECURITY.md, HTTP boundary rule 9), applied before business logic; sits beneath the network-layer WAF/DDoS boundary (rule 11, issue 013).
- **Zero Trust Consideration**: Request volume is attacker-controlled; limits are enforced per client server-side, keyed on server-verified identity where available (authenticated principal / B2B client) rather than trusting any client-supplied identifier.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 API and Web Service; V6 Authentication (anti-automation on authentication endpoints) — chapter-level, per SECURITY.md's ASVS 5.0 L2 baseline
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-5 (Denial-of-Service Protection), AC-7 (Unsuccessful Logon Attempts — supporting control for the stricter auth-endpoint class; full OTP lockout is issue 059)
- **NIST SP 800-207**: Per-request, per-client policy enforcement independent of network location
- **Regulatory**: N/A
- **Other**: OWASP REST Security Cheat Sheet (SECURITY.md, References)

## Acceptance Criteria
1. **AC-01**: Given the shared middleware with a configured per-client policy, when a client exceeds its limit, then subsequent requests within the window receive 429 with a `Retry-After` header and an RFC 9457 problem-details body (SECURITY.md, HTTP boundary rule 7; CC-API-008).
2. **AC-02**: Given rate limiting is keyed per client, when client A exhausts its limit, then client B's requests in the same window are unaffected (per-client partitioning, not global throttling) (SECURITY.md, HTTP boundary rule 7).
3. **AC-03**: Given the named-policy registry, when an endpoint is declared an authentication or order-creation endpoint, then it can be attached to a stricter policy class than the default, and the stricter limit is observably enforced independently of the default policy (SECURITY.md, HTTP boundary rule 7).
4. **AC-04**: Given a request rejected with 429, then no handler, model binding, or state change executes for that request — in particular an over-limit order-creation request MUST NOT create an order or charge (fail closed on the money path; CC-PRC-005 adjacency).
5. **AC-05**: Given over-limit rejections, when they occur, then each is logged as a structured security event (client key, policy, endpoint class — no credentials or tokens in the log) to centralized monitoring (SECURITY.md, Logging rules 3–4; CC-SEC-010).
6. **AC-06**: Negative — 429 responses MUST NOT disclose internal state (no queue depths, backend identifiers, or stack traces; SECURITY.md, Logging rule 1), and the middleware MUST NOT fail open: if the limiter's state store is unavailable, requests to endpoints in the stricter auth/order classes are rejected rather than passed unthrottled (SECURITY.md, Logging rule 2).

## Failure Behavior
- **On Invalid Input**: N/A for payloads (handled by issue 018); over-limit traffic → 429 + `Retry-After`, RFC 9457 body, correlation ID logged, no processing.
- **On System Error**: Fail closed for auth, order-creation, and other security-sensitive policy classes — limiter malfunction results in denial, never unthrottled access (SECURITY.md, Logging rule 2). Any deviation (e.g., failing open on read-only catalog traffic for availability, CC-NFR-001) requires explicit justification and human sign-off; none is assumed here.
- **Alerting**: Alert on sustained 429 spikes per client and on limiter-state-store failures via centralized monitoring (SECURITY.md, Logging rules 3 and 8; CC-NFR-003); auth-endpoint throttle spikes feed the authentication-failure-spike alerting of issue 022.

## Test Strategy
- **Unit Tests**: Policy registry resolution (default vs. stricter classes); partition-key derivation for authenticated principal, B2B client, and anonymous/guest cases; `Retry-After` computation. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: `WebApplicationFactory` tests: N allowed then 429 at N+1 within the window; per-client isolation (AC-02); stricter policy on a flagged endpoint; no order created on a 429'd order-creation call; structured event emitted on rejection.
- **Security Tests**: DAST/load probe confirming 429 behavior under burst (CC-QA-007); fail-closed test with the limiter store made unavailable; check that 429 bodies leak no internal state.
- **Compliance Tests**: Automated evidence: policy configuration snapshot and rejection-event log presence per release (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% branch coverage for the rate-limiting module; tests tagged `CC-API-008`, `CC-CNT-004` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 016 TLS/HSTS enforcement and security middleware ordering (pipeline slot); 021 RFC 9457 error handling (429 problem-details body)
- **Downstream**: 056 B2B per-client rate limits (applies the ratified 600/60 rpm numbers and partner tiers to this infrastructure); 059 Email OTP hardening (per-account/per-IP issuance and verification throttles with backoff/lockout); 076 Contact form with abuse controls (CC-CNT-004); 036 Order submission (order-creation policy class); 058 Consumer authentication (auth policy class); 022 Structured security logging (rejection events and spike alerting)
- **External**: None (WAF/DDoS at ingress is issue 013)

## Implementation Notes
- **Constraints**: .NET 10 ASP.NET Core built-in rate-limiting middleware (`AddRateLimiter` named policies, `RequireRateLimiting`/`[EnableRateLimiting]` per endpoint) — first-party framework capability, consistent with the zero-new-dependencies preference (SECURITY.md, Dependency rule 1). Runs in the pipeline position fixed by issue 016. Partition keys: server-side authenticated identity (consumer account, B2B client ID, portal tenant) where present; the anonymous-client key derivation is an open question below. Multi-pod AKS deployment means per-instance in-memory counters undercount per-client global rates — see open question.
- **Anti-Patterns**: MUST NOT key limits solely on client-supplied headers an attacker controls; MUST NOT rate-limit as a substitute for the WAF/DDoS layer (SECURITY.md, HTTP boundary rule 11) or vice versa; MUST NOT return 429 without `Retry-After`; MUST NOT hardcode B2B partner numeric limits here (issue 056 owns them, tunable per partner tier); MUST NOT log tokens or credentials in throttle events (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated middleware passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Default numeric limits for consumer-facing surfaces (storefront browsing, auth attempts, contact form) are not specified anywhere in the specs — only the B2B defaults (600/60 rpm, CC-API-008) are ratified, and those belong to issue 056. Concrete consumer/auth numbers need a human decision; acceptance criteria therefore test the mechanism at configured values.
- The partition key for anonymous/unauthenticated clients (source IP, IP+route, guest-session ID) is not specified; IP-based keying interacts with shared NATs and must be decided at implementation.
- Whether counters must be shared across pod replicas (distributed limiter state) or per-instance limits are acceptable given the AKS multi-replica topology is not specified.
- The limiter algorithm (fixed window, sliding window, token bucket) is not specified; the specs constrain only the observable 429 + `Retry-After` behavior.
