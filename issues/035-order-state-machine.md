# 035 · Order state machine with audited transitions

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-006
- **Title**: Order state machine with audited transitions
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The order state machine MUST be exactly `received -> confirmed -> packed -> shipped -> delivered`, with `cancelled` and `refunded` as terminal branches, and every transition MUST be appended to the append-only audit log (REQUIREMENTS.md CC-ORD-006; SECURITY.md, Logging rule 6).
- **Rationale**: Orders are a money path: an immutable, complete transition history is what makes refunds, disputes, and financial audit defensible, and it feeds the 7-year financial audit retention (REQUIREMENTS.md CC-DSH-004). SECURITY.md, Logging rule 6 requires every order state transition written to the append-only audit store (actor, action, object, before/after, timestamp) so that no single compromised credential can both act and erase history (CC-SEC-020). ARCHITECTURE.md, Dependency rule 6 fixes audit as an append-only sink: corrections are new records, never mutations.
- **Design**: N/A for this server-side issue. The consumer-facing five-stage presentation mapped from this machine (DESIGN.md §7, "Order tracker"; REQUIREMENTS.md CC-ORD-008) is issue 070; dashboard state-transition UI is issue 082.

## Scope
- **Applies To**: Both (consumed by storefront order flows and the B2B API; enforced in the Ordering & Payments bounded context)
- **Components**: Ordering & Payments bounded context (ARCHITECTURE.md, "Server bounded contexts" #4); audit-event emission toward the append-only audit store (store itself is issue 081 — this issue covers only the order-side emission)
- **Actors**: Order service (system transitions), internal dashboard staff (ops-agent/admin transitions per CC-DSH-002/003), signature-verified processor webhooks (payment-driven transitions, issue 041)
- **Data Classification**: Confidential (order data; audit events are financial records under CC-DSH-004 retention)

## Security Context
- **Defense Layer**: Architecture (state-machine invariants enforced server-side; append-only audit sink)
- **Threat(s) Addressed**: Repudiation and Tampering (STRIDE) — unaudited or out-of-order state changes; illegitimate order advancement (relates to the forged-payment path closed by CC-ORD-009/issue 041); CWE-778 (insufficient logging)
- **Trust Boundary**: Internal module boundary of the Ordering & Payments context; all transition triggers arrive from authenticated/verified callers (dashboard RBAC, verified webhooks) — the state machine itself never trusts a client-supplied state value
- **Zero Trust Consideration**: Transition requests are validated against the allowed-transition table regardless of caller; requested target states from any input are treated as untrusted and rejected unless the transition is legal from the current persisted state. Any exception in the transition/audit path is a denial (SECURITY.md, Logging rule 2).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V16 (Security Logging and Error Handling); ASVS 5.0 V2 (Validation and Business Logic — business-logic sequence enforcement)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-2 (event logging), AU-9 (protection of audit information)
- **NIST SP 800-207**: N/A
- **Regulatory**: Audit events on financial actions retained 7 years (REQUIREMENTS.md CC-DSH-004, ratified 2026-07-15)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given an order in any state, when a transition is requested, then it succeeds only if it is one of: `received->confirmed`, `confirmed->packed`, `packed->shipped`, `shipped->delivered`, or a legal branch to `cancelled` or `refunded`; every other requested transition is rejected without mutating the order (CC-ORD-006).
2. **AC-02**: Given a successful transition, when it commits, then exactly one audit event is durably appended containing actor, action, object (order ID), before/after state, and timestamp (SECURITY.md, Logging rule 6; CC-ORD-006).
3. **AC-03**: Given the audit append fails (store unavailable, insert rejected), when a transition is attempted, then the state change does NOT commit — transition and audit event persist atomically, so no transition can exist without its audit record (fail closed; SECURITY.md, Logging rule 2).
4. **AC-04**: Given an order in `cancelled` or `refunded`, when any further transition is requested, then it is rejected — both states are terminal (CC-ORD-006).
5. **AC-05**: Given a transition request that skips a state (e.g., `received->shipped`) or moves backward (e.g., `shipped->packed`), then it MUST NOT be applied, and the rejection is logged as a structured security-relevant validation event (SECURITY.md, Logging rule 3).
6. **AC-06**: Given any code path in the Ordering context, when it changes order state, then it does so only through the single state-machine transition API — no direct writes to the state column exist (verified by tests and review; ARCHITECTURE.md bounded-context ownership).
7. **AC-07**: Given emitted audit events, when inspected, then no credentials, tokens, or PANs appear and PII is redacted per SECURITY.md, Logging rule 4 (negative case).

## Failure Behavior
- **On Invalid Input**: Illegal transition requests are rejected with RFC 9457 problem details (issue 021) — HTTP 409 for a state-conflict on an API surface — logged with correlation ID, no internal state disclosed (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception during transition validation or audit emission denies the transition; the order remains in its prior persisted state (SECURITY.md, Logging rule 2).
- **Alerting**: Audit-append failures and repeated illegal-transition attempts alert through centralized monitoring (SECURITY.md, Logging rule 3; Azure Monitor per ARCHITECTURE.md, "Observability").

## Test Strategy
- **Unit Tests**: Full transition-table coverage — every (state, requested-state) pair asserted allow/deny; terminal-state enforcement; audit-event payload shape (actor/action/object/before-after/timestamp). xUnit on .NET 10.
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL asserting transition + audit append commit atomically and that a simulated audit-insert failure rolls back the transition.
- **Security Tests**: Tests asserting no audit event contains tokens/PII (Logging rule 4); attempt state mutation via any non-state-machine path fails. Mutation testing SHOULD run on this state-machine code (CC-QA-001 — money/gating/authz mutation-testing scope; suite wiring in issue 099).
- **Compliance Tests**: Automated evidence that every transition in a test run has a matching audit record (supports CC-DSH-004 audit completeness).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged `CC-ORD-006` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 (Solution scaffold — bounded contexts), 015 (PostgreSQL per-context schemas/roles), 081 (Append-only audit store with WORM retention — this issue emits into it; INSERT-only enforcement lives there per SECURITY.md, Logging rule 6), 021 (RFC 9457 error handling), 022 (structured security logging)
- **Downstream**: 036 (Order submission — creates orders in `received`), 041 (Inbound processor webhook verification — the only payment authority that advances state, CC-ORD-009), 043 (Transactional order emails triggered by transitions), 044 (cold-store routing), 070 (Order tracking UI — five-stage mapping, CC-ORD-008), 082 (Dashboard order management — staff transitions/refunds)
- **External**: None directly (processor webhooks arrive via issue 041)

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith; order state and audit events in the Ordering context's own PostgreSQL schema, connecting with its least-privilege role over TLS (SECURITY.md, Secret handling rule 10). Application role holds INSERT-only rights on audit tables — no UPDATE/DELETE (SECURITY.md, Logging rule 6; enforced in issue 081). Transition + audit append in one database transaction. Order/payment state advances only on signature-verified processor webhooks reconciled with a server-initiated confirmation call — never a client redirect (ARCHITECTURE.md, "Server bounded contexts" #4; CC-ORD-009 — enforced in issue 041, but the transition API here must expose no trigger reachable from unverified client input).
- **Anti-Patterns**: MUST NOT mutate or delete audit records — corrections are new records (ARCHITECTURE.md, Dependency rule 6). MUST NOT accept a client-supplied target state as authoritative. MUST NOT emit audit events asynchronously in a way that lets a transition commit unaudited. MUST NOT log via string interpolation (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, ≥ 80% coverage, lint, SAST/SCA/secret-scan clean — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs fix the linear path and name `cancelled` and `refunded` as terminal branches, but do not enumerate from which source states each branch is legal (e.g., may a `shipped` order be `cancelled`? may `refunded` be reached only after `delivered`, or from any post-payment state?). The allowed-transition table for the branches needs a human decision before implementation.
- Which roles/actors may trigger which transitions (beyond: webhook-verified payment events advance payment-dependent state, and refunds are a dashboard re-auth-gated sensitive action per SECURITY.md, Authentication rule 2) is not fully specified; the role–permission matrix is issue 080's scope but the per-transition mapping is not in the specs.
