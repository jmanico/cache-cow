# Cache Cow: Architecture (ARCHITECTURE.md)

Version 0.1 | Status: Provisional — bootstrapped from REQUIREMENTS.md v1.0, DESIGN.md v1.0, and explicit human-provided platform inputs. No implementation exists yet; nothing below is inferred from code.

---

## Required Architecture Inputs

- Requirements source: REQUIREMENTS.md
- System purpose: multi-market (US, ES, MX, DE, JP, IN) direct-to-consumer and B2B commerce platform for frozen BBQ products, with regional catalogs/pricing, server-side market compliance gating (notably the vegetarian-only IN market), grocery wholesale, a B2B ordering API, and an internal operations dashboard (REQUIREMENTS.md §1).
- Primary use cases: consumer browse/search/checkout with guest checkout and per-market pricing/tax; order fulfillment routed to regional cold stores; wholesale partner ordering via portal and versioned REST API; internal staff operations (sales analytics, order/invoice management, inventory by cold store, partner approval, employee management) (§§3–8, 11).
- Target users / actors: consumers in six markets / seven locales; approved grocery wholesale partners (portal users and API clients); internal staff by role (sales-viewer, ops-agent, finance, hr-admin, admin); anonymous visitors (guest checkout, content pages).
- Runtime environment: web application, API
- Server framework: .NET Core 10, C#, Kubernetes, Azure Key Vault, all private endpoints
- Client framework: Angular
- API style and integration model: REST
- Authentication and session model: passwords with optional SMS-MFA or passkey for consumers; passkey mandatory for admins and staff. NOTE: consumer SMS-MFA and mandatory-password posture must be reconciled with CC-SEC-005 (passkeys + email-code login, passwords optional per ASVS V6) and CC-DSH-001 (staff = SSO + WebAuthn, which "passkey mandatory" satisfies). B2B API clients are separate: OAuth 2.0 client credentials with `private_key_jwt` or mTLS per CC-API-002/003.
- Data model expectations: per-market catalog with per-SKU structured food data (CC-CAT-001/004); inventory per SKU per regional cold store (CC-CAT-002); prices per SKU per market in integer minor units (CC-PRC-001/003); orders with immutable audit-logged state machine (CC-ORD-006); per-market legal invoices, immutable with credit-note corrections (CC-INV-001); partner/tenant data strictly isolated (CC-API-004); employee records with field-level-encrypted compensation (CC-DSH-005); append-only audit store (CC-DSH-004); market gating policy as data, not code (CC-MKT-006).
- Deployment model: Terraform and GitOps
- Scale expectations: worldwide — six launch markets across the Americas, Europe, and Asia; regional presence must serve LCP < 2.5s p75 per market and API p95 targets (CC-NFR-002) for users on four continents.
- Security expectations: TO BE DECIDED — dedicated security architecture pass is next. Interim floor already committed by REQUIREMENTS.md: OWASP ASVS 5.0 L2 platform-wide (§12), SAQ A payment scope (CC-ORD-003), RFC 9700 OAuth BCP for the B2B API (§8).

## Initial Architecture (Provisional)

A modular monolith-or-services decision is deliberately deferred; what follows fixes the *boundaries*, which hold under either packaging.

