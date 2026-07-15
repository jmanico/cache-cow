# 048 · Invoice PDF rendering and authenticated link-only delivery

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** ARCHITECTURE.md "Known unknowns" — *Data residency vs. "single primary write region"* and *Cross-border transfer mechanism for processors*. Rendered invoice PDFs persist EU/IN personal data (full buyer address), and delivery email flows through Azure Communication Services, a named processor whose transfer basis is undocumented (CC-CMP-006). Rendering, access control, and tests can proceed; production storage topology and email data flows for EU/IN await those human decisions. Not resolved here.

## Metadata
- **ID**: CC-INV-002
- **Title**: Invoice PDF rendering and authenticated link-only delivery
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: Invoice PDFs MUST render server-side from structured invoice data, and consumer invoice email MUST deliver only a link to an authenticated download — never an attachment containing full address data — where guest-order access authenticates via the CC-ORD-010 per-order capability token and the link never resolves to a guessable order identifier (REQUIREMENTS.md CC-INV-002).
- **Rationale**: Link-only delivery was ratified 2026-07-15 (CC-INV-002; ARCHITECTURE.md decision record, "CC-INV-002 link-only invoice delivery"): email is an uncontrolled channel, and an attachment with full address data violates the data-minimization stance of CC-ORD-007 (no full address in email body). Guest orders carry no session identity, so access is gated by the unguessable, expiring, server-revocable per-order capability token of CC-ORD-010 (threat-model-derived CC-SEC-017; SECURITY.md, Authentication and authorization rule 14); a guessable order identifier would allow invoice enumeration and PII disclosure. Account-holder access stays under object-level authorization (SECURITY.md, Authentication rule 9; CC-SEC-007). The Invoicing context owns "server-rendered PDFs behind authenticated download" (ARCHITECTURE.md, "Server bounded contexts" item 7).
- **Design**: The PDF is a document surface: the horizontal logo lockup is the designated variant for documents and invoices (DESIGN.md §2.2); invoice numbers and amounts in IBM Plex Mono, amounts locale-formatted per DESIGN.md §4.4; zero puns in legal/financial content (DESIGN.md §5.4). Dashboard-side invoice viewing/printing is issue 086 (DESIGN.md §12).

## Scope
- **Applies To**: Both (server-side rendering and download endpoint; email delivery to consumers)
- **Components**: Invoicing bounded context (PDF renderer, download endpoint); Content & Localization / transactional email pipeline via Azure Communication Services (issue 043); guest capability-token validation (issue 042); object-level authorization (issue 062).
- **Actors**: Guest purchasers (capability token), authenticated account holders (session + object-level authorization), the email pipeline.
- **Data Classification**: Restricted/PII (full name and address on the invoice document) and Regulated (legal financial record)

## Security Context
- **Defense Layer**: Strict API (authenticated download endpoint); Architecture (link-only channel design)
- **Threat(s) Addressed**: OWASP Top Ten A01:2021 Broken Access Control — IDOR/enumeration of invoices (CWE-639); predictable-token access (CWE-330); PII disclosure through the email channel; STRIDE Information Disclosure.
- **Trust Boundary**: Client-server edge at the download endpoint: every request proves authorization per request — capability token (guest) or session identity + ownership check (account).
- **Zero Trust Consideration**: The link itself confers nothing without server-side validation of an unexpired, unrevoked capability token bound to exactly one order, or an authenticated session passing object-level authorization scoped to the caller (SECURITY.md, Authentication rules 9, 14). No trust is placed in email possession alone beyond the token's own entropy, expiry, and revocability.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); Authorization/Access Control and Session Management chapters (unguessable, expiring, revocable access tokens; object-level checks).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), SC-8 (transmission confidentiality — HTTPS-only links)
- **NIST SP 800-207**: Per-request authorization; possession of a URL is not identity — every download is independently validated.
- **Regulatory**: GDPR data minimization for ES/DE consumers (CC-CMP-001) — no full address data in email; invoice content legality per CC-INV-001 (issues 046/047).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given an issued invoice with structured data (issues 046/047), when the PDF is generated, then it renders server-side from those structured fields only — no client-side rendering, no free-text CMS content (CC-INV-002).
2. **AC-02**: Given a consumer invoice email, when it is sent, then it contains a link to the authenticated download and no PDF attachment and no full address data in the body (CC-INV-002; CC-ORD-007).
3. **AC-03**: Given a guest order, when the invoice link is requested with a valid, unexpired, unrevoked CC-ORD-010 capability token (≥ 128 bits entropy, bound to exactly one order — issue 042), then the PDF downloads over HTTPS with `Cache-Control: no-store` (CC-INV-002; SECURITY.md, Authentication rule 14; HTTP boundary rule 3).
4. **AC-04**: Given a missing, expired, revoked, or wrong-order capability token, when the download is requested, then the server returns 404 without confirming whether the invoice exists, and logs an authorization-denial event (SECURITY.md, Authentication rules 9, 14; Logging rule 3).
5. **AC-05**: Given an authenticated account holder, when they request an invoice download, then object-level authorization scopes access to their own orders' invoices; a request for another customer's invoice returns 404 and is logged (SECURITY.md, Authentication rule 9; CC-SEC-007; issue 062).
6. **AC-06**: Negative: the download link MUST NOT contain or resolve via a guessable or enumerable identifier (sequential invoice/order numbers, order-number-plus-email) — enumeration attempts across identifier ranges yield uniform 404s (CC-INV-002; CC-ORD-010).
7. **AC-07**: Negative: capability tokens MUST NOT appear in logs or telemetry, in the `Referer` header, or in analytics query strings (SECURITY.md, Authentication rule 14; Logging rule 4; Email and messaging security rule 1 — never in logged headers/metadata).
8. **AC-08**: Given any invoice locale, when the PDF renders, then amounts are locale-formatted (JPY zero-decimal, INR lakh/crore grouping) per CC-PRC-004 and DESIGN.md §4.4, from the structured tax content of issue 047.

