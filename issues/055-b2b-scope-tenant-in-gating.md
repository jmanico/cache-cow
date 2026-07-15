# 055 · B2B scope and tenant enforcement with IN gating parity

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-API-004, CC-API-007
- **Title**: B2B scope and tenant enforcement with IN gating parity
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Every B2B endpoint MUST enforce the least-privilege scopes `catalog:read`, `orders:write`, `orders:read`, `invoices:read` and partner tenancy server-side so that a partner can never read or mutate another partner's orders or invoices, and MUST apply market gating identically to the storefront so that a partner authorized for the IN market cannot order non-veg SKUs through any endpoint (REQUIREMENTS.md CC-API-004, CC-API-007; SECURITY.md, Authentication rules 8–9).
- **Rationale**: Cross-tenant access on orders/invoices is the classic broken-object-level-authorization failure on the API's most sensitive data; wholesale terms are confidential per partner (CC-WHS-003). IN gating parity is a regulatory-compliance path: CC-MKT-003 requires server-side exclusion of non-veg SKUs from the IN market, and the API is an equal enforcement surface — ARCHITECTURE.md Dependency rule 1 makes the Market & Gating Policy service the single upstream enforcement point that the B2B API must consult, never reimplement.
- **Design**: N/A (machine-to-machine surface; no UI).

