# 084 · Dashboard inventory-by-cold-store module

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-003, CC-CAT-002
- **Title**: Dashboard inventory-by-cold-store module
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: The internal operations dashboard MUST provide an inventory-by-cold-store module that displays per-SKU inventory for each regional cold store from the Catalog & Inventory bounded context, role-gated and rendered per the dashboard design language (REQUIREMENTS.md CC-DSH-003, CC-CAT-002; DESIGN.md §12).
- **Rationale**: Inventory is tracked per SKU per regional cold store (CC-CAT-002), and "inventory by cold store" is one of the six launch dashboard modules (CC-DSH-003). Operations staff need this view to see what each regional cold store holds — the availability shown to consumers derives from the cold store(s) serving their market (CC-CAT-002), so this is the operational ground truth behind the storefront's three stock states (CC-CAT-003). DESIGN.md §12 notes this module is the literal cache view — the one dashboard moment where the brand metaphor and the operational truth are the same thing; the per-SKU per-region hit-rate metric itself (CC-DSH-006) belongs to the sales analytics module (issue 083), not this one.
- **Design**: DESIGN.md §12 — Pit background (`color.pit.950`), Pitpaper text, cache-green for good states, Ember for alerts, Smoke for neutral; Archivo for UI and IBM Plex Mono for every number; table numerals right-aligned with units in column headers, not cells; compact density (40px rows), sticky headers, keyboard-first filtering. All colors/type consumed from the generated `tokens.json` (ARCHITECTURE.md, Dependency rule 8; issue 005). If charts are used: bar and line only, Char/Ember/Cache/Smoke series colors, direct labeling where series count ≤ 3 (DESIGN.md §12).

## Scope
- **Applies To**: Both (dashboard Angular client; Back Office + Catalog & Inventory server endpoints)
- **Components**: Internal dashboard (Back Office bounded context, ARCHITECTURE.md Server bounded contexts item 8); read access to the Catalog & Inventory context's inventory model (item 2; issue 030); dashboard Angular app (issue 079)
- **Actors**: Authenticated internal staff (role per the issue 080 role–permission matrix)
- **Data Classification**: Internal (operational inventory data; no PII)

## Security Context
- **Defense Layer**: Strict API (server-side RBAC-gated read endpoints; typed, schema-validated responses)
- **Threat(s) Addressed**: Broken access control on internal tooling (OWASP Top Ten A01:2021); exposure of internal operational data outside the dashboard trust boundary
- **Trust Boundary**: Dashboard origin — separate origin from storefront and portal, VPN-restricted, distinct session scope (SECURITY.md, HTTP boundary rule 8; CC-SEC-011; issue 079)
- **Zero Trust Consideration**: Every request is authenticated (staff SSO with mandatory passkeys, CC-DSH-001) and authorized against the RBAC matrix server-side (SECURITY.md, Authentication rule 8); the client renders inventory values only from typed, validated responses (SECURITY.md, Input validation rule 1) and never computes or gates data itself (ARCHITECTURE.md, Dependency rule 1).

## Standards Alignment
- **OWASP ASVS**: V8 Authorization (RBAC enforcement on every dashboard endpoint); V16 Security Logging and Error Handling — under the platform-wide ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege)
- **NIST SP 800-207**: Per-request authorization decisions; no implicit trust from being on the internal network path (VPN is defense in depth, not the authorization mechanism)
- **Regulatory**: N/A (no PII or regulated data in this module)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a staff user whose role grants inventory access (per the issue 080 role–permission matrix), when they open the inventory module, then per-SKU inventory levels are displayed for each regional cold store, sourced from the Catalog & Inventory context's inventory model (CC-CAT-002, CC-DSH-003; issue 030).
2. **AC-02**: Given the inventory view, when the user filters, then filtering by regional cold store, market, and SKU is available and keyboard-first, with sticky headers and compact 40px rows (CC-DSH-003; DESIGN.md §12).
3. **AC-03**: Given any rendered inventory table, then every number is set in IBM Plex Mono, right-aligned, with units in column headers not cells, and all colors/typography come from the generated `tokens.json` with no hardcoded brand values (DESIGN.md §12; ARCHITECTURE.md, Dependency rule 8).
4. **AC-04**: Given a staff user whose role does not include inventory access, when they request the module's endpoints, then the server returns 404 (hardening default for inaccessible resources) and the denial is logged as a structured security event (SECURITY.md, Authentication rules 8–9; Logging rule 3; CC-DSH-002).
5. **AC-05**: Given an unauthenticated request to any module endpoint, then the deny-by-default fallback authorization policy rejects it — no endpoint is reachable without an explicit policy (SECURITY.md, Authentication rule 1; issue 020).
6. **AC-06**: Given any module response, then it carries `Cache-Control: no-store` (authenticated response) and is served only on the dashboard origin — the module MUST NOT be reachable from, or share modules/cookies/tokens with, the storefront or portal (SECURITY.md, HTTP boundary rules 3 and 8; ARCHITECTURE.md, Dependency rule 4).
7. **AC-07**: Given the module's data flow, then it is read-only display of Catalog & Inventory data via typed, schema-validated responses — the client MUST NOT recompute availability states or implement its own market conditionals (SECURITY.md, Input validation rule 1; ARCHITECTURE.md, Dependency rule 1).

