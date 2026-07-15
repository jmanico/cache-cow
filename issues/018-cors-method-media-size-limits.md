# 018 · CORS, HTTP method/media-type allowlists, request size caps

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-001 (HTTP-layer trust-boundary hardening), CC-CNT-004 (request-cap clause) — authored in SECURITY.md, HTTP boundary rules 4, 6, and 7 (size/clamp clauses only)
- **Title**: CORS, HTTP method/media-type allowlists, request size caps
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every host MUST configure CORS with an explicit `WithOrigins()` allowlist only (never combining credentials with wildcard or suffix-matched origins), MUST allowlist HTTP methods per route rejecting everything else with 405, MUST validate Content-Type rejecting unexpected media types with 415, and MUST cap request body sizes (Kestrel `MaxRequestBodySize`, `[RequestSizeLimit]`) and clamp page sizes (SECURITY.md, HTTP boundary rules 4, 6, and 7).
- **Rationale**: These are the HTTP-layer half of treating every input crossing a trust boundary as attacker-controlled (SECURITY.md, Input validation rule 1; CC-SEC-001). Suffix-matched CORS origins are explicitly bypassable (an attacker registers a domain matching the suffix), so only exact allowlisted origins may be combined with credentials (rule 4). Method and media-type allowlists shrink the attack surface before any handler runs; body-size caps and page-size clamps prevent resource-exhaustion and oversized-payload abuse on public endpoints such as the contact form (CC-CNT-004). Rate limiting from the same rule 7 is issue 019; B2B numeric limits are issue 056.
- **Design**: N/A (non-UI infrastructure).

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core hosts for storefront SSR, wholesale portal, internal dashboard, and B2B API (`/v1/...`); Kestrel server limits; per-route endpoint metadata (methods, media types, size limits); list-endpoint paging parameters platform-wide
- **Actors**: Anonymous visitors, authenticated consumers, wholesale-portal buyers, B2B API service clients, staff/admins; attackers attempting cross-origin credentialed requests, verb tampering, content-type confusion, or oversized-payload resource exhaustion
- **Data Classification**: Internal (control-plane hardening); protects surfaces up to Restricted/PII

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Cross-origin credentialed data theft via permissive/suffix-matched CORS (CWE-942); HTTP verb tampering (CWE-650); content-type confusion feeding parsers unexpected media; denial of service via oversized bodies or unbounded page sizes (CWE-400, CWE-770); OWASP Top Ten A05:2021 (Security Misconfiguration), OWASP API Security Top 10 API8:2023 (Security Misconfiguration), API4:2023 (Unrestricted Resource Consumption). STRIDE: Information Disclosure, Denial of Service, Tampering.
- **Trust Boundary**: Client–server edge on every public gateway (SECURITY.md, HTTP boundary rule 9); enforcement happens before model binding or business logic.
- **Zero Trust Consideration**: The `Origin` header, HTTP method, `Content-Type`, body length, and paging parameters are all attacker-controlled; each is validated against an explicit server-side allowlist/limit and rejected — not sanitized into acceptance (SECURITY.md, Input validation rule 1).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 API and Web Service; V13 Configuration; V2 Validation and Business Logic (chapter-level; ASVS 5.0 L2 baseline per SECURITY.md)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (Information Input Validation), SC-5 (Denial-of-Service Protection), CM-6 (Configuration Settings), CM-7 (Least Functionality)
- **NIST SP 800-207**: Every request attribute (origin, method, media type, size) verified per-request against explicit policy
- **Regulatory**: N/A
- **Other**: OWASP REST Security Cheat Sheet (SECURITY.md, References)

## Acceptance Criteria
1. **AC-01**: Given a host's CORS configuration, when it is inspected and exercised, then it uses an explicit `WithOrigins()` allowlist of exact origins only, and a cross-origin request from a non-allowlisted origin receives no `Access-Control-Allow-Origin` grant (SECURITY.md, HTTP boundary rule 4).
2. **AC-02**: Negative — no CORS policy on any host combines `AllowCredentials()` with a wildcard origin or any suffix-/pattern-matched origin (e.g., `SetIsOriginAllowed` endsWith checks); a startup or CI configuration test fails if such a combination is present (SECURITY.md, HTTP boundary rule 4: suffix matching is bypassable).
3. **AC-03**: Given any route, when a request uses an HTTP method not in that route's allowlist, then the response is 405 with an RFC 9457 problem-details body (via issue 021) and no handler or model binding executes (SECURITY.md, HTTP boundary rule 6).
4. **AC-04**: Given any body-accepting endpoint, when a request carries an unexpected `Content-Type` (or none), then the response is 415 and the body is not parsed (SECURITY.md, HTTP boundary rule 6).
5. **AC-05**: Given Kestrel and per-route limits, when a request body exceeds the configured cap (`MaxRequestBodySize` globally, `[RequestSizeLimit]` per route where a tighter cap applies), then the request is rejected with 413 and the handler never runs (SECURITY.md, HTTP boundary rule 7; CC-CNT-004).
6. **AC-06**: Given a list endpoint, when a client requests a page size above the configured maximum, then the effective page size is clamped to the maximum server-side and the response never exceeds it (SECURITY.md, HTTP boundary rule 7).
7. **AC-07**: Negative — a rejected request (405/415/413) MUST NOT produce any state change, MUST NOT reach business logic, and is logged as a structured validation-rejection security event (SECURITY.md, Logging rule 3).

