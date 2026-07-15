# 058 · Consumer authentication: passkeys and email-code via Entra External ID

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-005
- **Title**: Consumer authentication: passkeys and email-code via Entra External ID
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The consumer storefront MUST offer optional account authentication via WebAuthn passkeys and email-code login through Microsoft Entra External ID, MUST NOT offer SMS-based MFA or weaker recovery flows on any surface, and any password option offered MUST conform to OWASP ASVS 5.0 V6 (8–64+ character length, breached-password screening, no composition rules, no rotation) (SECURITY.md, Authentication and authorization rules 3–4; CC-SEC-005).
- **Rationale**: Consumer accounts are optional (guest checkout is supported per CC-ORD-001), but where offered they hold order history, addresses, and invoice access. SMS-based MFA is phishable and SIM-swap-vulnerable and is prohibited platform-wide (SECURITY.md, Authentication rule 3); weaker recovery flows (SMS codes, security questions) reintroduce phishable credentials and are prohibited (rule 4). Passkeys provide phishing-resistant authentication consistent with the platform's ASVS 5.0 Level 2 baseline. Identity provider is confirmed: Microsoft Entra External ID for consumers (ARCHITECTURE.md, Authentication model; decision record 2026-07-15).
- **Design**: Storefront auth surfaces follow the consumer design language — plain verbs, sentence case, no puns in error recovery (DESIGN.md §5.4, §9); all UI strings externalized per CC-I18N-002 across the seven launch locales.

## Scope
- **Applies To**: Both (storefront web app; Identity & Access bounded context server-side)
- **Components**: Identity & Access bounded context (ARCHITECTURE.md, Server bounded contexts item 9); consumer storefront Angular SSR client; Microsoft Entra External ID tenant configuration
- **Actors**: Consumer (anonymous/guest, account holder), Microsoft Entra External ID (identity provider)
- **Data Classification**: Restricted/PII (account identity, email addresses, credentials)

## Security Context
- **Defense Layer**: Architecture (identity delegation to Entra External ID; phishing-resistant credentials)
- **Threat(s) Addressed**: Credential phishing and credential stuffing (OWASP Top Ten A07:2021 Identification and Authentication Failures); SIM-swap account takeover (excluded by prohibiting SMS); recovery-flow downgrade attacks (CWE-640 Weak Password Recovery Mechanism)
- **Trust Boundary**: Client–server edge between the consumer browser and the Identity & Access context / Entra External ID; the platform trusts only assertions issued by the confirmed identity provider.
- **Zero Trust Consideration**: No client-supplied identity claim is trusted; authentication state derives solely from Entra External ID-issued assertions validated server-side. Sign-in from an unrecognized device is treated as a step-up event (SECURITY.md, Authentication rule 4).

## Standards Alignment
- **OWASP ASVS**: V6 Authentication (password policy: 8–64+ length, breached-password screening, no composition rules, no rotation), under the platform-wide ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: IA-2 (identification and authentication), IA-5 (authenticator management)
- **NIST SP 800-207**: Per-session, per-request authentication decisions; no implicit trust from network location
- **Regulatory**: GDPR (CC-CMP-001) applies to account data for ES/DE and EU visitors; DPDP/APPI/CCPA per CC-CMP-002
- **Other**: FIDO2/WebAuthn (FIDO Alliance passkeys guidance, SECURITY.md References)

## Acceptance Criteria
1. **AC-01**: Given a consumer without an account, when they check out, then checkout completes as a guest with no forced account creation (CC-ORD-001; accounts are optional per SECURITY.md Authentication rule 3).
2. **AC-02**: Given a consumer creating or signing into an account, when they choose a credential, then WebAuthn passkey registration/authentication and email-code login are both available, brokered through Microsoft Entra External ID (CC-SEC-005; ARCHITECTURE.md, Authentication model).
3. **AC-03**: Given any authentication or recovery surface for consumers, when the available factors are enumerated (UI and IdP configuration), then SMS-based MFA, SMS recovery codes, and security questions are absent — they MUST NOT be offered (SECURITY.md, Authentication rules 3–4).
4. **AC-04**: Given passwords are offered as a credential, when a consumer sets or changes a password, then the policy enforces 8–64+ length, screens against breached-password data, imposes no composition rules, and imposes no periodic rotation (SECURITY.md, Authentication rule 3; ASVS 5.0 V6).
5. **AC-05**: Given an account holder, when they register credentials, then they can register multiple passkeys including a hardware security key usable as recovery (SECURITY.md, Authentication rule 4).
6. **AC-06**: Given a sign-in attempt from an unrecognized device, when authentication proceeds, then the flow treats it as a step-up event rather than granting a session on the primary factor alone (SECURITY.md, Authentication rule 4).
7. **AC-07**: Given an authenticated consumer session is established, then it is protected by the session-cookie and CSRF policy of issue 061 (CC-SEC-006), and authentication successes/failures are logged as structured security events (SECURITY.md, Logging rule 3).
8. **AC-08**: Given any endpoint in the Identity & Access context, when no explicit authorization opt-out exists, then the deny-by-default fallback policy applies (SECURITY.md, Authentication rule 1; issue 020).

