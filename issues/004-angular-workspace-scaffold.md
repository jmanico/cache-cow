# 004 · Angular workspace scaffold: storefront (SSR), portal, dashboard with hardened builds

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: N/A — no single CC-* ID; implements ARCHITECTURE.md "Clients" and SECURITY.md, Deployment and CI/CD safety rule 9 (build hardening); SSR obligation traced to CC-MKT-003/004 and CC-I18N-004 via ARCHITECTURE.md
- **Title**: Angular workspace scaffold: storefront (SSR), portal, dashboard with hardened builds
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Operational

## Requirement
- **Description**: The repository MUST contain Angular application scaffolds for the three client surfaces — consumer storefront (with Angular SSR `@angular/ssr` and hydration), wholesale portal, and internal dashboard — whose production builds enforce AOT compilation (no JIT), strict TypeScript and strictTemplates, subresource integrity, and disabled production source maps, with no features or pages included.
- **Rationale**: ARCHITECTURE.md "Clients" confirms Angular for all three surfaces and Angular SSR (`@angular/ssr`) with hydration for the storefront — SSR is effectively required because CC-MKT-003/004 demand server-side exclusion of non-veg content from IN responses and CC-I18N-004 needs correct `lang`/`hreflang` per rendered locale. SECURITY.md Deployment rule 9 mandates the hardened build settings for Angular; ARCHITECTURE.md "Clients" further requires the dashboard on a separate origin/session scope (SECURITY.md, HTTP boundary rule 8) and all clients built to the SECURITY.md client rules (CSP-compatible construction, no raw-HTML sinks).
- **Design**: Storefront and portal share the design system, with the portal as a utilitarian variant (DESIGN.md §11); the dashboard is Pit-themed (DESIGN.md §12). This scaffold ships no styled surfaces — it only wires each app to consume design tokens from `tokens.json` (issue 005; ARCHITECTURE.md, Dependency rule 8).

