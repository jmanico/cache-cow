# 017 · CSP and security headers with payment-processor origin allowlists

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-003 (CSP and security-header clauses; authored in SECURITY.md, HTTP boundary rules 2–3)
- **Title**: CSP and security headers with payment-processor origin allowlists
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every HTML response MUST ship a strict Content-Security-Policy (nonce- or hash-based scripts, no `unsafe-inline`, `frame-ancestors 'none'`, `base-uri 'self'`, `form-action 'self'`) whose `form-action`, `frame-src`, and `connect-src` directives additionally allowlist only the exact external origins each payment processor requires (Stripe, Razorpay, and the local methods PayPal, SEPA, konbini, UPI — specific origins only, never wildcards or suffix matches), rolled out via Report-Only with violation collection before enforcement, and every response MUST emit `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, and a Permissions-Policy, with `Cache-Control: no-store` on authenticated and sensitive responses (SECURITY.md, HTTP boundary rules 2–3; CC-SEC-003).
- **Rationale**: CSP is the platform's structural XSS containment: with CMS content (Contentful), five translation supply chains (SECURITY.md, Input validation rule 7), and attacker-reachable forms, a script injection must be unable to execute. Redirect- and hosted-field-based payment methods break under `form-action 'self'` alone, so the processor origins are allowlisted exactly — wildcards or suffix matches would let an attacker-controlled subdomain receive checkout submissions (SECURITY.md, HTTP boundary rule 2). `no-store` on authenticated/sensitive responses is the header-level half of the cache discipline that CC-MKT-009/CC-SEC-013 depend on (cache-key architecture itself is issue 028). Report-Only rollout prevents a policy typo from breaking checkout — comedy never touches money movement, and neither do untested headers.
- **Design**: DESIGN.md §14 delegates all front-end security constraints to SECURITY.md; Angular components are CSP-friendly by construction — no component may require inline event handlers or inline styles (SECURITY.md, HTTP boundary rule 2).

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core security-header middleware (all hosts); Angular SSR storefront (nonce propagation into server-rendered HTML), wholesale portal, internal dashboard; checkout payment surfaces (Stripe/Razorpay hosted fields and redirects); CSP violation-report collection endpoint
- **Actors**: Anonymous visitors, authenticated consumers, wholesale-portal buyers, staff/admins; attackers attempting XSS, clickjacking, form-action exfiltration, or MIME confusion
- **Data Classification**: Restricted/PII (checkout, session, and account pages are covered surfaces); Regulated adjacency: payment-processor surfaces (card data itself never enters the system, CC-ORD-003)

## Security Context
- **Defense Layer**: Encoding (browser-enforced execution policy, defense in depth behind SECURITY.md Input validation rule 5's no-raw-HTML-sink rule)
- **Threat(s) Addressed**: XSS execution (OWASP Top Ten A03:2021, CWE-79); clickjacking (`frame-ancestors 'none'`, CWE-1021); base-tag hijack (CWE-Open Redirect class via `base-uri`); checkout form exfiltration to attacker origins via `form-action`; MIME sniffing (CWE-430 class, `nosniff`); referrer leakage of URLs (`Referrer-Policy`; interlocks with capability-token secrecy, SECURITY.md Authentication rule 14); sensitive-response caching (CC-SEC-013 header half). STRIDE: Tampering, Information Disclosure.
- **Trust Boundary**: Client–server edge — the browser is directed to enforce policy on everything it renders, treating all page content (CMS, translations, user data) as potentially compromised.
- **Zero Trust Consideration**: Assumes output encoding can fail somewhere: even then, non-nonced/non-hashed script must not execute and forms must not post outside the exact allowlist. No origin is trusted by pattern — only exact, enumerated processor origins.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Web Frontend Security (CSP, security headers, clickjacking defense); V13 Configuration (chapter-level per SECURITY.md Baseline, ASVS 5.0 L2 in full)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: CM-6 (Configuration Settings), SC-8 (in combination with issue 016 transport controls)
- **NIST SP 800-207**: Policy enforced per-response; no implicit trust in page content or third-party origins
- **Regulatory**: Supports the SAQ A hosted-fields/redirect payment posture (CC-ORD-003; SECURITY.md, Secret handling rule 7) by constraining payment frames/form targets to processor origins
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any HTML response from storefront, portal, or dashboard, when its headers are inspected, then it carries a CSP with nonce- or hash-based `script-src`, no `unsafe-inline` in any directive, `frame-ancestors 'none'`, `base-uri 'self'`, and `form-action 'self'` (plus exact processor origins only where the checkout surface requires them), and the Angular SSR-rendered document's scripts carry the response's nonce (CC-SEC-003; SECURITY.md, HTTP boundary rule 2).
2. **AC-02**: Given the checkout payment surface, when the CSP is inspected, then `form-action`, `frame-src`, and `connect-src` contain only the exact external origins required by Stripe, Razorpay, and the local methods (PayPal, SEPA, konbini, UPI), every other directive stays default-deny, and a configuration-validation test fails if any CSP source contains a wildcard (`*`) or suffix/pattern match (SECURITY.md, HTTP boundary rule 2).
3. **AC-03**: Given rollout, when the CSP is first deployed to an environment, then it ships as `Content-Security-Policy-Report-Only` with a `report-to`/report collection endpoint receiving violation reports into centralized monitoring, and enforcement mode is enabled only after the Report-Only phase (SECURITY.md, HTTP boundary rule 2; Logging rule 3).
4. **AC-04**: Given any response (HTML, JSON, static asset), when headers are inspected, then `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, and a Permissions-Policy are present (CC-SEC-003; SECURITY.md, HTTP boundary rule 3).
5. **AC-05**: Given an authenticated or sensitive response (account, cart, checkout, order status, dashboard, portal), when headers are inspected, then `Cache-Control: no-store` is present (SECURITY.md, HTTP boundary rule 3; supports CC-SEC-013/CC-MKT-009 — full cache-key discipline is issue 028).
6. **AC-06**: Negative — given a page containing an injected inline `<script>` (no nonce/hash) or an inline event handler, when rendered under the enforced CSP, then the script MUST NOT execute and a violation report is received; given a form manipulated to post to a non-allowlisted origin, the browser MUST block the submission.
7. **AC-07**: Negative — no Angular component in any of the three clients requires inline event handlers or inline styles to function under the enforced CSP; the production build runs correctly with the strict policy enforced (SECURITY.md, HTTP boundary rule 2; Deployment rule 9 AOT build).

