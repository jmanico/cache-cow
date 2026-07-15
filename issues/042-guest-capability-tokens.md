# 042 · Guest order capability tokens

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-010, CC-SEC-017, CC-INV-002
- **Title**: Guest order capability tokens
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Guest access to order status, tracking, and invoice download MUST be gated by an unguessable, single-purpose capability token with at least 128 bits of cryptographic entropy, bound to exactly one order, expiring, and server-revocable — never by a guessable or enumerable order-number-plus-email pair (CC-ORD-010, CC-SEC-017).
- **Rationale**: Guest checkout is mandatory in all markets (CC-ORD-001) and guest orders carry no session identity, so without a capability token the access path degrades to enumerable order-number-plus-email lookups that leak order, address, and invoice data (threat-model-derived control, 2026-07-15; SECURITY.md, Authentication and authorization rule 14). CC-INV-002 additionally requires that the guest invoice-download link resolve via this token, never a guessable order identifier.
- **Design**: N/A (server-side access control; the order-tracking UI itself is issue 070, per DESIGN.md §7 Order tracker).

## Scope
- **Applies To**: Both
- **Components**: Ordering & Payments bounded context (guest order status/tracking access); Invoicing bounded context (guest invoice download, CC-INV-002); storefront guest order-status routes (Angular SSR)
- **Actors**: Guest customer (no account session); attacker attempting order enumeration or token replay; authenticated customer (explicitly out of the token path — object-level authorization instead)
- **Data Classification**: Restricted/PII (order contents, delivery address, invoice data); tokens themselves are secrets (SECURITY.md, Authentication rule 14)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Broken object-level authorization / IDOR on guest orders and invoices (OWASP API Security Top 10 API1; CC-SEC-007 analog for the no-session case); order/user enumeration via guessable identifiers; token leakage through logs, `Referer` headers, and analytics query strings (SECURITY.md, Authentication rule 14)
- **Trust Boundary**: Client-server edge on guest order-status, tracking, and invoice-download endpoints
- **Zero Trust Consideration**: Possession of the token is the sole credential and is verified server-side on every request; the token is bound to exactly one order, time-limited, and revocable server-side, so no client-supplied order number, email, or other hint contributes to the authorization decision.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); authorization and session-management chapters (unguessable, expiring, revocable access credentials)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege — token scoped to one order and its documents), AU-2 (access-denial logging)
- **NIST SP 800-207**: Per-request verification; no trust from prior access or client-asserted identity
- **Regulatory**: GDPR data-protection duties for ES/DE guest order PII (CC-CMP-001) — the token prevents unauthorized disclosure of personal data via enumeration
- **Other**: OWASP API Security Top 10 (API1, object-level authorization) — referenced by SECURITY.md, References

## Acceptance Criteria
1. **AC-01**: Given a guest order is created, when its capability token is generated, then the token has at least 128 bits of entropy from a cryptographic RNG, is bound to exactly that one order, carries an expiry, and is server-revocable (CC-ORD-010; SECURITY.md, Authentication rule 14).
2. **AC-02**: Given a valid, unexpired, unrevoked token, when it is presented over HTTPS, then it grants access to exactly that order's status, tracking, and invoice download — and nothing else (CC-ORD-010, CC-INV-002).
3. **AC-03**: Given any guest order endpoint, when access is attempted with an order number plus email address (or any combination of enumerable identifiers), then access is denied — no such lookup path exists (CC-ORD-010). [Negative]
4. **AC-04**: Given a token bound to order A, when it is presented against order B's resources, then the response is HTTP 404 with no indication that order B exists (SECURITY.md, Authentication rule 9 — 404 hardening). [Negative]
5. **AC-05**: Given an expired or server-revoked token, when it is presented, then access is denied with HTTP 404 and a structured authz-denial event is logged (SECURITY.md, Logging rule 3). [Negative]
6. **AC-06**: Given all logging, telemetry, and analytics pipelines, when a token-bearing request is processed, then the token value never appears in logs or telemetry (SECURITY.md, Logging rule 4), is kept out of the `Referer` header via `Referrer-Policy: strict-origin-when-cross-origin` (SECURITY.md, HTTP boundary rule 3), and never appears in analytics query strings (CC-SEC-017). [Negative]
7. **AC-07**: Given an authenticated account holder accessing their own order, when they use the account path, then access is enforced by server-side object-level authorization (SECURITY.md, Authentication rule 9; issue 062), not by capability tokens (CC-SEC-017).
8. **AC-08**: Given a guest invoice-download link (CC-INV-002), when it is generated, then it resolves only via the capability token and never embeds a guessable order identifier as the access key. [Negative]

