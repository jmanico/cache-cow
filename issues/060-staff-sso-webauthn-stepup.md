# 060 · Staff SSO with mandatory WebAuthn and step-up re-authentication

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-DSH-001
- **Title**: Staff SSO with mandatory WebAuthn and step-up re-authentication
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Staff and admin authentication to the internal dashboard MUST be SSO via Microsoft Entra ID with mandatory FIDO2 WebAuthn passkeys — `userVerification='required'` in every registration and authentication ceremony, server rejecting any assertion whose authenticator-data UV flag is unset — with session lifetime capped at 12 hours and re-authentication required for sensitive actions (refunds, employee-record access, role changes) (CC-DSH-001; SECURITY.md, Authentication and authorization rule 2).
- **Rationale**: The dashboard exposes order management with refunds, invoices, partner management, and employee records including compensation (CC-DSH-003/005) — the platform's highest-value internal surface. Phishing-resistant passkeys bound to the relying-party ID defeat credential phishing against staff; requiring user verification (not merely `'preferred'`) ensures possession of the authenticator alone is insufficient; bounded sessions and step-up re-auth limit the blast radius of a hijacked staff session. Identity provider confirmed 2026-07-15: Microsoft Entra ID for staff SSO (ARCHITECTURE.md, Authentication model and decision record).
- **Design**: Dashboard shell is Pit-themed per DESIGN.md §12; auth error states follow DESIGN.md §9 (no mascots or humor in error states, per §5.4 zero puns near money movement).

## Scope
- **Applies To**: Both (dashboard web app; Back Office and Identity & Access server enforcement)
- **Components**: Internal operations dashboard (Angular, separate origin); Identity & Access bounded context; Back Office bounded context (sensitive-action endpoints); Microsoft Entra ID tenant
- **Actors**: Internal staff (sales-viewer, ops-agent, finance, hr-admin, admin roles per CC-DSH-002), Microsoft Entra ID (identity provider)
- **Data Classification**: Restricted/PII (employee records, compensation) and Regulated (financial actions under 7-year audit retention, CC-DSH-004)

## Security Context
- **Defense Layer**: Architecture (phishing-resistant SSO at the dashboard trust boundary)
- **Threat(s) Addressed**: Staff credential phishing (OWASP Top Ten A07:2021; passkeys are origin-bound, defeating relying-party spoofing); session hijack persistence (bounded 12-hour lifetime); privilege abuse via unattended/stolen sessions (step-up re-auth); STRIDE Spoofing and Elevation of Privilege
- **Trust Boundary**: The dashboard's separate origin on a VPN-restricted private network path (SECURITY.md, HTTP boundary rule 8; CC-SEC-011) — authentication is the identity gate at that boundary; network restriction is defense in depth, not a substitute.
- **Zero Trust Consideration**: VPN presence confers no identity: every dashboard request requires an authenticated Entra ID session under the deny-by-default fallback policy (SECURITY.md, Authentication rule 1), and sensitive actions independently re-verify the user rather than trusting the standing session.

## Standards Alignment
- **OWASP ASVS**: V6 Authentication (MFA, cryptographic authenticators, re-authentication for sensitive operations); V7 Session Management (bounded lifetime), under the ASVS 5.0 Level 2 baseline
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: IA-2 (identification and authentication, incl. MFA for privileged accounts), AC-11 (session controls), AC-6 (least privilege, with CC-DSH-002)
- **NIST SP 800-207**: Per-request access decisions; no trust from network location (VPN ≠ authentication)
- **Regulatory**: Financial actions feed the 7-year audit trail (CC-DSH-004); employee-record access restrictions per CC-DSH-005
- **Other**: FIDO2/WebAuthn (FIDO Alliance passkeys guidance, SECURITY.md References)

## Acceptance Criteria
1. **AC-01**: Given any dashboard route or Back Office endpoint, when an unauthenticated request arrives, then it is denied by the fallback authorization policy — no anonymous dashboard surface exists (SECURITY.md, Authentication rules 1–2; CC-DSH-001).
2. **AC-02**: Given a staff sign-in, when authentication completes, then it was performed via Entra ID SSO with a FIDO2 WebAuthn passkey; no password-only or SMS-based path exists for staff (CC-DSH-001; SECURITY.md, Authentication rules 2–3).
3. **AC-03**: Given any WebAuthn registration or authentication ceremony for staff, when options are generated, then `userVerification` is `'required'` — never `'preferred'` (SECURITY.md, Authentication rule 2).
4. **AC-04**: Given a WebAuthn assertion whose authenticator-data UV flag is unset, when the server validates it, then the assertion is rejected and the failure is logged as a security event (SECURITY.md, Authentication rule 2; Logging rule 3).
5. **AC-05**: Given an authenticated staff session, when 12 hours elapse from establishment, then the session is invalid and the user must fully re-authenticate (CC-DSH-001).
6. **AC-06**: Given an authenticated staff session, when the user initiates a refund, accesses an employee record, or changes a role, then a fresh re-authentication is required before the action executes; performing the action on the standing session alone MUST NOT be possible (SECURITY.md, Authentication rule 2; CC-DSH-001).
7. **AC-07**: Given any sensitive action completes after step-up, when the audit log is inspected, then the privileged action is recorded per CC-DSH-004 (actor, action, object, before/after, timestamp; SECURITY.md, Logging rule 6).
8. **AC-08**: Given the storefront or wholesale portal, when their session scopes are inspected, then no cookie, token, or module is shared with the dashboard origin (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md Dependency rule 4).

