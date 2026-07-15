# 021 · RFC 9457 error handling and fail-closed authorization/gating behavior

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-006 (error-format clause); supporting control for CC-SEC-007 and CC-MKT-003/004 fail-closed paths — authored in SECURITY.md, Logging and error handling rules 1, 2, and 7
- **Title**: RFC 9457 error handling and fail-closed authorization/gating behavior
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every error returned to a client MUST be a generic RFC 9457 ProblemDetails body with the correct status code and no stack traces, exception messages, SQL, file paths, or internal identifiers (developer exception pages in Development only), any exception in an authorization or gating path MUST result in denial rather than bypass, and the Angular clients MUST route errors through interceptors and a global ErrorHandler showing users only generic messages with correlation IDs while full details are logged server-side (SECURITY.md, Logging rules 1, 2, and 7; CC-API-006).
- **Rationale**: Error responses are an information-disclosure channel: stack traces, SQL fragments, and internal paths hand attackers a map (CC-API-006 requires machine-readable problem details without them). Fail-closed is load-bearing for the platform's two most sensitive path families: authorization (an authz exception that falls through becomes an access grant — CC-SEC-007, CC-QA-005) and market gating (a gating exception that falls through can serve non-veg content into IN — CC-MKT-003/004, a compliance breach). SECURITY.md Logging rule 2 makes denial the only permitted outcome of an error in those paths. Correlation IDs preserve operator debuggability without leaking internals to clients (Logging rule 7).
- **Design**: DESIGN.md §9 (Voice and microcopy): errors state what happened and what to do next — no apologies, no mascots in error states; the pun budget is zero inside checkout, payment, error recovery, legal, and allergen/nutrition content (DESIGN.md §5.4). The 404 page's "Signal lost" arc motif is DESIGN.md §5.1.

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core global exception-handling middleware and ProblemDetails services (all hosts); authorization and market-gating evaluation paths in every bounded context (especially Market & Gating Policy, Ordering & Payments, Wholesale & B2B API); Angular HTTP interceptors and global `ErrorHandler` in storefront, portal, and dashboard; correlation-ID propagation
- **Actors**: All clients (consumers, portal buyers, B2B service clients, staff); attackers harvesting error output or probing for fail-open error paths
- **Data Classification**: Internal (error plumbing) protecting up to Restricted/PII and Regulated-adjacent surfaces (gating errors touch the IN compliance regime, CC-MKT-003)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Information disclosure through verbose errors (CWE-209, CWE-497; OWASP Top Ten A05:2021); authorization/gating bypass through unhandled exceptions failing open (CWE-636 "fail open" class, CWE-755; OWASP Top Ten A01:2021); client-side leakage of raw error bodies and internal endpoints (SECURITY.md, Logging rule 7). STRIDE: Information Disclosure, Elevation of Privilege.
- **Trust Boundary**: Client–server edge (what leaves the server in error bodies) and the internal authorization/gating decision points (what happens inside when evaluation throws).
- **Zero Trust Consideration**: The error path is treated as an attacker-observable oracle: responses are uniform and generic regardless of internal cause; no error condition ever widens access — absence of a successful "allow" decision is always a deny.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V16 Security Logging and Error Handling (chapter-level, per SECURITY.md's ASVS 5.0 L2 baseline); V8 Authorization (fail-secure enforcement)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-11 (Error Handling), AC-3 (Access Enforcement — fail-closed dimension)
- **NIST SP 800-207**: Deny is the default outcome whenever policy evaluation cannot complete
- **Regulatory**: N/A (indirectly protects the IN FSSAI/veg-only gating regime, CC-MKT-003/CC-CNT-006, by refusing to fail open)
- **Other**: RFC 9457 (Problem Details for HTTP APIs) — named by CC-API-006 and SECURITY.md Logging rule 1

## Acceptance Criteria
1. **AC-01**: Given any unhandled exception in any host outside Development, when the response is produced, then it is an RFC 9457 ProblemDetails body with the correct status code and a correlation ID, and contains no stack trace, exception message, SQL, file path, or internal identifier (SECURITY.md, Logging rule 1; CC-API-006).
2. **AC-02**: Given the Development environment only, when an unhandled exception occurs, then the developer exception page may render; given any non-Development environment, it MUST NOT be registered (SECURITY.md, Logging rule 1).
3. **AC-03**: Given an exception thrown inside an authorization evaluation (policy handler, tenancy/scope check, object-level check), when the request is processed, then the outcome is a denial — the protected handler never executes and the client receives the same response shape as an ordinary denial (SECURITY.md, Logging rule 2).
4. **AC-04**: Given an exception thrown inside a market-gating evaluation (Market & Gating Policy consultation for catalog, search, API, sitemap, or content routes), when the request is processed, then the gated content is NOT served — the outcome is denial/404 semantics per CC-MKT-004, never the ungated content (SECURITY.md, Logging rule 2; CC-MKT-003/004).
5. **AC-05**: Given the Angular storefront, portal, and dashboard, when a server error or client-side exception occurs, then HTTP errors flow through a registered interceptor and uncaught exceptions through the global `ErrorHandler`, the user sees a generic message with the correlation ID only, full details having been logged server-side (SECURITY.md, Logging rule 7).
6. **AC-06**: Given any error in an authorization or gating path, when it occurs, then a structured security event (correlation ID, path class, denial outcome — no secrets or PII beyond policy) is written to centralized monitoring (SECURITY.md, Logging rules 2–3; wiring in issue 022).
7. **AC-07**: Negative — no error response body in any non-Development environment contains exception type names, stack frames, SQL text, connection details, file paths, or internal endpoint URLs, and the Angular clients never render a raw error body received from the server (SECURITY.md, Logging rules 1 and 7).
8. **AC-08**: Negative — a fault injected into an authorization or gating dependency (e.g., the policy store unavailable) MUST NOT yield a 200 with protected or gated content; it MUST yield a denial response.

## Failure Behavior
- **On Invalid Input**: Handled by validation layers (issue 018, SECURITY.md Input validation rules); this issue guarantees their rejections and all other errors serialize as RFC 9457 bodies with correlation IDs and no internal state.
- **On System Error**: Fail closed universally in authorization and gating paths (SECURITY.md, Logging rule 2): exception → denial (401/403/404 as the surface dictates; 404 for gated IN resources per CC-MKT-004 and Authentication rule 9), with the error logged server-side at full fidelity. Other paths: 500 with generic ProblemDetails. Fail open is not permitted anywhere in this issue's scope.
- **Alerting**: Error-rate and authz/gating-denial-anomaly alerts through centralized monitoring (SECURITY.md, Logging rules 3 and 8; CC-NFR-003); a spike of gating-path failures on IN routes is a compliance-relevant alert (production IN gating probe is issue 096).

## Test Strategy
- **Unit Tests**: ProblemDetails factory (status mapping, correlation-ID inclusion, forbidden-content exclusion); exception-to-denial translation in authorization and gating decision wrappers. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: `WebApplicationFactory` tests with fault injection: throwing endpoint returns sanitized RFC 9457 body; throwing authz handler denies; throwing gating lookup returns 404/denial not content (AC-08); environment-conditional developer exception page. Angular component/unit tests (per CC-QA gates): interceptor and `ErrorHandler` render generic message + correlation ID, never raw bodies.
- **Security Tests**: DAST error-disclosure checks (CC-QA-007); SAST rule/CI check that `UseDeveloperExceptionPage` is environment-guarded; fuzzing input class: malformed requests asserting uniform generic error shape; manual pentest checklist: error-oracle probing on authz and gating routes.
- **Compliance Tests**: Log-presence evidence that authz/gating-path errors produce denial events (SECURITY.md, Logging rule 3); IN-gating fail-closed test results retained for the market-gating matrix (CC-QA-003 adjacency).
- **Coverage Target**: ≥ 80% branch coverage for error-handling middleware and denial wrappers; tests tagged `CC-API-006`, `CC-SEC-007`, `CC-MKT-003`, `CC-MKT-004` as applicable (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 004 Angular workspace scaffold; 016 TLS/HSTS enforcement and security middleware ordering (exception middleware position); 020 Deny-by-default authorization fallback policy (denial semantics this issue's fail-closed behavior preserves)
- **Downstream**: 018 CORS/method/media-type/size limits (405/413/415 bodies); 019 Baseline rate limiting (429 bodies); 025 Server-side gating enforcement API (gating fail-closed consumer); 026 404 semantics for market-gated resources; 053 B2B API scaffold (RFC 9457 across `/v1`); 062 Object-level authorization suite; 022 Structured security logging (event pipeline for AC-06)
- **External**: None

## Implementation Notes
- **Constraints**: .NET 10 ASP.NET Core `AddProblemDetails()`/`IProblemDetailsService` with a global exception handler (`UseExceptionHandler`/`IExceptionHandler`) — first-party framework features, no new dependencies (SECURITY.md, Dependency rule 1); correlation ID from the trace context (`Activity`/W3C traceparent) consistent with the OpenTelemetry pipeline (ARCHITECTURE.md Observability, issue 095); authorization/gating fail-closed implemented at the decision boundary (policy handlers and the Market & Gating Policy client wrapper), not by sprinkling try/catch through business code; Angular: `HttpInterceptor` + `ErrorHandler` registered once per app in the shared client plumbing (Dependency rule 8 of ARCHITECTURE.md keeps this out of the shared kernel — it is per-client infrastructure).
- **Anti-Patterns**: MUST NOT return `ex.Message` or `ex.ToString()` in any response; MUST NOT register the developer exception page unguarded; MUST NOT catch-and-continue in authorization or gating paths (swallowing an exception and proceeding is a bypass); MUST NOT surface raw error bodies or internal endpoint URLs in Angular UI (SECURITY.md, Logging rule 7); MUST NOT invent per-endpoint bespoke error JSON shapes alongside RFC 9457 (CC-API-006 single format).
- **AI Development Guidance**: AI-generated error handling notoriously fails open (catch-log-continue); reviewers specifically verify denial-on-exception in authz/gating paths. All AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The correlation-ID transport (response header name vs. ProblemDetails extension member, and its exact format) is not specified; W3C trace context via the confirmed OpenTelemetry/Azure Monitor stack is the natural fit but the concrete choice is an implementation decision.
- The RFC 9457 `type` URI taxonomy (stable problem-type URIs per error class) is not defined in the specs; CC-API-010's schema-derived docs suggest it should be documented with the API, owned jointly with issue 053.
- Whether gating-path failures on the storefront render the branded 404 page ("Signal lost", DESIGN.md §5.1) versus a generic error page is presentation detail not fixed by the specs; CC-MKT-004's 404 requirement governs status semantics either way.
