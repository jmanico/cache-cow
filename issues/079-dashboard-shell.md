# 079 · Dashboard shell: separate origin, VPN-restricted, Pit theme

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-011 (shell context: CC-DSH-001, CC-DSH-003)
- **Title**: Dashboard shell: separate origin, VPN-restricted, Pit theme
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: The internal operations dashboard MUST run on a separate origin (or isolated subdomain with distinct session scope) from the consumer storefront, reachable only over a VPN-restricted private network path, sharing no cookies, tokens, or modules with the storefront or wholesale portal, and rendered in the Pit theme defined in DESIGN.md §12.
- **Rationale**: The dashboard exposes privileged operations (orders, refunds, invoices, employee records — CC-DSH-003) and therefore must be isolated from the public consumer surfaces at the origin, session, network, and code-dependency levels (CC-SEC-011; SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4). ARCHITECTURE.md "Technology decisions" confirms public ingress is limited to the storefront, portal, and API gateways — the dashboard is not public ingress. The network-restriction model was confirmed 2026-07-15: VPN required, plus SSO with mandatory passkeys (ARCHITECTURE.md, "Decision record (2026-07-15)"; CC-SEC-011).
- **Design**: DESIGN.md §12 — Pit theme: `color.pit.950` background, `color.pitpaper.100` primary text, cache-green for good states, Ember for alerts, Smoke for neutral; Archivo for UI, IBM Plex Mono for every number; compact density by default (40px rows), sticky headers, keyboard-first filtering. Contrast pair per DESIGN.md §3.2: Cache green on Pit 6.7:1 (passes AA). WCAG 2.2 AA applies to the dashboard (DESIGN.md §13, CC-NFR-004).

