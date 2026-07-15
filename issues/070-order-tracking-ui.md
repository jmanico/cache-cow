# 070 · Order tracking UI (five stages)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-008 (guest access via CC-ORD-010/CC-SEC-017, enforced by issue 042; authenticated access via CC-SEC-007, enforced by issue 062)
- **Title**: Order tracking UI (five stages)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Order tracking MUST expose exactly the five consumer-facing stages Smoked, Frozen, Packed, In transit, Delivered, mapped server-side from the internal order state machine plus carrier events, and rendered as arc-fan segments filling in Ember with plain-text stage labels and timestamps in IBM Plex Mono (REQUIREMENTS.md CC-ORD-008; DESIGN.md §7 "Order tracker", §5.1).
- **Rationale**: CC-ORD-008 fixes the consumer-facing tracking vocabulary at five stages mapped from the internal state machine (`received -> confirmed -> packed -> shipped -> delivered` with `cancelled`/`refunded` terminal branches, CC-ORD-006, issue 035) plus carrier events. DESIGN.md §5.1 makes the order tracker one of exactly four permitted uses of the smoke arc-fan motif — progress arcs fill as the order advances. Access control matters because order status and tracking reveal purchase and delivery data: guest access is gated by the unguessable, expiring, server-revocable per-order capability token (CC-ORD-010, CC-SEC-017; SECURITY.md, Authentication rule 14), and account access by server-side object-level authorization (CC-SEC-007; SECURITY.md, Authentication rule 9). Carrier events arrive over an untrusted boundary and are signature-verified before they influence anything (SECURITY.md, Input validation rule 11; issue 041).
- **Design**: DESIGN.md §7 (Order tracker: five stages, arc-fan segments filling in Ember, each stage labeled in plain text with a timestamp in Plex Mono); §5.1 (smoke-as-signal motif scarcity — tracker is a permitted placement); §4.1 (Plex Mono for dates/data); §13 (`prefers-reduced-motion` disables the arc-fill animation and all reveals, content renders in final state; status never conveyed by color alone; visible focus, ARIA labels); §9 (errors state what happened and what to do next; no mascots in error states); tokens from `tokens.json` (issue 005) — no hardcoded Ember/Smoke values.

## Scope
- **Applies To**: Both (Angular SSR storefront tracking page + server-side stage-mapping read endpoint)
- **Components**: Tracking page UI (arc-fan progress component, stage labels/timestamps); server-side mapping from internal order states (issue 035) and verified carrier events (issues 041, 044/EasyPost) to the five consumer stages; guest (token) and authenticated entry points. Excluded: capability-token issuance/validation mechanics (issue 042), object-level authorization framework and IDOR suite (issue 062), the order state machine itself (issue 035), carrier webhook verification (issue 041), tracking links in transactional emails (issue 043).
- **Actors**: Guest consumer (capability-token bearer), authenticated consumer
- **Data Classification**: Restricted/PII (order and delivery progress tied to a person)

## Security Context
- **Defense Layer**: Strict API (server-side mapping and authorization; UI displays only what the server released)
- **Threat(s) Addressed**: Order status/tracking enumeration and IDOR (CC-SEC-007, CC-SEC-017 — guest access never via guessable order-number-plus-email, CC-ORD-010); capability-token leakage via logs, `Referer`, or analytics query strings (SECURITY.md, Authentication rule 14); display of forged progress from unverified carrier callbacks (SECURITY.md, Input validation rule 11)
- **Trust Boundary**: Client–server edge for tracking reads; the carrier/EasyPost event receiver is a distinct untrusted boundary handled by issue 041
- **Zero Trust Consideration**: Every tracking read is authorized per request (token validation or object-level authorization, server-side); stage state renders only from typed, validated server responses (SECURITY.md, Input validation rule 1); no client-side inference of stage from carrier data

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, "Baseline"); Access Control chapter (object-level authorization, capability-token gating via issues 042/062)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement on order resources)
- **NIST SP 800-207**: Per-request authorization of every tracking read, no implicit trust in possession of a URL beyond the validated capability token
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA (CC-NFR-004; DESIGN.md §13)

## Acceptance Criteria
1. **AC-01**: Given an order in any internal state, when its tracking view is requested, then exactly one of the five stages — Smoked, Frozen, Packed, In transit, Delivered — is presented as current, derived from a single documented server-side mapping over the internal state machine plus verified carrier events; the client performs no mapping of its own (CC-ORD-008, CC-ORD-006 via issue 035).
2. **AC-02**: Given a tracking view, when it renders, then progress appears as arc-fan segments filling in Ember (design tokens from issue 005), each stage carries a plain-text label, and each reached stage shows its timestamp in IBM Plex Mono (DESIGN.md §7, §5.1, §4.1).
3. **AC-03**: Given a user agent with `prefers-reduced-motion`, when the tracking view renders, then the arc-fill animation and all reveals are disabled and content renders in its final state; stage status is additionally never conveyed by color alone (DESIGN.md §13).
4. **AC-04**: Given a guest order, when tracking is requested with a valid per-order capability token (issue 042), then the tracking view is returned; when the token is missing, expired, revoked, or bound to a different order, then the response is HTTP 404 with no indication the order exists (negative case; CC-ORD-010, CC-SEC-017; SECURITY.md, Authentication rules 9 and 14).
5. **AC-05**: Given an authenticated consumer, when they request tracking for an order they do not own, then the response is HTTP 404 (object-level authorization via issue 062); an order-number-plus-email pair MUST NOT grant tracking access on any path (negative case; CC-SEC-007, CC-ORD-010).
6. **AC-06**: Given any tracking response, when headers and telemetry are inspected, then the response is `Cache-Control: no-store` and never edge-cached (SECURITY.md, HTTP boundary rule 10; issue 028), and the capability token appears in no server log, telemetry event, or analytics query string (SECURITY.md, Authentication rule 14; Logging rule 4).
7. **AC-07**: Given an unverified or signature-invalid carrier callback (rejected by issue 041), when tracking is subsequently viewed, then the displayed stage and timestamps are unchanged — unverified events MUST NOT advance the consumer-facing display (negative case; CC-ORD-009 context; SECURITY.md, Input validation rule 11).

