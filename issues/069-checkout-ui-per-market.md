# 069 · Checkout UI per market

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-001, CC-ORD-002 (UI surface; validation in issue 038), CC-PRC-002 (display surface; formatting in issue 034)
- **Title**: Checkout UI per market
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The storefront checkout MUST support guest checkout with optional account creation in every market, capture addresses through the per-market address formats and server-side validation of issue 038, and display taxes and units per market convention (US tax-exclusive with estimated tax at checkout; DE/ES/MX/JP/IN tax-inclusive; DE unit price per kg alongside every price), including the JP precise delivery-window selection and noshi-style gift option and DE formal "Sie" address, in straight voice with zero cache/tech puns (REQUIREMENTS.md CC-ORD-001, CC-ORD-002, CC-PRC-002; DESIGN.md §10, §8.4, §8.5, §5.4).
- **Rationale**: Guest checkout with optional account creation is mandatory in all markets (CC-ORD-001) — forcing accounts loses sales and collects unnecessary PII. Address capture must match per-market formats including Japanese address structure and Indian PIN codes (CC-ORD-002). Tax-display convention is market law and expectation: DE additionally requires per-kilogram unit pricing (Preisangabenverordnung; CC-PRC-002). DESIGN.md §5.4 bans comedy from money movement: zero puns inside checkout, payment, and error recovery. The checkout UI is display-only for money — the server recomputes all prices, discounts, taxes, and totals at submission and ignores client-supplied prices (CC-PRC-005, authored in issue 036).
- **Design**: DESIGN.md §10 (Checkout row: "Straight voice, locale tax/units, delivery windows (JP), address formats per market"); §9 (plain verbs, sentence case, active voice; errors state what happened and what to do next, no apologies, no mascots); §5.4 (zero puns in checkout/payment/error recovery/legal/allergen content); §8.4 (DE: formal "Sie" throughout commerce; unit prices per kg in Plex Mono next to every price); §8.5 (JP: precise delivery-window selection, noshi-style gift option, dense information presentation — do not over-whitespace); §4.4 (worked locale price examples, Plex Mono); §7 (Price display component: always includes the market's tax-inclusion note).

## Scope
- **Applies To**: Web App (Angular SSR storefront checkout flow and its server-rendered views)
- **Components**: Checkout flow UI (address step, delivery options, order review with tax/units display, guest-vs-account choice); per-market form variants; error-recovery states. Excluded: address validation logic and formats (issue 038), serviceability enforcement — postal codes and 48-hour frozen transit (issue 045), order submission and server-side money recomputation (issue 036), idempotency (issue 037), payment-element embedding and processor flows (issues 039, 040), consumer account creation flows via Entra External ID (issue 058), cart (issue 068).
- **Actors**: Guest consumer, authenticated consumer
- **Data Classification**: Restricted/PII (delivery addresses, contact details); no cardholder data — card handling is delegated to the PCI DSS Level 1 processors via hosted fields or redirect (CC-ORD-003; SECURITY.md, Secret handling rule 7)

## Security Context
- **Defense Layer**: Input Validation (server-side, via issue 038) with the UI as a display/collection surface only
- **Threat(s) Addressed**: Client-side tampering with displayed prices/totals (client-supplied prices ignored per CC-PRC-005); PII exposure through caching of checkout responses (CC-SEC-013; SECURITY.md, HTTP boundary rule 10 — checkout is explicitly named never-edge-cached); CSRF on cookie-authenticated state-changing checkout requests (CC-SEC-006; SECURITY.md, Authentication rule 11, via issue 061)
- **Trust Boundary**: Client–server edge: every checkout field crossing it is attacker-controlled and validated server-side against explicit schemas (SECURITY.md, Input validation rules 1 and 3)
- **Zero Trust Consideration**: Client-side format hints and Angular form validation are UX only, never sole enforcement; prices, taxes, and availability render only from typed, validated server responses; server-controlled fields (user ID, prices, timestamps) are set from server state only (SECURITY.md, Input validation rules 1, 3)

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, "Baseline"); Validation and Business Logic chapters (server-side validation authority), Session Management chapter for the cookie/CSRF posture (via issue 061)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation), SC-8 (checkout served exclusively over TLS; SECURITY.md, HTTP boundary rule 1)
- **NIST SP 800-207**: N/A
- **Regulatory**: German Preisangabenverordnung — unit price per kg alongside every price (CC-PRC-002); PCI DSS SAQ A posture context — no card data in scope (CC-ORD-003); GDPR data-minimization posture for address/PII collection (CC-CMP-001 context)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any of the six launch markets, when a user with no account reaches checkout, then guest checkout completes without account creation, and account creation is offered as optional — checkout MUST NOT require an account in any market (negative case; CC-ORD-001).
2. **AC-02**: Given a market's checkout address step, when the form renders and the user submits, then the fields follow that market's address format and the submission is validated server-side via issue 038 (including Japanese address structure for JP and PIN codes for IN); client-side hints alone MUST NOT accept an address the server would reject (CC-ORD-002).
3. **AC-03**: Given the order-review display, when prices and totals render, then the US checkout shows tax-exclusive prices with estimated tax computed at checkout, DE/ES/MX/JP/IN show tax-inclusive prices with the market's tax-inclusion note, and DE additionally shows unit price per kg alongside every price — all locale-formatted via issue 034, never hand-formatted (CC-PRC-002, CC-PRC-004; DESIGN.md §4.4, §7, §8.4).
4. **AC-04**: Given the JP market checkout, when delivery options render, then precise delivery-window selection and a noshi-style gift option are offered (DESIGN.md §8.5).
5. **AC-05**: Given the de-DE locale, when any checkout string renders, then the formal "Sie" address is used throughout, with strings externalized in ICU MessageFormat resources (DESIGN.md §8.4; CC-I18N-002 via issue 064).
6. **AC-06**: Given every checkout, payment, and error-recovery view in every locale, when the copy is reviewed, then it contains zero cache/tech puns and errors state what happened and what to do next with no apologies or mascots (negative case; DESIGN.md §5.4, §9).
7. **AC-07**: Given any checkout response, when its headers and content are inspected, then it carries `Cache-Control: no-store` and is never edge-cached, and displayed totals derive only from server-computed typed responses — a client-forged price value MUST NOT alter any displayed or submitted amount (negative case; CC-SEC-013 via issue 028; CC-PRC-005 via issue 036).

