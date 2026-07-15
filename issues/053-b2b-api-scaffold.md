# 053 · B2B API scaffold: /v1 versioning, schema validation, docs from schemas

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-001, CC-API-006, CC-API-010
- **Title**: B2B API scaffold: /v1 versioning, schema validation, docs from schemas
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The B2B API MUST be a versioned HTTPS JSON API under `/v1/...` whose request bodies are validated server-side against published JSON Schemas — the same schemas from which the API documentation is generated — rejecting unknown fields and returning 400 with RFC 9457 problem details, with breaking changes incrementing the version and v(n-1) supported for a documented deprecation window of at least 6 months (REQUIREMENTS.md CC-API-001, CC-API-006, CC-API-010).
- **Rationale**: Grocery partners integrate machine-to-machine against this API; an unversioned or loosely validated API breaks partner integrations on change and admits attacker-controlled input at a trust boundary. SECURITY.md (Input validation rule 1) treats every input crossing a trust boundary as attacker-controlled and requires rejection over sanitization; ARCHITECTURE.md Dependency rule 7 requires schemas to be the single source of truth at every trust boundary — API validation and generated docs derive from the same published schemas, with no hand-maintained parallel definitions (CC-API-010).
- **Design**: N/A for the API itself. API documentation presentation follows DESIGN.md §11 (Pit background, Plex Mono code, single small lockup) — documentation *styling* is out of scope here; only doc *generation from schemas* is in scope.

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context (ARCHITECTURE.md, Server bounded contexts #6); API gateway ingress
- **Actors**: B2B API clients (approved grocery partners' service accounts)
- **Data Classification**: Confidential (wholesale orders, partner data)

## Security Context
- **Defense Layer**: Input Validation | Strict API
- **Threat(s) Addressed**: Mass assignment / unexpected-field injection (CWE-915), improper input validation (CWE-20), information disclosure via error responses (CWE-209); OWASP API Security Top 10 — Unrestricted Resource Consumption is handled by issue 056; this issue addresses mass assignment and improper inventory/versioning management.
- **Trust Boundary**: API gateway / B2B API HTTP edge — every partner request body, query, and header crosses it as untrusted input (SECURITY.md, Input validation rule 1).
- **Zero Trust Consideration**: All request bodies are validated against explicit published JSON Schemas server-side; unknown fields are rejected, not stripped; no client-supplied field maps onto server-controlled state (SECURITY.md, Input validation rules 2–3).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V1 (input validation at trust boundaries — schema-based validation); error handling per the ASVS chapter on error/logging handling
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (Information Input Validation)
- **NIST SP 800-207**: All partner input treated as untrusted regardless of network origin; validation is enforced at the resource, not assumed from the private-network topology.
- **Regulatory**: N/A
- **Other**: RFC 9457 (Problem Details for HTTP APIs); OWASP REST Security Cheat Sheet (SECURITY.md, References)

## Acceptance Criteria
1. **AC-01** (CC-API-001): Given the B2B API is deployed, when any endpoint is invoked, then it is reachable only under an HTTPS `/v1/...` route serving JSON; no unversioned route exists.
2. **AC-02** (CC-API-001): Given the versioning policy document, when a breaking change is proposed, then the policy requires a version increment to `/v2` and continued support of `/v1` for a documented deprecation window of at least 6 months; the policy is published with the API docs.
3. **AC-03** (CC-API-006): Given a published JSON Schema for an endpoint, when a request body violates the schema (wrong type, missing required field, constraint violation), then the API responds 400 with an RFC 9457 problem-details body and no processing occurs.
4. **AC-04** (CC-API-006): Given any request body containing a field not defined in the endpoint's published schema, when it is submitted, then the request is rejected with 400 problem details — the unknown field MUST NOT be silently ignored or stripped into acceptance.
5. **AC-05** (CC-API-010): Given the generated API documentation, when it is built in CI, then it is generated from the exact schema artifacts the running service validates against; the build fails if docs and validation schemas can diverge (no hand-maintained parallel definitions — ARCHITECTURE.md, Dependency rule 7).
6. **AC-06** (CC-API-006, SECURITY.md Logging rule 1): Given any validation failure or server error, when the response is produced, then the body is a generic RFC 9457 problem-details document containing no stack traces, exception messages, SQL, file paths, or internal identifiers.
7. **AC-07** (CC-API-001, SECURITY.md HTTP boundary rule 1): Given a plaintext HTTP connection attempt to an API host, when it arrives, then it is rejected outright — the API MUST NOT listen on plaintext HTTP and MUST NOT redirect.
8. **AC-08** (SECURITY.md HTTP boundary rule 6): Given a request with a disallowed HTTP method or unexpected Content-Type for a route, when it arrives, then the API responds 405 or 415 respectively.

## Failure Behavior
- **On Invalid Input**: Reject with 400 + RFC 9457 problem details (405/415 for method/media-type violations); log the validation rejection as a structured security event with correlation ID (SECURITY.md, Logging rules 1, 3); no partial processing, no internal-state disclosure.
- **On System Error**: Fail closed — any exception in the validation path is a rejection, never a bypass (SECURITY.md, Logging rule 2). Generic problem-details response only.
- **Alerting**: Validation-rejection spikes per client surface in centralized monitoring with alerting (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Schema-validation unit tests per endpoint schema: valid bodies pass; each constraint violation and each unknown-field case yields 400 problem details. Versioned-route registration tests.
- **Integration Tests**: ASP.NET Core integration tests (in-process test server) exercising `/v1` routes end-to-end: schema rejection, 405/415 handling, problem-details shape; a CI check asserting the docs artifact is byte-derived from the validation schemas.
- **Security Tests**: Fuzzing of request bodies against schemas (type confusion, oversized values, unknown fields); SAST gate per SECURITY.md Deployment rule 7; DAST against staging per CC-QA-007.
- **Compliance Tests**: CI gate proving docs-from-schema single-sourcing (CC-API-010); tests tagged CC-API-001/006/010 per REQUIREMENTS.md §17.
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold: .NET 10 modular monolith with enforced bounded-context boundaries; 016 TLS/HSTS enforcement and security middleware ordering; 018 CORS, HTTP method/media-type allowlists, request size caps; 021 RFC 9457 error handling and fail-closed authorization/gating behavior.
- **Downstream**: 054 B2B OAuth2 client-credentials authentication and token validation; 055 B2B scope and tenant enforcement with IN gating parity; 056 B2B per-client rate limits; 057 Outbound partner webhooks: HMAC signing, SSRF-safe registration (CC-API-002–009).
- **External**: None (authorization-server and processor integrations live in neighboring issues).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith on AKS (ARCHITECTURE.md, Technology decisions); API style is versioned REST (CC-API-001). Bind requests only to dedicated DTOs with explicit source attributes (`[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromHeader]`); never bind to entity or domain models (SECURITY.md, Input validation rule 3). JSON Schema validation server-side at the trust boundary (SECURITY.md, Input validation rules 1–2). API p95 latency budgets: reads < 300 ms, order creation < 800 ms (CC-NFR-002).
- **Anti-Patterns**: No hand-maintained OpenAPI/doc definitions parallel to the validation schemas (CC-API-010; ARCHITECTURE.md Dependency rule 7). No sanitizing invalid input into acceptance (SECURITY.md, Input validation rule 1). No plaintext HTTP listener or HTTPS redirect on API hosts (SECURITY.md, HTTP boundary rule 1). No stack traces or internal identifiers in error bodies (SECURITY.md, Logging rule 1). No unversioned endpoints.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret scan, tests green, ≥ 80% coverage, lint, plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs mandate JSON Schema as the validation source of truth but do not name the schema-validation library or the doc-generation toolchain for .NET; selection must satisfy SECURITY.md Dependency Rules 1–8.
- CC-API-001 requires the deprecation window to be "documented" but does not specify where the version-deprecation policy is published (API docs vs. partner agreement); recorded here rather than guessed.
