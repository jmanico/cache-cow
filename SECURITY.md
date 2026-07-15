# Cache Cow: Security Rules (SECURITY.md)

Version 0.1 | Status: Provisional — pending the dedicated security architecture pass (ARCHITECTURE.md §Security expectations)

This repository is early-stage: it contains REQUIREMENTS.md, DESIGN.md, ARCHITECTURE.md, and logo assets only. No code exists yet. This file governs **all future code written in this repository, including AI-generated code** — AI-generated code passes the identical merge gates plus mandatory human review, with no auto-merge (CC-QA-002). Every rule below is grounded in the three project documents or in the distilled security reference set; unknowns are marked UNKNOWN or TO BE DECIDED, never guessed.

---

## Required Security Inputs

Inputs this file derives from:

- REQUIREMENTS.md v1.0 — especially §12 (CC-SEC-001–012), §8 (CC-API), §11 (CC-DSH), §15 (CC-QA)
- DESIGN.md v1.0 §14 — secure-by-design front-end defaults
- ARCHITECTURE.md v0.1 — confirmed stack, bounded contexts, dependency rules, known unknowns
- OWASP ASVS 5.0 Level 2 — platform-wide baseline (REQUIREMENTS.md §12)
- RFC 9700 (OAuth 2.0 Security BCP) — B2B API authorization (REQUIREMENTS.md §8)
- Distilled reference rule sets: dotnet-web, dotnet-api, dotnet-azure, angular-core, owasp-proactive, owasp-api, auth-deploy

Security inputs still missing (ARCHITECTURE.md "known unknowns"):

| Missing input | Status |
|---|---|
| Payment processor and per-market local methods | TO BE DECIDED |
| Identity provider (consumer + staff + OAuth2 AS for B2B) | TO BE DECIDED |
| Data store(s) | TO BE DECIDED |
| Search engine | TO BE DECIDED |
| CMS | TO BE DECIDED |
| Email provider | TO BE DECIDED |
| Region topology / data residency (GDPR, DPDP placement) | TO BE DECIDED |
| Observability tooling | TO BE DECIDED |
| Security architecture pass | TO BE DECIDED — next dedicated pass |

---

## Provisional Security Rules

**These rules are provisional until the dedicated security architecture pass completes.** They are safe, durable defaults derived only from confirmed facts: .NET Core 10 / C# / ASP.NET Core on Kubernetes, Azure (Key Vault confirmed, all private endpoints), Angular client, versioned REST B2B API, Terraform + GitOps, ASVS 5.0 L2 baseline, SAQ A payment scope (ARCHITECTURE.md).

### HTTP boundary security

