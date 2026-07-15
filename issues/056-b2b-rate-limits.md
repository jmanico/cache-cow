# 056 · B2B per-client rate limits

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-008
- **Title**: B2B per-client rate limits
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The B2B API MUST enforce per-client rate limits — default 600 requests/minute overall and 60/minute for order creation (ratified 2026-07-15), tunable per partner tier — responding 429 with a `Retry-After` header when a limit is exceeded (REQUIREMENTS.md CC-API-008; SECURITY.md, HTTP boundary rule 7).
- **Rationale**: Rate limiting protects the availability target (99.9% for the API, CC-NFR-001) and constrains abuse of the order-creation money path from a compromised or misbehaving partner client. SECURITY.md HTTP boundary rule 7 requires per-client limits, stricter on order-creation endpoints; rule 11 notes application rate limits do not absorb volumetric floods — network-layer DDoS/WAF protection is issue 013's scope, and this issue is the application-layer complement.
- **Design**: N/A (machine-to-machine surface; no UI).

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context; baseline rate-limiting middleware from issue 019 (SECURITY.md, HTTP boundary rule 7)
- **Actors**: B2B API clients (authenticated partner service accounts, per tier)
- **Data Classification**: Internal (limit configuration); protects Confidential partner data paths

## Security Context
- **Defense Layer**: Strict API | Architecture
- **Threat(s) Addressed**: Unrestricted resource consumption (OWASP API Security Top 10), application-layer DoS (CWE-770, uncontrolled resource consumption), order-creation abuse amplification; volumetric/network floods are out of scope here (SECURITY.md, HTTP boundary rule 11 / issue 013)
- **Trust Boundary**: B2B API HTTP edge — limits are keyed to the authenticated client identity established at the token boundary, not to spoofable network attributes.
- **Zero Trust Consideration**: Limits attach to the verified OAuth client identity (issue 054), so a client cannot evade its quota by rotating IPs; unauthenticated traffic never reaches per-client quota logic (deny by default, SECURITY.md Authentication rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); the ASVS chapter on API/web-service protection and resource limiting (anti-automation / denial-of-service resistance)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-5 (Denial-of-Service Protection)
- **NIST SP 800-207**: Quota decisions bound to verified per-client identity, not network locality.
- **Regulatory**: N/A
- **Other**: OWASP REST Security Cheat Sheet (SECURITY.md, References)

## Acceptance Criteria
1. **AC-01** (CC-API-008): Given a partner client on the default tier, when it exceeds 600 requests within one minute, then subsequent requests in the window receive 429 with a `Retry-After` header and are not processed.
2. **AC-02** (CC-API-008): Given a partner client on the default tier, when it exceeds 60 order-creation requests within one minute, then further order-creation requests receive 429 + `Retry-After` even though the overall 600/minute budget is not exhausted.
3. **AC-03** (CC-API-008): Given per-partner tier configuration, when an operator assigns a partner a non-default tier, then that partner's limits take effect without code changes and other partners' limits are unaffected.
4. **AC-04** (SECURITY.md HTTP boundary rule 7): Given two distinct partner clients, when client A exhausts its quota, then client B's requests are unaffected — limits are per client, never shared or global-only.
5. **AC-05** (negative, CC-ORD-005/CC-API-005 interplay): Given a client that receives 429 on an order-creation request, when the request was rejected by the limiter, then no order state was created — a throttled request MUST NOT partially process, and a retry with the same `Idempotency-Key` behaves per issue 037's semantics.
6. **AC-06** (SECURITY.md Logging rule 3): Given rate-limit rejections, when they occur, then each 429 is logged as a structured security event with client identity and correlation ID, and sustained limit-hitting by a client raises an alert.
7. **AC-07** (negative): Given an unauthenticated or invalid-token request, when it arrives, then it is rejected 401 by authentication and MUST NOT consume any partner's per-client quota.

## Failure Behavior
- **On Invalid Input**: N/A for request bodies (limits act pre-handler); over-limit requests get 429 + `Retry-After` with an RFC 9457 problem-details body and no processing.
- **On System Error**: If the limiter store/evaluation fails, requests to order-creation and other mutating endpoints fail closed (503, no processing) — an error in a protection path must not become a bypass (SECURITY.md, Logging rule 2). Any deliberate fail-open posture for read-only endpoints requires explicit human sign-off per the template's fail-open justification rule; none is assumed here.
- **Alerting**: Alert on sustained per-client 429 rates and on limiter-infrastructure failures via centralized monitoring (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: Limiter policy tests: window accounting at 600/min and 60/min boundaries (599th/600th/601st request), per-tier configuration resolution, `Retry-After` computation.
- **Integration Tests**: ASP.NET Core integration tests with two authenticated test clients verifying per-client isolation (AC-04), order-creation sublimit independence (AC-02), 429 + `Retry-After` shape, and no order persistence on throttled creates (AC-05).
- **Security Tests**: Load-style test evidencing sustained enforcement under burst; DAST/abuse checks per CC-QA-007; SAST/SCA gates per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Structured 429 security-event log presence asserted in integration tests (SECURITY.md, Logging rule 3); tests tagged CC-API-008 (REQUIREMENTS.md §17).
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 019 Baseline rate-limiting middleware (429 + Retry-After) — this issue specializes it per B2B client and endpoint class; 054 B2B OAuth2 client-credentials authentication and token validation (client identity to key limits on); 053 B2B API scaffold (routes/endpoint classes); 049 Partner tenancy and onboarding approval workflow (partner tier attribute).
- **Downstream**: 013 Ingress WAF and DDoS protection (defense-in-depth layer above this per SECURITY.md HTTP boundary rule 11); 100 DAST and penetration-test cadence (validates enforcement).
- **External**: None.
- Note: multi-region deployment (ARCHITECTURE.md, Technology decisions) means limiter state topology interacts with the open residency decisions only if limiter state contains personal data; client-ID counters are not personal data, so this issue is not blocked.

## Implementation Notes
- **Constraints**: Build on ASP.NET Core rate-limiting middleware established in issue 019 (SECURITY.md, HTTP boundary rule 7), partitioned by the authenticated OAuth client ID; a stricter partition for order-creation routes (60/min) layered on the overall 600/min budget; tier values as per-partner configuration data, not code. Middleware ordering per SECURITY.md HTTP boundary rule 5 (after authentication so the partition key is the verified client identity).
- **Anti-Patterns**: No IP-only keying for authenticated B2B traffic (client identity is the key). No global-only limiter that lets one partner starve others. No silent drops — always 429 + `Retry-After` (SECURITY.md, HTTP boundary rule 7). No unbounded in-memory counters that reset per pod without accounting for horizontal scale.
- **AI Development Guidance**: AI-generated code passes identical merge gates — SAST/SCA/secret scan, tests, coverage, lint, mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs do not define the rate-limit window algorithm (fixed vs. sliding window, burst allowance) — only the per-minute numerals; nor where limiter state lives in a multi-pod, multi-region deployment (no distributed-cache technology is named in ARCHITECTURE.md's confirmed stack).
- Named partner tiers and their non-default limit values are not enumerated anywhere ("tune per partner tier" only); tier definitions need a human decision or partner-management data model input (issue 049/085).
- Whether read-only endpoints may fail open on limiter-infrastructure failure (vs. the fail-closed default assumed here) is unstated.
