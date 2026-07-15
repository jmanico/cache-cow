# Cache Cow: Platform Requirements (REQUIREMENTS.md)

Version 1.0 | Status: Draft for review

---

## 1. System overview

Cache Cow is a multi-market direct-to-consumer and B2B commerce platform for frozen BBQ products with regional catalogs, regional pricing, six markets with seven launch locales (US English, Spain Spanish, Mexican Spanish, German, Japanese, India English/Hindi), grocery wholesale distribution, a B2B ordering API, and an internal operations dashboard covering sales, orders, invoices, and employee management.

Subsystems:

1. Consumer storefront (web)
2. Market/catalog/pricing engine
3. Order, payment, and fulfillment services
4. B2B API and wholesale portal
5. Internal operations dashboard
6. Content pages (Chefs, Cows, Cuts, Contact, legal)

## 2. Definitions

- **Market**: a commercial region with its own catalog, currency, tax rules, and compliance regime. Launch markets: US, ES, MX, DE, JP, IN. We are launching in the USA, India, Spain, Mexico, Germany, Japan.
- **Locale**: a language/region string (BCP 47) controlling UI strings and formatting. Locale and market are independent user selections (a user MAY shop the DE market in English).
- **Regional cold store**: a fulfillment node holding frozen inventory for one or more markets.
- **Veg SKU / Non-veg SKU**: product classification driving regional gating and labeling.

## 3. Markets and regional gating (MKT)

- CC-MKT-001 [P1]: The platform MUST support exactly these launch markets: US, ES, MX, DE, JP, IN, each with independent catalog, currency, tax treatment, and legal content.
- CC-MKT-002 [P1]: Market selection MUST be proposed from geolocation (IP-based) and MUST be user-overridable. The user's explicit choice MUST persist across sessions.
- CC-MKT-003 [P1]: The IN market catalog MUST contain vegetarian SKUs only. Non-veg SKUs MUST be excluded server-side from every IN response: catalog listings, product detail, search, recommendations, sitemaps, structured data, and feeds. Client-side hiding is non-compliant.
- CC-MKT-004 [P1]: Requesting a non-veg product URL in the IN market MUST return HTTP 404 (not 403, not a redirect to the product in another market).
- CC-MKT-005 [P1]: The "Meet our Cuts" experience MUST NOT render, link, or be reachable in the IN market. The "Meet our Cows" page MUST be present in primary navigation in the IN market.
- CC-MKT-006 [P1]: Market gating rules MUST be encoded as data (per-market policy configuration), not scattered conditionals, and MUST be covered by an automated test matrix (every gated route x every market).
- CC-MKT-007 [P2]: US, ES, and DE markets MUST carry the full catalog including non-veg SKUs; vegetarian SKUs MUST be available and filterable in all markets.
- CC-MKT-008 [P2]: Cross-market navigation MUST preserve the user's cart only where all cart items exist in the destination market; otherwise the user MUST be shown exactly which items were removed and why.

## 4. Catalog and menu (CAT)

- CC-CAT-001 [P1]: Every SKU MUST carry: unique ID, name (localized), classification (veg/non-veg), cut/category, net weight, serving estimate, ingredients, allergens, nutrition per market format, storage and reheat instructions, and per-market availability flags.
- CC-CAT-002 [P1]: Inventory MUST be tracked per SKU per regional cold store. Product availability shown to a user MUST reflect the cold store(s) serving that user's market.
- CC-CAT-003 [P1]: Stock states MUST map to exactly three user-facing states: in stock (ships from regional store), restocking (preorder permitted), unavailable in region. Presentation follows DESIGN.md 5.2.
- CC-CAT-004 [P1]: Allergen and nutrition data MUST render from structured fields, never from free-text CMS content, in every locale.
- CC-CAT-005 [P2]: Catalog search MUST operate per market (search in IN MUST NOT surface non-veg SKUs, per CC-MKT-003) and per locale (search in Japanese matches Japanese product names).
- CC-CAT-006 [P2]: Vegetarian filtering MUST be available in all markets as a single-toggle filter.

## 5. Pricing, tax, and promotions (PRC)

