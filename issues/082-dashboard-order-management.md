# 082 · Dashboard order management module

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-003 (order management module; transitions per CC-ORD-006)
- **Title**: Dashboard order management module
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The internal dashboard MUST provide an order management module supporting order search, order state transitions, and refunds, where every transition respects the CC-ORD-006 state machine, refunds require step-up re-authentication, and every action writes an audit event.
- **Rationale**: CC-DSH-003 names order management (search, state transitions, refunds) as a launch dashboard module. Order state is money-adjacent and customer-facing: transitions must follow the canonical machine `received -> confirmed -> packed -> shipped -> delivered` with `cancelled` and `refunded` as terminal branches (CC-ORD-006), refunds are a sensitive action requiring re-authentication (SECURITY.md, Authentication rule 2; CC-DSH-001), and every privileged action is audited (CC-DSH-004; SECURITY.md, Logging rule 6).
- **Design**: DESIGN.md §12 — Pit theme within the issue 079 shell; compact 40px rows, sticky headers, keyboard-first filtering; Archivo for UI, IBM Plex Mono for every number (order numbers are monospace per DESIGN.md §1 "Brand personality"), table numerals right-aligned, units in column headers not cells. No puns in error recovery or anything touching money movement (DESIGN.md §5.4).

## Scope
- **Applies To**: Both (dashboard Angular views plus Back Office endpoints invoking the Ordering & Payments context)
- **Components**: Dashboard order-management Angular views (search/list, order detail, transition and refund actions); Back Office endpoints for order search, state transition, and refund initiation, delegating to the Ordering & Payments context (ARCHITECTURE.md, "Server bounded contexts" 4 and 8). Explicitly excluded: the order state machine itself (issue 035), processor refund execution against Stripe/Razorpay (issues 039/040 own processor integration), the audit store (issue 081 — this module writes to it), step-up re-authentication mechanics (issue 060 — this module invokes it), RBAC matrix definition (issue 080 — this module consumes it), consumer-facing order tracking (issue 070).
- **Actors**: Internal staff in roles granted order-management permissions by the issue 080 matrix (role names per CC-DSH-002; specific grants are the matrix's to define)
- **Data Classification**: Restricted/PII (orders contain customer name, address, and purchase history) and Regulated on the refund path (money movement)

## Security Context
- **Defense Layer**: Strict API
- **Threat(s) Addressed**: OWASP Top Ten A01:2021 Broken Access Control (unauthorized refunds or transitions); fraudulent refund issuance from a hijacked staff session (mitigated by step-up re-auth, SECURITY.md, Authentication rule 2); repudiation of staff actions (mitigated by audit, CC-DSH-004); invalid state manipulation corrupting fulfillment and money state (CC-ORD-006)
- **Trust Boundary**: Dashboard endpoints at the Back Office boundary; all order mutation flows through the Ordering & Payments context's state machine — the dashboard never mutates order rows directly
- **Zero Trust Consideration**: Every action re-checks role authorization server-side (issue 080); refunds additionally demand fresh re-authentication rather than trusting session age (SECURITY.md, Authentication rule 2); transition legality is validated by the issue 035 state machine, never by the client's view of current state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (role- and action-level enforcement); ASVS 5.0 V3 Session Management (step-up re-authentication for sensitive actions) — platform baseline ASVS 5.0 Level 2 (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement), AC-6 (least privilege), AU-2 (auditable events), IA-11 (re-authentication)
- **NIST SP 800-207**: Per-action authorization; sensitive operations require step-up verification of the actor
- **Regulatory**: N/A
- **Other**: FIDO2/WebAuthn (step-up re-auth uses the mandatory staff passkeys, SECURITY.md, Authentication rule 2, via issue 060)

## Acceptance Criteria
1. **AC-01**: Given a staff member whose role grants order search per the issue 080 matrix, when they search orders, then results are returned with role-shaped fields only, rendered per DESIGN.md §12 (Plex Mono order numbers and numerals, 40px rows, sticky headers, keyboard-first filtering) (CC-DSH-003; SECURITY.md, Authentication rule 8).
2. **AC-02**: Given an order in a given state, when an authorized staff member requests a transition, then only transitions legal under the CC-ORD-006 machine (`received -> confirmed -> packed -> shipped -> delivered`; `cancelled`/`refunded` terminal branches, via issue 035) are offered and accepted, and the transition is appended to the audit log with actor, action, object, before/after, timestamp (CC-ORD-006; CC-DSH-004; issue 081).
3. **AC-03**: Given an illegal transition request (e.g., `delivered -> packed`, or any transition out of a terminal state), when it is submitted — including directly against the endpoint, bypassing the UI — then it is rejected server-side with no state change and a generic RFC 9457 error (negative case; CC-ORD-006; SECURITY.md, Logging rules 1–2).
4. **AC-04**: Given an authorized staff member initiating a refund, when they have not completed step-up re-authentication for this sensitive action, then the refund is refused until re-auth succeeds (issue 060), and a refund request without fresh re-auth MUST NOT execute (negative case; SECURITY.md, Authentication rule 2; CC-DSH-001).
5. **AC-05**: Given a completed refund action, when it is recorded, then the order reaches the terminal `refunded` branch via the state machine, the refund is delegated to the Ordering & Payments context (processor execution per issues 039/040), monetary amounts are handled exclusively in the shared integer-minor-unit Money type with overflow-checked arithmetic (CC-PRC-003), and the action is audited (CC-ORD-006; CC-DSH-004).
6. **AC-06**: Given a staff member whose role lacks a given order-management permission, when they invoke that endpoint, then the request fails closed, the denial is logged as a structured authz event, and inaccessible orders return 404 (negative case; CC-DSH-002; SECURITY.md, Authentication rules 8–9, Logging rules 2–3).
7. **AC-07**: Given any order-management error state, when it is rendered, then the user sees a generic message with a correlation ID only — no raw error bodies, internal endpoints, or humor (SECURITY.md, Logging rules 1 and 7; DESIGN.md §5.4, §9).

## Failure Behavior
- **On Invalid Input**: Reject with 400 and an RFC 9457 problem body (no internal detail); illegal state transitions rejected without processing; all rejections logged with correlation IDs (SECURITY.md, Logging rule 1; Input validation rules 1 and 3).
- **On System Error**: Fail closed — exceptions in authorization, re-auth, state-transition, or refund paths result in denial/no state change, never a bypass or partial mutation; the audit append and the action commit together (SECURITY.md, Logging rule 2; issue 081).
- **Alerting**: Authz denials, step-up failures, and refund anomalies (failure spikes) alert via structured security events to centralized monitoring (SECURITY.md, Logging rules 3 and 8; CC-SEC-010).

## Test Strategy
- **Unit Tests**: Transition-legality guards against the CC-ORD-006 machine; refund amount handling in Money type (overflow-checked, CC-PRC-003); role-shaped DTO mapping. ≥ 80% coverage (CC-QA-001), mutation testing SHOULD run on the money and authz code.
- **Integration Tests**: ASP.NET Core integration tests: full search/transition/refund flows per role; illegal-transition rejection at the endpoint; refund blocked without step-up; audit event presence and content for every action (composes with issue 081 tests).
- **Security Tests**: Cross-role attempts on every order-management endpoint failing closed (CC-QA-005, with issue 062); IDOR attempts on order IDs returning 404 (SECURITY.md, Authentication rule 9); DAST against staging includes this module (CC-QA-007).
- **Compliance Tests**: Automated evidence that every state transition and refund produced an audit record with the required fields (CC-DSH-004, CC-ORD-006).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-DSH-003, CC-ORD-006, CC-DSH-004, CC-QA-005 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 035 "Order state machine with audited transitions" (CC-ORD-006 — the machine this module drives); 060 "Staff SSO with mandatory WebAuthn and step-up re-authentication" (refund re-auth); 079 "Dashboard shell" (origin, VPN, theme); 080 "Dashboard RBAC: role-permission matrix and enforcement"; 081 "Append-only audit store with WORM retention"; 002 "Shared kernel: Money type"; 021 "RFC 9457 error handling and fail-closed authorization/gating behavior".
- **Downstream**: 039 "Stripe payment integration" and 040 "Razorpay payment integration" (execute the refunds this module initiates); 099 "Money-path and mutation-testing suite" (covers refund arithmetic).
- **External**: Stripe and Razorpay (refund execution, via issues 039/040 — never called directly from dashboard code); Microsoft Entra ID (staff identity and step-up, via issue 060).

## Implementation Notes
- **Constraints**: All order mutation goes through the Ordering & Payments context — the Back Office module calls its API, never its tables (ARCHITECTURE.md, "Server bounded contexts", Dependency rule 9; SECURITY.md, Secret handling rule 10 keeps schemas cross-context-inaccessible anyway); requests bind to dedicated DTOs with explicit source attributes, server-controlled fields (actor, timestamps, amounts) set from server state only (SECURITY.md, Input validation rule 3); order search uses parameterized queries with allowlisted sort/filter columns (SECURITY.md, Input validation rule 4); authenticated responses are `Cache-Control: no-store` (SECURITY.md, HTTP boundary rule 3).
- **Anti-Patterns**: MUST NOT accept client-supplied monetary values or order state (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2); MUST NOT mutate order or audit rows directly from the dashboard module (ARCHITECTURE.md, Dependency rule 6); MUST NOT allow refunds on session trust alone — step-up is mandatory (SECURITY.md, Authentication rule 2); MUST NOT surface internal state machine errors or processor responses raw to the UI (SECURITY.md, Logging rules 1 and 7); MUST NOT use floating point for refund amounts, including in tests (CC-PRC-003).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Which roles hold which order-management permissions (in particular, who may issue refunds — finance vs. ops-agent vs. admin) is not specified; it is defined by the issue 080 role–permission matrix, which itself needs human approval.
- Refund granularity is unspecified: the specs define `refunded` as a terminal order state (CC-ORD-006) but do not address partial refunds or per-line-item refunds; no partial-refund behavior is asserted here.
- Search capabilities (searchable fields, cross-market scope of a staff member's search) are not enumerated in the specs beyond "order management (search, ...)" (CC-DSH-003).
- Whether a dashboard-initiated `cancelled` transition triggers an automatic refund, or refund and cancellation are always separate staff actions, is not specified.