## Scope
- **Applies To**: Web App
- **Components**: Angular internal-dashboard application shell (from the issue 004 workspace): app chrome, navigation, theming, session integration point; dashboard hosting/ingress configuration on the private network path (VPN-only); dashboard session cookie scope. Explicitly excluded: staff SSO/WebAuthn ceremonies (issue 060), session cookie/CSRF policy details (issue 061), RBAC (issue 080), the functional modules (issues 082–087), VPN infrastructure provisioning itself (Terraform/AKS issues 008–013).
- **Actors**: Internal staff (sales-viewer, ops-agent, finance, hr-admin, admin per CC-DSH-002); platform engineers.
- **Data Classification**: Restricted/PII (the dashboard surfaces orders, invoices, and employee data; the shell itself renders no data but gates access to surfaces that do)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Cross-origin session/token theft between consumer and admin surfaces; CSRF/XSS pivot from the public storefront into privileged admin functions (OWASP Top Ten A01:2021 Broken Access Control, A05:2021 Security Misconfiguration); exposure of the admin surface to the public internet (CC-SEC-011)
- **Trust Boundary**: The dashboard origin and its private network path — a distinct trust zone from the public storefront/portal origins (SECURITY.md, HTTP boundary rules 8–9)
- **Zero Trust Consideration**: Network restriction (VPN) is defense in depth, not the authentication control: every request still requires staff SSO with mandatory passkeys (issue 060; CC-DSH-001) and deny-by-default authorization (issue 020). No trust is inherited from storefront or portal sessions — the session scopes are disjoint by construction.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Session Management (distinct session scope per origin); ASVS 5.0 V1/V15 Secure Coding and Architecture (surface segregation) — platform baseline ASVS 5.0 Level 2 (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-7 (boundary protection — private network path, no public ingress); AC-17 (remote access via VPN); AC-3 (access enforcement)
- **NIST SP 800-207**: Network location is not the sole trust signal — VPN restriction composes with per-request SSO/passkey authentication rather than replacing it
- **Regulatory**: N/A
- **Other**: FIDO2/WebAuthn (staff authentication integrated from issue 060; ceremony requirements live there)

## Acceptance Criteria
1. **AC-01**: Given the deployed platform, when the dashboard application is served, then it is served from a separate origin (or isolated subdomain with distinct session scope) from the consumer storefront, and its session cookies are scoped so they are never sent to — and never accepted from — the storefront or portal origins (CC-SEC-011; SECURITY.md, HTTP boundary rule 8).
2. **AC-02**: Given a request to any dashboard route or static asset that does not arrive over the VPN-restricted private network path, when it reaches the platform edge, then it is refused at the network layer and no dashboard content, redirect, or error detail identifying the dashboard is returned (CC-SEC-011; ARCHITECTURE.md, "Technology decisions" — public ingress limited to storefront, portal, and API gateways).
3. **AC-03**: Given a valid storefront or portal session cookie or token, when it is presented to any dashboard endpoint, then it MUST NOT authenticate the request — dashboard sessions are established only via staff SSO with mandatory passkeys (issue 060) (negative case; CC-SEC-011, CC-DSH-001).
4. **AC-04**: Given the Angular workspace, when build-time dependency checks run, then the dashboard application imports no modules from the storefront or portal applications and neither imports dashboard modules; a violating import fails the build (ARCHITECTURE.md, Dependency rule 4; SECURITY.md, HTTP boundary rule 8).
5. **AC-05**: Given the dashboard shell UI, when rendered, then it uses the Pit theme exclusively via design tokens consumed from the generated `tokens.json` — `pit.950` background, `pitpaper.100` text, cache-green/Ember/Smoke status accents, Archivo UI type, Plex Mono numerals — with no hardcoded brand colors or type (DESIGN.md §12, §3.1; ARCHITECTURE.md, Dependency rule 8).
6. **AC-06**: Given the shell's table and list scaffolding, when rendered at default density, then rows are 40px, headers are sticky, and filtering is keyboard-first operable with visible focus (Cache-colored 2px outline on Pit) (DESIGN.md §12, §13).
7. **AC-07**: Given automated CI contrast checks (issue 005), when the dashboard token combinations are evaluated, then all shipped text/background pairs meet WCAG 2.2 AA, including Cache green on Pit at 6.7:1 (DESIGN.md §3.2, §13; CC-NFR-004).

## Failure Behavior
- **On Invalid Input**: N/A for the shell itself (no domain input surface); requests outside the VPN path are refused at the network layer with no application response.
- **On System Error**: Fail closed — any failure in session validation or the authentication handshake denies access (SECURITY.md, Logging rule 2); the dashboard never degrades to an unauthenticated or storefront-session-authenticated mode.
- **Alerting**: Authentication failures and authorization denials on dashboard endpoints are logged as structured security events with alerting on failure spikes (SECURITY.md, Logging rules 3 and 8; CC-SEC-010).

## Test Strategy
- **Unit Tests**: Angular component tests for the shell chrome: token-based theming (no literal color values), density/sticky-header behavior, keyboard operability of filtering controls.
- **Integration Tests**: ASP.NET Core integration tests asserting dashboard endpoints reject storefront/portal cookies and tokens (AC-03); cookie-scope assertions (AC-01); build-level dependency test failing on cross-surface imports (AC-04).
- **Security Tests**: Network-path verification in staging: dashboard hostname unreachable from the public internet (AC-02); DAST run against staging includes the dashboard origin from inside the VPN path only (CC-QA-007); CI grep gate confirms no raw-HTML sinks in the shell (SECURITY.md, Input validation rule 5).
- **Compliance Tests**: CI contrast-check evidence for Pit-theme token pairs (AC-07); CI evidence that Dependency rule 4 checks ran and passed.
- **Coverage Target**: ≥ 80% for the shell package (CC-QA-001); tests tagged CC-SEC-011, CC-DSH-001, CC-NFR-004 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 004 "Angular workspace scaffold: storefront (SSR), portal, dashboard with hardened builds"; 005 "Design token pipeline: tokens.json from DESIGN.md with CI contrast checks"; 016 "TLS/HSTS enforcement and security middleware ordering"; 017 "CSP and security headers"; 020 "Deny-by-default authorization fallback policy"; 060 "Staff SSO with mandatory WebAuthn and step-up re-authentication" (CC-DSH-001); 061 "Session cookie policy and CSRF protection".
- **Downstream**: 080 "Dashboard RBAC: role-permission matrix and enforcement"; 082 "Dashboard order management module"; 083 "Dashboard sales analytics module"; 084 "Dashboard inventory-by-cold-store module"; 085 "Dashboard partner management module"; 086 "Dashboard invoice management module"; 087 "Dashboard employee management".
- **External**: Microsoft Entra ID (staff SSO provider, per ARCHITECTURE.md "Authentication model"); Azure (AKS private networking per ARCHITECTURE.md "Technology decisions").

## Implementation Notes
- **Constraints**: Angular client built to the SECURITY.md client rules — CSP-compatible construction, no raw-HTML sinks, AOT, strict TypeScript/strictTemplates, no production source maps (SECURITY.md, Deployment rule 9); all theming consumed from generated `tokens.json` (ARCHITECTURE.md, Dependency rule 8); dashboard served over HTTPS with the full security-header set and `Cache-Control: no-store` on authenticated responses (SECURITY.md, HTTP boundary rules 1–3); fonts self-hosted, no third-party runtime CDNs (SECURITY.md, Deployment rule 10).
- **Anti-Patterns**: MUST NOT share cookies, tokens, or Angular modules between dashboard and storefront/portal (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4); MUST NOT treat VPN presence as authentication; MUST NOT expose the dashboard through the public gateways (ARCHITECTURE.md, "Technology decisions" — any exposure beyond the gateways is a security defect per SECURITY.md, HTTP boundary rule 9); MUST NOT hardcode Pit colors or typography (ARCHITECTURE.md, Dependency rule 8); no `bypassSecurityTrust*` or unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- SECURITY.md HTTP boundary rule 8 permits either a fully separate origin or an isolated subdomain with distinct session scope; the specs do not pick one. The choice (and the actual dashboard hostname) needs a human decision at implementation.
- The VPN technology/product is not named in the specs (only "VPN required" is ratified in the ARCHITECTURE.md decision record); provisioning presumably lands with the infrastructure issues but no spec names the mechanism.
- The specs do not state whether dashboard static assets are served from the same private path as the API or may use a separate (still private) asset host.