- CC-PRC-001 [P1]: Prices MUST be defined per SKU per market in the market's currency (USD, EUR, MXN, EUR, JPY, INR). No runtime FX conversion of consumer prices.
- CC-PRC-002 [P1]: Price display MUST follow market convention: DE/ES/JP/IN tax-inclusive; US tax-exclusive with estimated tax computed at checkout. DE MUST additionally display unit price per kilogram alongside every price (Preisangabenverordnung).
- CC-PRC-003 [P1]: All monetary values MUST be stored and computed as integer minor units (or exact decimal type). Binary floating point MUST NOT be used for money anywhere, including tests.
- CC-PRC-004 [P1]: All price formatting MUST use locale-aware formatting (`Intl.NumberFormat` or server equivalent). Hand-formatted currency strings are a defect.
- CC-PRC-005 [P1]: The server MUST recompute all prices, discounts, taxes, and totals at order submission from canonical data. Client-supplied prices MUST be ignored.
- CC-PRC-006 [P2]: Promotions ("sale") MUST support: per-market percentage or fixed discounts, per-SKU or per-category scope, start/end timestamps (market timezone), and stacking rules (default: no stacking). Expired promotions MUST NOT apply even if cached UI still displays them; final authority is the order service.
- CC-PRC-007 [P2]: Clearance promotions MAY use the "Eviction Specials" presentation per DESIGN.md 5.3; naming is presentation-only and MUST NOT leak into invoice line-item legal descriptions.

## 6. Consumer ordering, payment, and fulfillment (ORD, FUL)

- CC-ORD-001 [P1]: Guest checkout MUST be supported in all markets. Account creation MUST be optional at checkout.
- CC-ORD-002 [P1]: Address capture MUST use per-market address formats and validation (including Japanese address structure and Indian PIN codes).
- CC-ORD-003 [P1]: Payment processing MUST be delegated to a PCI DSS Level 1 payment processor via hosted fields or redirect. Card primary account numbers MUST NOT transit or rest on Cache Cow systems (SAQ A scope target).
- CC-ORD-004 [P1]: Payment methods MUST include, at minimum per market: US cards; DE cards plus one dominant local method `[ASSUMPTION: PayPal and/or SEPA]`; ES cards; JP cards plus konbini or equivalent `[ASSUMPTION]`; IN UPI plus cards. Confirm local-method selection with payments provider.
- CC-ORD-005 [P1]: Order submission MUST be idempotent (client idempotency key); double submission MUST NOT create duplicate orders or charges.
- CC-ORD-006 [P1]: Order state machine MUST be: `received -> confirmed -> packed -> shipped -> delivered`, with `cancelled` and `refunded` as terminal branches, and every transition appended to an immutable audit log (actor, timestamp, prior state).
- CC-ORD-007 [P1]: Customers MUST receive order confirmation and shipment notification by email in their locale, containing no more personal data than necessary (no full address in email body).
- CC-ORD-008 [P2]: Order tracking MUST expose the five consumer-facing stages defined in DESIGN.md (Smoked, Frozen, Packed, In transit, Delivered) mapped from the internal state machine plus carrier events.
- CC-FUL-001 [P1]: Every consumer order MUST be routed to the regional cold store serving the delivery address; cross-region fulfillment MUST require explicit operations override (dashboard permission, audited).
- CC-FUL-002 [P1]: Frozen shipping constraints MUST be enforced at checkout: serviceable postal-code validation per market and carrier transit-time limits for frozen product `[ASSUMPTION: max 48h transit; confirm cold-chain spec]`.
- CC-FUL-003 [P2]: DE (Widerrufsrecht) exception handling: perishable frozen food is exempt from the standard 14-day withdrawal right; legal texts MUST state this accurately and per legal review.

## 7. Grocery wholesale (B2B) (WHS)

- CC-WHS-001 [P2]: A wholesale portal MUST allow approved grocery partners to place case-quantity orders against wholesale price lists per market.
- CC-WHS-002 [P2]: Partner onboarding MUST be an approval workflow executed in the internal dashboard (no self-service activation), with business identity captured per market (e.g., USt-IdNr. in DE, GSTIN in IN).
- CC-WHS-003 [P2]: Wholesale prices and terms MUST be invisible to consumer sessions and MUST NOT be derivable from consumer API responses.
- CC-WHS-004 [P2]: Wholesale orders MUST generate invoices per market legal format (see CC-INV) with payment terms (net-30 default) `[ASSUMPTION]`.

## 8. B2B API (API)

Authorization requirements in this section are grounded in RFC 9700 (OAuth 2.0 Security BCP) and OWASP ASVS 5.0.