## Failure Behavior
- **On Invalid Input**: Server rejects with HTTP 400 and an RFC 9457 problem-details body; the UI shows field-level guidance in straight voice without disclosing internal state; validation rejections logged as structured security events with correlation ID (SECURITY.md, Logging rules 1, 3; issue 021).
- **On System Error**: Fail closed — no order proceeds on an error in validation, serviceability, or money paths (SECURITY.md, Logging rule 2); the Angular client routes errors through interceptors and a global ErrorHandler, showing generic messages with correlation IDs only (SECURITY.md, Logging rule 7).
- **Alerting**: Spikes in checkout validation rejections or client-reported errors alert via centralized structured logging (SECURITY.md, Logging rule 3; issue 022).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for per-market checkout view-model composition (tax-display mode selection, DE unit-price presence, JP option presence).
- **Integration Tests**: ASP.NET Core integration tests per market fixture: guest checkout end-to-end to the submission boundary (issue 036 mocked at its contract), server-side address rejection despite valid-looking client state, `no-store` header assertions.
- **Security Tests**: Forged client price/total payload ignored; CSRF token enforcement on state-changing checkout requests (via issue 061); CI grep gate for raw-HTML sinks in checkout components (SECURITY.md, Input validation rule 5).
- **Compliance Tests**: Snapshot evidence of DE unit-price-per-kg rendering and US estimated-tax line (CC-PRC-002); i18n CI — key parity, schema validation, pseudo-localization, and locale visual regression covering checkout templates within the 130% expansion budget (CC-QA-006, CC-I18N-005 via issues 064, 065).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-ORD-001, CC-ORD-002, CC-PRC-002 (REQUIREMENTS.md §17).
- **Angular Component Tests**: Per-market form variants, JP delivery-window/noshi controls, de-DE "Sie" strings, error-recovery states.

## Dependencies
- **Upstream**: 017 "CSP and security headers with payment-processor origin allowlists" (checkout page CSP must admit processor origins); 024 "Transacting market/locale resolution"; 028 "Cache-safe gating" (no-store); 034 "Locale-aware price formatting and market tax-display conventions"; 036 "Order submission: guest checkout and server-side money recomputation"; 037 "Idempotency service" (submission contract); 038 "Per-market address capture and validation"; 045 "Checkout serviceability: postal codes and 48-hour frozen transit" (results surfaced in this UI); 058 "Consumer authentication: passkeys and email-code via Entra External ID" (optional account creation); 061 "Session cookie policy and CSRF protection"; 063 "Storefront SSR shell"; 064 "ICU MessageFormat resource pipeline"; 068 "Cart with cross-market preservation rules" (cart feeds checkout).
- **Downstream**: 039 "Stripe payment integration" and 040 "Razorpay payment integration" (payment elements embed into this flow); 043 "Transactional order emails" (post-submission); 099 "Money-path and mutation-testing suite" (exercises the display-vs-recomputation boundary).
- **External**: Stripe and Razorpay (via issues 039/040 only); Microsoft Entra External ID (via issue 058 only).

## Implementation Notes
- **Constraints**: Angular SSR (`@angular/ssr`) with hydration; checkout responses are personalized and never cacheable (SECURITY.md, HTTP boundary rule 10); DTOs with explicit binding-source attributes, server-controlled fields from server state only (SECURITY.md, Input validation rule 3); `Intl.NumberFormat`/server-equivalent formatting only (CC-PRC-004); ICU MessageFormat strings with no HTML in resources (SECURITY.md, Input validation rule 7); CSP `form-action`/`frame-src`/`connect-src` allowlists for the exact processor origins come from issue 017 — this issue must not widen them.
- **Anti-Patterns**: MUST NOT require account creation (CC-ORD-001); MUST NOT use client-side validation as sole enforcement (SECURITY.md, Input validation rule 1); MUST NOT trust or transmit client-computed prices/totals (CC-PRC-005); no hand-formatted currency (CC-PRC-004); no cache/tech puns in checkout, payment, or error recovery (DESIGN.md §5.4); no card fields rendered by Cache Cow code — hosted fields/redirect only (CC-ORD-003); no `bypassSecurityTrust*`/unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5); no over-whitespaced JP layout (DESIGN.md §8.5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Source and granularity of the JP delivery windows (carrier-provided via EasyPost? fixed window sizes? cutoff rules) are unspecified; DESIGN.md §8.5 names the feature but no data source.
- Noshi-style gift option specifics (variants, whether it affects price, packaging render at checkout) are unspecified.
- Mechanics and timing of optional account creation at checkout (inline during checkout vs. post-order invitation; hand-off to the Entra External ID flow of issue 058) are unspecified.
- Whether checkout is single-page or multi-step, and the step order, are not defined in the specs.
- No automated enforcement mechanism is specified for the zero-pun rule (DESIGN.md §5.4); treated here as copy review — confirm whether a string-resource lint is wanted.
- es-MX price display: DESIGN.md §4.4 shows `$149.00 MXN` "where cross-currency ambiguity exists" — whether checkout always qualifies as ambiguous for MX is unspecified.
