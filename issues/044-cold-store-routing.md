# 044 · Regional cold-store order routing with audited cross-region override

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** ARCHITECTURE.md "Known unknowns" — *Data residency vs. "single primary write region"*. Routing records persist delivery-address-derived personal data for EU and IN orders; ARCHITECTURE.md states this conflict "blocks any implementation that persists EU or India personal data". Routing logic, schema, and tests can proceed; the production persistence topology awaits a human decision. Not resolved here.

## Metadata
- **ID**: CC-FUL-001
- **Title**: Regional cold-store order routing with audited cross-region override
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: Every consumer order MUST be routed to the regional cold store serving the delivery address, and cross-region fulfillment MUST occur only through an explicit operations override gated by a dashboard permission and appended to the audit log (REQUIREMENTS.md CC-FUL-001).
- **Rationale**: Inventory is tracked per SKU per regional cold store and availability shown to users reflects the cold store(s) serving their market (REQUIREMENTS.md CC-CAT-002); routing every order to its serving store keeps the frozen cold chain regional (REQUIREMENTS.md CC-FUL-002 constrains transit) and keeps stock states truthful. The override is a privileged action, so it falls under the audit mandate for privileged dashboard actions (REQUIREMENTS.md CC-DSH-004; SECURITY.md, Logging rule 6). ARCHITECTURE.md, "Server bounded contexts" item 5 fixes this in the Fulfillment context: "routing to the regional cold store for the delivery address, cross-region override gated by audited dashboard permission".
- **Design**: N/A — server-side routing logic. The dashboard UI surface for the override belongs to issue 082 (DESIGN.md §12 governs that surface).

## Scope
- **Applies To**: API
- **Components**: Fulfillment bounded context (routing decision); consumes order + validated delivery address from Ordering & Payments (issues 036, 038); override permission check via dashboard RBAC (issue 080); audit events to the append-only audit store (issue 081).
- **Actors**: Consumer (indirect, via order submission); operations staff holding the cross-region override permission; the Fulfillment service itself.
- **Data Classification**: Restricted/PII (delivery addresses drive routing)

## Security Context
- **Defense Layer**: Architecture (server-side routing authority); Strict API (permission-gated override endpoint)
- **Threat(s) Addressed**: OWASP Top Ten A01:2021 Broken Access Control (unauthorized cross-region fulfillment); STRIDE Elevation of Privilege and Repudiation (override without permission or without audit trail).
- **Trust Boundary**: Dashboard-to-Fulfillment service edge for the override action; routing itself runs entirely server-side from server state.
- **Zero Trust Consideration**: The routing decision derives only from the server-validated delivery address (issue 038) and server-held cold-store serving data — never from any client-supplied routing hint. The override is honored only for an authenticated staff session whose RBAC permission is checked server-side on every call (SECURITY.md, Authentication and authorization rules 1, 8).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); Authorization/Access Control and Security Logging chapters.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement on the override), AC-6 (least privilege), AU-9 (protection of the audit record, via issue 081)
- **NIST SP 800-207**: Per-request authorization decision for the override; no implicit trust from network location (dashboard is VPN-restricted per SECURITY.md, HTTP boundary rule 8, but that is defense in depth, not the authorization).
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a submitted consumer order with a validated delivery address, when the Fulfillment context routes it, then the order is assigned to the regional cold store serving that delivery address (CC-FUL-001).
2. **AC-02**: Given no operations override exists for an order, when routing completes, then the order MUST NOT be assigned to any cold store other than the one serving the delivery address — including when that store's stock is low (CC-FUL-001; availability semantics stay with CC-CAT-002/003, issue 030).
3. **AC-03**: Given an authenticated staff user holding the cross-region override permission, when they apply a cross-region override to an order, then the order is re-routed to the specified cold store and an audit event is appended recording actor, action, object, before/after cold-store assignment, and timestamp (CC-FUL-001; CC-DSH-004; SECURITY.md, Logging rule 6).
4. **AC-04**: Given an authenticated staff user without the override permission, when they attempt a cross-region override, then the request is denied, no routing change occurs, and an authorization-denial security event is logged (SECURITY.md, Authentication and authorization rules 1, 8; Logging rule 3).
5. **AC-05**: Given an unauthenticated or consumer-session caller, when the override endpoint is invoked, then the fallback deny-by-default policy rejects it (SECURITY.md, Authentication and authorization rule 1; issue 020) — the override is reachable only from the dashboard origin, never from storefront or portal (ARCHITECTURE.md, Dependency rule 4).
6. **AC-06**: Given an exception thrown inside the routing or override-permission path, when it occurs, then the operation is denied/aborted rather than defaulting to any assignment or bypass (SECURITY.md, Logging rule 2).
7. **AC-07**: Given an applied override, when the audit event cannot be written, then the override does not take effect (fail closed; SECURITY.md, Logging rule 2) — an unaudited cross-region fulfillment MUST NOT exist (CC-FUL-001).