## Failure Behavior
- **On Invalid Input**: Disallowed method → 405; unexpected media type → 415; oversized body → 413; all with RFC 9457 problem-details bodies (CC-API-006, issue 021), correlation ID logged, no internal state disclosed; oversized page size → clamped, request served at the maximum.
- **On System Error**: Fail closed — if limit/allowlist configuration cannot be loaded, the host fails to start; a failure while evaluating CORS or limits results in rejection, never a permissive default (SECURITY.md, Logging rule 2).
- **Alerting**: Validation rejections stream to centralized monitoring as structured security events (SECURITY.md, Logging rule 3); sustained 405/413/415 spikes from a single client surface via the same alerting pipeline as issue 022.

## Test Strategy
- **Unit Tests**: CORS policy construction tests (exact origins, credentials never with wildcard/suffix); page-size clamp logic; size-limit option wiring. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: `WebApplicationFactory` tests per host: preflight and simple cross-origin requests from allowlisted vs. non-allowlisted origins; 405 on undeclared methods for representative routes; 415 on wrong/missing Content-Type; 413 at cap+1 bytes and success at cap; page-size clamping on a list endpoint.
- **Security Tests**: DAST verb-tampering and oversized-payload probes (CC-QA-007); CI configuration test greping/analyzing for `AllowAnyOrigin`+credentials or `SetIsOriginAllowed` suffix patterns; fuzzing input class: content-type header variants.
- **Compliance Tests**: Automated evidence capture of Kestrel limits and CORS configuration per release; test-tag report per REQUIREMENTS.md §17.
- **Coverage Target**: ≥ 80% branch coverage for the CORS/limits configuration module; tests tagged `CC-SEC-001`, `CC-CNT-004` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 016 TLS/HSTS enforcement and security middleware ordering (pipeline slots); 001 Solution scaffold; 021 RFC 9457 error handling (problem-details bodies for 405/413/415 — can land in parallel, wiring converges)
- **Downstream**: 019 Baseline rate-limiting middleware (same rule-7 family, separate issue); 053 B2B API scaffold (applies these allowlists to `/v1` routes); 056 B2B per-client rate limits; 076 Contact form with abuse controls (relies on body caps per CC-CNT-004); 063 Storefront SSR shell; 079 Dashboard shell
- **External**: None

## Implementation Notes
- **Constraints**: ASP.NET Core CORS middleware with named policies and `WithOrigins()` exact strings; endpoint routing method constraints (`MapGet`/`MapPost`/`[HttpPost]` etc.) so 405 comes from routing, not handlers; `[Consumes(...)]`/media-type filters for 415; Kestrel `MaxRequestBodySize` as global ceiling with `[RequestSizeLimit]` for tighter per-route caps; paging clamps in shared list-endpoint plumbing so no endpoint hand-rolls its own. Middleware ordering per issue 016 (CORS after security headers, before authentication, per standard ASP.NET Core ordering within the rule-5 frame).
- **Anti-Patterns**: MUST NOT use `AllowAnyOrigin()` with credentials, `SetIsOriginAllowed(...)` suffix/pattern matching, or reflection of the request `Origin` header; MUST NOT accept any Content-Type "leniently" and sniff the body; MUST NOT trust client-supplied page-size values unbounded; MUST NOT implement per-endpoint ad-hoc size checks in handlers instead of the platform limits.
- **AI Development Guidance**: AI-generated configuration passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); CORS-origin and limit changes are security-sensitive review items.

## Open Questions
- No dedicated CC-SEC-* pointer exists for SECURITY.md HTTP boundary rules 4 and 6 (rules 1–3 map to CC-SEC-003, rule 7's numeric limits to CC-API-008); this issue anchors them to CC-SEC-001's trust-boundary principle — flagging the traceability gap rather than inventing an ID.
- Concrete numeric values for `MaxRequestBodySize`, per-route tighter caps, and the maximum page size are not specified anywhere in the specs; values need a human/engineering decision and are excluded from acceptance criteria (which test the mechanism at whatever cap is configured).
- The exact cross-origin topology (which origins ever legitimately call which hosts — e.g., whether the storefront SPA calls any API on a different origin at all) is not documented; the allowlist contents follow from issues 004/053/063/079 host layout.
