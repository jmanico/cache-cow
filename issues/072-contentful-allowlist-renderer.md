# 072 · Contentful integration and sanitizing allowlist renderer

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-001, CC-SEC-002
- **Title**: Contentful integration and sanitizing allowlist renderer
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: All CMS-sourced content MUST be retrieved from Contentful by the Content & Localization module and rendered exclusively through a sanitizing allowlist renderer with no raw-HTML sinks on any client, and Contentful publish events MUST be accepted only after signature verification through the inbound-callback framework.
- **Rationale**: SECURITY.md (Input validation rule 1) classifies CMS content as attacker-controlled input crossing a trust boundary; a compromised CMS account or malicious rich-text entry becomes stored XSS unless rendering is allowlist-based. CC-SEC-002 requires output encoding, no raw-HTML sinks, and the allowlist renderer (authored in SECURITY.md, Input validation rule 5). ARCHITECTURE.md confirms Contentful as the CMS ("Technology decisions") and assigns CMS-sourced rendering to bounded context 10, Content & Localization ("all CMS content renders through the sanitizing allowlist renderer"). SECURITY.md (Input validation rule 11) names CMS publish events among the inbound callbacks that are an untrusted boundary requiring signature verification before parsing.
- **Design**: N/A for the renderer itself (infrastructure for content pages); consuming pages (DESIGN.md §10: Chefs, Cows, Cuts, legal) are issues 073–075 and 077.

## Scope
- **Applies To**: Both (server-side rendering pipeline in the Content & Localization module; Angular storefront consumption of its sanitized output)
- **Components**: Content & Localization bounded context (ARCHITECTURE.md, "Server bounded contexts" item 10): Contentful delivery integration, sanitizing allowlist renderer, Contentful publish-event receiver. Explicitly excluded: the CI grep gate banning raw-HTML sinks (issue 006), the generic inbound-callback signature-verification framework (issue 041), the ICU MessageFormat translation pipeline (issue 064), and the individual content pages (issues 073–075, 077).
- **Actors**: CMS editors (via Contentful, untrusted at the platform boundary), consumer storefront visitors, the Contentful publish-event sender (untrusted callback origin)
- **Data Classification**: Public (published marketing/content copy) — treated as untrusted input regardless

## Security Context
- **Defense Layer**: Sanitization (allowlist rendering) and Input Validation (publish-event verification)
- **Threat(s) Addressed**: Stored XSS via CMS rich text (OWASP Top Ten A03:2021 Injection, CWE-79); forged CMS publish events driving content state (SECURITY.md, Input validation rule 11); unsafe URL schemes in data-derived `href`/`src` (CC-SEC-004, CWE-601/CWE-79)
- **Trust Boundary**: Contentful → platform (both content delivery and inbound publish callbacks); server → browser (encoded output only)
- **Zero Trust Consideration**: CMS content is never trusted because it is "ours": every element, attribute, and URL is validated against an explicit allowlist and everything outside it is rejected, not sanitized into acceptance (SECURITY.md, Input validation rules 1 and 5); publish events are authenticated by signature over the raw body before parsing (rule 11), never by source IP or presence alone.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V1 Encoding and Sanitization (context-aware output encoding, sanitization of untrusted rich content); V2 Validation and Business Logic (allowlist validation of untrusted input); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a Contentful rich-text entry containing only allowlisted elements and attributes, when a content page renders it, then the output is produced by the sanitizing allowlist renderer and is context-encoded for HTML (CC-SEC-002; SECURITY.md, Input validation rule 5).
2. **AC-02**: Given a CMS entry containing `<script>`, inline event handlers (e.g., `onerror`), or a `javascript:` URL, when the entry is rendered, then none of that content reaches any response in executable or attribute form — it is stripped or the entry is rejected, and the rejection is logged as a structured validation event (CC-SEC-002; SECURITY.md, Input validation rule 5; Logging rule 3). (Negative case.)
3. **AC-03**: Given the Angular storefront codebase consuming CMS content, when it is reviewed and scanned, then it contains no `bypassSecurityTrust*` calls and no unsanitized `[innerHTML]`/`outerHTML` bindings — CMS output is bound only through the sanitized pipeline (SECURITY.md, Input validation rule 5; the blocking CI grep gate itself is issue 006). (Negative case.)
4. **AC-04**: Given a data-derived `href` or `src` inside CMS content, when it is rendered, then its scheme is validated against the https/mailto/tel allowlist with `#` fallback on failure (CC-SEC-004; SECURITY.md, Input validation rule 6).
5. **AC-05**: Given an inbound Contentful publish event with a missing or invalid signature, when it reaches the receiver, then it is rejected before parsing via the issue 041 inbound-callback framework, no content state changes, and a security event is logged and alerted (SECURITY.md, Input validation rule 11; Logging rule 3). (Negative case.)
6. **AC-06**: Given a sanitizer or renderer exception while processing an entry, when the page renders, then the affected content is omitted or the request fails with a generic error — raw, unsanitized CMS content is never emitted as a fallback (SECURITY.md, Logging rules 1–2).

