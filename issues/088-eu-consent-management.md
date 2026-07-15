# 088 · EU consent management

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CMP-001
- **Title**: EU consent management
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The platform MUST implement GDPR consent management for non-essential cookies and analytics for the ES and DE markets and for EU visitors, with no dark patterns and rejection exactly as easy as acceptance, and MUST produce the lawful-basis mapping and processor DPA documentation named in CC-CMP-001 (REQUIREMENTS.md CC-CMP-001).
- **Rationale**: GDPR applies to ES/DE and EU visitors (REQUIREMENTS.md CC-CMP-001). Non-essential cookies and analytics require consent under a lawful basis; the requirement explicitly prohibits dark patterns and mandates "reject as easy as accept". The same requirement names lawful-basis mapping and DPAs with processors as obligations, so this issue carries those documentation deliverables alongside the consent UI/enforcement.
- **Design**: Consent UI follows DESIGN.md §9 (plain verbs, sentence case, active voice; no humor in legal content per DESIGN.md §5.4) and DESIGN.md §13 (WCAG 2.2 AA, keyboard operability, visible focus). Rendered in the active locale per REQUIREMENTS.md CC-I18N-001/002. Legal pages that describe cookie use are per CC-CNT-005 (issue 077).

## Scope
- **Applies To**: Web App
- **Components**: Consumer storefront (Angular SSR), wholesale portal (browser surface for EU partners), Content & Localization context (consent strings), observability/analytics initialization (client-side)
- **Actors**: Anonymous visitor, authenticated consumer, wholesale portal buyer — any browser session attributable to ES/DE markets or EU visitors
- **Data Classification**: Restricted/PII (consent records are personal data; consent gates collection of behavioral/analytics PII)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Unlawful processing of personal data without lawful basis (GDPR exposure); over-collection of behavioral data contrary to data minimization (REQUIREMENTS.md CC-CMP-003)
- **Trust Boundary**: Client-server edge — consent state is captured client-side but the record of consent and the decision to load non-essential collection is enforced from server-verifiable stored consent, not from a client-forgeable signal alone (consistent with SECURITY.md, Input validation rule 1: every cookie/input crossing the boundary is attacker-controlled)
- **Zero Trust Consideration**: The consent cookie/state is untrusted input; it is validated against the stored consent record before non-essential scripts or analytics beacons are emitted in server-rendered responses. Absence or invalidity of consent state is treated as "no consent" (fail closed).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies platform-wide (SECURITY.md, Baseline); V1 Encoding and Sanitization / V3 Web Frontend Security govern the consent banner's construction (CSP-compatible, no inline handlers, per SECURITY.md HTTP boundary rule 2)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (enforcement of the consent decision before collection occurs)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR (REQUIREMENTS.md CC-CMP-001): consent for non-essential cookies/analytics, lawful-basis mapping, DPAs with processors; no dark patterns; reject as easy as accept
- **Other**: N/A

## Acceptance Criteria

1. **AC-01**: Given a first-time visitor whose transacting market is ES or DE or who is an EU visitor, when any page renders, then no non-essential cookie is set and no analytics/tracking beacon fires before an explicit consent choice is recorded (CC-CMP-001).
2. **AC-02**: Given the consent prompt is displayed, when the user inspects it, then "Reject all" is available at the same interaction depth, size, and prominence as "Accept all" — one click/tap each, no pre-ticked non-essential categories, no color/contrast asymmetry that disadvantages rejection (CC-CMP-001, no dark patterns).
3. **AC-03**: Given a user rejects non-essential cookies, when they continue browsing across pages and sessions, then the rejection persists, no non-essential collection occurs, and the prompt is not re-shown to badger the user into accepting (CC-CMP-001).
4. **AC-04**: Given a user has consented, when they later withdraw consent via a persistently reachable control, then non-essential collection stops for subsequent requests and the withdrawal is recorded with a timestamp (CC-CMP-001).
5. **AC-05**: Given consent records exist, when a compliance review requests evidence, then each record shows what was consented to, the policy version, and when — retained per the retention schedule (CC-CMP-003, issue 090).
6. **AC-06**: Given a request whose consent cookie is absent, malformed, or fails validation, when the server renders the page, then the system behaves as if no consent was given — non-essential collection MUST NOT occur (negative case; SECURITY.md, Logging rule 2 fail-closed analogue).
7. **AC-07**: Given the documentation deliverables, when the issue closes, then a lawful-basis mapping (per processing activity) and DPA status records for each confirmed processor (Stripe, Razorpay, EasyPost, Contentful, Microsoft Azure/Entra, Azure Communication Services) exist in the repository, versioned (CC-CMP-001; processor list per REQUIREMENTS.md CC-CMP-006).
8. **AC-08**: Given any launch locale, when the consent UI renders, then all strings come from ICU MessageFormat resources with key parity across locales (CC-I18N-002) and the UI passes WCAG 2.2 AA checks (CC-NFR-004, DESIGN.md §13).