- CC-API-001 [P1]: The B2B API MUST be a versioned HTTPS JSON API (`/v1/...`). Breaking changes MUST increment the version; v(n-1) MUST be supported for a documented deprecation window of at least 6 months.
- CC-API-002 [P1]: Client authorization MUST use OAuth 2.0 client credentials per RFC 9700 guidance. Client authentication MUST use `private_key_jwt` or mutual TLS (RFC 8705). Static shared secrets in query strings or long-lived static API keys MUST NOT be accepted.
- CC-API-003 [P1]: Access tokens MUST be sender-constrained where the client supports it (mTLS-bound per RFC 8705 or DPoP per RFC 9449) and MUST be short-lived (max 15 minutes). Bearer-only tokens, if permitted for a client tier, MUST be scoped read-only.
- CC-API-004 [P1]: Tokens MUST carry least-privilege scopes: `catalog:read`, `orders:write`, `orders:read`, `invoices:read`. Every endpoint MUST enforce scope and partner-tenancy: a partner MUST NOT be able to read or mutate another partner's orders or invoices (object-level authorization tested per endpoint).
- CC-API-005 [P1]: Order-creation endpoints MUST require an `Idempotency-Key` header; replays within the retention window MUST return the original result.
- CC-API-006 [P1]: All request bodies MUST be validated against published JSON Schemas; requests failing validation MUST be rejected with 400 and a machine-readable error body (RFC 9457 problem details). Unknown fields MUST be rejected, not ignored.
- CC-API-007 [P1]: Market gating applies to the API identically to the storefront: a partner authorized for the IN market MUST NOT be able to order non-veg SKUs through any endpoint (CC-MKT-003 parity test required).
- CC-API-008 [P1]: Rate limiting MUST be enforced per client with 429 responses and `Retry-After`. Default `[ASSUMPTION]`: 600 requests/minute, order creation 60/minute; tune per partner tier.
- CC-API-009 [P2]: Webhooks for order status MUST be signed (HMAC with per-partner secret, rotating), MUST include timestamp to bound replay, and receivers MUST be registered HTTPS endpoints validated at registration (no redirects followed at delivery time).
- CC-API-010 [P2]: API documentation MUST be generated from the same schemas the service validates against (single source of truth).

## 9. Internationalization and localization (I18N)

- CC-I18N-001 [P1]: UI languages at launch: en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN. Locale MUST be user-selectable independent of market (CC-MKT-002).
- CC-I18N-002 [P1]: All user-facing strings MUST be externalized in ICU MessageFormat resources. String resources MUST NOT contain HTML. Interpolated values MUST be escaped by default. Translation files MUST be treated as untrusted input and schema-validated in CI (key parity across locales, no markup, placeholder consistency).
- CC-I18N-003 [P1]: Dates, numbers, currency, and units MUST use locale-aware formatting; US displays imperial-primary weights, all other markets metric.
- CC-I18N-004 [P1]: Every page MUST declare correct `lang` (and `hreflang` alternates for SEO) per rendered locale.
- CC-I18N-005 [P2]: Layouts MUST accommodate 130 percent of English string length without truncation or overflow (automated visual regression across locales for the top 20 templates).
- CC-I18N-006 [P2]: Transactional email templates MUST exist in all launch locales; fallback is the market's primary language, never a broken template.

## 10. Content pages (CNT)

- CC-CNT-001 [P2]: Meet our Chefs: structured profiles (shared roster, localized bios) rendered from the CMS through a sanitizing allowlist renderer.
- CC-CNT-002 [P2]: Meet our Cows: mascot/herd content per DESIGN.md 8.1. This page MUST NOT contain links to non-veg PDPs in any market.
- CC-CNT-003 [P2]: Meet our Cuts: interactive cut diagram filtering the menu; excluded from IN per CC-MKT-005; MUST have an accessible list-based equivalent.
- CC-CNT-004 [P1]: Contact us: form with server-side validation, rate limiting, and CAPTCHA-equivalent abuse control; submissions MUST NOT trigger email header injection (fields validated, no user input in SMTP headers).
- CC-CNT-005 [P1]: Legal pages per market MUST include: privacy policy, terms, shipping and returns; DE additionally Impressum and Widerrufsbelehrung; all served per locale and versioned.
- CC-CNT-006 [P1]: FSSAI vegetarian marking MUST appear on all IN product presentations per regulation (green mark), and the non-veg mark MUST NOT appear anywhere in the IN market.

## 11. Internal dashboard (DSH) and back office