## Failure Behavior
- **On Invalid Input**: Not input-driven; policy is server-set. CSP violation reports (attempted inline execution, disallowed form/frame/connect targets) are collected, structured-logged, and monitored (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — if nonce generation fails, the response MUST NOT be served with a weakened or absent CSP; header middleware failure is a 500 via the RFC 9457 handler (issue 021), never a headerless page.
- **Alerting**: Alert on CSP violation-report spikes and on any violation from checkout/payment surfaces (potential skimming attempt) to centralized monitoring per SECURITY.md Logging rule 3 / CC-NFR-003.

## Test Strategy
- **Unit Tests**: Header-middleware tests per host asserting every directive of AC-01/AC-04/AC-05; CSP builder tests rejecting wildcard/suffix sources; nonce uniqueness per response. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: `WebApplicationFactory` tests asserting headers on HTML, JSON, and static responses; Angular SSR integration test asserting rendered script tags carry the response nonce; report-endpoint test asserting violation reports are persisted as structured events.
- **Security Tests**: DAST (CC-QA-007) header audit on every surface; CI grep gate for `bypassSecurityTrust*`/unsanitized `[innerHTML]` remains issue 006's gate but is exercised here by the no-inline-handler build check; manual pentest checklist item: attempt form-action exfiltration and framing on checkout.
- **Compliance Tests**: Automated per-release header-evidence capture per surface; CSP config snapshot diffed in CI so allowlist changes require review.
- **Coverage Target**: ≥ 80% branch coverage for the header/CSP middleware module; tests tagged `CC-SEC-003` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 016 TLS/HSTS enforcement and security middleware ordering (this middleware runs in the header slot ordered there); 001 Solution scaffold; 004 Angular workspace scaffold (CSP-compatible construction, AOT builds)
- **Downstream**: 039 Stripe payment integration and 040 Razorpay payment integration (each supplies and verifies its exact processor origin list against this allowlist mechanism); 028 Cache-safe gating (builds on the no-store discipline); 063 Storefront SSR shell (nonce propagation); 069 Checkout UI; 072 Contentful integration (CSP is its containment backstop); 079 Dashboard shell
- **External**: Stripe (incl. PayPal, SEPA, konbini local methods), Razorpay (UPI + cards) — origin lists per their integration documentation; Azure Monitor (violation-report ingestion)

## Implementation Notes
- **Constraints**: ASP.NET Core header middleware ordered per issue 016; Angular built with AOT, strict templates, no inline styles/handlers (SECURITY.md, Deployment rule 9); SSR nonce must flow from the ASP.NET response into the Angular SSR-rendered document. Fonts/assets are self-hosted (SECURITY.md, Deployment rule 10), so no font/CDN origins belong in the CSP — a directive needing a third-party CDN origin is a defect, not an allowlist entry.
- **Anti-Patterns**: MUST NOT use `unsafe-inline` or `unsafe-eval` in any directive; MUST NOT use wildcard or suffix-matched CSP sources (e.g., `*.stripe.com` patterns beyond what "exact origins" means — enumerate specific origins); MUST NOT skip the Report-Only phase; MUST NOT weaken every-response headers per-route as a convenience; MUST NOT serve authenticated/sensitive responses without `no-store`.
- **AI Development Guidance**: AI-generated header/CSP code passes the identical merge gates (tests, coverage, lint, SAST/SCA/secret scan, no raw-HTML sinks) plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). CSP allowlist changes are security-sensitive and always human-reviewed.

## Open Questions
- The specs mandate "the exact external origins each processor requires" but do not enumerate them; the concrete origin lists must be taken from Stripe and Razorpay integration documentation during issues 039/040 and reviewed into the allowlist — they are recorded here as required inputs, not guessed.
- Duration/exit criteria for the Report-Only phase (e.g., N days with zero unexplained violations) are not specified.
- The specific Permissions-Policy directive set (which features to deny) is not enumerated in the specs; SECURITY.md requires only that "a Permissions-Policy" be emitted.
- The CSP violation-report endpoint's own abuse controls (it is an unauthenticated public endpoint) are not specified; presumably issue 019's baseline rate limiting applies.