1. Serve everything exclusively over HTTPS with TLS 1.2+ (1.3 preferred). Browser-facing apps (storefront, portal, dashboard) enforce with `UseHttpsRedirection()` and `UseHsts()` with preload; API hosts do not listen on plaintext HTTP at all — reject plaintext connections outright, never redirect (a redirect arrives after credentials have already leaked, and HSTS is a browser-only control) (CC-SEC-003, CC-SEC-008).
2. Ship a strict CSP on every HTML response: nonce- or hash-based scripts, no `unsafe-inline`, `frame-ancestors 'none'`, `base-uri 'self'`, `form-action 'self'`; collect CSP violation reports and roll out via Report-Only before enforcing (CC-SEC-003).
3. Emit `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, and a Permissions-Policy on every response; set `Cache-Control: no-store` on authenticated and sensitive responses.
4. Configure CORS with an explicit `WithOrigins()` allowlist only; never combine credentials with wildcard or suffix-matched origins (suffix matching is bypassable).
5. Order ASP.NET Core middleware so HTTPS and security headers run before static files, then authentication, then authorization — in that order.
6. Allowlist HTTP methods per route and reject everything else with 405; validate Content-Type and reject unexpected media types with 415.
7. Cap request body sizes (Kestrel `MaxRequestBodySize`, `[RequestSizeLimit]`), clamp page sizes, and rate-limit per client with 429 + `Retry-After` — stricter on auth and order-creation endpoints (CC-API-008, CC-CNT-004).
8. Keep the internal dashboard on a separate origin with its own session scope, network-restricted, on a private network path; storefront and portal never share cookies, tokens, or modules with it (CC-SEC-011, ARCHITECTURE.md Dependency Rule 4).
9. Expose only the storefront/portal/API gateways publicly; all intra-platform services and data stores stay on private endpoints (ARCHITECTURE.md cross-cutting assumptions).

### Authentication and authorization

1. Deny by default: set a fallback authorization policy requiring authentication so every endpoint is protected unless explicitly opted out; grant access via named policies (`[Authorize(Policy=...)]` / `RequireAuthorization()`).
2. Staff and admin authentication is SSO with mandatory passkeys (FIDO2 WebAuthn) — phishing-resistant, bound to the relying-party ID, with user verification **required** (`userVerification='required'`, never `'preferred'`) in every registration and authentication ceremony, and the server rejects any assertion whose authenticator-data UV flag is unset; session lifetime max 12 hours with re-auth for sensitive actions (CC-DSH-001).
3. Consumer authentication supports WebAuthn passkeys and email-code login; passwords, if offered, follow ASVS 5.0 V6 (8–64+ length, breached-password screening, no composition rules, no rotation) (CC-SEC-005). NOTE: the SMS-MFA/mandatory-password posture in ARCHITECTURE.md is flagged for reconciliation against CC-SEC-005 — do not implement SMS-MFA until resolved.
4. Never fall back to weaker recovery flows (SMS codes, security questions) that reintroduce phishable credentials; treat sign-in from an unrecognized device as a step-up event; let users register multiple passkeys including a hardware security key as recovery.
5. B2B API clients authenticate via OAuth 2.0 client credentials per RFC 9700, with `private_key_jwt` or mTLS (RFC 8705); no static shared secrets, no API keys in query strings, no long-lived static keys (CC-API-002).
6. Access tokens are sender-constrained where the client supports it (mTLS-bound or DPoP) and short-lived (max 15 minutes); bearer-only tokens, where permitted, are scoped read-only (CC-API-003).
7. Validate JWTs fully: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all true, minimal clock skew (target zero, ≤ 2 minutes — derived hardening default, tighter than the ASP.NET Core 5-minute default; confirm in the security architecture pass), pinned `ValidAlgorithms` (reject `alg=none`), audience set to this API's specific resource identifier.
8. Enforce least-privilege scopes (`catalog:read`, `orders:write`, `orders:read`, `invoices:read`) and partner tenancy on every B2B endpoint; enforce RBAC least privilege on every dashboard endpoint with a documented, tested role–permission matrix (CC-API-004, CC-DSH-002).
9. Enforce object-level authorization server-side on every resource access: scope every query to the caller's identity/tenant, with mandatory IDOR test coverage on orders, invoices, addresses, and partner data (CC-SEC-007, CC-QA-005). Return 404 for inaccessible resources as a derived hardening default (avoids confirming resource existence); the 404-not-403 behavior is a hard requirement only for non-veg product URLs in the IN market (CC-MKT-004).
10. IP-derived geolocation is untrusted personalization data: it may only *propose* a default market, which the user can override (CC-MKT-002). Every market/compliance gating decision (CC-MKT-003/004, CC-API-007) keys exclusively off the server-side transacting-market state — never off geolocation, `Accept-Language`, or any client-supplied locale hint (CC-SEC-012).
11. Session cookies are HttpOnly, Secure, SameSite, with bounded expiry, server-side revocation, and session refresh on sign-in and privilege change; CSRF protection (`[AutoValidateAntiforgeryToken]` globally) on all cookie-authenticated state-changing requests — bearer-token API endpoints need none (CC-SEC-006).

### Input validation

1. Treat every input crossing a trust boundary — HTTP parameters, headers, cookies, bodies, webhooks, CMS content, translation files, partner uploads — as attacker-controlled; validate server-side against explicit schemas and reject invalid input rather than sanitizing it into acceptance (CC-SEC-001).
2. Validate B2B API request bodies against published JSON Schemas; reject unknown fields; return 400 with RFC 9457 problem details; generate API docs from the same schemas (CC-API-006, CC-API-010, ARCHITECTURE.md Dependency Rule 7).
3. Bind requests only to dedicated DTOs/ViewModels with explicit source attributes (`[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromHeader]`); never bind to entity or domain models; set server-controlled fields (user ID, ownership, timestamps, prices) from server state only (CC-PRC-005).
4. Access data stores exclusively through parameterized queries or safe ORM APIs (LINQ, `FromSqlInterpolated`); never concatenate user input into SQL, shell commands, or templates; allowlist user-chosen sort/filter column names.
5. Encode output for its exact context (HTML, JavaScript, URL); no raw-HTML sinks anywhere — for the Angular client, `bypassSecurityTrust*` and unsanitized `[innerHTML]`/`outerHTML` bindings are banned and enforced by a CI grep gate; CMS rich text renders only through the allowlist renderer (CC-SEC-002, DESIGN.md §14).
6. Validate all data-derived `href`/`src` values against a scheme allowlist (https/mailto/tel) with `#` fallback; validate partner and locator links against a registered-domain allowlist (CC-SEC-004).
7. Treat translation files as untrusted input: ICU MessageFormat only, no HTML in string resources, interpolated values escaped by default, schema-validated in CI (CC-I18N-002).
8. Validate and allowlist any user-influenced URL before the server fetches it (webhook receivers, partner endpoints); block internal, loopback, and cloud-metadata addresses (SSRF); webhook receiver URLs are validated at registration and no redirects are followed at delivery (CC-API-009).
9. If XML must ever be parsed, set `DtdProcessing.Prohibit` and `XmlResolver = null`; never use `BinaryFormatter` or JSON `TypeNameHandling` other than `None`.
10. Contact and other public forms get server-side validation, rate limiting, and CAPTCHA-equivalent abuse control; user input never reaches SMTP headers (CC-CNT-004).