- CC-DSH-001 [P1]: Dashboard access MUST require SSO with phishing-resistant MFA (WebAuthn) for all staff accounts. Session lifetime max 12 hours; re-auth for sensitive actions (refunds, employee records, role changes).
- CC-DSH-002 [P1]: Authorization MUST be role-based with least privilege. Minimum roles: sales-viewer, ops-agent, finance, hr-admin, admin. Role-permission matrix MUST be documented and tested (every sensitive endpoint x every role).
- CC-DSH-003 [P1]: Modules at launch: sales analytics (by market, SKU, channel), order management (search, state transitions, refunds), invoice management, inventory by cold store, partner (wholesale) management, employee management.
- CC-DSH-004 [P1]: Every privileged action MUST write an audit event (actor, action, object, before/after, timestamp) to an append-only store retained `[ASSUMPTION]` 7 years for financial actions.
- CC-DSH-005 [P1]: Employee records MUST be restricted to hr-admin; compensation fields additionally restricted and encrypted at the field level. Employee PII MUST NOT be exportable except through an audited export function.
- CC-DSH-006 [P2]: Sales analytics MUST include per-region per-SKU stock service level (the regional "cache hit rate"), revenue, AOV, and promotion performance, filterable by market and date range.
- CC-INV-001 [P1]: Invoices MUST be generated per market legal requirements: sequential numbering per legal entity, market tax lines (US sales tax, EU VAT with rates and USt-IdNr., JP consumption tax with qualified-invoice number, IN GST with GSTIN and HSN codes) `[legal review required]`. Issued invoices are immutable; corrections occur via credit notes.
- CC-INV-002 [P2]: Invoice PDFs MUST render from structured data server-side; consumer invoice email delivers a link to authenticated download, not an attachment containing full address data `[ASSUMPTION]`.

## 12. Security requirements (SEC)

Baseline: OWASP ASVS 5.0 Level 2 for the whole platform. The requirements below are the platform-specific elaborations; ASVS L2 applies in full.

- CC-SEC-001 [P1]: All input crossing a trust boundary (HTTP parameters, headers, bodies, webhooks, CMS content, translation files, partner uploads) MUST be validated against explicit schemas (typed schema validation such as Zod on the client, JSON Schema server-side). Invalid input MUST be rejected, not sanitized into acceptance.
- CC-SEC-002 [P1]: Output encoding MUST be context-appropriate everywhere; raw-HTML sinks MUST NOT appear in the codebase — for the Angular client this means `bypassSecurityTrust*` and any `[innerHTML]`/`outerHTML` binding of content not produced by the allowlist renderer — enforced by CI grep gate. CMS rich text renders through an allowlist renderer only.
- CC-SEC-003 [P1]: A strict Content Security Policy MUST be enforced (no `unsafe-inline` scripts, nonce or hash based), plus HSTS with preload, X-Content-Type-Options, and frame-ancestors restrictions. CSP violations MUST be report-collected.
- CC-SEC-004 [P1]: All `href`/`src` values derived from data MUST pass URL validation (scheme allowlist https/mailto/tel) with `#` fallback; partner and locator links validated against a registered-domain allowlist.
- CC-SEC-005 [P1]: Consumer authentication (optional accounts) MUST support WebAuthn passkeys and email-code login; passwords, if offered, per ASVS 5.0 V6 (length 8 to 64+, breached-password screening, no composition rules, no rotation policy).
- CC-SEC-006 [P1]: Session management: HttpOnly, Secure, SameSite cookies; server-side revocation; CSRF protection on all state-changing browser requests.
- CC-SEC-007 [P1]: Authorization MUST be enforced server-side per object (IDOR test coverage on orders, invoices, addresses, partner data) in storefront, portal, API, and dashboard alike.
- CC-SEC-008 [P1]: Secrets MUST live in a managed secrets store, never in source or client bundles; TLS 1.2+ everywhere with 1.3 preferred; data at rest encrypted; field-level encryption for employee compensation (CC-DSH-005).
- CC-SEC-009 [P1]: Dependency policy: minimal third-party dependencies; every new dependency requires documented justification, CVE history review, maintenance check, and transitive analysis; SCA and secret scanning MUST run in CI and block on criticals; SBOM generated per release.
- CC-SEC-010 [P1]: Structured security logging (authn events, authz denials, validation rejections, admin actions) to centralized monitoring with alerting; logs MUST NOT contain credentials, tokens, or full PANs (which never exist in-system per CC-ORD-003).
- CC-SEC-011 [P2]: The dashboard MUST be served from a separate origin (or isolated subdomain with distinct session scope) from the consumer storefront, network-restricted `[ASSUMPTION: VPN or IP allowlist plus SSO]`.
- CC-SEC-012 [P2]: Geolocation input (IP-derived market proposal) is untrusted personalization data: it MUST NOT be used for any security or compliance decision. Compliance gating (CC-MKT-003) keys off the transacting market, which is enforced server-side.

