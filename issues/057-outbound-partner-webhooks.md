# 057 · Outbound partner webhooks: HMAC signing, SSRF-safe registration

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-009
- **Title**: Outbound partner webhooks: HMAC signing, SSRF-safe registration
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: Order-status webhooks MUST be delivered to registered partner HTTPS endpoints, signed with per-partner rotating HMAC secrets sourced from Azure Key Vault with timestamps bounding replay, and receiver URLs MUST be validated at registration against an SSRF policy (blocking internal, loopback, and cloud-metadata addresses) with no redirects followed at delivery (REQUIREMENTS.md CC-API-009; SECURITY.md, Secret handling rule 8 and Input validation rule 8).
- **Rationale**: Outbound webhooks make Cache Cow a server-side HTTP client to partner-influenced URLs — a classic SSRF vector into the private network and cloud metadata services if unvalidated (SECURITY.md, Input validation rule 8). Signing with per-partner rotating HMAC secrets and replay-bounding timestamps lets partners authenticate event provenance and reject replays; a shared or static secret would make one partner's leak forge events to all partners (SECURITY.md, Secret handling rule 8).
- **Design**: N/A (machine-to-machine surface; webhook endpoint registration UX, if any, belongs to issue 052/085 scope).

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context (webhook registration + delivery); Ordering & Payments bounded context (order-status event source); Azure Key Vault (per-partner HMAC secrets)
- **Actors**: B2B API clients / partners (webhook receivers); the platform as outbound HTTP client
- **Data Classification**: Confidential (partner order-status data; HMAC signing secrets)

## Security Context
- **Defense Layer**: Input Validation | Architecture
- **Threat(s) Addressed**: SSRF via partner-registered receiver URLs (CWE-918; OWASP Top Ten A10:2021 SSRF), webhook forgery/spoofing toward partners (CWE-345, insufficient verification of data authenticity), replay of captured deliveries (CWE-294), secret sprawl of signing keys (CWE-798)
- **Trust Boundary**: Two boundaries: (1) partner-supplied receiver URLs enter as untrusted input at registration (SECURITY.md, Input validation rules 1 and 8); (2) the outbound delivery leaves the platform toward an external, untrusted network endpoint.
- **Zero Trust Consideration**: Registered URLs are validated and allowlisted before the server ever fetches them; delivery never follows redirects (a redirect is an unvalidated URL); each partner authenticates event provenance cryptographically rather than trusting source IP.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); the ASVS chapters covering SSRF defense / URL validation and cryptographic verification of communications
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-8 (Transmission Confidentiality and Integrity), SI-10 (Information Input Validation)
- **NIST SP 800-207**: Outbound trust is never implied by network position; event authenticity is established cryptographically per partner.
- **Regulatory**: N/A
- **Other**: OWASP API Security Top 10 (SSRF; SECURITY.md, References)

## Acceptance Criteria
1. **AC-01** (CC-API-009): Given an approved partner, when it registers a webhook endpoint, then only `https://` URLs are accepted; `http://` and any other scheme are rejected at registration.
2. **AC-02** (SECURITY.md Input validation rule 8): Given a registration URL resolving to an internal, loopback, link-local, private-range, or cloud-metadata address, when registration is attempted, then it is rejected and no delivery is ever attempted to it — including hostnames that resolve to such addresses, re-checked at delivery time (DNS-rebinding safe).
3. **AC-03** (CC-API-009, SECURITY.md Secret handling rule 8): Given an order-status event for partner A, when it is delivered, then the request carries an HMAC signature computed with partner A's own secret sourced from Azure Key Vault, plus a timestamp bounding replay; partner B's secret is never used for and cannot verify partner A's deliveries.
4. **AC-04** (SECURITY.md Secret handling rule 8): Given a partner's HMAC secret rotation, when the secret is rotated, then subsequent deliveries sign with the new secret without downtime, and the old secret ceases to be used; secrets exist only in Key Vault — never in source, config, manifests, or logs (SECURITY.md, Secret handling rule 1; Logging rule 4).
5. **AC-05** (SECURITY.md Input validation rule 8): Given a delivery attempt whose receiver responds with any 3xx redirect, when the response is received, then the redirect is NOT followed and the attempt completes without any further outbound request.
6. **AC-06** (CC-API-009): Given delivered order-status events, when a partner receives them, then event contents are scoped to that partner's own orders only (tenancy per CC-API-004; issue 055).
7. **AC-07** (negative): Given any webhook delivery or registration failure, when it is handled, then signing secrets and full receiver URLs with embedded credentials are never written to logs, and delivery failures are recorded as structured events with correlation IDs (SECURITY.md, Logging rules 3–4).