## Failure Behavior
- **On Invalid Input**: CMS entries failing allowlist validation are rejected/stripped, never sanitized into acceptance; the validation rejection is logged as a structured event with correlation ID (SECURITY.md, Input validation rule 1; Logging rule 3). Invalid publish-event signatures are rejected with 400/401 per the issue 041 framework, with no state change.
- **On System Error**: Fail closed — any exception in the sanitization path suppresses the content or fails the request with an RFC 9457 generic error (issue 021); raw content is never a fallback (SECURITY.md, Logging rules 1–2).
- **Alerting**: Publish-event signature-verification failures are logged and alerted as security events (SECURITY.md, Secret handling rule 9; Logging rule 3); spikes in sanitizer rejections alert via centralized monitoring (CC-SEC-010).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the allowlist renderer: allowlisted markup passes; scripts, event handlers, disallowed elements/attributes, and disallowed URL schemes are stripped/rejected; encoding is context-correct. Fuzz-style corpus of XSS payloads (polyglots, nested/malformed markup) asserting no executable output.
- **Integration Tests**: ASP.NET Core integration tests rendering pages from stubbed Contentful payloads end-to-end through SSR, asserting sanitized output; publish-event receiver tests (valid, missing, and invalid signatures) through the issue 041 framework.
- **Security Tests**: SAST plus the raw-HTML-sink CI grep gate (issue 006) over the Angular workspace; DAST XSS checks against CMS-rendered pages on staging (CC-QA-007).
- **Compliance Tests**: CI evidence that the sink gate ran clean; structured-log presence checks for signature-failure security events.
- **Coverage Target**: ≥ 80% for the Content & Localization rendering module (CC-QA-001); tests tagged CC-CNT-001, CC-SEC-002, CC-SEC-004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 "Solution scaffold" (Content & Localization module home); 041 "Inbound processor webhook verification and payment authority" (the shared inbound-callback verification framework used for Contentful publish events); 021 "RFC 9457 error handling"; 022 "Structured security logging"; 014 "Azure Key Vault" (verification secrets per SECURITY.md, Secret handling rule 9).
- **Downstream**: 073 "Meet our Chefs page", 074 "Meet our Cows page", 075 "Meet our Cuts interactive diagram", 077 "Legal pages per market" (all render CMS content through this renderer); 006 "CI merge gates" enforces the sink ban this issue must satisfy (SECURITY.md, Deployment rule 7).
- **External**: Contentful (confirmed CMS, ARCHITECTURE.md "Technology decisions").

## Implementation Notes
- **Constraints**: Renderer lives in the Content & Localization bounded context (ARCHITECTURE.md item 10) behind its module boundary; Angular SSR consumes only sanitized, typed output (SECURITY.md, Input validation rule 1: clients render only from typed, validated responses); Contentful credentials and publish-event verification material live in Key Vault, accessed via managed identity (SECURITY.md, Secret handling rules 1–2, 9); any caching of rendered CMS content is keyed on transacting market + locale per SECURITY.md, HTTP boundary rule 10 (CC-MKT-009).
- **Anti-Patterns**: No `bypassSecurityTrust*`, no unsanitized `[innerHTML]`/`outerHTML` (SECURITY.md, Input validation rule 5); no denylist-based sanitization ("reject invalid input rather than sanitizing it into acceptance", rule 1); no rendering path that bypasses the allowlist renderer for "trusted" CMS spaces; no free-text CMS content for allergen/nutrition data (CC-CAT-004 — that data renders from structured fields, outside this renderer's scope); no accepting publish events on source-IP trust or without raw-body signature verification (rule 11).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs mandate an allowlist renderer but do not enumerate the allowed element/attribute set for CMS rich text; the concrete allowlist needs definition and review.
- Whether Contentful content is fetched at request time, synced/cached server-side, or both is unspecified; any caching layer must satisfy the market/locale cache-key discipline of CC-MKT-009 / SECURITY.md HTTP boundary rule 10.
- The specs name CMS publish events as verified inbound callbacks (SECURITY.md, Input validation rule 11) but do not specify what platform behavior a publish event triggers (cache invalidation, content sync, nothing); scope here covers only verified receipt and rejection.
