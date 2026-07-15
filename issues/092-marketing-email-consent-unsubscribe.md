# 092 · Marketing email consent and one-click unsubscribe

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CMP-005
- **Title**: Marketing email consent and one-click unsubscribe
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Compliance

## Requirement
- **Description**: Marketing email MUST be opt-in per market law, with double opt-in for DE and a functioning RFC 8058 one-click unsubscribe on every marketing message (REQUIREMENTS.md CC-CMP-005).
- **Rationale**: CC-CMP-005 encodes per-market email marketing law: consent-based sending (opt-in), the German double-opt-in standard, and RFC 8058 one-click unsubscribe. Marketing mail is also a prime phishing lure (SECURITY.md, Email and messaging security rule 1), so the subscription lifecycle must not itself become an abuse or enumeration vector. Sending is via the confirmed provider, Azure Communication Services (ARCHITECTURE.md, Technology decisions).
- **Design**: Opt-in touchpoints and confirmation/unsubscribe pages follow DESIGN.md §9 (plain verbs, sentence case; no puns in legal/consent content per §5.4) and DESIGN.md §13 accessibility. Email templates exist in all launch locales with the market's primary language as fallback (CC-I18N-006).

## Scope
- **Applies To**: Both
- **Components**: Content & Localization context (marketing email, subscription state), Azure Communication Services sending path, storefront opt-in touchpoints, unsubscribe endpoint (public HTTPS)
- **Actors**: Consumer (subscriber), anonymous visitor (newsletter signup), mailbox providers (RFC 8058 POST callers), marketing staff (dashboard-driven sends, audited)
- **Data Classification**: Restricted/PII (email addresses, consent records)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: Unlawful sending without consent (regulatory exposure per market law); forged subscription of victims' addresses (mitigated by double opt-in); unsubscribe-endpoint abuse — forged mass-unsubscribe or enumeration of subscriber addresses; email header injection from user input (SECURITY.md, Input validation rule 10)
- **Trust Boundary**: Public gateway edge — signup and unsubscribe endpoints and the RFC 8058 POST receiver are unauthenticated public surfaces; every input is attacker-controlled (SECURITY.md, Input validation rule 1)
- **Zero Trust Consideration**: Unsubscribe/confirmation links carry unguessable per-recipient tokens validated server-side; the endpoints trust the token, never a client-supplied email address parameter; consent state is authoritative server-side at send time — the send pipeline re-checks consent per recipient rather than trusting a cached list.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic (subscription lifecycle correctness, injection-free input handling)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation on the public signup/unsubscribe endpoints)
- **NIST SP 800-207**: N/A
- **Regulatory**: Per-market email marketing law as encoded by CC-CMP-005: opt-in per market, double opt-in for DE; GDPR consent standard for ES/DE (CC-CMP-001 — no dark patterns; withdrawing as easy as consenting)
- **Other**: RFC 8058 (one-click unsubscribe: `List-Unsubscribe` + `List-Unsubscribe-Post: List-Unsubscribe=One-Click`, HTTPS POST honored without further user interaction)

## Acceptance Criteria

1. **AC-01**: Given a visitor or customer who has not opted in, when marketing sends execute, then no marketing email is sent to that address in any market — opt-in is checked per recipient at send time (CC-CMP-005).
2. **AC-02**: Given a DE-market signup, when the address is submitted, then no marketing email is sent until the recipient confirms via a tokenized confirmation link delivered to that address (double opt-in), and both the signup and confirmation events are recorded with timestamps (CC-CMP-005).
3. **AC-03**: Given any marketing email, when its headers are inspected, then it carries RFC 8058-conformant `List-Unsubscribe` (HTTPS URI) and `List-Unsubscribe-Post: List-Unsubscribe=One-Click` headers (CC-CMP-005).
4. **AC-04**: Given a mailbox provider issues the RFC 8058 one-click POST to the unsubscribe URI, when the request arrives, then the recipient is unsubscribed immediately without any further interaction (no login, no confirmation page, no "are you sure"), and subsequent sends exclude them (CC-CMP-005).
5. **AC-05**: Given an unsubscribe or confirmation request with a missing, malformed, or invalid token, when it is processed, then no subscription state changes, the request is rejected with a generic error, and the response does not reveal whether the address or token exists (negative case; SECURITY.md, Logging rules 1–2; enumeration-safety consistent with SECURITY.md, Authentication rule 13).
6. **AC-06**: Given user-supplied signup input containing CR/LF or header-injection payloads, when it is processed, then it is rejected server-side and never reaches SMTP headers (negative case; SECURITY.md, Input validation rule 10; CC-CNT-004 pattern).
7. **AC-07**: Given an unsubscribed or never-confirmed (DE) address, when a marketing send is attempted to it, then the send pipeline excludes it and logs the exclusion — cached or stale lists MUST NOT override current consent state (negative case, CC-CMP-005).
8. **AC-08**: Given any launch locale, when confirmation and marketing templates render, then locale-correct templates are used with fallback to the market's primary language, never a broken template (CC-I18N-006).

