# 050 · Wholesale price lists isolated from consumer sessions

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-WHS-001, CC-WHS-003, CC-WHS-004
- **Title**: Wholesale price lists isolated from consumer sessions
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: The platform MUST maintain wholesale price lists per market, with payment terms defaulting to net-60 and adjustable per partner (ratified 2026-07-15), and these prices and terms MUST be invisible to consumer sessions and MUST NOT be derivable from consumer API responses (REQUIREMENTS.md CC-WHS-001, CC-WHS-003, CC-WHS-004).
- **Rationale**: Wholesale prices and terms are commercially sensitive partner data. ARCHITECTURE.md, Dependency rule 3 fixes the boundary: "Consumer surfaces must not depend on wholesale data. Wholesale prices/terms live behind the partner tenancy boundary and are unreachable from consumer session context (CC-WHS-003)." Price lists are the canonical input for partner case-quantity ordering (CC-WHS-001) and for the payment terms carried onto wholesale invoices (CC-WHS-004).
- **Design**: N/A — server-side data model and boundary enforcement. Portal presentation of these prices/terms is issue 052 (DESIGN.md §11).

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context (ARCHITECTURE.md, Server bounded contexts 6) — wholesale price-list and payment-terms model; boundary assertions against the consumer storefront/API response schemas (contexts 2–4 emit consumer responses this issue must prove clean).
- **Actors**: Approved wholesale partners (tenant-scoped, via portal or B2B API); consumers (guest and authenticated) as the population that MUST NOT see this data; internal staff who administer price lists (administration UI is out of scope here, see Open Questions).
- **Data Classification**: Confidential (partner-specific commercial terms and wholesale pricing).

## Security Context
- **Defense Layer**: Architecture (bounded-context/tenancy boundary) + Strict API (response schemas with no wholesale fields)
- **Threat(s) Addressed**: OWASP API Security Top 10 — broken object-level authorization and excessive data exposure (wholesale fields leaking into consumer responses); OWASP Top Ten A01:2021 Broken Access Control; STRIDE Information Disclosure (consumer session deriving wholesale prices/terms, CC-WHS-003).
- **Trust Boundary**: The partner tenancy boundary (ARCHITECTURE.md, Dependency rule 3). Consumer session context sits entirely outside it; only tenant-scoped partner sessions/tokens (issues 051, 054/055) cross it.
- **Zero Trust Consideration**: Access to a price list is decided from server-side tenancy and authorization state only — never from a client-supplied parameter, header, or hint requesting "wholesale" treatment. Consumer clients render prices only from typed, validated consumer-schema responses (SECURITY.md, Input validation rule 1), which contain no wholesale fields by schema.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V4 Access Control (object-level authorization, tenant scoping); API/Web Service verification for schema-constrained responses.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement at the tenancy boundary), AC-6 (least privilege — consumer surfaces hold no wholesale read path), SC-8 (TLS on every data-store connection, SECURITY.md, Secret handling rule 10).
- **NIST SP 800-207**: Per-request, per-tenant authorization; no implicit trust from network position or session type.
- **Regulatory**: N/A (invoice legal content is issues 046–048).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a market, when a wholesale price list is defined, then it carries per-SKU wholesale prices for that market in the market's currency, stored as integer minor units with overflow-checked arithmetic — no binary floating point (CC-WHS-001, CC-PRC-001, CC-PRC-003; shared kernel Money type, ARCHITECTURE.md, Dependency rule 9).
2. **AC-02**: Given an approved partner with no partner-specific adjustment, when its payment terms are resolved, then the result is net-60 (default ratified 2026-07-15); given a partner with adjusted terms, the per-partner value overrides the default (CC-WHS-004).
3. **AC-03**: Given a tenant-scoped partner context (portal session or B2B token), when it requests price lists or terms, then it receives only its own partner's data for markets it is authorized in; a request for another partner's price list or terms is denied with 404 (CC-WHS-003; SECURITY.md, Authentication rule 9).
4. **AC-04 (negative)**: Given any consumer session (guest or authenticated consumer), when it calls any storefront or consumer API endpoint, then no response contains wholesale prices, wholesale terms, or any field from the wholesale price-list model — asserted at the response-schema level (published schemas are the single source of truth, ARCHITECTURE.md, Dependency rule 7) and by integration test (CC-WHS-003).
5. **AC-05 (negative)**: Given a consumer request carrying any client-supplied parameter, header, or cookie attempting to select wholesale pricing (e.g., a forged tenant/price-list identifier), when processed, then the request is either rejected (400, unknown fields rejected per SECURITY.md, Input validation rule 2) or served consumer data only — wholesale data is never derivable from a consumer session (CC-WHS-003).
6. **AC-06 (negative)**: Given wholesale price-list data at rest, when consumer bounded contexts access the database, then their per-context least-privilege roles hold no grants on the wholesale schema, so even an injection flaw in a consumer module cannot read partner terms (SECURITY.md, Secret handling rule 10; CC-SEC-021).
7. **AC-07**: Given an edge/SSR/CDN cache, when consumer catalog responses are cached, then no cached response ever contains wholesale data, and tenant-scoped wholesale responses are never edge-cached (personalized/authenticated responses are `Cache-Control: no-store` — SECURITY.md, HTTP boundary rule 10; CC-MKT-009).

