# 059 · Email OTP hardening

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-016
- **Title**: Email OTP hardening
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Email-code (OTP) login MUST use single-use codes with at least 6 digits of cryptographic-RNG entropy, expiry ≤ 10 minutes, invalidation on use and on issuance of a newer code, per-account and per-IP rate limits with backoff/lockout on both issuance and verification, constant-time comparison, and enumeration-safe responses and timing, with codes never written to logs or telemetry (SECURITY.md, Authentication and authorization rule 13; CC-SEC-016).
- **Rationale**: Email-code login is a phishable-by-brute-force surface: without entropy, expiry, single-use, and throttling controls, an attacker can brute-force short-lived codes or use issuance/verification responses to enumerate which email addresses hold accounts. CC-SEC-016 was added by the 2026-07-15 threat model (THREAT_MODEL.md; REQUIREMENTS.md v1.3 preamble) specifically to harden this factor. Logged codes would turn the telemetry pipeline into a credential store (SECURITY.md, Logging rule 4).
- **Design**: N/A (server-side control; user-facing error copy follows DESIGN.md §9 — states what happened and what to do next, no puns in error recovery per §5.4).

## Scope
- **Applies To**: Both (API enforcement; storefront web app consumes the flow)
- **Components**: Identity & Access bounded context (email-code issuance/verification, per ARCHITECTURE.md Server bounded contexts item 9); Azure Communication Services (code delivery); rate-limiting middleware
- **Actors**: Consumer (account holder or unauthenticated visitor), attacker attempting brute force or enumeration
- **Data Classification**: Restricted/PII (email addresses, authentication codes)

## Security Context
- **Defense Layer**: Input Validation (verification hardening) and Architecture (issuance controls)
- **Threat(s) Addressed**: OTP brute force (CWE-307 Improper Restriction of Excessive Authentication Attempts); user enumeration (CWE-204 Observable Response Discrepancy); timing side channels (CWE-208); predictable codes (CWE-330 Use of Insufficiently Random Values); credential leakage via logs (CWE-532); OWASP Top Ten A07:2021
- **Trust Boundary**: Client–server edge: every issuance and verification request is attacker-controlled input (SECURITY.md, Input validation rule 1).
- **Zero Trust Consideration**: Submitted codes are untrusted input compared in constant time against server-held state; no client-observable behavior (body, status, or timing) may reveal whether an address maps to an account.

## Standards Alignment
- **OWASP ASVS**: V6 Authentication (one-time verifier lifecycle, anti-automation/throttling), under the ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: IA-5 (authenticator management), AC-7 (unsuccessful logon attempts), SI-10 (input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a code is issued, when its value is generated, then it comes from a cryptographic RNG with at least 6 digits of entropy and is stored server-side for at most 10 minutes, after which verification of it fails (CC-SEC-016).
2. **AC-02**: Given a code has been successfully used once, when the same code is submitted again, then verification fails — codes are single-use and invalidated on use (CC-SEC-016).
3. **AC-03**: Given a newer code has been issued for the same account, when the older code is submitted, then verification fails — issuance of a newer code invalidates prior codes (CC-SEC-016).
4. **AC-04**: Given repeated issuance or verification requests, when per-account or per-IP thresholds are exceeded on either operation, then the endpoint applies backoff/lockout after a small number of failed attempts and rate-limits with 429 + `Retry-After` (CC-SEC-016; SECURITY.md, HTTP boundary rule 7 — stricter limits on auth endpoints).
5. **AC-05**: Given a verification request, when the submitted code is compared to the stored code, then the comparison is constant-time (no early-exit string comparison) (CC-SEC-016).
6. **AC-06**: Given issuance or verification requests for an address that does and an address that does not map to an account, when responses are compared, then response body, status code, and response timing are identical — no user enumeration (CC-SEC-016).
7. **AC-07**: Given any log, trace, or telemetry emission in the OTP path, when inspected, then no OTP code value appears anywhere — MUST NOT be logged (SECURITY.md, Logging rule 4; CC-SEC-016).
8. **AC-08**: Given issuance and verification events (success, failure, lockout), when they occur, then structured security events are logged with correlation IDs to centralized monitoring (SECURITY.md, Logging rule 3).

## Failure Behavior
- **On Invalid Input**: Invalid, expired, superseded, or replayed codes are rejected with a generic RFC 9457 error (401/400 as appropriate to the flow) that is byte- and timing-identical regardless of the reason for failure and of account existence; no internal state disclosed (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception in the verification path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authentication-failure spikes and lockout-threshold events alert via centralized monitoring (SECURITY.md, Logging rules 3 and 8; Azure Monitor per ARCHITECTURE.md Observability).

## Test Strategy
- **Unit Tests**: Code generation entropy source (cryptographic RNG API used, ≥ 6 digits); expiry clock logic; single-use and supersession invalidation state machine; constant-time comparison primitive; backoff/lockout counters. Tagged CC-SEC-016.
- **Integration Tests**: ASP.NET Core integration tests driving issuance → verification: replay after use fails; old code after reissue fails; code after 10 minutes fails; 429 + `Retry-After` after threshold on both issuance and verification, per-account and per-IP.
- **Security Tests**: Enumeration test comparing full responses (and measured timing distributions) for existing vs. non-existing addresses; log-scrubbing assertion that no code value reaches log sinks or Application Insights telemetry; DAST auth-surface scan (CC-QA-007).
- **Compliance Tests**: Automated evidence that security events for OTP failures/lockouts are present in structured logs (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-SEC-016 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 058 Consumer authentication: passkeys and email-code via Entra External ID (the flow this hardens); 019 Baseline rate-limiting middleware (429 + Retry-After); 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging, PII redaction.
- **Downstream**: 061 Session cookie policy and CSRF protection (session established after successful verification); 093 Sender domain authentication (SPF/DKIM/DMARC) — OTP mail is a prime phishing lure (SECURITY.md, Email and messaging security rule 1).
- **External**: Azure Communication Services (email delivery, ARCHITECTURE.md Technology decisions); Microsoft Entra External ID (consumer identity provider).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core: generate codes with a cryptographic RNG (e.g., `RandomNumberGenerator`), compare with a fixed-time comparison (e.g., `CryptographicOperations.FixedTimeEquals`); structured log templates only, never string interpolation into log messages (SECURITY.md, Logging rule 4); rate limits are stricter on auth endpoints than the platform baseline (SECURITY.md, HTTP boundary rule 7). Transactional OTP mail carries no more PII than necessary and never carries secrets in logged headers/metadata (CC-ORD-007; SECURITY.md, Email and messaging rule 1).
- **Anti-Patterns**: MUST NOT use `Random`/non-cryptographic RNG; MUST NOT use ordinary `==`/`Equals` string comparison for codes; MUST NOT return distinct errors or measurably distinct timing for "no such account" vs. "wrong code"; MUST NOT allow a code to verify twice or after a newer code exists; MUST NOT write codes to logs, traces, or telemetry.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret-scan, tests green, coverage, lint — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The exact numeric thresholds for "a small number of failed attempts" and the backoff/lockout durations are not specified in SECURITY.md Authentication rule 13; concrete values need a human decision before implementation (excluded from acceptance criteria).
- Division of responsibility between Entra External ID's native email-OTP capability and first-party issuance/verification is not pinned down: if Entra External ID issues and verifies the codes, several controls (entropy, expiry, constant-time comparison) become IdP configuration/assurance items rather than first-party code. The specs name Entra External ID as the consumer provider but author these controls as platform rules; where enforcement lands needs confirmation.
