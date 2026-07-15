# 083 · Dashboard sales analytics module

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-006 (module inventory: CC-DSH-003)
- **Title**: Dashboard sales analytics module
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: The internal dashboard MUST provide a sales analytics module showing per-region per-SKU stock service level (the regional "cache hit rate"), revenue, AOV, and promotion performance, filterable by market and date range.
- **Rationale**: CC-DSH-006 defines the analytics content and filters; CC-DSH-003 lists sales analytics (by market, SKU, channel) as a launch dashboard module. DESIGN.md §12 notes the per-SKU per-region hit rate is the one dashboard moment where the brand metaphor and the operational truth are the same thing, and requires the sales overview regions to map to the same region model as consumer pricing — preventing a parallel, drifting region taxonomy.
- **Design**: DESIGN.md §12 — Pit theme within the issue 079 shell; charts bar and line only by default, series colors from the Char/Ember/Cache/Smoke families, one accent family per meaning, never decorative palettes; direct labeling over legends where series count is 3 or fewer; Archivo for UI, IBM Plex Mono for every number, table numerals right-aligned, units in column headers not cells; compact 40px rows, sticky headers, keyboard-first filtering. Accessibility per DESIGN.md §13: WCAG 2.2 AA including the dashboard, status never conveyed by color alone, `prefers-reduced-motion` honored.