## Failure Behavior
- **On Invalid Input**: Invalid assertions, expired sessions, or failed step-up return 401 (or 403 where an authenticated identity lacks the step-up) with RFC 9457 generic bodies and correlation IDs; no internal detail disclosed (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — any exception in authentication, session validation, or step-up evaluation is a denial (SECURITY.md, Logging rule 2).
- **Alerting**: Authentication failure spikes and repeated UV-flag rejections alert via centralized monitoring (SECURITY.md, Logging rules 3 and 8).

## Test Strategy
- **Unit Tests**: WebAuthn option-generation (asserts `userVerification='required'` in every ceremony); assertion validation rejecting unset UV flags; session-lifetime clock logic; step-up policy evaluation for the three named sensitive-action classes. Tagged CC-DSH-001.
- **Integration Tests**: ASP.NET Core integration tests with a test Entra ID tenant / WebAuthn emulator: full SSO sign-in; session expiry at 12 hours; refund/employee-record/role-change endpoints blocked without fresh re-auth and permitted after it; audit event written on each sensitive action.
- **Security Tests**: AuthZ suite attempts sensitive actions with stale sessions and without step-up and MUST fail closed (CC-QA-005); assertion-tampering tests (UV flag cleared) rejected; DAST against staging dashboard (CC-QA-007).
- **Compliance Tests**: Automated evidence that every step-up-gated action produced an audit record (CC-DSH-004) and that security events reached centralized logging.
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-DSH-001 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 020 Deny-by-default authorization fallback policy; 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging; 079 Dashboard shell: separate origin, VPN-restricted (the surface this authenticates). External: Entra ID tenant with passkey policy.
- **Downstream**: 080 Dashboard RBAC: role-permission matrix and enforcement (CC-DSH-002 authorization layered on this authentication); 081 Append-only audit store (receives the audit events of AC-07); 082 Dashboard order management (refunds), 087 Dashboard employee management (record access) — both consume the step-up gate; 062 Object-level authorization and IDOR test suite.
- **External**: Microsoft Entra ID (confirmed staff IdP, ARCHITECTURE.md decision record 2026-07-15).

## Implementation Notes
- **Constraints**: ASP.NET Core with Entra ID SSO (OpenID Connect); enforce via named `[Authorize(Policy=...)]` policies over the deny-by-default fallback (SECURITY.md, Authentication rule 1); middleware ordering HTTPS/headers → static files → authentication → authorization (SECURITY.md, HTTP boundary rule 5); token validation per SECURITY.md Authentication rule 7 (all validators true, clock skew ≤ 2 minutes, pinned algorithms). Passkeys bound to the dashboard's relying-party ID (its separate origin). Step-up should be modeled as a distinct, short-lived authorization state so it cannot be satisfied by the 12-hour session alone.
- **Anti-Patterns**: MUST NOT use `userVerification='preferred'` anywhere; MUST NOT accept assertions with UV unset; MUST NOT offer password-only or SMS-based factors for staff; MUST NOT treat VPN reachability as authentication; MUST NOT share cookies, tokens, or modules between dashboard and storefront/portal (SECURITY.md, HTTP boundary rule 8); MUST NOT log tokens or assertions (SECURITY.md, Logging rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret-scan, tests green, coverage, lint — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The maximum age of a step-up re-authentication (how long a fresh re-auth remains valid for subsequent sensitive actions) is not specified in CC-DSH-001 or SECURITY.md Authentication rule 2; a concrete window needs a human decision (excluded from acceptance criteria).
- Whether "re-auth for sensitive actions" must itself be a full WebAuthn ceremony (vs. any Entra ID re-authentication) is not stated explicitly; the passkey-mandatory posture suggests WebAuthn, but this is not assumed in the criteria.
- The complete list of "sensitive actions" beyond the three named (refunds, employee-record access, role changes) is not enumerated in the specs.