### Secret handling

1. All secrets live in Azure Key Vault only — never in source, config files, client bundles, environment variables, or Terraform code/variables/state (CC-SEC-008, ARCHITECTURE.md).
2. Authenticate every Azure SDK client with managed identity (`DefaultAzureCredential` or explicit `TokenCredential`); never embed keys or credentialed connection strings anywhere.
3. Grant app identities only narrow data-plane RBAC roles scoped to specific resources (e.g., Key Vault Secrets User); never Contributor, Owner, or Key Vault Administrator.
4. On Kubernetes, use a keyless workload-identity mechanism to reach Key Vault (e.g., Azure Workload Identity with the Key Vault CSI driver — recommended default from the deployment reference set; exact mechanism TO BE CONFIRMED in the security architecture pass); do not bake secrets into images or manifests.
5. Cache Key Vault secrets only with a TTL honoring expiry, and react to rotation events; never cache indefinitely.
6. Encrypt data at rest; apply field-level encryption to employee compensation with Key Vault-managed keys (CC-DSH-005, CC-SEC-008).
7. Payment card data never enters the system: delegate all card handling to a PCI DSS L1 processor via hosted fields or redirect; no service may accept, log, or store PANs (CC-ORD-003, SAQ A scope).
8. Sign webhooks with per-partner rotating HMAC secrets sourced from Key Vault, with timestamps to bound replay (CC-API-009).
9. Secret scanning runs in CI and blocks merge on findings (CC-SEC-009, CC-QA-002).

### Logging and error handling

1. Return only generic errors to clients: ProblemDetails/RFC 9457 bodies with correct status codes and no stack traces, exception messages, SQL, file paths, or internal identifiers; developer exception pages in dev only (CC-API-006).
2. Fail closed: any exception in an authorization or gating path is a denial, never a bypass.
3. Log security events — authn successes/failures, authz denials, validation rejections, admin actions — as structured logs to centralized monitoring with alerting (CC-SEC-010, CC-NFR-003).
4. Never log credentials, tokens, passwords, connection strings, or PANs (which never exist in-system per CC-ORD-003); redact PII; use structured log templates, never string interpolation into log messages (CC-SEC-010).
5. Encode or sanitize user-supplied values before they enter log entries to prevent log injection.
6. Write every privileged dashboard action and every order state transition to the append-only audit store (actor, action, object, before/after, timestamp); nothing mutates audit records or issued invoices — corrections are new records (CC-DSH-004, CC-ORD-006, CC-INV-001).
7. In the Angular client, route errors through interceptors and a global ErrorHandler: full details logged server-side, users see generic messages with correlation IDs only; never surface raw error bodies or internal endpoints.
8. Filter PII from telemetry pipelines; alert on Key Vault access denials and authentication failure spikes. Observability tooling: TO BE DECIDED (ARCHITECTURE.md).
9. Enforce documented data retention per class with automated deletion jobs; logs are in scope (CC-CMP-003).