## Failure Behavior
- **On Invalid Input**: Invalid or malformed token/identifier → 404 (existence not confirmed — SECURITY.md, Authentication rule 9), RFC 9457 problem details where a body is returned, correlation ID logged, no internal state disclosed (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception in token validation or authorization is a denial (SECURITY.md, Logging rule 2); a PDF rendering failure returns a generic error and never a partial document or another order's data.
- **Alerting**: Spikes in 404/denial responses on the download endpoint (enumeration attempts) and token-validation failures alert via centralized monitoring (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for token-gated access decisions (valid/expired/revoked/wrong-order), link construction (no guessable identifiers), and PDF composition from structured fixtures across all seven launch locales. Tagged CC-INV-002, CC-ORD-010, CC-SEC-017 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests: guest download happy path; account-holder ownership path; cross-tenant attempts fail closed with 404 (CC-QA-005 IDOR coverage on invoices); email content assertions (link present, no attachment, no address) against the issue-043 pipeline with Azure Communication Services stubbed.
- **Security Tests**: Enumeration fuzzing across identifier ranges asserting uniform 404s; log-scrubbing assertion that tokens never reach logs/telemetry (SECURITY.md, Logging rule 4); DAST against the download endpoint per CC-QA-007.
- **Compliance Tests**: Automated evidence that consumer invoice emails carry no attachment and no full address data (CC-INV-002; CC-ORD-007).
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 042 (guest order capability tokens — CC-ORD-010), 043 (transactional email pipeline, all locales), 046 (invoice core), 047 (per-market tax content), 062 (object-level authorization and IDOR suite), 021 (error handling), 022 (structured logging/redaction), 016/017 (HTTPS/security headers on the download endpoint).
- **Downstream**: 086 (dashboard invoice management may reuse the server-side renderer), 052 (wholesale portal invoice history — partner access is governed by portal tenancy/API scopes, not by this consumer flow).
- **External**: Azure Communication Services (email delivery — confirmed vendor, ARCHITECTURE.md).
- **Open decision**: AT RISK on data residency vs. single primary write region, and on the cross-border transfer mechanism (Azure Communication Services) (ARCHITECTURE.md, "Known unknowns") — see blockquote.

## Implementation Notes
- **Constraints**: Renderer runs inside the Invoicing bounded context (ARCHITECTURE.md, "Server bounded contexts" item 7) reading only structured invoice data (issues 046/047). Download endpoint: HTTPS-only (SECURITY.md, HTTP boundary rule 1), `Cache-Control: no-store` (rule 3), never edge-cached (rule 10 — authenticated/personalized responses), correct Content-Type with `X-Content-Type-Options: nosniff`. `Referrer-Policy: strict-origin-when-cross-origin` (rule 3) supports keeping tokens out of `Referer`. Guest links carry the CC-ORD-010 token as the sole access credential; account-holder links resolve through session authorization (rule 9). Email templates exist in all launch locales with market-primary-language fallback (CC-I18N-006; issue 043).
- **Anti-Patterns**: MUST NOT attach the PDF to email or place full address data in email bodies (CC-INV-002; CC-ORD-007); MUST NOT use order-number-plus-email as the guest access mechanism (CC-ORD-010); MUST NOT log capability tokens (SECURITY.md, Logging rule 4); MUST NOT return 403 or existence-confirming errors for inaccessible invoices (SECURITY.md, Authentication rule 9); MUST NOT render invoice content from free text or client-supplied data (CC-INV-002; CC-PRC-005).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). Any PDF library added must satisfy the full dependency policy — justification with CVE-history review, active maintenance within 6 months, latest stable major, pinned versions, transitive audit (SECURITY.md, Dependency Rules 1–8). PR must cite CC-INV-002 (REQUIREMENTS.md §17).

## Open Questions
- The PDF rendering mechanism/library is unspecified; whatever is chosen must clear SECURITY.md's Dependency Rules (prefer zero new dependencies; justify if genuinely required).
- The expiry duration and re-issuance flow for guest invoice links is unspecified: CC-ORD-010 requires tokens to be expiring and revocable but sets no lifetime, and no flow is defined for a guest who needs the invoice after token expiry.
- Whether rendered PDFs are persisted (stored artifacts inheriting encryption/residency/retention rules per SECURITY.md, Secret handling rule 6) or rendered on demand per request is unspecified.
- Whether wholesale (B2B) invoice PDF delivery reuses this renderer and which access path governs it (portal session per CC-WHS tenancy vs. `invoices:read` API scope per CC-API-004) is not stated in CC-INV-002, which addresses the consumer flow.
