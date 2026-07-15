# 030 · Inventory per SKU per regional cold store with three-state availability

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CAT-002, CC-CAT-003
- **Title**: Inventory per SKU per regional cold store with three-state availability
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: Inventory MUST be tracked per SKU per regional cold store, product availability shown to a user MUST reflect the cold store(s) serving that user's market, and stock states MUST map to exactly three user-facing states — in stock (ships from regional store), restocking (preorder permitted), unavailable in region (REQUIREMENTS.md CC-CAT-002, CC-CAT-003).
- **Rationale**: Cache Cow fulfills frozen product from regional cold stores (REQUIREMENTS.md §2, "Regional cold store"); availability that ignores the serving cold store misleads customers and breaks the 48-hour frozen-transit promise downstream (CC-FUL-002, issue 045). A closed three-state model keeps every surface (storefront, B2B API, dashboard) consistent and testable, and feeds the per-region stock service level metric (CC-DSH-006).
- **Design**: State *derivation* only. The cache-status presentation vocabulary (CACHE HIT / WARMING / CACHE MISS badges and plain lines, DESIGN.md §5.2) is issue 066's scope; this issue exposes the three canonical states for it to consume.

## Scope
- **Applies To**: Both
- **Components**: Catalog & Inventory bounded context (ARCHITECTURE.md, "Server bounded contexts" #2): cold-store entity, per-SKU per-store inventory records, market→cold-store serving relationship, and the availability-derivation function returning one of exactly three states per SKU per market. Excludes: badge/vocabulary presentation (issue 066), order routing and cross-region override (issue 044), checkout serviceability (issue 045), preorder order-flow mechanics (issue 036), dashboard inventory views (issue 084).
- **Actors**: Catalog/Inventory service (internal), storefront SSR, B2B API (`catalog:read`), fulfillment service (read), dashboard modules (read).
- **Data Classification**: Internal

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Information-integrity failure (STRIDE: Tampering) — availability derived from the wrong region's stock; indirect gating exposure — a gated SKU acquiring a user-facing availability state in a market where it must not exist (OWASP Top Ten A01:2021, via CC-MKT-003).
- **Trust Boundary**: Availability is computed server-side from server-held inventory and the server-side transacting market (SECURITY.md, Authentication rule 10); no client hint selects the cold store. Consumer/B2B reads pass through the gating enforcement point first (ARCHITECTURE.md, Dependency rule 1).
- **Zero Trust Consideration**: Clients render inventory values only from typed, validated responses (SECURITY.md, Input validation rule 1); the client never computes or overrides an availability state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation on inventory mutations)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a SKU stocked in two cold stores, when inventory is recorded, then quantities are tracked independently per SKU per cold store and are queryable per store (CC-CAT-002).
2. **AC-02**: Given a user whose transacting market is served by cold store X, when availability is derived for a SKU, then it reflects cold store X's inventory only — stock in a store not serving that market MUST NOT make the SKU appear in stock (CC-CAT-002).
3. **AC-03**: Given any SKU/market combination, when availability is derived, then the result is exactly one of the closed set {in-stock, restocking (preorder permitted), unavailable-in-region} — no fourth state, no null, no free-text status (CC-CAT-003).
4. **AC-04**: Given a SKU in the restocking state, when the state is exposed to ordering surfaces, then it carries the preorder-permitted signal; given unavailable-in-region, then no purchase or preorder is offered (CC-CAT-003).
5. **AC-05**: Given a SKU excluded from a market by gating (e.g., non-veg in IN), when availability is requested for that market, then the derivation is never reached for consumer/B2B surfaces — the SKU is absent upstream via the gating enforcement point (issue 025), and the inventory module returns no user-facing state for it (negative case; CC-MKT-003; ARCHITECTURE.md, Dependency rule 1).
6. **AC-06**: Given an inventory mutation with a negative or non-numeric quantity, when validation runs, then the write is rejected with 400 (RFC 9457) and no state change occurs (SECURITY.md, Input validation rule 1).
7. **AC-07**: Given the market→cold-store serving relationship, when a cold store serves multiple markets (REQUIREMENTS.md §2), then availability for each market derives from that same store without duplicating inventory records (CC-CAT-002).

## Failure Behavior
- **On Invalid Input**: Reject inventory mutations failing schema validation with HTTP 400 and RFC 9457 problem details; structured validation-rejection log with correlation ID (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed (SECURITY.md, Logging rule 2): if inventory or the market→store mapping cannot be resolved, derive unavailable-in-region (no purchase offered) rather than defaulting to in-stock.
- **Alerting**: Derivation errors and mapping-resolution failures logged as structured events to Azure Monitor with alerting (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the derivation function: exhaustive matrix of stock levels × store-market mappings × preorder flags asserting the closed three-state output; failure-path tests asserting fail-closed to unavailable-in-region.
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL (issue 015 schema) for per-SKU per-store tracking, multi-market stores, and typed read-model responses per market.
- **Security Tests**: Test that availability requests keyed by client-supplied geolocation/locale hints do not alter derivation (server-side transacting market only, SECURITY.md, Authentication rule 10); gating-absence test with issue 025's enforcement point (gated SKU yields no state).
- **Compliance Tests**: Contributes fixtures to the market-gating test matrix (issue 027, CC-QA-003).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-CAT-002, CC-CAT-003.

## Dependencies
- **Upstream**: 003 Shared kernel identity types; 015 PostgreSQL Flexible Server; 021 RFC 9457 error handling; 025 Server-side gating enforcement (upstream filter for all user-facing reads); 029 SKU domain model.
- **Downstream**: 044 Regional cold-store order routing; 045 Checkout serviceability; 066 Menu page (DESIGN.md §5.2 badges); 067 Product detail page; 083 Dashboard sales analytics (stock service level, CC-DSH-006); 084 Dashboard inventory-by-cold-store module.
- **External**: N/A

## Implementation Notes
- **Constraints**: Catalog & Inventory module schema and least-privilege role (SECURITY.md, Secret handling rule 10). The three states are a closed enum in the shared read model; DESIGN.md §5.2 vocabulary maps 1:1 onto it downstream (in stock→CACHE HIT, restocking→WARMING, unavailable→CACHE MISS) but the strings live in the presentation layer (issue 066), not here. Availability keys on the server-side transacting market (issue 024), never on client hints (CC-SEC-012).
- **Anti-Patterns**: MUST NOT expose raw stock counts as the user-facing availability contract (the contract is the three states, CC-CAT-003); MUST NOT let clients or caches compute availability; MUST NOT implement market conditionals locally (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); MUST NOT default to in-stock on error.
- **AI Development Guidance**: Identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR cites CC-CAT-002/003 (REQUIREMENTS.md §17).

## Open Questions
- How the market→cold-store serving relationship is administered (static configuration vs. dashboard-managed data) is not specified in the specs.
- What distinguishes "restocking (preorder permitted)" from "unavailable in region" when stock is zero — i.e., the signal source for an expected restock (planned replenishment record? manual flag?) — is unspecified.
- DESIGN.md §5.2 mentions offering the "nearest substitute" on CACHE MISS; how a substitute is derived is undefined anywhere (presentation is issue 066's scope, but the data source is unspecified).
- Whether preorders reserve inventory or cap at a quantity is unspecified (order-flow side, but it constrains what this module must expose).