## Failure Behavior
- **On Invalid Input**: Malformed filter/query parameters rejected with 400 and an RFC 9457 problem-details body containing a correlation ID and no internal state (SECURITY.md, Logging rules 1 and 7; issue 021).
- **On System Error**: Fail closed — any exception in the authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2). If the Catalog & Inventory read fails, the module shows a generic error with correlation ID; it never renders stale or fabricated inventory values.
- **Alerting**: Authorization denials and authentication failures logged as structured security events to Azure Monitor / Application Insights with alerting on spikes (SECURITY.md, Logging rules 3 and 8; CC-NFR-003).

## Test Strategy
- **Unit Tests**: Server-side query/filter logic for the per-SKU per-cold-store read model; response DTO mapping. Tagged CC-DSH-003, CC-CAT-002.
- **Integration Tests**: ASP.NET Core integration tests asserting RBAC gating per role (authorized role sees data; every other role gets 404), deny-by-default on unauthenticated requests, and `Cache-Control: no-store` on responses.
- **Security Tests**: AuthZ suite attempts cross-role access to the module's endpoints and MUST fail closed (CC-QA-005; SECURITY.md, Authentication rules 8–9); SAST/SCA/secret-scan/raw-HTML-sink gates per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Automated assertion that authz denials appear in structured security logs (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); Angular component tests for table rendering, filtering, and token consumption; tests tagged with the CC-* IDs they verify (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 030 Inventory per SKU per regional cold store (the data model this module displays); 079 Dashboard shell (separate origin, VPN, Pit theme); 080 Dashboard RBAC (role–permission matrix); 020 Deny-by-default authorization fallback policy; 021 RFC 9457 error handling; 022 Structured security logging; 005 Design token pipeline; 060 Staff SSO with mandatory WebAuthn (CC-DSH-001).
- **Downstream**: 083 Dashboard sales analytics module (the per-SKU per-region stock service level / "cache hit rate" metric, CC-DSH-006, builds on the same inventory data — metric explicitly out of scope here).
- **External**: Azure Monitor / Application Insights (observability, ARCHITECTURE.md Cross-cutting).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith — the Back Office module reads inventory through the Catalog & Inventory context's internal module boundary, not by querying its schema directly (each context connects with its own least-privilege role confined to its own schema; SECURITY.md, Secret handling rule 10 — a cross-schema read grant would violate CC-SEC-021, so expose the data through the owning module's API). Endpoints gated with named `[Authorize(Policy=...)]` policies (SECURITY.md, Authentication rule 1). Angular dashboard built AOT, strict TypeScript, CSP-compatible (SECURITY.md, Deployment rule 9; HTTP boundary rule 2). Allowlist user-chosen sort/filter column names (SECURITY.md, Input validation rule 4).
- **Anti-Patterns**: MUST NOT hardcode brand colors/type (ARCHITECTURE.md, Dependency rule 8); MUST NOT recompute or gate availability in the client (Dependency rule 1); MUST NOT share modules, cookies, or tokens with storefront/portal (Dependency rule 4); MUST NOT use `bypassSecurityTrust*` or unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5); MUST NOT edge-cache or omit `no-store` on these authenticated responses (SECURITY.md, HTTP boundary rules 3 and 10).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Which dashboard role(s) (sales-viewer, ops-agent, finance, hr-admin, admin — CC-DSH-002) are granted inventory access is not specified; the mapping belongs to the documented role–permission matrix owned by issue 080 and needs a human decision there.
- CC-DSH-003 names the module but does not define any inventory *write* operations (adjustments, stock corrections). This issue drafts the module as read-only display; if inventory mutation from the dashboard is wanted, it must be ratified in REQUIREMENTS.md first (per §17, unreferenced code paths are scope creep).
- Data freshness/refresh cadence for the view (live vs. periodic) is unspecified in the canonical docs.
