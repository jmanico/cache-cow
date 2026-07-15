# 068 · Cart with cross-market preservation rules

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-008 (with cache-safety clauses of CC-MKT-009 / CC-SEC-013 applied to cart responses; gating parity with CC-MKT-003/006)
- **Title**: Cart with cross-market preservation rules
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: Cross-market navigation MUST preserve the user's cart only where all cart items exist in the destination market, otherwise the user MUST be shown exactly which items were removed and why, with the availability decision made server-side through the Market & Gating Policy enforcement point (REQUIREMENTS.md CC-MKT-008; ARCHITECTURE.md, Dependency rule 1).
- **Rationale**: CC-MKT-008 makes market switching lossless only when safe and transparent when not. The rule is load-bearing for compliance gating: a cart carried from US into IN must not deliver non-veg SKUs into an IN session, because the IN catalog MUST contain vegetarian SKUs only and client-side hiding is non-compliant (REQUIREMENTS.md CC-MKT-003). Gating rules are policy-as-data behind a single server-side enforcement point — nothing may implement its own market conditionals (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1). Cart responses are per-user personalized responses and therefore must never be edge-cached, so a cached cart cannot leak across users or markets (CC-MKT-009 / CC-SEC-013; SECURITY.md, HTTP boundary rule 10; ARCHITECTURE.md, "Edge & SSR caching").
- **Design**: DESIGN.md §9 — empty cart microcopy is exactly "Your cache is empty. Warm it up." with a menu CTA, and that is the entire permitted joke in the cart; DESIGN.md §5.4 — pun budget (at most one cache/tech pun per viewport; zero inside checkout, payment, error recovery); DESIGN.md §9 — errors/notices state what happened and what to do next, no apologies, no mascots; prices in the cart render locale-formatted in IBM Plex Mono per DESIGN.md §4.4 (via issue 034). Translated copy is written per market by native speakers, not translated puns (DESIGN.md §9).

## Scope
- **Applies To**: Both (Angular SSR storefront cart UI + server-side cart reconciliation endpoints)
- **Components**: Storefront cart view (Angular SSR); server-side cart state and the market-switch reconciliation logic that consults the Market & Gating Policy enforcement API (issue 025) and per-market availability (issue 030); removal-notice UI. Excluded: checkout flow (issue 069), order submission and money recomputation (issue 036), market/locale switcher controls and persistence (issues 024, 063), product cards / add-to-cart entry points (issue 066).
- **Actors**: Guest consumer, authenticated consumer
- **Data Classification**: Internal (per-user cart contents; no payment data — card data never enters the system per CC-ORD-003)

## Security Context
- **Defense Layer**: Architecture (server-side gating upstream of everything user-visible; ARCHITECTURE.md, Dependency rule 1)
- **Threat(s) Addressed**: Market-gating bypass via cart carry-over — a client-preserved or cached cart delivering non-veg SKUs into an IN session despite CC-MKT-003; cross-user/cross-market cache leakage of personalized cart responses (CC-SEC-013, CC-MKT-009; THREAT_MODEL.md-derived, per REQUIREMENTS.md v1.3 note); client-side tampering with cart availability or prices (client-supplied prices are ignored per CC-PRC-005 — recomputation authority is issue 036)
- **Trust Boundary**: Client–server edge: all cart mutations and the market-switch reconciliation are validated and decided server-side; the client displays what the server already gated (ARCHITECTURE.md, Dependency rule 1)
- **Zero Trust Consideration**: Client cart state, claimed availability, and claimed prices are untrusted; availability comes only from the Market & Gating Policy service and Catalog & Inventory, and prices/inventory render only from typed, validated responses (SECURITY.md, Input validation rules 1 and 3). Market gating keys exclusively off server-side transacting-market state, never client hints (SECURITY.md, Authentication rule 10 / CC-SEC-012).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, "Baseline"); Business Logic and Access Control chapters (server-side enforcement of market availability rules)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (server-side validation of cart mutation input), AC-3 (server-side enforcement of the gating decision)
- **NIST SP 800-207**: Policy decision made server-side per request, independent of client-asserted state
- **Regulatory**: IN veg-only catalog context (FSSAI regime referenced by CC-CNT-006/CC-CMP-004) motivates the gating parity, via CC-MKT-003
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a cart whose items all exist in the destination market's catalog, when the user switches market, then the cart is preserved in full and redisplayed with the destination market's per-market prices in the market currency, locale-formatted (CC-MKT-008; CC-PRC-001, CC-PRC-004 via issue 034).
2. **AC-02**: Given a cart containing items that do not exist in the destination market's catalog, when the user switches market, then those items are removed server-side and the user is shown exactly which items were removed and why (CC-MKT-008), in plain straight-voice copy per DESIGN.md §9.
3. **AC-03**: Given a cart containing non-veg SKUs, when the user switches to the IN market, then no IN cart response (SSR HTML, transfer state, or API JSON) contains any non-veg SKU, and the removal decision is produced by the Market & Gating Policy enforcement point (issue 025) — not by client-side logic or a cart-local market conditional (negative case; CC-MKT-003, CC-MKT-006; ARCHITECTURE.md, Dependency rule 1).
4. **AC-04**: Given any cart response, when its headers are inspected, then it carries `Cache-Control: no-store` and is never edge-cached, and any SSR transfer/hydration state embedded for the cart contains only data gated for that exact response (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary rule 10; via issue 028).
5. **AC-05**: Given an empty cart rendered in en-US, when the cart view displays, then the microcopy is "Your cache is empty. Warm it up." with a menu CTA, and no other cache/tech pun appears anywhere in the cart view (DESIGN.md §9, §5.4); non-English locales use natively written equivalents from the ICU MessageFormat resources (DESIGN.md §9; CC-I18N-002 via issue 064).
6. **AC-06**: Given a client request asserting an item's availability or price (forged payload), when the server processes any cart operation or market switch, then the client-supplied availability/price values are ignored and server-side canonical data is used; the forged values MUST NOT affect the stored cart (negative case; CC-PRC-005; SECURITY.md, Input validation rule 3).