### Deployment and CI/CD safety

1. Provision infrastructure only through Terraform executed in CI/CD pipelines with short-lived, least-privilege provider credentials; never run applies from developer machines with long-lived keys (ARCHITECTURE.md deployment model).
2. Store Terraform state in an encrypted remote backend with state locking and least-privilege access; treat state files as secrets.
3. Pin Terraform provider and module versions; consume modules only from reviewed internal or verified sources; encode secure defaults (encryption on, no public access, no open security groups, least-privilege IAM) in reusable golden modules.
4. Enforce policy-as-code (e.g., OPA) and IaC scanning in CI so noncompliant plans are blocked before merge and apply; run automated drift detection against GitOps-declared state.
5. Deliver applications via GitOps: declarative manifests reconciled to clusters; no manual mutations of cluster state (ARCHITECTURE.md).
6. Keep all intra-platform services and data stores on private endpoints with public ingress limited to the gateways (ARCHITECTURE.md cross-cutting assumptions). Harden Kubernetes workloads per the deployment reference set: restricted pod security, default-deny ingress and egress network policies, image scanning before deployment, and default-deny network controls as defense in depth on top of identity-based auth.
7. CI merge gates are mandatory and blocking: tests green, ≥80% line coverage per package, SAST clean of high/critical, SCA clean of critical, secret scan clean, lint clean, no raw-HTML sinks, human code review; AI-generated code passes identical gates plus mandatory human review, no auto-merge (CC-QA-001/002).
8. Run the market-gating test matrix, money-path tests, and authz cross-tenant/cross-role suite on every merge; DAST per release; external penetration test before launch and annually (CC-QA-003/004/005/007).
9. Build Angular with AOT (no JIT in production bundles), strict TypeScript and strictTemplates, subresource integrity enabled, and production source maps disabled (hardening defaults from the Angular reference set); CI lint/grep gates block `bypassSecurityTrust*` and unsanitized `[innerHTML]`/`outerHTML` bindings (CC-SEC-002, DESIGN.md §14).
10. Self-host all fonts and static assets; no third-party runtime CDNs for fonts or scripts (CC-NFR-005). Generate an SBOM per release (CC-SEC-009).

---

## Prompt Placeholders To Resolve

The stack is identified in ARCHITECTURE.md, so all six placeholders RESOLVE as follows:

| Placeholder | Resolution | Justifying fact |
|---|---|---|
| `{{CODE_QUALITY_PROMPT}}` | "Low cyclomatic complexity, low cognitive complexity, and separation of concerns." Plus the repo's CC-QA gates: ≥80% coverage, mutation testing on money/gating/authz code, requirement-ID traceability (CC-QA-001/002, REQUIREMENTS.md §17). | REQUIREMENTS.md §15 defines the binding quality gates. |
| `{{API_SECURITY_PROMPT}}` | https://owasp.org/www-project-api-security/ and https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html | ARCHITECTURE.md confirms the API style is versioned REST (CC-API-001). |
| `{{BACKEND_FRAMEWORK_PROMPT}}` | `/Users/jmanico/Dropbox/github/platform/context/prompts/code security/Backend Frameworks/DotNet/00 Secure C# ASP.NET Core Web Developer` plus `/Users/jmanico/Dropbox/github/platform/context/prompts/code security/Backend Frameworks/DotNet/01 Secure C# ASP.NET Core API Developer` | ARCHITECTURE.md confirms .NET Core 10 / C# / ASP.NET Core server with a REST API surface. |
| `{{FRONTEND_FRAMEWORK_PROMPT}}` | `/Users/jmanico/Dropbox/github/platform/context/prompts/code security/Client Side Frameworks/Angular/00 Secure Angular Core Security Developer` | ARCHITECTURE.md confirms Angular for all three clients (SSR mechanism still open). |
| `{{AUTH_PROMPT}}` | https://fidoalliance.org/passkeys/ — passkeys are mandatory for staff/admins. NOTE: the consumer SMS-MFA/password posture is flagged in ARCHITECTURE.md for reconciliation against CC-SEC-005 and remains an open item. | ARCHITECTURE.md auth model: passkey mandatory for admins/staff (CC-DSH-001); consumer reconciliation pending. |
| `{{DEPLOYMENT_PROMPT}}` | https://www.wiz.io/academy/application-security/terraform-security-best-practices/ plus `/Users/jmanico/Dropbox/github/platform/context/prompts/code security/Backend Frameworks/DotNet/05 Secure C# Azure Cloud Developer` | ARCHITECTURE.md confirms Terraform + GitOps deployment on Azure (Key Vault, private endpoints, Kubernetes). |