## Failure Behavior
- **On Invalid Input**: Invalid, expired, revoked, or wrong-order tokens return HTTP 404 (existence-concealing per SECURITY.md, Authentication rule 9) with a generic RFC 9457 body; structured authz-denial event logged with correlation ID (SECURITY.md, Logging rules 1, 3) — the token value itself is never written to the log (Logging rule 4).
- **On System Error**: Fail closed: any exception in token validation is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Alert on spikes of token-validation failures against guest order endpoints (enumeration/brute-force signal), per SECURITY.md, Logging rules 3 and 8; baseline rate limiting on these endpoints per SECURITY.md, HTTP boundary rule 7 (issue 019).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for token generation (entropy source is cryptographic RNG, length ≥ 128 bits), single-order binding, expiry evaluation, and revocation state; tagged `CC-ORD-010`, `CC-SEC-017`, `CC-INV-002`.
- **Integration Tests**: ASP.NET Core integration tests covering AC-01–AC-08 end to end, including the cross-order 404 case and the absence of any order-number+email route; Angular component tests for the guest status page consuming only typed, validated responses (SECURITY.md, Input validation rule 1).
- **Security Tests**: Cross-tenant/IDOR suite extension: enumeration attempts across sequential order identifiers MUST fail closed (CC-QA-005); log-scrubbing test asserting tokens never reach log sinks or telemetry (SECURITY.md, Logging rule 4); DAST probe of guest endpoints (CC-QA-007).
- **Compliance Tests**: Automated check that invoice links in generated output contain no guessable order identifier (CC-INV-002).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this authz code (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold; 015 PostgreSQL Flexible Server (token persistence in the owning context's schema); 016 TLS/HSTS (tokens are HTTPS-only); 019 Baseline rate-limiting middleware; 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging; 036 Order submission: guest checkout. CC-ORD-001.
- **Downstream**: 043 Transactional order emails (guest links carry the token); 048 Invoice PDF rendering and authenticated link-only delivery (CC-INV-002 uses this token for guest orders); 062 Object-level authorization and cross-tenant/IDOR test suite (authenticated-path counterpart); 070 Order tracking UI.
- **External**: None (Azure Database for PostgreSQL and Key Vault via upstream issues).

## Implementation Notes
- **Constraints**: Token endpoints live behind the deny-by-default authorization fallback with an explicit opt-out policy for the token-gated routes (SECURITY.md, Authentication rule 1); `Cache-Control: no-store` on all token-gated responses (SECURITY.md, HTTP boundary rules 3 and 10 — personalized responses are never edge-cached, CC-MKT-009); token comparison in constant time; storage in the Ordering context's own schema under its least-privilege role (SECURITY.md, Secret handling rule 10).
- **Anti-Patterns**: MUST NOT implement any order-number-plus-email lookup (CC-ORD-010); MUST NOT log tokens or place them in analytics query strings or allow them into `Referer` (SECURITY.md, Authentication rule 14); MUST NOT return 403 or existence-revealing errors for inaccessible orders (SECURITY.md, Authentication rule 9); MUST NOT reuse one token across multiple orders or purposes (single-purpose, single-order binding); MUST NOT substitute this token path for object-level authorization on authenticated accounts (CC-SEC-017).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-ORD-010/CC-SEC-017/CC-INV-002 (REQUIREMENTS.md §17).

## Open Questions
- Token lifetime is required to be "expiring" but no duration is specified in the specs (nor whether expiry should track order lifecycle, e.g. delivery + N days for invoice access).
- At-rest storage form (hashed vs. plaintext token in the database) is not specified; SECURITY.md treats tokens as secrets, which suggests hashing, but the specs do not mandate it.
- Whether an expired token can be re-issued to the guest (and through what verified channel) is not specified.
- The delivery channel for the token (order-confirmation email link is implied by CC-ORD-007/CC-INV-002 flows via issue 043) is not explicitly stated in the specs.