## Failure Behavior
- **On Invalid Input**: Malformed consent state is discarded and treated as "no consent recorded"; the request proceeds with essential-only behavior. Consent-write endpoints validate against an explicit schema and reject invalid bodies with HTTP 400 RFC 9457 problem details, logged with correlation ID and no internal state disclosure (SECURITY.md, Input validation rules 1–3; Logging rule 1).
- **On System Error**: Fail closed — if the consent service or record store is unavailable, render with non-essential collection disabled (SECURITY.md, Logging rule 2).
- **Alerting**: Structured security/compliance logs for consent-record write failures; alert on sustained consent-store write failure via centralized monitoring (SECURITY.md, Logging rule 3; Azure Monitor per ARCHITECTURE.md, Cross-cutting Observability).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit tests for consent-state validation, market/EU-visitor applicability resolution (keyed off server-side transacting-market state, CC-SEC-012), and consent-record persistence logic. Angular component tests for the banner: symmetric accept/reject affordances, no pre-ticked categories, withdrawal control reachability.
- **Integration Tests**: ASP.NET Core integration tests asserting SSR responses for ES/DE/EU sessions contain no non-essential script/beacon markup pre-consent and post-rejection; consent persistence across sessions (CC-MKT-002-style persistence).
- **Security Tests**: Forged/malformed consent-cookie fuzzing asserts essential-only behavior; CI grep gate confirms no raw-HTML sinks in the banner (SECURITY.md, Input validation rule 5); CSP compatibility (no inline handlers) verified (SECURITY.md, HTTP boundary rule 2).
- **Compliance Tests**: Automated evidence: presence and schema-validity of the lawful-basis mapping and DPA records in the repo; locale key-parity CI for consent strings (CC-QA-006); automated accessibility checks (CC-NFR-004).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-CMP-001 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 004 Angular workspace scaffold; 024 Transacting market/locale resolution (issue 024 — determines ES/DE/EU applicability server-side); 063 Storefront SSR shell; 064 ICU MessageFormat resource pipeline; 015 PostgreSQL Flexible Server (consent record storage). CC-MKT-002, CC-SEC-012, CC-I18N-002.
- **Downstream**: 092 Marketing email consent and one-click unsubscribe (CC-CMP-005 builds on the consent model); 095 OpenTelemetry observability to Azure Monitor and 097 Real-user monitoring (client-side analytics/RUM collection for EU visitors is gated by this consent); 089 Data-subject rights endpoints (consent records are in scope for access/deletion).
- **External**: Processors named for DPA documentation: Stripe, Razorpay, EasyPost, Contentful, Microsoft Azure/Entra, Azure Communication Services (ARCHITECTURE.md, Technology decisions; REQUIREMENTS.md CC-CMP-006).

## Implementation Notes
- **Constraints**: Consent state must be honored during Angular SSR (`@angular/ssr`) — the server-rendered HTML for a non-consented EU session must not include analytics bootstrap; SSR transfer state carries only data gated for that response (SECURITY.md, HTTP boundary rule 10). Consent-state-dependent responses must respect cache discipline: personalized responses are `Cache-Control: no-store` and never edge-cached (CC-MKT-009, CC-SEC-013). Consent records live in the owning context's PostgreSQL schema with its own least-privilege role over TLS (SECURITY.md, Secret handling rule 10). Consent cookies follow SECURITY.md, Authentication rule 11 cookie attributes where applicable.
- **Anti-Patterns**: MUST NOT use dark patterns — no pre-ticked boxes, no asymmetric prominence, no reject flow longer than the accept flow (CC-CMP-001). MUST NOT key EU applicability off client hints like `Accept-Language` or client-supplied geolocation alone — gating decisions key off server-side transacting-market state; IP geolocation only proposes (SECURITY.md, Authentication rule 10; CC-SEC-012). MUST NOT load third-party runtime CDNs for a consent-management script (SECURITY.md, Deployment rule 10). MUST NOT treat client-side hiding of trackers as compliance — enforcement is server-side (analogous to CC-MKT-003's server-side rule).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must reference CC-CMP-001 (REQUIREMENTS.md §17).

## Open Questions
- CC-CMP-001 does not specify a consent-management vendor or whether the CMP is first-party; no CMP tool is named in ARCHITECTURE.md's confirmed stack, so this issue assumes first-party implementation — confirm.
- The specs do not define which cookie/analytics categories are "essential" vs "non-essential" for Cache Cow (e.g., whether the RUM required by CC-NFR-002 is consent-gated in the EU). A human-approved category inventory is needed.
- Retention period for consent records is not stated per data class; it must be added to the CC-CMP-003 retention schedule (issue 090).
- Whether the consent prompt applies to EU visitors browsing non-EU markets (the "EU visitors" clause of CC-CMP-001 suggests yes, but the server-side signal for "EU visitor" beyond transacting market is unspecified).