---

## Selected Prompt Imports

Imports selected from confirmed decisions:

- **Architecture decisions** (REST, private endpoints, separate-origin dashboard) → OWASP API Security Top 10 + REST Security Cheat Sheet (`{{API_SECURITY_PROMPT}}`).
- **Backend framework** (.NET Core 10 / ASP.NET Core) → DotNet `00 Secure C# ASP.NET Core Web Developer` + `01 Secure C# ASP.NET Core API Developer` (`{{BACKEND_FRAMEWORK_PROMPT}}`).
- **Frontend framework** (Angular) → Angular `00 Secure Angular Core Security Developer` (`{{FRONTEND_FRAMEWORK_PROMPT}}`).
- **Auth model** (passkey-mandatory staff, WebAuthn) → FIDO Alliance passkeys guidance (`{{AUTH_PROMPT}}`); consumer flow import deferred pending the CC-SEC-005 reconciliation.
- **Deployment model** (Terraform + GitOps on Azure/Kubernetes) → Wiz Terraform best practices + DotNet `05 Secure C# Azure Cloud Developer` (`{{DEPLOYMENT_PROMPT}}`).

No import is selected for the **identity provider**, **data stores**, or **payment processor** — all three remain TO BE DECIDED (ARCHITECTURE.md known unknowns). Select and add those imports when the products are chosen.

---

## Dependency Rules

These align with and elaborate CC-SEC-009 (minimal dependencies, documented justification, CVE review, maintenance check, transitive analysis, SCA/secret scanning in CI, SBOM per release).

1. Do not add a dependency when the standard library or a few lines of first-party code will do.
2. Prefer zero new dependencies; when a library is genuinely required, justify it in the PR description.
3. Use only actively maintained libraries — a commit or release within the last 12 months (the 12-month window is this repository's policy elaboration of the CC-SEC-009 maintenance check, `[ASSUMPTION]` pending ratification).
4. Use only the latest stable major version; no deprecated, abandoned, or pre-release packages.
5. Reject any library with known unpatched CVEs; check before adding it and again on every update.
6. Audit transitive dependencies, not just direct ones; a small direct dependency with a large or unvetted tree is grounds for rejection.
7. Pin exact versions with a committed lockfile; no floating version ranges in production.
8. Prefer libraries with narrow scope, minimal dependencies of their own, and a clear security track record.

---

## Open Questions

Beyond the missing inputs listed under Required Security Inputs (not repeated here):

- Consumer auth reconciliation: SMS-MFA/mandatory-password (ARCHITECTURE.md human input) vs. passkeys + email-code with optional ASVS V6 passwords (CC-SEC-005). Resolve before any consumer auth code is written.
- Dashboard network restriction model: VPN vs. IP allowlist plus SSO (CC-SEC-011 `[ASSUMPTION]`).
- SSR mechanism for the Angular storefront (server-side gating per CC-MKT-003/004 requires it; mechanism open).
- B2B API rate-limit defaults (600 req/min, order creation 60/min) are `[ASSUMPTION]` in CC-API-008; tune per partner tier.
- Audit retention period (7 years for financial actions) is `[ASSUMPTION]` in CC-DSH-004; confirm per market legal review.
