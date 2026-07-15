# Cache Cow: Security Rules (SECURITY.md)

Version 0.2 | Status: Provisional — pending the dedicated security architecture pass (tracked in ARCHITECTURE.md, Known unknowns)

This document is the single author of the platform's security requirements and controls. The security requirement IDs in REQUIREMENTS.md (CC-SEC-001–012 and the security clauses of other CC-* requirements) are pointers into this file; ARCHITECTURE.md and DESIGN.md reference these rules and never restate them. These rules govern **all code written in this repository, including AI-generated code** (merge gates in Deployment rule 7).

## Baseline

- **OWASP ASVS 5.0 Level 2 applies in full, platform-wide.** The rules below are the platform-specific elaborations; they narrow ASVS, never relax it.
- **RFC 9700 (OAuth 2.0 Security BCP)** governs B2B API authorization.
- Confirmed stack these rules assume (from ARCHITECTURE.md): .NET Core 10 / C# / ASP.NET Core on Kubernetes, Azure Key Vault, all private endpoints, Angular clients, versioned REST B2B API, Terraform + GitOps.

---

## HTTP boundary security

1. Serve everything exclusively over HTTPS with TLS 1.2+ (1.3 preferred) (CC-SEC-008). Browser-facing apps (storefront, portal, dashboard) enforce with `UseHttpsRedirection()` and `UseHsts()` with preload; API hosts do not listen on plaintext HTTP at all — reject plaintext connections outright, never redirect (a redirect arrives after credentials have already leaked, and HSTS is a browser-only control) (CC-SEC-003).
2. Ship a strict CSP on every HTML response: nonce- or hash-based scripts, no `unsafe-inline`, `frame-ancestors 'none'`, `base-uri 'self'`, `form-action 'self'`; no component may require inline event handlers or inline styles — CSP-friendly by construction; collect CSP violation reports and roll out via Report-Only before enforcing (CC-SEC-003).
3. Emit `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, and a Permissions-Policy on every response; set `Cache-Control: no-store` on authenticated and sensitive responses (CC-SEC-003).
4. Configure CORS with an explicit `WithOrigins()` allowlist only; never combine credentials with wildcard or suffix-matched origins (suffix matching is bypassable).
5. Order ASP.NET Core middleware so HTTPS and security headers run before static files, then authentication, then authorization — in that order.
6. Allowlist HTTP methods per route and reject everything else with 405; validate Content-Type and reject unexpected media types with 415.
7. Cap request body sizes (Kestrel `MaxRequestBodySize`, `[RequestSizeLimit]`), clamp page sizes, and rate-limit per client with 429 + `Retry-After` — stricter on auth and order-creation endpoints; numeric defaults per partner tier are set in CC-API-008 (CC-CNT-004).
8. The internal dashboard runs on a separate origin (or isolated subdomain with distinct session scope) from the consumer storefront, network-restricted (restriction model open — ARCHITECTURE.md, Known unknowns), on a private network path; storefront and portal never share cookies, tokens, or modules with it (CC-SEC-011).
9. Network topology (gateway-only public ingress; private endpoints for everything else) is authored in ARCHITECTURE.md (Technology decisions); any exposure beyond the gateways is a security defect.

## Authentication and authorization

1. Deny by default: set a fallback authorization policy requiring authentication so every endpoint is protected unless explicitly opted out; grant access via named policies (`[Authorize(Policy=...)]` / `RequireAuthorization()`).
2. Staff and admin authentication is SSO with mandatory passkeys (FIDO2 WebAuthn) — phishing-resistant, bound to the relying-party ID, with user verification **required** (`userVerification='required'`, never `'preferred'`) in every registration and authentication ceremony, and the server rejects any assertion whose authenticator-data UV flag is unset; session lifetime max 12 hours with re-auth for sensitive actions (refunds, employee-record access, role changes) (CC-DSH-001).
3. Consumer authentication (accounts are optional) supports WebAuthn passkeys and email-code login; passwords, if offered, follow ASVS 5.0 V6 (8–64+ length, breached-password screening, no composition rules, no rotation). SMS-based MFA is not offered on any surface — it is phishable and SIM-swap-vulnerable (CC-SEC-005).
4. Never fall back to weaker recovery flows (SMS codes, security questions) that reintroduce phishable credentials; treat sign-in from an unrecognized device as a step-up event; let users register multiple passkeys including a hardware security key as recovery.
5. B2B API clients authenticate via OAuth 2.0 client credentials per RFC 9700, with `private_key_jwt` or mutual TLS (RFC 8705); no static shared secrets, no API keys in query strings, no long-lived static keys (CC-API-002).
6. Access tokens are sender-constrained where the client supports it (mTLS-bound per RFC 8705 or DPoP per RFC 9449) and short-lived (max 15 minutes); bearer-only tokens, where permitted for a client tier, are scoped read-only (CC-API-003).
7. Validate JWTs fully: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all true, minimal clock skew (target zero, ≤ 2 minutes — derived hardening default, tighter than the ASP.NET Core 5-minute default; confirm in the security architecture pass), pinned `ValidAlgorithms` (reject `alg=none`), audience set to this API's specific resource identifier.
8. Enforce the least-privilege scopes defined in CC-API-004 and partner tenancy on every B2B endpoint; enforce RBAC least privilege on every dashboard endpoint with a documented, tested role–permission matrix, and design role-based views so each screen exposes only the fields the role requires — employee management especially (CC-DSH-002, CC-DSH-005).
9. Enforce object-level authorization server-side on every resource access: scope every query to the caller's identity/tenant, with mandatory IDOR test coverage on orders, invoices, addresses, and partner data (CC-SEC-007, CC-QA-005). Return 404 for inaccessible resources as a derived hardening default (avoids confirming resource existence); the 404-not-403 behavior is a hard requirement for non-veg product URLs in the IN market (CC-MKT-004).
10. IP-derived geolocation is untrusted personalization data: it may only *propose* a default market, which the user can override (CC-MKT-002). Every market/compliance gating decision (CC-MKT-003/004, CC-API-007) keys exclusively off the server-side transacting-market state — never off geolocation, `Accept-Language`, or any client-supplied locale hint (CC-SEC-012).
11. Session cookies are HttpOnly, Secure, SameSite, with bounded expiry, server-side revocation, and session refresh on sign-in and privilege change; CSRF protection (`[AutoValidateAntiforgeryToken]` globally) on all cookie-authenticated state-changing requests — bearer-token API endpoints need none (CC-SEC-006).
12. Employee records are restricted to the hr-admin role; employee PII leaves the system only through the audited export function (CC-DSH-005; compensation encryption under Secret handling rule 6).

## Input validation

1. Treat every input crossing a trust boundary — HTTP parameters, headers, cookies, bodies, webhooks, CMS content, translation files, partner uploads — as attacker-controlled; validate server-side against explicit schemas (typed schema validation at the client HTTP boundary, JSON Schema server-side) and reject invalid input rather than sanitizing it into acceptance; clients render prices and inventory values only from typed, validated responses (CC-SEC-001).
2. Validate B2B API request bodies against the published JSON Schemas (the same schemas that generate the API docs, CC-API-010); reject unknown fields; return 400 with RFC 9457 problem details (CC-API-006).
3. Bind requests only to dedicated DTOs/ViewModels with explicit source attributes (`[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromHeader]`); never bind to entity or domain models; set server-controlled fields (user ID, ownership, timestamps, prices) from server state only (CC-PRC-005).
4. Access data stores exclusively through parameterized queries or safe ORM APIs (LINQ, `FromSqlInterpolated`); never concatenate user input into SQL, shell commands, or templates; allowlist user-chosen sort/filter column names.
5. Encode output for its exact context (HTML, JavaScript, URL); no raw-HTML sinks anywhere — for the Angular client, `bypassSecurityTrust*` and unsanitized `[innerHTML]`/`outerHTML` bindings are banned, enforced by a CI grep gate; CMS rich text renders only through the allowlist renderer (CC-SEC-002).
6. Validate all data-derived `href`/`src` values against a scheme allowlist (https/mailto/tel) with `#` fallback; validate partner and locator links against a registered-domain allowlist (CC-SEC-004).
7. Treat translation files as untrusted input — five languages means five supply chains for strings: ICU MessageFormat only, no HTML in string resources, interpolated values escaped by default, schema-validated in CI including placeholder consistency across locales (CC-I18N-002).
8. Validate and allowlist any user-influenced URL before the server fetches it (webhook receivers, partner endpoints); block internal, loopback, and cloud-metadata addresses (SSRF); webhook receiver URLs are validated at registration and no redirects are followed at delivery (CC-API-009).
9. If XML must ever be parsed, set `DtdProcessing.Prohibit` and `XmlResolver = null`; never use `BinaryFormatter` or JSON `TypeNameHandling` other than `None`.
10. Contact and other public forms get server-side validation, rate limiting, and CAPTCHA-equivalent abuse control; user input never reaches SMTP headers (email header injection) (CC-CNT-004).

