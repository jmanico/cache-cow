# 076 · Contact form with abuse controls

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-004
- **Title**: Contact form with abuse controls
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The Contact us form MUST validate all submissions server-side, apply rate limiting and CAPTCHA-equivalent abuse control, and MUST NOT allow user input to reach SMTP headers.
- **Rationale**: CC-CNT-004 [P1] mandates the contact form with server-side validation and delegates abuse and injection controls to SECURITY.md, Input validation rule 10: "Contact and other public forms get server-side validation, rate limiting, and CAPTCHA-equivalent abuse control; user input never reaches SMTP headers (email header injection)." A public, unauthenticated form is an abuse magnet (spam relay, resource exhaustion) and an email-header-injection vector (CRLF in name/subject/reply-to fields turning the platform's authenticated sender domain into a phishing relay — compounding the sender-reputation stakes of SECURITY.md, Email and messaging security rule 1).
- **Design**: DESIGN.md §10 (page inventory: Contact); §9 (voice: plain verbs, errors state what happened and what to do next, no apologies, no mascots in error states); §5.4 (pun budget: zero puns in error recovery); §13 (WCAG 2.2 AA, keyboard operability, visible focus).

## Scope
- **Applies To**: Both (Angular storefront form page; ASP.NET Core submission endpoint)
- **Components**: Storefront contact page and form component; server-side contact-submission endpoint with validation, rate limiting, and abuse control; outbound message composition toward Azure Communication Services. Explicitly excluded: the baseline rate-limiting middleware itself (issue 019 — this issue configures the stricter per-form policy on top of it), RFC 9457 error plumbing (issue 021), structured security logging plumbing (issue 022), sender domain authentication (issue 093), transactional-email template pipeline (issue 043).
- **Actors**: Anonymous public visitors (untrusted); automated abusers (bots, spammers); operations staff receiving submissions
- **Data Classification**: Restricted/PII (submitter name, email address, message content)

## Security Context
- **Defense Layer**: Input Validation
- **Threat(s) Addressed**: Email header injection via CRLF in user input (CWE-93, CWE-88 family; OWASP Top Ten A03:2021 Injection); automated abuse/spam of a public unauthenticated endpoint (resource exhaustion, outbound-mail abuse); log injection via submitted values (SECURITY.md, Logging rule 5); XSS if submissions are ever redisplayed (CWE-79, prevented by output encoding per SECURITY.md, Input validation rule 5)
- **Trust Boundary**: Public client → server HTTP boundary (unauthenticated, fully attacker-controlled input per SECURITY.md, Input validation rule 1)
- **Zero Trust Consideration**: Every field is validated server-side against an explicit schema and rejected (not sanitized into acceptance) on failure; client-side validation is a UX aid only, never enforcement; submitted values are treated as hostile in every downstream sink (SMTP composition, logs, any redisplay).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 Validation and Business Logic (server-side schema validation, anti-automation); V1 Encoding and Sanitization (no user input into SMTP headers; encoding before logs/redisplay); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR (CC-CMP-001 — submitter PII from EU visitors; data minimization and retention per CC-CMP-003)
- **Other**: RFC 9457 (error format, via issue 021)

## Acceptance Criteria
1. **AC-01**: Given a well-formed submission, when it is posted, then the server validates every field against an explicit schema (typed DTO with explicit binding sources per SECURITY.md, Input validation rule 3) and accepts it; a submission failing validation is rejected with HTTP 400 and an RFC 9457 problem body, and no processing occurs (CC-CNT-004; SECURITY.md, Input validation rule 1).
2. **AC-02**: Given any field containing CR/LF sequences or header-delimiter content (e.g., `\r\nBcc:` in the name or email field), when the submission is processed, then no user-supplied byte reaches any SMTP header — headers are composed exclusively from server-controlled values, with user content confined to the message body — and the attempt is logged as a validation rejection (SECURITY.md, Input validation rule 10). (Negative case.)
3. **AC-03**: Given repeated submissions from one client exceeding the form's rate limit, when the limit is hit, then subsequent requests receive 429 with `Retry-After` and are not processed (SECURITY.md, HTTP boundary rule 7; CC-CNT-004).
4. **AC-04**: Given a submission that fails the CAPTCHA-equivalent abuse control, when it is posted, then it is rejected without processing or outbound mail, and the rejection is logged (SECURITY.md, Input validation rule 10; mechanism/provider is an Open Question — the criterion binds to the control's outcome, not a vendor).
5. **AC-05**: Given a submission whose body exceeds the request size cap, when it is posted, then it is rejected per the platform's body-size limits (SECURITY.md, HTTP boundary rule 7 — Kestrel `MaxRequestBodySize`/`[RequestSizeLimit]`).
6. **AC-06**: Given submitted values entering log entries, when they are logged, then they are encoded/sanitized against log injection, use structured templates (never string interpolation), and PII is redacted per policy (SECURITY.md, Logging rules 4–5). (Negative case: no raw user input concatenated into log messages.)
7. **AC-07**: Given the form UI with client-side validation disabled or bypassed (direct POST), when invalid input is sent, then the server-side outcome is identical to AC-01/AC-02 — client-side validation is never the sole enforcement (SECURITY.md, Input validation rule 1). (Negative case.)
8. **AC-08**: Given a validation or abuse-control error shown to the user, when the error renders, then it states what happened and what to do next, in the active locale, with no internal details, no puns, and no mascots (DESIGN.md §9, §5.4; SECURITY.md, Logging rule 1).

## Failure Behavior
- **On Invalid Input**: Reject with HTTP 400 and an RFC 9457 problem body (issue 021), log a structured validation-rejection event with correlation ID, disclose no internal state (SECURITY.md, Logging rules 1 and 3). Rate-limit breaches return 429 + `Retry-After` (HTTP boundary rule 7). Abuse-control failures are rejected without processing.
- **On System Error**: Fail closed — an exception in validation or abuse-control checks results in rejection, never acceptance (SECURITY.md, Logging rule 2); outbound mail is never sent for a submission that did not pass all controls.
- **Alerting**: Spikes in validation rejections, rate-limit hits, or abuse-control failures on this endpoint alert via centralized monitoring (SECURITY.md, Logging rules 3 and 8; CC-SEC-010).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests: field schema validation matrix (valid/invalid/boundary); header-injection corpus (CRLF, encoded CRLF, header-name payloads in every field) asserting server-composed headers only; log-output encoding.
- **Integration Tests**: ASP.NET Core integration tests: end-to-end submission through validation, rate limiter (429 + `Retry-After`), abuse-control rejection path, and message composition with a stubbed Azure Communication Services sender asserting no user input in headers.
- **Security Tests**: DAST against the form on staging (CC-QA-007) including injection and anti-automation checks; fuzzing of all fields with CRLF/SMTP metacharacter classes; SAST clean per merge gates.
- **Compliance Tests**: Log-presence checks for validation-rejection and abuse events; evidence that submissions are covered by the retention schedule (CC-CMP-003, enforcement in issue 090).
- **Coverage Target**: ≥ 80% (CC-QA-001); tests tagged CC-CNT-004 per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 019 "Baseline rate-limiting middleware" (this form applies a stricter policy on it); 018 "CORS, HTTP method/media-type allowlists, request size caps"; 021 "RFC 9457 error handling"; 022 "Structured security logging"; 063 "Storefront SSR shell"; 064 "ICU MessageFormat resource pipeline" (form strings and error messages in all locales); 061 "Session cookie policy and CSRF protection" (if the form is cookie-session-bound, antiforgery applies per SECURITY.md, Authentication rule 11).
- **Downstream**: 090 "Retention schedule and automated deletion jobs" (submission PII is a retained data class per CC-CMP-003); 093 "Sender domain authentication" (protects the sending domain this form's mail transits).
- **External**: Azure Communication Services (confirmed email provider, ARCHITECTURE.md "Technology decisions"). The CAPTCHA-equivalent provider is NOT confirmed — see Open Questions; do not select one in implementation without a human decision.

## Implementation Notes
- **Constraints**: Submission endpoint binds only to a dedicated DTO with explicit `[FromBody]` source attributes (SECURITY.md, Input validation rule 3); mail composition uses Azure Communication Services APIs with server-controlled headers only; rate limiting is stricter on this endpoint than the baseline per SECURITY.md, HTTP boundary rule 7; responses carry `Cache-Control: no-store` where sensitive (HTTP boundary rule 3); the page meets WCAG 2.2 AA (DESIGN.md §13).
- **Anti-Patterns**: MUST NOT use client-side validation as sole enforcement (SECURITY.md, Input validation rule 1); MUST NOT interpolate user input into SMTP headers, log messages, or SQL (rules 4, 10; Logging rules 4–5); MUST NOT sanitize invalid input into acceptance — reject it; MUST NOT load an abuse-control script from a third-party runtime CDN in violation of SECURITY.md, Deployment rule 10, or widen the CSP beyond exact allowlisted origins (HTTP boundary rule 2) — this tension is unresolved, see Open Questions.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- **CAPTCHA-equivalent provider/mechanism is not named anywhere in the specs.** SECURITY.md, Input validation rule 10 mandates "CAPTCHA-equivalent abuse control" without choosing one. Any third-party challenge widget conflicts with two standing rules: the ban on third-party runtime CDNs for scripts (SECURITY.md, Deployment rule 10) and the strict default-deny CSP (SECURITY.md, HTTP boundary rule 2 / issue 017), which allowlists only exact payment-processor origins. Candidate shapes (self-hosted proof-of-work, first-party challenge, vendor service with CSP allowlisting) each have different CSP/supply-chain consequences. This requires a human decision; this issue implements the enforcement hook and rejection path either way, and MUST NOT silently pick a vendor.
- The destination and handling of accepted submissions (email to an operations mailbox via Azure Communication Services? persisted queue surfaced in the dashboard? both?) is unspecified in the specs.
- The form's field set (name, email, subject, message, market?) is not enumerated in REQUIREMENTS.md or DESIGN.md; the DTO schema needs a product decision.
- Whether submissions require antiforgery tokens depends on whether the endpoint is cookie-session-authenticated (SECURITY.md, Authentication rule 11 exempts pure bearer/anonymous APIs); the session posture of guest storefront browsing at this endpoint should be confirmed with issue 061.
- Retention period for contact submissions (a PII data class under CC-CMP-003's documented schedule) is not yet defined; owned by issue 090's schedule.
