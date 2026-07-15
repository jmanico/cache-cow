# 052 · Wholesale portal UI: case-quantity ordering and history

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** The wholesale-portal identity provider / federation model is an open decision (ARCHITECTURE.md, "Known unknowns"; CC-SEC-019). The portal screens and components in this issue can be built and tested behind a stubbed authenticated session, but nothing here can integrate sign-in or ship to any environment until issue 051 is unblocked by a human decision.

## Metadata
- **ID**: CC-WHS-001, CC-WHS-004
- **Title**: Wholesale portal UI: case-quantity ordering and history
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Functional

## Requirement
- **Description**: The wholesale portal MUST allow approved grocery partners to place case-quantity orders against their market's wholesale price lists and to view their invoice history with payment terms (net-60 default, adjustable per partner), presented as the utilitarian variant of the design system (REQUIREMENTS.md CC-WHS-001, CC-WHS-004; DESIGN.md §11).
- **Rationale**: CC-WHS-001 requires a portal for approved partners to place case-quantity orders per market; CC-WHS-004 requires wholesale orders to generate legal invoices with payment terms, which partners need to see. DESIGN.md §11 defines the surface: "utilitarian variant of the consumer system on Paper, case quantities, pallet configs, lead times in Plex Mono, PO upload, invoice history" — the docs look like a working tool, not the storefront.
- **Design**: DESIGN.md §11 (wholesale portal: utilitarian variant on Paper; case quantities, pallet configs, lead times in Plex Mono; invoice history); §3 color tokens (`color.paper.100` surfaces); §4.1 typography (Archivo UI, IBM Plex Mono for all data: quantities, prices, SKUs, order and invoice numbers); §4.4 locale price formatting; §5.4 pun budget (zero puns in checkout/payment/legal contexts — ordering and invoices are money surfaces); §6 layout/grid; §13 accessibility (WCAG 2.2 AA, status never color-only, keyboard operability). Design tokens consumed from generated `tokens.json` only (ARCHITECTURE.md, Dependency rule 8).

## Scope
- **Applies To**: Web App (wholesale portal Angular client), consuming existing wholesale back-end endpoints
- **Components**: Wholesale portal Angular app (ARCHITECTURE.md, Clients); consumes the Wholesale & B2B API bounded context (price lists/terms from issue 050, partner tenancy from 049) and invoice access (issues 046–048). Out of scope: dashboard partner management (085), portal authentication mechanics (051), invoice generation (046–048), B2B machine API (053–057).
- **Actors**: Authenticated, tenant-scoped wholesale portal buyers of approved partners (CC-WHS-005 population).
- **Data Classification**: Confidential (wholesale prices, terms, partner orders and invoices).