**Clients (Angular):**
- Consumer storefront SPA/SSR — SSR (or equivalent server rendering) is effectively required, not optional: CC-MKT-003/004 demand server-side exclusion of non-veg content from IN responses including sitemaps and structured data, and CC-I18N-004/SEO needs correct `lang`/`hreflang` per rendered locale. Angular Universal is the natural fit but the SSR mechanism is an open decision.
- Wholesale portal — utilitarian variant of the same design system (DESIGN.md §11).
- Internal dashboard — **separate origin and session scope** from the storefront (CC-SEC-011, DESIGN.md §14), Pit-themed, network-restricted.
- All three consume design tokens generated from DESIGN.md §§3–4 (`tokens.json`), self-hosted locale-subset fonts (CC-NFR-005), strict CSP with no inline handlers/styles (CC-SEC-003, DESIGN.md §14).
- Resolved (issue #7): CC-SEC-001/002 and DESIGN.md §14 now state client-side controls framework-neutrally with the Angular sinks named explicitly — typed schema validation at the HTTP boundary, and a CI-gated ban on `bypassSecurityTrust*` and unsanitized `[innerHTML]`/`outerHTML` bindings.

**Server (.NET Core 10 / C# on Kubernetes, all private endpoints, REST):**
Bounded contexts, each owning its data:
1. **Market & Gating Policy** — per-market policy configuration as data (catalog gating, veg/non-veg exclusion, content-page availability, currency/tax convention, legal content set). Single enforcement point consulted by storefront rendering, search, B2B API, sitemap/feed generation (CC-MKT-001–008). Geolocation only *proposes* a market; it is never a compliance input (CC-SEC-012).
2. **Catalog & Inventory** — SKUs with structured food data; inventory per SKU per regional cold store; three-state availability derived per requesting market (CC-CAT-001–003); per-market, per-locale search (CC-CAT-005).
3. **Pricing & Promotions** — per-SKU per-market prices in minor units; promotion rules with market-timezone windows; the order service is final authority on applied promotions (CC-PRC-001–006).
4. **Ordering & Payments** — idempotent order submission, server-side recomputation of all money (CC-PRC-005, CC-ORD-005), order state machine with immutable audit log (CC-ORD-006), payment delegated to a PCI DSS L1 processor via hosted fields/redirect so PANs never touch the platform (CC-ORD-003). Processor selection: TO BE DECIDED (must cover per-market local methods, CC-ORD-004).
5. **Fulfillment** — routing to the regional cold store for the delivery address, cross-region override gated by audited dashboard permission (CC-FUL-001); frozen-shipping serviceability checks at checkout (CC-FUL-002).
6. **Wholesale & B2B API** — partner tenancy, approval workflow driven from the dashboard (CC-WHS-002), wholesale price lists invisible to consumer sessions (CC-WHS-003); versioned `/v1` REST API with OAuth2 client-credentials (`private_key_jwt`/mTLS), sender-constrained short-lived tokens, least-privilege scopes, JSON Schema request validation with unknown-field rejection, RFC 9457 errors, per-client rate limits, signed webhooks (CC-API-001–010). Docs generated from the same schemas (CC-API-010).
7. **Invoicing** — per-market legal invoice generation (sequential numbering per legal entity, market tax regimes), immutable with credit notes; server-rendered PDFs behind authenticated download (CC-INV-001/002).
8. **Back Office** — dashboard modules (analytics, orders, invoices, inventory, partners, employees) with RBAC and tested role–permission matrix (CC-DSH-002/003); append-only audit store for privileged actions (CC-DSH-004); hr-restricted employee records with field-level-encrypted compensation via Azure Key Vault-managed keys (CC-DSH-005, CC-SEC-008).
9. **Identity & Access** — consumer accounts (optional; passwords + optional SMS-MFA or passkeys per human input, subject to the CC-SEC-005 reconciliation above), staff SSO with mandatory passkeys/WebAuthn (CC-DSH-001), OAuth2 authorization server or managed equivalent for B2B clients. Product selection: TO BE DECIDED.
10. **Content & Localization** — CMS-sourced content rendered through a sanitizing allowlist renderer (CC-CNT-001, CC-SEC-002); ICU MessageFormat string resources, schema-validated in CI, no HTML in strings (CC-I18N-002); transactional email in all launch locales (CC-I18N-006).

**Cross-cutting (assumptions labeled):**
- ASSUMPTION: multi-region deployment (Kubernetes clusters or regional edges in at least Americas/Europe/Asia) to meet per-market latency budgets worldwide; exact region topology, data-residency placement (DPDP/GDPR implications), and single-vs-multi-region write model: TO BE DECIDED.
- ASSUMPTION: "all private endpoints" means intra-platform services and data stores are reachable only on private networking, with public ingress limited to the storefront/portal/API gateways; the dashboard additionally network-restricted per CC-SEC-011.
- Infrastructure provisioned by Terraform; application delivery via GitOps (declarative manifests reconciled to the clusters). CI enforces the CC-QA gates (coverage, SAST/SCA/secret scan, market-gating matrix, money-path and authz suites, i18n validation).
- Secrets in Azure Key Vault only (CC-SEC-008). ASSUMPTION: Azure Key Vault implies Azure as the primary cloud, but no other Azure services are chosen yet: TO BE DECIDED.
- Observability: structured logs, metrics, traces from every service; continuous per-market synthetic probes including the production IN gating probe (CC-NFR-003). Tooling: TO BE DECIDED.
- Data stores are deliberately unchosen. Constraints any choice must satisfy: exact-decimal/integer money (CC-PRC-003), append-only audit (CC-ORD-006, CC-DSH-004), per-locale search (CC-CAT-005), field-level encryption (CC-DSH-005), automated retention deletion (CC-CMP-003). Store selection: TO BE DECIDED.

**Known unknowns (kept visible, not guessed):** payment processor and per-market local methods; identity provider; data store(s); search engine; CMS; email provider; region topology and data residency; cold-chain shipping spec (48h transit is an assumption in CC-FUL-002); tax/VAT calculation approach per market; carrier integrations; security architecture (next pass).

## Requirement Traceability

| Architecture element | Requirement groups | Status |
|---|---|---|
| Market & Gating Policy service (policy-as-data, single server-side enforcement point) | CC-MKT-001–008, CC-SEC-012, CC-CNT-005/006, CC-QA-003 | Boundary fixed |
| SSR storefront (Angular) | CC-MKT-003/004 (server-side exclusion incl. sitemaps), CC-I18N-001–005, CC-NFR-002/005, DESIGN.md §§3–8, 13–14 | SSR mechanism TO BE DECIDED |
| Catalog & Inventory service | CC-CAT-001–006, CC-FUL-001/002 | Data store TO BE DECIDED |
| Pricing & Promotions service | CC-PRC-001–007, CC-QA-004 | Tax engine per market TO BE DECIDED |
| Ordering & Payments service | CC-ORD-001–008, CC-PRC-005 | Payment processor + local methods TO BE DECIDED (CC-ORD-004) |
| Wholesale portal + B2B REST API | CC-WHS-001–004, CC-API-001–010, CC-QA-005 | OAuth2 AS / token infra TO BE DECIDED |
| Invoicing service | CC-INV-001/002 | Per-market legal formats: legal review required |
| Internal dashboard (separate origin) + Back Office | CC-DSH-001–006, CC-SEC-011 | Network restriction model (VPN vs allowlist) TO BE DECIDED |
| Identity & Access | CC-SEC-005/006, CC-DSH-001, CC-API-002/003 | Provider TO BE DECIDED; consumer SMS-MFA vs CC-SEC-005 reconciliation required |
| Content & Localization pipeline | CC-CNT-001–006, CC-I18N-001–006, CC-SEC-002 | CMS and email provider TO BE DECIDED |
| Terraform + GitOps delivery, k8s, Key Vault, private endpoints | CC-SEC-008/009, CC-QA-001/002, CC-NFR-001 | Region topology and residency TO BE DECIDED |
| Observability & synthetic gating probes | CC-NFR-003, CC-SEC-010 | Tooling TO BE DECIDED |
| Privacy & compliance controls (GDPR, DPDP, APPI, CCPA; retention jobs) | CC-CMP-001–005 | Legal review required; data-residency design TO BE DECIDED |
| Security architecture | §12 (ASVS 5.0 L2 baseline) | TO BE DECIDED — next dedicated pass |

## Dependency Rules

1. **Compliance gating is server-side and upstream of everything user-visible.** Storefront rendering, search, feeds, sitemaps, and the B2B API all depend on the Market & Gating Policy service; nothing may implement its own market conditionals (CC-MKT-003/006). Clients never gate; they display what the server already gated.
2. **Money flows one way.** Clients and caches may *display* prices; only the Ordering service *computes* them, from Pricing as canonical source, at submission time (CC-PRC-005/006). Nothing depends on client-supplied monetary values.
3. **Consumer surfaces must not depend on wholesale data.** Wholesale prices/terms live behind the partner tenancy boundary and are unreachable from consumer session context (CC-WHS-003).
4. **The dashboard is isolated.** Separate origin, separate session scope, private network path (CC-SEC-011); storefront and portal never import dashboard modules or share cookies/tokens with it.
5. **Payment card data never enters the dependency graph.** All card handling is delegated outward to the PCI L1 processor (CC-ORD-003); no service may accept, log, or store PANs.
6. **Audit and invoices are append-only sinks.** Services write to the audit log and issued invoices; nothing mutates them. Corrections are new records (credit notes, compensating events) (CC-ORD-006, CC-DSH-004, CC-INV-001).
7. **Schemas are the single source of truth at every trust boundary.** API validation, generated docs, and translation-file CI validation all derive from the same published schemas (CC-API-006/010, CC-I18N-002, CC-SEC-001); no hand-maintained parallel definitions.
8. **Design tokens flow from DESIGN.md outward.** All three clients consume the generated `tokens.json`; no client hardcodes brand colors, type, or status vocabulary (DESIGN.md §§3–5).
9. **Shared kernel is minimal:** market/locale identifiers, money type (integer minor units), SKU identity, requirement-tagged test utilities. Everything else stays inside its bounded context.