## Failure Behavior
- **On Invalid Input**: Authentication failures return a generic error (RFC 9457 ProblemDetails, HTTP 401 where a challenge is appropriate) with a correlation ID and no indication whether the account exists; malformed requests are rejected 400 (SECURITY.md, Logging rule 1; Input validation rule 1).
- **On System Error**: Fail closed — any exception in the authentication path is a denial, never a bypass (SECURITY.md, Logging rule 2).
- **Alerting**: Authentication failure spikes alert to centralized monitoring (SECURITY.md, Logging rules 3 and 8); Azure Monitor / Application Insights per ARCHITECTURE.md Observability.

## Test Strategy
- **Unit Tests**: Token/assertion validation logic in the Identity & Access context; password-policy validators (length bounds, breached-password screening hook, absence of composition/rotation rules); step-up decision logic for unrecognized devices. Tagged CC-SEC-005.
- **Integration Tests**: ASP.NET Core integration tests against the Entra External ID flows (test tenant): passkey registration/sign-in, email-code sign-in, multi-passkey registration including a security key; assert SMS/security-question factors are not configured or reachable.
- **Security Tests**: AuthZ suite confirms unauthenticated access to account resources is denied (CC-QA-005); DAST against staging covers the auth surfaces (CC-QA-007); SAST/secret-scan gates per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Automated check of the Entra External ID tenant policy (allowed factors) as configuration evidence; structured auth-event log presence (SECURITY.md, Logging rule 3).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged with CC-SEC-005 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 020 Deny-by-default authorization fallback policy; 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging; 016 TLS/HSTS enforcement. External Entra External ID tenant availability.
- **Downstream**: 059 Email OTP hardening (the email-code factor's hardening detail); 061 Session cookie policy and CSRF protection; 062 Object-level authorization and IDOR test suite; 089 Data-subject rights endpoints (identity verification); 042 Guest order capability tokens (the non-account access path).
- **External**: Microsoft Entra External ID (confirmed consumer IdP, ARCHITECTURE.md decision record 2026-07-15); Azure Communication Services for email-code delivery (ARCHITECTURE.md, Technology decisions).

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith; authentication brokered by Entra External ID — do not build a first-party credential store. JWT/assertion validation per SECURITY.md Authentication rule 7 (ValidateIssuer/Audience/Lifetime/IssuerSigningKey all true, clock skew ≤ 2 minutes, pinned ValidAlgorithms). Angular client is CSP-compatible by construction (SECURITY.md, HTTP boundary rule 2). All secrets (client credentials for the IdP integration) live in Key Vault via Workload Identity (SECURITY.md, Secret handling rules 1–4).
- **Anti-Patterns**: MUST NOT offer SMS-based MFA on any surface; MUST NOT implement SMS or security-question recovery; MUST NOT impose password composition rules or rotation; MUST NOT log credentials, tokens, or OTP codes (SECURITY.md, Logging rule 4); MUST NOT roll a custom WebAuthn/password implementation parallel to the confirmed IdP.
- **AI Development Guidance**: AI-generated code passes the identical merge gates — SAST/SCA/secret-scan, tests green, coverage, lint — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs say passwords, "if offered", follow ASVS 5.0 V6 (SECURITY.md, Authentication rule 3) — whether the consumer surface actually offers passwords at launch (vs. passkeys + email-code only) is not decided in the canonical docs. AC-04 applies only if passwords are enabled; the go/no-go is a product decision to surface, not resolve here.
- Whether the WebAuthn `userVerification='required'` mandate written for staff ceremonies (SECURITY.md, Authentication rule 2) also binds consumer passkey ceremonies is not stated; consumer rules 3–4 are silent on UV. Not assumed either way.
- The concrete step-up mechanism for an unrecognized consumer device (which second factor is demanded) is not specified beyond "treat as a step-up event".