## Scope
- **Applies To**: API
- **Components**: Wholesale & B2B API bounded context (partner tenancy); Market & Gating Policy bounded context as the consulted enforcement point (ARCHITECTURE.md, Server bounded contexts #1 and #6)
- **Actors**: B2B API clients (partner service accounts) with per-market authorization
- **Data Classification**: Confidential (partner orders, invoices, wholesale terms); Regulated market-compliance data (IN veg-only catalog, CC-MKT-003)

## Security Context
- **Defense Layer**: Strict API | Architecture
- **Threat(s) Addressed**: Broken object-level authorization / IDOR (OWASP API Security Top 10 API1, CWE-639), broken function-level authorization (excessive scopes), cross-tenant data exposure, market-gating bypass via the API channel (compliance violation of CC-MKT-003)
- **Trust Boundary**: B2B API endpoints — the caller's token scopes and tenant claim are the only authority; every resource access is re-authorized server-side per request (SECURITY.md, Authentication rule 9).
- **Zero Trust Consideration**: No identifier supplied by the client (order ID, invoice ID, SKU, market parameter) is trusted; every query is scoped to the authenticated client's tenant, and gating decisions key exclusively off server-side market authorization state, never client-supplied hints (SECURITY.md, Authentication rule 10).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V4-equivalent access-control chapter (object-level and function-level authorization, deny by default)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (Access Enforcement), AC-6 (Least Privilege)
- **NIST SP 800-207**: Per-request resource-level authorization decisions using verified identity and tenancy, independent of network origin.
- **Regulatory**: IN veg-only market compliance per REQUIREMENTS.md CC-MKT-003 (FSSAI context under CC-CMP-004 governs presentation; the exclusion rule itself is CC-MKT-003).
- **Other**: RFC 9700 (least-privilege scope guidance for OAuth-protected APIs)

## Acceptance Criteria
1. **AC-01** (CC-API-004): Given the API's scope model, when tokens are issued and enforced, then exactly the scopes `catalog:read`, `orders:write`, `orders:read`, `invoices:read` exist, and each endpoint requires its documented scope via named authorization policies — an otherwise-valid token lacking the required scope receives 403.
2. **AC-02** (CC-API-004, SECURITY.md Authentication rule 9): Given partner A's valid token, when it requests any order or invoice belonging to partner B (direct ID reference, list filter manipulation, or nested resource path), then the API returns 404 (existence not confirmed) and no data crosses the tenant boundary.
3. **AC-03** (CC-API-004): Given partner A's token with `orders:write`, when it attempts to mutate (create against, update, or cancel) an order belonging to partner B, then the mutation is rejected and no state change occurs.
4. **AC-04** (CC-API-007): Given a partner authorized for the IN market, when it submits an order containing a non-veg SKU through any order-creation or order-mutation endpoint, then the request is rejected and no order line is created — enforcement is performed via the server-side Market & Gating Policy enforcement point (issue 025; ARCHITECTURE.md, Dependency rule 1), not API-local conditionals.
5. **AC-05** (CC-API-007, CC-MKT-003): Given a partner authorized for the IN market, when it lists or reads the catalog, then non-veg SKUs are absent from every response; a direct request for a non-veg SKU resource in IN-market context returns 404 (CC-MKT-004 semantics per SECURITY.md, Authentication rule 9).
6. **AC-06** (CC-API-007, CC-QA-003): Given the CC-MKT-003 parity test required by CC-API-007, when the market-gating test matrix runs, then it exercises the B2B API surface (catalog read, order create) for every market × veg/non-veg SKU combination and passes.
7. **AC-07** (negative, SECURITY.md Logging rule 2): Given an exception thrown inside scope, tenancy, or gating evaluation, when any request is being authorized, then the request is denied — an authorization-path error never results in data being returned or an order being created.

## Failure Behavior
- **On Invalid Input**: Cross-tenant or inaccessible-resource requests return 404 (never 403 confirming existence — SECURITY.md, Authentication rule 9); missing scope returns 403; gated-SKU order attempts are rejected with an RFC 9457 problem-details body that does not enumerate the gated catalog. All denials logged as structured authz-denial security events with correlation IDs (SECURITY.md, Logging rule 3).
- **On System Error**: Fail closed — any exception in an authorization or gating path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authorization-denial spikes and repeated cross-tenant probing per client alert via centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Authorization-policy unit tests per endpoint (scope required, deny-by-default); tenant-scoping predicate tests ensuring every order/invoice query is filtered by the caller's tenant.
- **Integration Tests**: ASP.NET Core integration tests with two seeded partner tenants (one IN-authorized): full cross-tenant matrix on orders and invoices (read + mutate); IN partner ordering veg (succeeds) and non-veg (rejected, no state change); catalog listing in IN-market context contains zero non-veg SKUs; direct non-veg SKU fetch returns 404.
- **Security Tests**: The CC-QA-005 authz suite attempts cross-tenant and cross-role access for every sensitive endpoint and MUST fail closed; the CC-QA-003 market-gating matrix includes the API surface (CC-API-007 parity test); DAST IDOR checks against staging (CC-QA-007). Mutation testing SHOULD run on gating and authz code (CC-QA-001).
- **Compliance Tests**: Gating-matrix results retained as CI evidence; tests tagged CC-API-004/007, CC-QA-005 (REQUIREMENTS.md §17).
- **Coverage Target**: ≥ 80% per package (CC-QA-001), with mutation testing on authz/gating paths per CC-QA-001.

## Dependencies
- **Upstream**: 054 B2B OAuth2 client-credentials authentication and token validation (authenticated identity and scopes); 025 Server-side gating enforcement API with IN veg-only exclusion (the single enforcement point this issue consults); 026 404 semantics for market-gated resources; 049 Partner tenancy and onboarding approval workflow (tenant model); 020 Deny-by-default authorization fallback policy.
- **Downstream**: 027 Market-gating CI test matrix (extends to the API surface via this issue's parity test); 062 Object-level authorization and cross-tenant/IDOR test suite (covers these endpoints); 052 Wholesale portal UI (consumes tenant-scoped API); 057 Outbound partner webhooks (per-partner event scoping).
- **External**: Microsoft Entra ID (scope/claims issuance per ARCHITECTURE.md, Authentication model).

## Implementation Notes
- **Constraints**: Named ASP.NET Core authorization policies (`[Authorize(Policy=...)]`/`RequireAuthorization()`) per SECURITY.md Authentication rule 1; object-level authorization enforced by scoping every EF/LINQ query to the caller's tenant claim (SECURITY.md, Authentication rule 9); market gating consulted from the Market & Gating Policy service — the API must not implement its own market conditionals (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1). Partner market authorization is server-side state; never derived from request parameters or client hints (SECURITY.md, Authentication rule 10).
- **Anti-Patterns**: No API-local veg/non-veg conditionals (CC-MKT-006). No 403 on inaccessible resources (use 404 — SECURITY.md, Authentication rule 9). No trusting client-supplied market, tenant, or ownership fields (SECURITY.md, Input validation rule 3 — server-controlled fields set from server state only). No unscoped repository/query methods callable from B2B request handlers.
- **AI Development Guidance**: AI-generated code passes identical merge gates — SAST/SCA/secret scan, tests, coverage, lint, mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); the CC-QA-003/005 suites run on every merge (SECURITY.md, Deployment rule 8).

## Open Questions
- Whether a partner can be authorized for multiple markets simultaneously (and how per-market authorization is represented in the token vs. the tenancy record) is not specified; CC-API-007 says "authorized for the IN market" without defining the multi-market shape.
- The problem-details error code/shape for a gated-SKU order rejection (vs. a plain validation error) is not specified; recorded rather than invented.
