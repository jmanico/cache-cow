# 037 · Idempotency service: scoped keys with request-fingerprint binding

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-005, CC-API-005, CC-SEC-015
- **Title**: Idempotency service: scoped keys with request-fingerprint binding
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Idempotency keys MUST be scoped to the authenticated client/tenant (or, for consumers, the guest-checkout session) that issued them and bound to a fingerprint of the original request, such that a replay within the retention window returns the original result, the same key with a different request body is rejected with 409, and double submission never creates duplicate orders or charges (REQUIREMENTS.md CC-ORD-005, CC-API-005, CC-SEC-015; SECURITY.md, Input validation rule 12).
- **Rationale**: Order submission is a money path: a retried or double-clicked submission must not double-charge or duplicate orders (CC-ORD-005). The 2026-07-15 threat model added CC-SEC-015 because unscoped keys let one partner's key collide with — or probe — another's, and an unbound key lets an attacker replay a stored result against a different request or smuggle a new order under an old key. Scoping plus fingerprint binding closes both.
- **Design**: N/A (server-side infrastructure; no user-facing surface).

## Scope
- **Applies To**: Both — consumer order submission (issue 036) and B2B `/v1` order-creation endpoints (CC-API-005; issues 053/055)
- **Components**: Ordering & Payments bounded context (ARCHITECTURE.md, "Server bounded contexts" #4) — idempotency storage and enforcement for order creation; reusable by B2B order endpoints
- **Actors**: Guest consumer (guest-checkout session scope), authenticated consumer, B2B partner client (tenant scope)
- **Data Classification**: Confidential (stored request fingerprints and original responses relate to orders)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Duplicate financial transactions from retries/double submission (CC-ORD-005); cross-tenant idempotency-key collision and response disclosure (CC-SEC-015 — a partner receiving another partner's stored order result is a tenancy break, CC-API-004); replay of a key with a mutated body to silently obtain the original result or process a divergent order (CWE-294 class replay concerns)
- **Trust Boundary**: The `Idempotency-Key` header and request body are attacker-controlled input at the HTTP boundary (SECURITY.md, Input validation rule 1); the scope identity (tenant / session) comes only from server-side authentication state, never from the request payload
- **Zero Trust Consideration**: The key alone grants nothing: lookup is by (server-derived scope, key), and even a matching entry is honored only when the request fingerprint matches the stored one. A key presented by a different tenant/session is treated as a fresh key in that scope, never a cross-scope hit.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 (Validation and Business Logic); ASVS 5.0 V4 (API and Web Service)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation), AC-3 (access enforcement — scope isolation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: RFC 9457 (problem-details error format for the 409 conflict; SECURITY.md, Logging rule 1)

## Acceptance Criteria
1. **AC-01**: Given an order submission processed with idempotency key K in scope S (tenant or guest-checkout session), when the identical request (same K, matching fingerprint) is replayed within the retention window, then the original stored result is returned and no second order or charge is created (CC-ORD-005, CC-API-005).
2. **AC-02**: Given a stored key K in scope S, when the same K arrives in scope S with a *different* request body (fingerprint mismatch), then the request is rejected with HTTP 409 — it is never silently served the original result and never processed as a new order (CC-SEC-015; SECURITY.md, Input validation rule 12) (negative case).
3. **AC-03**: Given partner A has used key K, when partner B submits key K, then B's request is handled entirely within B's scope — B can neither read A's stored result nor collide with A's entry (CC-SEC-015, CC-API-004) (negative case).
4. **AC-04**: Given a B2B order-creation request without an `Idempotency-Key` header, when received, then it is rejected with HTTP 400 and RFC 9457 problem details — the header is required on order creation (CC-API-005; SECURITY.md, Input validation rule 2).
5. **AC-05**: Given two concurrent submissions with the same key and fingerprint in the same scope (double click / racing retries), when both execute, then exactly one order is created and both callers receive that single result (CC-ORD-005).
6. **AC-06**: Given a guest checkout, when the guest's session submits with key K, then K is scoped to that guest-checkout session — a different session presenting K cannot retrieve the stored result (CC-SEC-015).
7. **AC-07**: Given the request completes with an error that did not create an order, when the same key is retried, then the retry is not permanently poisoned into replaying a failure of a never-created order (i.e., idempotency protects side effects, not transient infrastructure errors); any exception in enforcement itself denies processing rather than bypassing the check (SECURITY.md, Logging rule 2).

## Failure Behavior
- **On Invalid Input**: Missing required key → 400; key/fingerprint conflict → 409; both as RFC 9457 problem details with correlation ID and no internal state (SECURITY.md, Logging rule 1). Conflicts (AC-02) are logged as structured validation-rejection security events (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — if the idempotency store cannot be consulted or written, order processing is denied rather than proceeding unprotected (a bypass here is a duplicate-charge path; SECURITY.md, Logging rule 2).
- **Alerting**: Spikes in 409 fingerprint-mismatch conflicts (probing/tampering signal) and idempotency-store failures alert via centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Fingerprint computation stability (same logical request → same fingerprint; any body difference → mismatch); scope-key composition; conflict/replay decision table.
- **Integration Tests**: ASP.NET Core + PostgreSQL tests: replay returns stored result byte-consistently; 409 on mutated body; cross-tenant and cross-guest-session isolation; concurrency test asserting exactly one order under racing duplicates (database uniqueness on scope+key).
- **Security Tests**: Idempotent-submission cases are explicitly required in the money-path suite (CC-QA-004); cross-tenant attempts fold into the authz/IDOR suite pattern (CC-QA-005, issue 062); mutation testing on this money-adjacent code (CC-QA-001).
- **Compliance Tests**: Evidence that conflict events are present in structured logs (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged `CC-ORD-005`, `CC-API-005`, `CC-SEC-015` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 (scaffold), 015 (PostgreSQL per-context schemas/roles — unique index on scope+key), 021 (RFC 9457 error handling), 054/055 (tenant identity for B2B scoping when consumed by the API; consumer scope needs the guest-checkout session from 036's flow)
- **Downstream**: 036 (consumer order submission uses this service), 053 (B2B API scaffold — order-creation endpoints require `Idempotency-Key`), 039/040 (no duplicate charges: payment initiation happens at most once per idempotent submission)
- **External**: None

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core; storage in the Ordering context's PostgreSQL schema with a uniqueness constraint on (scope, key) so concurrent duplicates collapse at the database (SECURITY.md, Secret handling rule 10 for role/TLS). The scope identifier derives from server-side auth state (Entra-issued client/tenant identity for B2B per SECURITY.md, Authentication rules 5–8; guest-checkout session for consumers) — never from a client-supplied field (SECURITY.md, Input validation rule 3). Fingerprint is computed over the received request content before any mutation. `Idempotency-Key` is read via explicit `[FromHeader]` binding (SECURITY.md, Input validation rule 3).
- **Anti-Patterns**: MUST NOT key the store on the idempotency key alone (unscoped keys are the exact CC-SEC-015 finding). MUST NOT return the stored result on a fingerprint mismatch, and MUST NOT process the mismatched request as a new order — 409 only. MUST NOT fail open when the store is unavailable. MUST NOT log stored response bodies containing PII without redaction (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); the money-path suite including idempotent submission runs on every merge (SECURITY.md, Deployment rule 8).

## Open Questions
- The retention window duration is not specified anywhere in the specs (CC-API-005 says "within the retention window" without a number). Needs a human decision; it also interacts with the CC-CMP-003 retention schedule.
- The fingerprint construction is unspecified: which parts of the request participate (raw body bytes vs. canonicalized body; whether method/path/headers are included) and the hash primitive. The specs mandate the binding, not the algorithm.
- CC-API-005 mandates the `Idempotency-Key` header for B2B order creation; CC-ORD-005 says only "client idempotency key" for consumers — whether the consumer storefront uses the same header (and whether it is mandatory or merely supported on the consumer endpoint) is not specified.
- How a guest-checkout session is identified/issued (the scope anchor for guests, per SECURITY.md, Input validation rule 12) is not defined in the specs; it depends on the cart/checkout-session model flagged as open in issue 036.