## Failure Behavior
- **On Invalid Input**: HTTP 400 with RFC 9457 problem details on signup; unsubscribe/confirmation token failures return a generic failure without state change or existence disclosure; all validation rejections logged with correlation ID (SECURITY.md, Input validation rules 1, 10; Logging rule 1).
- **On System Error**: Fail closed for sending — if consent state cannot be verified for a recipient, do not send to that recipient. Unsubscribe processing failures alert rather than silently drop (a lost unsubscribe is a compliance failure).
- **Alerting**: Structured logs for signup, confirmation, and unsubscribe events (no tokens logged — SECURITY.md, Logging rule 4); alert on unsubscribe-endpoint error spikes and on send-pipeline consent-check failures (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit tests for subscription state machine (pending → confirmed → unsubscribed; DE requires confirmed before sendable), token validation, header-injection rejection, RFC 8058 header construction.
- **Integration Tests**: ASP.NET Core integration tests: DE double-opt-in end-to-end; RFC 8058 POST unsubscribes without interaction; send-pipeline exclusion of unsubscribed/pending recipients; Azure Communication Services send path exercised against a test double.
- **Security Tests**: Fuzzing of signup input (CRLF/header injection class); forged/expired token attempts on unsubscribe and confirmation endpoints; rate-limit verification on the public endpoints (SECURITY.md, HTTP boundary rule 7); enumeration parity checks on responses.
- **Compliance Tests**: Automated evidence: RFC 8058 headers present on every marketing template build; consent-event audit records present for each subscription transition; per-market opt-in rule configuration validated in CI.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CMP-005, CC-I18N-006 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 088 EU consent management (consent model and no-dark-patterns standard, CC-CMP-001); 093 Sender domain authentication (SPF/DKIM/DMARC before marketing sends, CC-SEC-018); 043 Transactional order emails (shared Azure Communication Services sending infrastructure and locale template pipeline, CC-I18N-006); 019 Baseline rate-limiting middleware (public endpoints); 064 ICU MessageFormat resource pipeline.
- **Downstream**: 089 Data-subject rights endpoints (marketing consent records in scope for access/erasure); 090 Retention schedule (marketing data class retention, CC-CMP-003).
- **External**: Azure Communication Services (confirmed email provider, ARCHITECTURE.md, Technology decisions).

## Implementation Notes
- **Constraints**: Sending exclusively via Azure Communication Services with managed-identity authentication (SECURITY.md, Secret handling rule 2). Confirmation/unsubscribe tokens are secrets: HTTPS-only links, never logged, kept out of analytics query strings (pattern per SECURITY.md, Authentication rule 14). Subscription state lives in the Content & Localization context's schema under its own least-privilege role (SECURITY.md, Secret handling rule 10). The RFC 8058 endpoint must accept the mailbox-provider POST without CSRF token (it is token-authenticated, not cookie-authenticated — SECURITY.md, Authentication rule 11 scopes CSRF to cookie-authenticated requests). Marketing mail carries no more PII than necessary (CC-ORD-007 pattern; SECURITY.md, Email rule 1).
- **Anti-Patterns**: MUST NOT send marketing mail on signup alone in DE (double opt-in). MUST NOT require login, multi-step flows, or confirmation pages on the RFC 8058 one-click path. MUST NOT use pre-ticked marketing checkboxes or bundle marketing consent into checkout consent (CC-CMP-001 no-dark-patterns standard). MUST NOT let user input reach SMTP headers (SECURITY.md, Input validation rule 10). MUST NOT log unsubscribe/confirmation tokens (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must reference CC-CMP-005 (REQUIREMENTS.md §17).

## Open Questions
- CC-CMP-005 mandates "opt-in per market law" but the specs do not enumerate the per-market rules beyond DE double opt-in; the per-market opt-in matrix (e.g., whether JP/IN/US/ES/MX also require confirmed opt-in) awaits the CC-CMP-002 assessment (issue 091) and human ratification.
- Confirmation-token entropy/expiry values are not specified for this flow (SECURITY.md, Authentication rules 13–14 specify OTP and guest-order tokens, not marketing tokens); parameters need human confirmation before hardening claims are made.
- Whether marketing sends are triggered from the internal dashboard (and thus audited under CC-DSH-004) is not specified; the send-trigger surface needs definition.