## 13. Privacy and compliance (CMP)

- CC-CMP-001 [P1]: GDPR applies to ES/DE (and EU visitors): lawful-basis mapping, DPA with processors, data-subject rights endpoints (access, deletion, portability) with identity verification, and consent management for non-essential cookies/analytics (no dark patterns; reject as easy as accept).
- CC-CMP-002 [P1]: India DPDP Act 2023 and Japan APPI obligations MUST be assessed and implemented for their markets; US state privacy laws (CCPA/CPRA at minimum) honored for US consumers `[legal review required]`.
- CC-CMP-003 [P1]: Data minimization and retention schedule MUST be documented per data class (orders, marketing, employee, logs) and enforced by automated deletion jobs.
- CC-CMP-004 [P1]: Food-information compliance per market: EU FIC (allergens, nutrition declaration), FSSAI labeling for IN (CC-CNT-006), US FDA labeling, JP labeling `[legal review required]`. Structured data per CC-CAT-004 is the single source.
- CC-CMP-005 [P2]: Marketing email MUST be opt-in per market law (double opt-in for DE), with functioning one-click unsubscribe (RFC 8058).

## 14. Non-functional requirements (NFR)

- CC-NFR-001 [P1]: Availability target 99.9 percent monthly for storefront and API; dashboard 99.5.
- CC-NFR-002 [P1]: Performance budgets: storefront LCP under 2.5s at p75 per market (real-user monitoring), API p95 latency under 300ms for reads and 800ms for order creation.
- CC-NFR-003 [P1]: All services emit structured logs, metrics, and traces; per-market synthetic checks run continuously (including an IN-market gating probe asserting CC-MKT-003/004 in production).
- CC-NFR-004 [P2]: Accessibility: WCAG 2.2 AA across storefront, portal, and dashboard, enforced by automated checks plus manual audit per release train (details in DESIGN.md 13).
- CC-NFR-005 [P2]: Fonts and static assets self-hosted, subset per locale; no third-party runtime CDNs for fonts or scripts.

## 15. Testing and quality gates (QA)

- CC-QA-001 [P1]: Line coverage MUST be at or above 80 percent per package, enforced in CI; coverage MUST NOT be met via assertion-free tests (mutation testing SHOULD run on money, gating, and authz code).
- CC-QA-002 [P1]: Merge gates: all tests green, SAST clean of high/critical, SCA clean of critical, secret scan clean, lint clean, no raw-HTML sinks (CC-SEC-002), human code review approval. AI-generated code passes the identical gates plus mandatory human review; no auto-merge.
- CC-QA-003 [P1]: A market-gating test matrix MUST run on every merge: every market x veg/non-veg SKU x storefront/search/API/sitemap, asserting CC-MKT-003 through CC-MKT-007.
- CC-QA-004 [P1]: Money-path tests MUST cover: recomputation authority (CC-PRC-005), idempotent submission (CC-ORD-005, CC-API-005), promotion boundaries (start/end/timezone), and rounding in all five currencies including JPY zero-decimal and INR grouping.
- CC-QA-005 [P1]: AuthZ test suite MUST attempt cross-tenant and cross-role access for every sensitive endpoint (CC-API-004, CC-DSH-002, CC-SEC-007) and MUST fail closed.
- CC-QA-006 [P2]: i18n CI: translation resource schema validation (CC-I18N-002), key parity, pseudo-localization build, and locale visual regression (CC-I18N-005).
- CC-QA-007 [P2]: DAST against staging per release; external penetration test before launch and annually; findings triaged against ASVS with documented SLAs (critical 7 days, high 30).

## 16. Out of scope (v1)

Native mobile apps; consumer subscriptions; loyalty program; marketplace/third-party sellers; markets beyond the six listed; in-house payment processing; recipe/community content; RTL locales (architecture MUST NOT preclude them, per DESIGN.md 13).

## 17. Traceability

Every implemented feature MUST reference requirement IDs in its pull request description. The test suite MUST tag tests with the requirement IDs they verify, enabling a generated coverage report of requirements-to-tests. Unreferenced code paths discovered in review are treated as scope creep and removed or ratified here first.