## Scope
- **Applies To**: Both (dashboard Angular views plus Back Office read endpoints)
- **Components**: Dashboard sales-analytics Angular views (charts, tables, market and date-range filters); Back Office read-only analytics endpoints aggregating from the owning contexts — Catalog & Inventory (stock service level per regional cold store), Ordering & Payments (revenue, AOV), Pricing & Promotions (promotion performance) (ARCHITECTURE.md, "Server bounded contexts"). Explicitly excluded: the inventory-by-cold-store operational module (issue 084), promotion definition/administration (issue 033), RBAC matrix definition (issue 080), the metrics/observability pipeline (issue 095), chart tooling for other modules.
- **Actors**: Internal staff in roles granted analytics read access per the issue 080 matrix (sales-viewer at minimum is the natural reader; grants are the matrix's to define)
- **Data Classification**: Confidential (revenue and sales performance data; aggregates only — no customer PII is required by the specified metrics)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: OWASP Top Ten A01:2021 Broken Access Control (revenue data exposed to unauthorized roles or outside the dashboard boundary); excessive data exposure via over-broad aggregation endpoints
- **Trust Boundary**: Back Office read endpoints at the dashboard boundary (issue 079's separate origin and VPN path; issue 080's role enforcement)
- **Zero Trust Consideration**: Analytics endpoints enforce role authorization per request (deny-by-default fallback, SECURITY.md, Authentication rule 1); filter inputs (market, date range) are validated server-side as untrusted (SECURITY.md, Input validation rule 1); the client renders only typed, validated response data (no client-side aggregation of raw records).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (role-gated read access) — platform baseline ASVS 5.0 Level 2 (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: WCAG 2.2 AA (DESIGN.md §13; CC-NFR-004)

## Acceptance Criteria
1. **AC-01**: Given an authorized staff member, when they open the sales analytics module, then it presents per-region per-SKU stock service level (the regional "cache hit rate"), revenue, AOV, and promotion performance (CC-DSH-006).
2. **AC-02**: Given the analytics views, when the user applies a market filter and a date range, then every displayed metric reflects exactly that market and range, and region groupings map to the same region model as consumer pricing — no parallel region taxonomy (CC-DSH-006; DESIGN.md §12).
3. **AC-03**: Given any analytics chart, when it renders by default, then it is a bar or line chart using series colors drawn only from the Char/Ember/Cache/Smoke families, with direct labeling instead of a legend when the series count is 3 or fewer (DESIGN.md §12).
4. **AC-04**: Given analytics tables, when numeric values render, then they are in IBM Plex Mono, right-aligned, with units in the column headers not the cells, and monetary values are locale-formatted from server-provided integer-minor-unit data — never hand-formatted, never floating point (DESIGN.md §12, §4.4; CC-PRC-003, CC-PRC-004).
5. **AC-05**: Given a staff member whose role lacks analytics access per the issue 080 matrix, when they request any analytics endpoint, then the request fails closed with the denial logged as a structured authz event, and no analytics endpoint is reachable without an authenticated dashboard session (negative case; CC-DSH-002; SECURITY.md, Authentication rules 1 and 8, Logging rules 2–3).
6. **AC-06**: Given a chart or metric, when meaning is conveyed (e.g., good vs. alert states via cache-green vs. Ember), then it is never conveyed by color alone — text labels accompany it — and `prefers-reduced-motion` disables chart reveal animation with content rendered in final state (DESIGN.md §12, §13; CC-NFR-004).
7. **AC-07**: Given invalid filter input (unknown market, malformed or reversed date range), when it is submitted, then the server rejects it with 400 and an RFC 9457 problem body without executing any aggregation (negative case; SECURITY.md, Input validation rule 1, Logging rule 1).

## Failure Behavior
- **On Invalid Input**: Reject with 400 and RFC 9457 problem details, logged with correlation ID; no partial aggregation runs (SECURITY.md, Input validation rule 1; Logging rule 1).
- **On System Error**: Fail closed on authorization (SECURITY.md, Logging rule 2); on aggregation errors the user sees a generic message with a correlation ID only — never raw errors or internal endpoints (SECURITY.md, Logging rules 1 and 7). Read-only module: no state can be corrupted.
- **Alerting**: Authz denials logged as structured security events with alerting on spikes (SECURITY.md, Logging rule 3; CC-SEC-010).

## Test Strategy
- **Unit Tests**: Aggregation logic (service level, revenue, AOV, promotion performance) over fixture data, including Money-type arithmetic in integer minor units (CC-PRC-003); filter validation. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: ASP.NET Core integration tests for the analytics endpoints per role (grant/deny); market + date-range filter correctness against seeded PostgreSQL data; region-model parity with the pricing region model.
- **Security Tests**: Cross-role access attempts on analytics endpoints failing closed (CC-QA-005, with issue 062); filter-input fuzzing rejected at validation; parameterized-query verification with allowlisted filter columns (SECURITY.md, Input validation rule 4).
- **Compliance Tests**: Automated accessibility checks on the module (WCAG 2.2 AA, CC-NFR-004) including color-not-alone and reduced-motion assertions; CI contrast checks on chart token combinations (DESIGN.md §3.2, §13).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-DSH-006, CC-DSH-003, CC-NFR-004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 079 "Dashboard shell: separate origin, VPN-restricted, Pit theme"; 080 "Dashboard RBAC: role-permission matrix and enforcement"; 005 "Design token pipeline" (chart series tokens); 002 "Shared kernel: Money type"; 030 "Inventory per SKU per regional cold store with three-state availability" (stock service-level source); 032 "Per-SKU per-market price model" and 033 "Promotion engine" (revenue/promotion sources); 035 "Order state machine" (order data underlying revenue/AOV); 034 "Locale-aware price formatting" (monetary display).
- **Downstream**: 084 "Dashboard inventory-by-cold-store module" (shares the region/cold-store model; DESIGN.md §12 calls inventory the literal cache view); 098 "Accessibility gates (WCAG 2.2 AA)".
- **External**: None (internal data only; no third-party analytics services appear in the specs).

## Implementation Notes
- **Constraints**: Read-only endpoints in the Back Office context consuming other contexts through their APIs, not their schemas (ARCHITECTURE.md, Dependency rule 9; SECURITY.md, Secret handling rule 10); monetary aggregates computed in integer minor units with overflow-checked operations (CC-PRC-003) and formatted locale-aware at display (CC-PRC-004); chart colors and typography consumed from `tokens.json` (ARCHITECTURE.md, Dependency rule 8); authenticated responses `Cache-Control: no-store` (SECURITY.md, HTTP boundary rule 3); allowlisted sort/filter parameters, parameterized queries only (SECURITY.md, Input validation rule 4).
- **Anti-Patterns**: MUST NOT use decorative chart palettes or chart types beyond bar/line by default (DESIGN.md §12); MUST NOT hand-format currency strings or use binary floating point for money, including in tests (CC-PRC-003/004); MUST NOT expose raw order records or customer PII through aggregation endpoints when the specified metrics need only aggregates; MUST NOT invent a region taxonomy separate from the consumer pricing region model (DESIGN.md §12); MUST NOT rely on client-side filtering as enforcement (SECURITY.md, Input validation rule 1).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Metric definitions are not specified: the formula for the stock service level / "cache hit rate" (e.g., in-stock time share vs. demand fill rate), the AOV computation basis (which order states count; gross vs. net of refunds), revenue recognition basis (order submission vs. delivery; inclusive/exclusive of tax per market), and what "promotion performance" comprises. These need human definition before implementation; acceptance criteria assert presence and filter behavior, not formulas.
- CC-DSH-003 says sales analytics is "by market, SKU, channel", but CC-DSH-006 requires filters for market and date range only; whether a channel (consumer vs. wholesale) filter/dimension is in scope for v1 is ambiguous.
- Data freshness is unspecified (real-time vs. periodic aggregation) — no latency target exists for dashboard analytics in the specs.
- Currency presentation for cross-market aggregate views is unspecified: per-market revenue is in the market currency (CC-PRC-001, no runtime FX conversion), so whether a multi-market total may be displayed at all, and in what currency, needs a human decision.