## Security Context
- **Defense Layer**: Encoding (Angular client construction rules) — enforcement of authorization, gating, and money is server-side and out of scope here
- **Threat(s) Addressed**: OWASP Top Ten A03:2021 Injection / XSS in a session that can see confidential partner data (no raw-HTML sinks, SECURITY.md, Input validation rule 5); client-side tampering with displayed prices (mitigated by server recomputation authority, CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
- **Trust Boundary**: Client-server edge of the wholesale portal. The client displays what the server already authorized and priced; it enforces nothing (ARCHITECTURE.md, Dependency rules 1–2).
- **Zero Trust Consideration**: The portal renders prices, terms, quantities, and inventory values only from typed, schema-validated responses (SECURITY.md, Input validation rule 1); it never computes money authoritatively and never derives tenant scope client-side.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V5-area output encoding / injection defenses as elaborated by SECURITY.md, Input validation rule 5 (no `bypassSecurityTrust*`, no unsanitized `[innerHTML]`, CI grep gate).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (client boundary data validated against typed schemas), AC-3 (displays only server-authorized tenant data).
- **NIST SP 800-207**: Client holds no authorization logic; every request is re-authorized server-side.
- **Regulatory**: WCAG 2.2 AA floor (CC-NFR-004; DESIGN.md §13). Invoice legal content is authored server-side (CC-INV-001, issues 046–048), not in this UI.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given an authenticated buyer of an approved partner, when they open the portal catalog, then they see their market's wholesale SKUs with case-quantity units and wholesale prices from their partner's price list (issue 050), with quantities, prices, SKUs, and lead times rendered in IBM Plex Mono on the Paper utilitarian layout using design tokens only (CC-WHS-001; DESIGN.md §11, §4.1; ARCHITECTURE.md, Dependency rule 8).
2. **AC-02**: Given the buyer composes an order in case quantities, when they submit, then the portal sends SKUs and quantities with a client idempotency key, and displays the order result computed by the server — the client transmits no authoritative prices, and any price it displays pre-submission is presentational only (CC-WHS-001, CC-ORD-005, CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
3. **AC-03**: Given a submitted wholesale order, when the buyer views order/invoice history, then they see their partner's invoices with the applicable payment terms (net-60 default or the partner's adjusted terms) and each invoice links to the authenticated server-rendered PDF download (CC-WHS-004, CC-INV-001/002; issues 046–048; DESIGN.md §11 "invoice history").
4. **AC-04**: Given all prices shown in the portal, when rendered, then they are locale-formatted via `Intl.NumberFormat` (or equivalent from validated server data) per DESIGN.md §4.4 — hand-formatted currency strings are a defect (CC-PRC-004).
5. **AC-05 (negative)**: Given ordering, payment-terms, and invoice screens, when reviewed, then they contain zero cache/tech puns (comedy never touches money movement) and no status is conveyed by color alone (DESIGN.md §5.4, §13).
6. **AC-06 (negative)**: Given the portal codebase, when the CI grep gate runs, then no `bypassSecurityTrust*` or unsanitized `[innerHTML]`/`outerHTML` sinks exist, and the build is AOT with strict TypeScript/strictTemplates (SECURITY.md, Input validation rule 5; Deployment rule 9).
7. **AC-07 (negative)**: Given any portal screen, when rendered for a buyer of partner A, then it displays only data the server returned for partner A's tenant — the UI implements no cross-tenant selector, no client-side tenant switch, and no fallback rendering of another tenant's cached data (CC-WHS-003 boundary, enforced server-side in issues 050/051/055).
8. **AC-08**: Given the portal UI, when audited, then it meets WCAG 2.2 AA: full keyboard operability, visible focus (2px Ember outline on light per DESIGN.md §13), correct `lang` per locale, automated accessibility checks in CI (CC-NFR-004; DESIGN.md §13; issue 098 gates).

## Failure Behavior
- **On Invalid Input**: Client-side validation is UX only; the server rejects invalid orders with 400/RFC 9457 problem details, and the portal surfaces the generic message with a correlation ID — never raw error bodies or internal endpoints (SECURITY.md, Logging rules 1 and 7).
- **On System Error**: Errors route through Angular interceptors and a global ErrorHandler; full details log server-side; the UI fails to a safe state (no optimistic order confirmation — order state is only what the server confirms) (SECURITY.md, Logging rule 7; fail-closed principle, Logging rule 2).
- **Alerting**: Server-side security event logging and alerting are owned by the back-end issues (022, 050); the client contributes correlation IDs.

## Test Strategy
- **Unit Tests**: Angular component tests: catalog listing with case quantities, order composition, idempotency-key inclusion on submit, invoice-history rendering with terms, locale price formatting per DESIGN.md §4.4 examples, error-state rendering (generic + correlation ID).
- **Integration Tests**: Portal-to-API integration against the wholesale endpoints (typed schema-validated responses, SECURITY.md, Input validation rule 1); double-submit produces one order (CC-ORD-005).
- **Security Tests**: CI grep gate for raw-HTML sinks (SECURITY.md, Deployment rule 7); AOT/strictTemplates build gate (Deployment rule 9); assertion that no wholesale data is fetched or rendered without an authenticated session.
- **Compliance Tests**: Automated accessibility checks (WCAG 2.2 AA, CC-NFR-004); CI contrast checks on token combinations (DESIGN.md §13); i18n string externalization and key parity if portal strings enter the ICU pipeline (CC-I18N-002, see Open Questions).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-WHS-001, CC-WHS-004 (REQUIREMENTS.md §17).

## Dependencies
- **AT RISK**: Wholesale-portal identity provider — open decision (ARCHITECTURE.md, "Known unknowns"); sign-in integration and any environment deployment wait on issue 051.
- **Upstream**: 004 Angular workspace scaffold (portal app, hardened builds); 005 Design token pipeline (`tokens.json`); 049 Partner tenancy and onboarding approval workflow; 050 Wholesale price lists isolated from consumer sessions; 051 Wholesale portal buyer authentication [BLOCKED: IdP undecided]; 037 Idempotency service (order submission keys); 046 Invoice core, 047 Per-market invoice tax content, 048 Invoice PDF rendering and authenticated link-only delivery (invoice history targets).
- **Downstream**: 062 Object-level authorization/IDOR test suite (portal surface coverage); 098 Accessibility gates.
- **External**: None directly (payments/carriers do not surface in this UI; invoicing vendors N/A).

## Implementation Notes
- **Constraints**: Build as the wholesale portal app in the Angular workspace (ARCHITECTURE.md, Clients): utilitarian variant of the design system on Paper (DESIGN.md §11), consuming `tokens.json` — no hardcoded brand colors, type, or status vocabulary (ARCHITECTURE.md, Dependency rule 8). All data display in IBM Plex Mono per DESIGN.md §4.1/§11. CSP-compatible construction: no inline event handlers or inline styles (SECURITY.md, HTTP boundary rule 2). The portal never imports dashboard modules or shares cookies/tokens with the dashboard or storefront (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4). AOT production build, strict TypeScript, strictTemplates, SRI, no production source maps (SECURITY.md, Deployment rule 9). Fonts self-hosted (SECURITY.md, Deployment rule 10).
- **Anti-Patterns**: MUST NOT implement PO upload (see Open Questions — no CC-* requirement authors it); MUST NOT compute or submit authoritative prices/totals client-side (CC-PRC-005); MUST NOT hand-format currency (CC-PRC-004); MUST NOT use `bypassSecurityTrust*` or unsanitized `[innerHTML]` (SECURITY.md, Input validation rule 5); MUST NOT place puns in ordering/payment/legal views (DESIGN.md §5.4); MUST NOT convey any status by color alone (DESIGN.md §13).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests, ≥ 80% coverage, lint, SAST/SCA/secret scan, the raw-HTML-sink grep gate — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-WHS-001, CC-WHS-004 (REQUIREMENTS.md §17).

## Open Questions
- **PO upload**: DESIGN.md §11 lists "PO upload" for the portal, but no CC-* requirement authors purchase-order upload behavior (file handling, formats, parsing, storage, security controls). This is a conflict between a design mention and the absence of a requirement — flagged per CLAUDE.md working rules, not specced here. File-upload behavior is excluded from this issue until a human ratifies a requirement.
- **Pallet configs and lead times**: DESIGN.md §11 displays "pallet configs, lead times in Plex Mono", but no CC-* requirement or bounded context authors the underlying data (where pallet configuration and lead-time values come from). Display slots can be built; data source needs a human decision (related open question recorded on issue 050).
- **Order submission path**: whether portal orders flow through the B2B `/v1` API endpoints (CC-API-005 `Idempotency-Key` header) or dedicated portal endpoints within the wholesale context is not specified; CC-ORD-005/CC-SEC-015 idempotency semantics apply either way.
- **Portal locale set**: CC-I18N-001 lists the seven launch UI locales without stating whether the wholesale portal ships all of them or only the partner's market language; CC-I18N-002 string externalization is assumed to apply to all three Angular surfaces.