## Scope
- **Applies To**: Web App
- **Components**: Angular workspace scaffolding for three applications: consumer storefront SPA/SSR (`@angular/ssr` + hydration), wholesale portal, internal dashboard; production build configuration per SECURITY.md Deployment rule 9; token-consumption wiring stub per ARCHITECTURE.md Dependency rule 8. Excluded: all features, pages, routing content, components (issues 052, 063–079); CSP headers (server-emitted, issue 017); token generation (005); CI pipeline (006); dashboard network restriction/VPN (079).
- **Actors**: Frontend engineers; CI system (build gates)
- **Data Classification**: Internal (build configuration; no runtime data)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: XSS via JIT template compilation and non-strict templates (Deployment rule 9); tampered static assets (subresource integrity); source-map disclosure of client internals in production; cross-surface session/module bleed between dashboard and consumer surfaces (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4)
- **Trust Boundary**: Client-server edge — clients render only typed, validated responses and never gate or compute money (ARCHITECTURE.md, Dependency rules 1–2); dashboard vs. storefront/portal origin separation
- **Zero Trust Consideration**: Clients are untrusted display surfaces by construction: no raw-HTML sinks, CSP-compatible construction with no inline handlers/styles (SECURITY.md, Input validation rule 5; HTTP boundary rule 2), and the build denies the unsafe modes (JIT, loose templates) rather than relying on developer discipline.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V3 Web Frontend Security; ASVS 5.0 V13 Configuration (hardened build/deployment configuration), under the platform-wide Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: CM-6 (configuration settings), CM-7 (least functionality — no JIT in production)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the workspace, when applications are enumerated, then exactly three exist — storefront, wholesale portal, internal dashboard — all Angular (ARCHITECTURE.md, "Clients"), each buildable independently, containing no feature pages beyond a minimal bootstrap shell.
2. **AC-02**: Given the storefront application, when it is built and served, then it server-renders via `@angular/ssr` and hydrates on the client (ARCHITECTURE.md, "Clients": SSR mechanism confirmed), proving the SSR pipeline that later gating/`lang`/`hreflang` work (issues 025, 063, 071) requires.
3. **AC-03**: Given any of the three production builds, when build output and configuration are inspected, then AOT is used with no JIT compiler in the bundle, TypeScript `strict` and `strictTemplates` are enabled, subresource integrity is enabled, and no source maps are emitted (SECURITY.md, Deployment rule 9).
4. **AC-04**: Given a deliberate violation — JIT enabled, `strict`/`strictTemplates` disabled, source maps enabled for production, or SRI disabled — when the CI build-verification check runs, then the build/gate fails (negative case; SECURITY.md, Deployment rule 9 via Deployment rule 7's blocking gates).
5. **AC-05**: Given the workspace structure, when module dependencies are inspected, then storefront and portal import no dashboard modules and the dashboard imports no storefront/portal modules — the dashboard is buildable for deployment to a separate origin (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4).
6. **AC-06**: Given any application's styles/config, when scanned, then no brand colors, type, or status vocabulary are hardcoded — each app consumes generated design tokens (`tokens.json`) via the wiring stub (ARCHITECTURE.md, Dependency rule 8; DESIGN.md §15).
7. **AC-07**: Given the workspace source, when the raw-HTML-sink grep gate runs, then there are zero uses of `bypassSecurityTrust*` or unsanitized `[innerHTML]`/`outerHTML` bindings (SECURITY.md, Input validation rule 5) — and the scaffold introduces none.
8. **AC-08**: Given the scaffold, when fonts/assets are inspected, then no third-party runtime CDN references exist; any fonts/assets present are self-hosted (SECURITY.md, Deployment rule 10; CC-NFR-005).

## Failure Behavior
- **On Invalid Input**: N/A (no runtime user input in scope; boundary validation belongs to feature issues).
- **On System Error**: Fail closed at build time — any hardening check, SSR build, or sink-grep failure blocks the merge (SECURITY.md, Deployment rule 7); a scaffold app that cannot satisfy Deployment rule 9 does not ship.
- **Alerting**: CI failure on the merge request; no runtime alerting in scope (client global ErrorHandler/interceptor wiring per SECURITY.md, Logging rule 7 lands with feature issues).

## Test Strategy
- **Unit Tests**: Angular component tests for the minimal bootstrap shells (build sanity), tagged per REQUIREMENTS.md §17.
- **Integration Tests**: SSR smoke test: the storefront renders a route server-side and hydrates without errors (`@angular/ssr` per ARCHITECTURE.md).
- **Security Tests**: CI assertions on build configuration (AOT/no-JIT, strict + strictTemplates, SRI on, production source maps off — SECURITY.md, Deployment rule 9); the raw-HTML-sink grep gate over the workspace (SECURITY.md, Input validation rule 5); a check that no third-party runtime CDN URLs appear in build output (Deployment rule 10).
- **Compliance Tests**: Build-config verification output retained as CI evidence.
- **Coverage Target**: ≥ 80% per package where measurable (CC-QA-001); the scaffold's testable surface is minimal by design.

## Dependencies
- **Upstream**: None strictly; 001 "Solution scaffold" recommended first so repository layout is settled.
- **Downstream**: 005 "Design token pipeline" (tokens consumed by all three apps); 006 "CI merge gates" (runs the build-hardening and sink-grep checks); 052 "Wholesale portal UI"; 063 "Storefront SSR shell"; 079 "Dashboard shell: separate origin, VPN-restricted, Pit theme"; 098 "Accessibility gates".
- **External**: None at scaffold stage.

## Implementation Notes
- **Constraints**: Angular for all three surfaces with Angular SSR (`@angular/ssr`) + hydration for the storefront, running on AKS (ARCHITECTURE.md, "Clients"/"Technology decisions"); components must be CSP-compatible by construction — no inline event handlers or inline styles (SECURITY.md, HTTP boundary rule 2); dashboard deploys to a separate origin with distinct session scope (SECURITY.md, HTTP boundary rule 8); dependency additions follow SECURITY.md Dependency Rules (pinned exact versions, committed lockfile, actively maintained, latest stable major).
- **Anti-Patterns**: MUST NOT enable JIT in production bundles, disable strict/strictTemplates, or emit production source maps (SECURITY.md, Deployment rule 9); MUST NOT use `bypassSecurityTrust*` or unsanitized `[innerHTML]`/`outerHTML` (Input validation rule 5); MUST NOT load fonts/scripts from third-party runtime CDNs (Deployment rule 10); MUST NOT share cookies, tokens, or modules between the dashboard and the other surfaces (HTTP boundary rule 8); MUST NOT hardcode brand colors/type/status vocabulary in any client (ARCHITECTURE.md, Dependency rule 8).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — including the raw-HTML-sink CI grep — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs pin no Angular major version; per SECURITY.md Dependency Rules the latest stable major applies at implementation time.
- Whether the three applications live in one Angular workspace or separate workspaces is not specified; SECURITY.md HTTP boundary rule 8 requires no module/cookie sharing with the dashboard either way, so the acceptance criteria assert isolation rather than workspace topology.
- The concrete SRI mechanism for an Angular build (build-time `subresourceIntegrity` output vs. deployment-time injection) is not detailed in the specs beyond "subresource integrity enabled" (SECURITY.md, Deployment rule 9).