## Failure Behavior
- **On Invalid Input**: Unroutable or unserviceable delivery address is rejected upstream at checkout (issue 045, CC-FUL-002) with RFC 9457 problem details (issue 021); a routing request with an invalid order or cold-store reference returns 400/404 without disclosing internal topology (SECURITY.md, Logging rule 1; Authentication rule 9).
- **On System Error**: Fail closed — any exception in an authorization or gating path is a denial, never a bypass (SECURITY.md, Logging rule 2). Orders that cannot be routed remain in their current state for retry; no default-region fallback.
- **Alerting**: Authorization denials on the override endpoint and routing failures logged as structured security events to centralized monitoring with alerting (SECURITY.md, Logging rule 3; CC-NFR-003 — Azure Monitor per ARCHITECTURE.md).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit-style unit tests on the routing resolution (address region → serving cold store), override permission evaluation, and fail-closed exception paths. Tagged CC-FUL-001 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests: order submission → routed assignment; override with/without permission; audit event content and append (with issue 081's store); deny-by-default on anonymous calls.
- **Security Tests**: AuthZ suite attempts cross-role access to the override endpoint for every dashboard role and MUST fail closed (CC-QA-005; SECURITY.md, Deployment rule 8).
- **Compliance Tests**: Automated assertion that every cross-region fulfillment in test runs has a matching audit event (CC-DSH-004 evidence).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this authz-adjacent code (CC-QA-001).

## Dependencies
- **Upstream**: 001 (solution scaffold), 020 (deny-by-default authorization), 030 (inventory per SKU per regional cold store), 035 (order state machine), 036 (order submission), 038 (per-market address capture and validation), 080 (dashboard RBAC), 081 (append-only audit store).
- **Downstream**: 045 (checkout serviceability computes transit from the serving store), 082 (dashboard order management UI exposing the override), 084 (inventory-by-cold-store module).
- **External**: None directly (carrier interaction is issue 045 via EasyPost).
- **Open decision**: AT RISK on data residency vs. single primary write region (ARCHITECTURE.md, "Known unknowns") — see blockquote.

## Implementation Notes
- **Constraints**: Implement inside the Fulfillment bounded context of the .NET 10 modular monolith with its own PostgreSQL schema and least-privilege database role over TLS (ARCHITECTURE.md, "Packaging"; SECURITY.md, Secret handling rule 10). Override endpoint protected by a named policy (`[Authorize(Policy=...)]`) under the global fallback policy (SECURITY.md, Authentication rules 1, 8). Bind override requests to dedicated DTOs with explicit `[FromBody]`/`[FromRoute]` sources; server sets actor identity and timestamps from server state (SECURITY.md, Input validation rule 3). Audit rows are INSERT-only at the database-privilege level (SECURITY.md, Logging rule 6; issue 081).
- **Anti-Patterns**: MUST NOT route from client-supplied region/market hints (SECURITY.md, Authentication rule 10); MUST NOT allow the storefront or portal to import or invoke the override (ARCHITECTURE.md, Dependency rule 4); MUST NOT implement per-module market conditionals — gating consults the Market & Gating Policy context (ARCHITECTURE.md, Dependency rule 1); MUST NOT mutate or delete audit records (SECURITY.md, Logging rule 6).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, ≥ 80% coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-FUL-001 (REQUIREMENTS.md §17).

## Open Questions
- The cold-store topology is not enumerated anywhere in the specs: which regional cold stores exist, and which markets/postal regions each one serves ("a fulfillment node holding frozen inventory for one or more markets", REQUIREMENTS.md §2) — this mapping is required data for routing and must be supplied, not invented.
- Which dashboard role(s) (sales-viewer, ops-agent, finance, hr-admin, admin — CC-DSH-002) hold the cross-region override permission is not specified.
- Whether the cross-region override is a "sensitive action" requiring step-up re-authentication (SECURITY.md, Authentication rule 2 lists refunds, employee-record access, role changes) is not specified.