## Failure Behavior
- **On Invalid Input**: Registration of a non-HTTPS or SSRF-blocked URL is rejected 400 with RFC 9457 problem details (via issue 053's validation pipeline) and logged as a validation-rejection security event (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed on the SSRF check — if URL validation or resolution cannot complete, no outbound request is made (SECURITY.md, Logging rule 2). Key Vault secret-retrieval failure halts delivery for that partner rather than falling back to any cached-forever or default secret (SECURITY.md, Secret handling rule 5).
- **Alerting**: Alert on SSRF-validation rejections, on sustained delivery failures per partner, and on Key Vault access denials (SECURITY.md, Logging rules 3 and 8).

## Test Strategy
- **Unit Tests**: URL-validation policy tests: scheme allowlist, loopback/private/link-local/metadata-address blocking (IPv4 and IPv6 forms), hostname-resolution checks; HMAC signature computation and timestamp inclusion; rotation selection logic.
- **Integration Tests**: ASP.NET Core integration tests with a stub receiver: signed delivery verifies against the per-partner secret and fails against another partner's secret; 3xx responses are not followed (no second outbound request observed); registration rejection matrix for blocked addresses; delivery-time re-resolution blocking (DNS rebinding case simulated).
- **Security Tests**: SSRF payload class in fuzzing (decimal/octal/IPv6-mapped address encodings, redirect chains); SAST/SCA/secret-scan gates per SECURITY.md Deployment rule 7; pentest checklist item for SSRF on webhook registration (CC-QA-007).
- **Compliance Tests**: Evidence that secrets resolve only via Key Vault + managed identity (no secret material in repo/config — secret-scan gate); tests tagged CC-API-009 (REQUIREMENTS.md §17).
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 014 Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation (secret sourcing and rotation events); 049 Partner tenancy and onboarding approval workflow (partner registry to attach endpoints/secrets to); 035 Order state machine with audited transitions (order-status event source); 053 B2B API scaffold (registration endpoint validation, problem details); 055 B2B scope and tenant enforcement with IN gating parity (per-partner event scoping).
- **Downstream**: 052 Wholesale portal UI: case-quantity ordering and history (partners consuming order-status flow); 085 Dashboard partner management module (operational view of registrations/deliveries, if in its scope).
- **External**: Azure Key Vault (per-partner rotating HMAC secrets); partner HTTPS receiver endpoints.

## Implementation Notes
- **Constraints**: Outbound HTTP client pinned to no-redirect behavior (`HttpClientHandler.AllowAutoRedirect = false`) and TLS-only; SSRF validation both at registration and re-validated at delivery time after DNS resolution (SECURITY.md, Input validation rule 8 validates "before the server fetches it"); HMAC secrets per partner, retrieved via managed identity (`DefaultAzureCredential`) from Key Vault, cached only with TTL honoring expiry and reacting to rotation events (SECURITY.md, Secret handling rules 2, 5, 8); timestamp included in the signed payload to bound replay. Egress from AKS is default-deny (SECURITY.md, Deployment rule 6), so webhook delivery needs an explicit, narrow egress policy.
- **Anti-Patterns**: MUST NOT follow redirects at delivery (SECURITY.md, Input validation rule 8). MUST NOT use one shared HMAC secret across partners or a static never-rotated secret (Secret handling rule 8). MUST NOT log secrets or tokens (Logging rule 4). MUST NOT validate URLs by denylist string-matching alone — resolve and check addresses. MUST NOT store signing secrets in config, environment variables, or Terraform (Secret handling rule 1).
- **AI Development Guidance**: AI-generated code passes identical merge gates — SAST/SCA/secret scan, tests, coverage, lint, mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Delivery retry/backoff policy, delivery-failure retention, and whether partners receive a dead-letter/replay facility are unspecified in the specs.
- The signature transport format (header name, hash algorithm, timestamp encoding) and the replay-window duration are not specified — only "HMAC" and "timestamps to bound replay"; partners need this documented in the API docs (issue 053) once decided.
- Which order-state transitions of CC-ORD-006 emit webhook events (all transitions vs. a subset) is not stated in CC-API-009 ("order-status webhooks").
- Whether webhook endpoint registration is self-service via the B2B API, via the portal (052), or dashboard-managed (085) is not specified.