## Secret handling

1. All secrets live in Azure Key Vault only — never in source, config files, client bundles, environment variables, or Terraform code/variables/state (CC-SEC-008).
2. Authenticate every Azure SDK client with managed identity (`DefaultAzureCredential` or explicit `TokenCredential`); never embed keys or credentialed connection strings anywhere.
3. Grant app identities only narrow data-plane RBAC roles scoped to specific resources (e.g., Key Vault Secrets User); never Contributor, Owner, or Key Vault Administrator.
4. On Kubernetes, use a keyless workload-identity mechanism to reach Key Vault (e.g., Azure Workload Identity with the Key Vault CSI driver; exact mechanism TO BE CONFIRMED in the security architecture pass); do not bake secrets into images or manifests.
5. Cache Key Vault secrets only with a TTL honoring expiry, and react to rotation events; never cache indefinitely.
6. Encrypt data at rest; apply field-level encryption to employee compensation with Key Vault-managed keys (CC-DSH-005, CC-SEC-008).
7. Payment card data never enters the system: delegate all card handling to a PCI DSS Level 1 processor via hosted fields or redirect (SAQ A scope); no service may accept, log, or store primary account numbers (CC-ORD-003).
8. Sign webhooks with per-partner rotating HMAC secrets sourced from Key Vault, with timestamps to bound replay (CC-API-009).