## Failure Behavior
- **On Invalid Input**: Reject malformed cart mutations with HTTP 400 and an RFC 9457 problem-details body (generic, no internal state; SECURITY.md, Logging rule 1, via issue 021); log the validation rejection as a structured security event with correlation ID (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — any exception in the gating/availability path during market-switch reconciliation is a denial, never a bypass (SECURITY.md, Logging rule 2): an item whose destination-market availability cannot be determined is treated as not preserved, never carried over ungated; the user sees a generic error with correlation ID only (SECURITY.md, Logging rule 7).
- **Alerting**: Gating-path exceptions and validation-rejection spikes alert through centralized structured logging (SECURITY.md, Logging rule 3; issue 022).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the reconciliation logic: all-items-present preservation, partial removal with exact removed-item list and reason, IN destination with non-veg items, gating-lookup failure treated as removal (fail closed).
- **Integration Tests**: ASP.NET Core integration tests exercising market switch across every market pair fixture relevant to gating (notably any-market → IN with non-veg cart contents), asserting server response contents and `Cache-Control: no-store` headers; cart surface included in the storefront leg of the market-gating matrix (CC-QA-003; issue 027).
- **Security Tests**: Forged-payload tests (client-asserted price/availability ignored); assertion that no cart endpoint output is cacheable; CI grep gate confirms no raw-HTML sinks in the cart components (SECURITY.md, Input validation rule 5; issue 006).
- **Compliance Tests**: Automated evidence that IN cart responses contain zero non-veg SKUs across SSR HTML, transfer state, and JSON (CC-MKT-003 parity).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on the gating-relevant reconciliation code (CC-QA-001); tests tagged CC-MKT-008, CC-MKT-003, CC-SEC-013 (REQUIREMENTS.md §17).
- **Angular Component Tests**: Removal-notice rendering (exact items and reasons), empty-cart microcopy per locale resource, no hand-formatted currency strings (CC-PRC-004).

## Dependencies
- **Upstream**: 003 "Shared kernel: Market, Locale, and SKU identity types"; 023 "Market & Gating Policy: policy-as-data model"; 024 "Transacting market/locale resolution"; 025 "Server-side gating enforcement API with IN veg-only exclusion"; 028 "Cache-safe gating" (no-store on personalized responses); 029 "SKU domain model"; 030 "Inventory per SKU per regional cold store"; 032 "Per-SKU per-market price model"; 034 "Locale-aware price formatting"; 063 "Storefront SSR shell"; 064 "ICU MessageFormat resource pipeline"; 066 "Menu page" (add-to-cart entry point).
- **Downstream**: 069 "Checkout UI per market" (cart feeds checkout); 036 "Order submission" (cart contents become the submitted order); 027 "Market-gating CI test matrix" (covers the cart storefront surface).
- **External**: None.

## Implementation Notes
- **Constraints**: Angular SSR (`@angular/ssr`) storefront with hydration (ARCHITECTURE.md, "SSR"); server-side reconciliation in ASP.NET Core consuming the gating enforcement API; cart state persisted server-side in the owning context's PostgreSQL schema under its least-privilege role (SECURITY.md, Secret handling rule 10) — see Open Questions on context ownership; DTO binding with explicit source attributes, never entity models (SECURITY.md, Input validation rule 3).
- **Anti-Patterns**: Client-side hiding of unavailable/non-veg items is non-compliant (CC-MKT-003); no market conditionals in cart code — consult the gating service only (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); never cache cart responses at edge/CDN/SSR (SECURITY.md, HTTP boundary rule 10); no hand-formatted currency strings (CC-PRC-004); no cache/tech puns beyond the single permitted empty-cart line (DESIGN.md §9, §5.4); no `bypassSecurityTrust*` or unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- No bounded context in ARCHITECTURE.md "Server bounded contexts" is named as owning the cart; the owning module (e.g., Ordering & Payments vs. a storefront-session concern) needs a decision before the schema/role placement (SECURITY.md, Secret handling rule 10) is fixed.
- Cart persistence mechanism and lifetime for guests vs. account holders (server-side session, durable per-account cart, merge-on-login) are unspecified.
- CC-MKT-008 wording ("preserve the cart only where all items exist … which items were removed") implies partial preservation — unavailable items removed, remaining items kept. Confirm that entire-cart clearing is not intended when only some items are unavailable.
- DESIGN.md's page inventory (§10) contains no cart page; only the empty-cart microcopy and pun budget are specified. Visual layout of the cart and of the removal notice (inline banner vs. dialog; persistence until dismissed) is undefined.
- The required granularity of "why" in the removal notice (e.g., generic "not available in <market>" vs. differentiated reasons) is unspecified.