## Failure Behavior
- **On Invalid Input**: Reject with 400 and RFC 9457 problem details, unknown fields rejected, no internal identifiers disclosed (SECURITY.md, Input validation rule 2; Logging rule 1).
- **On System Error**: Fail closed — any exception resolving price-list authorization or tenancy is a denial (SECURITY.md, Logging rule 2); the system MUST NOT fall back to serving wholesale data on error, and consumer price paths never fall through to wholesale tables.
- **Alerting**: Authorization denials on wholesale endpoints and any validation rejection indicating attempted cross-tenant or consumer-to-wholesale access are logged as structured security events with alerting (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on price-list resolution (per market, per partner), net-60 default vs. per-partner override, Money-type arithmetic (integer minor units, overflow-checked, all five currencies including JPY zero-decimal and INR grouping — CC-PRC-003, CC-QA-004).
- **Integration Tests**: ASP.NET Core integration tests: tenant-scoped retrieval; consumer endpoints exercised against published JSON Schemas asserting zero wholesale fields; database-role test proving consumer-context roles cannot select from the wholesale schema.
- **Security Tests**: Cross-tenant AuthZ suite (partner A → partner B price list/terms) failing closed with 404 (CC-QA-005; SECURITY.md, Authentication rules 8–9); consumer-session derivation attempts (forged parameters/headers) in the authz suite; SAST/SCA gates per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: CI schema check that consumer API response schemas contain no wholesale-model fields (schemas as single source of truth, CC-API-010; ARCHITECTURE.md, Dependency rule 7).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this money/authz code (CC-QA-001); tests tagged CC-WHS-001/003/004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 002 Shared kernel Money type (CC-PRC-003); 003 Market/Locale/SKU identity types; 015 PostgreSQL per-context schemas/roles (CC-SEC-021); 020 Deny-by-default authorization; 029 SKU domain model (price lists reference SKU identity); 049 Partner tenancy and onboarding approval workflow (the tenant scoping key and per-partner terms adjustment target).
- **Downstream**: 052 Wholesale portal UI (displays these prices/terms); 053/055 B2B API scaffold and scope/tenant enforcement (serves `catalog:read` wholesale data); 046–048 Invoicing (wholesale orders generate invoices carrying these payment terms, CC-WHS-004); 062 Object-level authorization/IDOR test suite; 085 Dashboard partner management module.
- **External**: None.

## Implementation Notes
- **Constraints**: Wholesale price-list tables live in the Wholesale & B2B API bounded context's own PostgreSQL schema, reachable only by that context's least-privilege role over TLS (SECURITY.md, Secret handling rule 10). Prices use the shared-kernel Money type (integer minor units, ARCHITECTURE.md, Dependency rule 9). All wholesale endpoints sit behind named `[Authorize]` policies with tenant scoping in every query (SECURITY.md, Authentication rules 1, 8–9); parameterized queries / safe ORM only (Input validation rule 4). Wholesale responses set `Cache-Control: no-store` (HTTP boundary rules 3, 10). Display of prices is downstream; only the Ordering path computes money at submission (ARCHITECTURE.md, Dependency rule 2).
- **Anti-Patterns**: MUST NOT reuse consumer catalog response DTOs with nullable wholesale fields (excessive-data-exposure vector — schemas must be disjoint); MUST NOT gate wholesale visibility client-side; MUST NOT grant consumer-context database roles any access to the wholesale schema; MUST NOT accept client-supplied price-list or tenant identifiers as authorization input; MUST NOT store money as floating point anywhere including tests (CC-PRC-003).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests, ≥ 80% coverage, lint, SAST/SCA/secret scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-WHS-001/003/004 (REQUIREMENTS.md §17).

## Open Questions
- The internal structure of a wholesale price list is not specified beyond "case-quantity orders against wholesale price lists per market" (CC-WHS-001) — whether it prices per case, supports volume tiers, or carries lead-time data (DESIGN.md §11 displays lead times, but no CC-* authors that data) needs a human decision.
- Whether wholesale prices follow the consumer tax-display conventions of CC-PRC-002 or are net-of-tax (with tax applied at invoicing per CC-INV-001) is not specified for B2B.
- Which staff role administers price lists and per-partner terms adjustments, and through which surface (presumably dashboard partner management, issue 085), is not authored anywhere.
- Whether per-partner terms adjustments are a privileged audited action under CC-DSH-004 is implied (dashboard action) but not explicit.