## Logging and error handling

1. Return only generic errors to clients: ProblemDetails/RFC 9457 bodies with correct status codes and no stack traces, exception messages, SQL, file paths, or internal identifiers; developer exception pages in dev only (CC-API-006).
2. Fail closed: any exception in an authorization or gating path is a denial, never a bypass.
3. Log security events — authn successes/failures, authz denials, validation rejections, admin actions — as structured logs to centralized monitoring with alerting (CC-SEC-010, CC-NFR-003).
4. Never log credentials, tokens, passwords, connection strings, or PANs (which never exist in-system per Secret handling rule 7); redact PII; use structured log templates, never string interpolation into log messages (CC-SEC-010).
5. Encode or sanitize user-supplied values before they enter log entries to prevent log injection.
6. Write every privileged dashboard action and every order state transition to the append-only audit store (actor, action, object, before/after, timestamp); nothing mutates audit records or issued invoices — corrections are new records (credit notes, compensating events) (CC-DSH-004, CC-ORD-006, CC-INV-001).
7. In the Angular client, route errors through interceptors and a global ErrorHandler: full details logged server-side, users see generic messages with correlation IDs only; never surface raw error bodies or internal endpoints.
8. Filter PII from telemetry pipelines; alert on Key Vault access denials and authentication failure spikes.
9. Logs are in scope for the documented retention schedule and automated deletion jobs (CC-CMP-003).

## Deployment and CI/CD safety

1. Provision infrastructure only through Terraform executed in CI/CD pipelines with short-lived, least-privilege provider credentials; never run applies from developer machines with long-lived keys.
2. Store Terraform state in an encrypted remote backend with state locking and least-privilege access; treat state files as secrets.
3. Pin Terraform provider and module versions; consume modules only from reviewed internal or verified sources; encode secure defaults (encryption on, no public access, no open security groups, least-privilege IAM) in reusable golden modules.
4. Enforce policy-as-code (e.g., OPA) and IaC scanning in CI so noncompliant plans are blocked before merge and apply; run automated drift detection against GitOps-declared state.
5. Deliver applications via GitOps: declarative manifests reconciled to clusters; no manual mutations of cluster state.
6. Harden Kubernetes workloads: restricted pod security, default-deny ingress and egress network policies, image scanning before deployment — default-deny network controls as defense in depth on top of identity-based auth.
7. Security merge gates are mandatory and blocking: SAST clean of high/critical, SCA clean of critical, secret scan clean, no raw-HTML sinks (Input rule 5's CI grep gate); these compose with the QA gates in CC-QA-001/002 (tests green, coverage, lint, human review). AI-generated code passes the identical gates plus mandatory human review; no auto-merge (CC-QA-002).
8. Run the market-gating test matrix, money-path tests, and authz cross-tenant/cross-role suite on every merge (CC-QA-003/004/005); DAST and penetration-test cadence per CC-QA-007.
9. Build Angular with AOT (no JIT in production bundles), strict TypeScript and strictTemplates, subresource integrity enabled, and production source maps disabled.
10. Self-host all fonts and static assets; no third-party runtime CDNs for fonts or scripts — third-party runtime CDNs are an unaudited supply chain (CC-NFR-005). Generate an SBOM per release (CC-SEC-009).

---

## Dependency Rules

This section authors the dependency policy (CC-SEC-009): minimal third-party dependencies, with SCA and secret scanning in CI blocking on criticals (Deployment rule 7).

1. Do not add a dependency when the standard library or a few lines of first-party code will do.
2. Prefer zero new dependencies; when a library is genuinely required, justify it in the PR description with a CVE-history review, maintenance check, and transitive analysis.
3. Use only actively maintained libraries — a commit or release within the last 12 months (the 12-month window is this repository's elaboration of the CC-SEC-009 maintenance check, `[ASSUMPTION]` pending ratification).
4. Use only the latest stable major version; no deprecated, abandoned, or pre-release packages.
5. Reject any library with known unpatched CVEs; check before adding it and again on every update.
6. Audit transitive dependencies, not just direct ones; a small direct dependency with a large or unvetted tree is grounds for rejection.
7. Pin exact versions with a committed lockfile; no floating version ranges in production.
8. Prefer libraries with narrow scope, minimal dependencies of their own, and a clear security track record.

---

## References

- OWASP ASVS 5.0 — https://owasp.org/www-project-application-security-verification-standard/
- OWASP API Security Top 10 — https://owasp.org/www-project-api-security/
- OWASP REST Security Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html
- RFC 9700, OAuth 2.0 Security Best Current Practice — https://www.rfc-editor.org/rfc/rfc9700
- FIDO Alliance passkeys guidance — https://fidoalliance.org/passkeys/
- Wiz Terraform security best practices — https://www.wiz.io/academy/application-security/terraform-security-best-practices/

Open security decisions (identity provider, data stores, dashboard network-restriction model, region topology/data residency, observability tooling, and the dedicated security architecture pass) are tracked once, in ARCHITECTURE.md "Known unknowns".