## Failure Behavior
- **On Invalid Input**: Invalid or unauthorized tracking requests return HTTP 404 (existence-hiding hardening per SECURITY.md, Authentication rule 9) with an RFC 9457 body; the authz denial is logged as a structured security event with correlation ID (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — an exception in token validation or object-level authorization is a denial, never a bypass (SECURITY.md, Logging rule 2); if the stage mapping encounters an unknown internal state, the server returns a generic error with correlation ID rather than fabricating a stage (SECURITY.md, Logging rules 1, 7).
- **Alerting**: Spikes in guest-token validation failures (enumeration/brute-force signal) and unknown-state mapping errors alert via centralized structured logging (SECURITY.md, Logging rule 3; issue 022).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests over the stage-mapping function: every internal state and every mapped carrier-event type yields exactly one expected consumer stage; unknown state fails closed.
- **Integration Tests**: ASP.NET Core integration tests: guest access with valid/expired/revoked/wrong-order tokens (404 on all invalid paths), authenticated cross-user access (404), `no-store` header assertions, verified-vs-unverified carrier event effect on the read model (with issue 041's verifier).
- **Security Tests**: Cross-tenant/cross-user attempts folded into the authz suite (CC-QA-005 via issue 062); log-scrub assertion that tokens never appear in structured logs or telemetry (SECURITY.md, Logging rule 4).
- **Compliance Tests**: Automated accessibility checks on the tracker (labels, contrast, reduced-motion, keyboard/focus) per CC-NFR-004 (issue 098 gates).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-ORD-008, CC-ORD-010, CC-SEC-017 (REQUIREMENTS.md §17).
- **Angular Component Tests**: Arc-fan segment fill per stage, plain-text labels and Plex Mono timestamps, `prefers-reduced-motion` behavior, error states without mascots (DESIGN.md §9).

## Dependencies
- **Upstream**: 005 "Design token pipeline" (Ember/Smoke tokens, type); 028 "Cache-safe gating" (no-store personalized responses); 035 "Order state machine with audited transitions"; 041 "Inbound processor webhook verification" (verified carrier/EasyPost events); 042 "Guest order capability tokens"; 062 "Object-level authorization and cross-tenant/IDOR test suite"; 063 "Storefront SSR shell"; 064 "ICU MessageFormat resource pipeline" (stage labels and copy).
- **Downstream**: 043 "Transactional order emails in all locales" (shipment notification links to tracking); 098 "Accessibility gates" (tracker in scope).
- **External**: EasyPost (carrier events, consumed through issues 041/044 — no direct integration in this issue).

## Implementation Notes
- **Constraints**: Angular SSR (`@angular/ssr`) with hydration; tracking is personalized and never edge-cached, and its SSR transfer state carries only data authorized for that exact response (SECURITY.md, HTTP boundary rule 10); the mapping lives server-side in the Ordering & Payments context read path (ARCHITECTURE.md, "Server bounded contexts" 4) so clients display what the server already decided (ARCHITECTURE.md, Dependency rule 1 spirit); arc motif usage stays within DESIGN.md §5.1's four permitted placements; `Referrer-Policy: strict-origin-when-cross-origin` (issue 017) supports token-leak prevention.
- **Anti-Patterns**: No order-number-plus-email access path (CC-ORD-010); no client-side stage derivation from raw carrier payloads; no logging of capability tokens (SECURITY.md, Logging rule 4); no hardcoded brand colors — tokens only (ARCHITECTURE.md, Dependency rule 8); no animation that ignores `prefers-reduced-motion` (DESIGN.md §13); no additional smoke-arc usage outside the permitted placements (DESIGN.md §5.1); no `bypassSecurityTrust*`/unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The exact mapping from internal states (`received`, `confirmed`, `packed`, `shipped`, `delivered`) plus carrier events to the five consumer stages is not defined anywhere in the specs — in particular which internal states/events produce "Smoked" and "Frozen". CC-ORD-008 and DESIGN.md §7 name the stages but not the mapping; a human decision is needed before AC-01's "single documented mapping" can be fixed.
- Tracker presentation for the `cancelled` and `refunded` terminal branches (CC-ORD-006) is unspecified — whether the tracker shows a distinct state, hides, or messages differently.
- Which carrier events (beyond advancing "In transit"/"Delivered") surface to consumers (e.g., exceptions/delays) is unspecified.
- Localized stage vocabulary: DESIGN.md §9 requires natively written copy per market, but no translated stage names are specified for ja-JP, hi-IN, es-*, de-DE.
