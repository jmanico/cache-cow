# Cache Cow: Architecture (ARCHITECTURE.md)

Version 0.2 | Status: Provisional

This document owns HOW the platform is built: components, interfaces, data flow, trust boundaries, and technology choices. WHAT the system must do is REQUIREMENTS.md; security rules are authored in SECURITY.md; the design language is DESIGN.md.

---

## Technology decisions (confirmed)

- **Server:** .NET Core 10 / C# (ASP.NET Core) on Kubernetes, all private endpoints. `ASSUMPTION`: "all private endpoints" means intra-platform services and data stores are reachable only on private networking, with public ingress limited to the storefront, portal, and API gateways.
- **Clients:** Angular for all three surfaces (storefront, wholesale portal, internal dashboard).
- **API style:** versioned REST (CC-API-001).
- **Cloud:** Azure Key Vault confirmed for secrets. `ASSUMPTION`: Azure as the primary cloud — no other Azure services are chosen yet (Known unknowns).
- **Delivery:** infrastructure provisioned by Terraform; application delivery via GitOps (declarative manifests reconciled to the clusters). CI enforces the CC-QA gates and the SECURITY.md deployment rules.
- **Packaging:** a modular monolith-or-services decision is deliberately deferred; the bounded-context boundaries below hold under either packaging.
- **Scale:** worldwide — six launch markets across the Americas, Europe, and Asia. `ASSUMPTION`: multi-region deployment (Kubernetes clusters or regional edges in at least Americas/Europe/Asia) to meet the per-market latency budgets (CC-NFR-002); exact region topology, data-residency placement, and single-vs-multi-region write model are Known unknowns.

## Authentication model

- **Consumers:** optional accounts with WebAuthn passkeys and email-code login (CC-SEC-005; policy in SECURITY.md, Authentication rules 3–4).
- **Staff and admins:** SSO with mandatory passkeys/WebAuthn (CC-DSH-001).
- **B2B API clients:** OAuth 2.0 client credentials (CC-API-002/003; policy in SECURITY.md, Authentication rules 5–7). Authorization server or managed equivalent: Known unknowns.

## Clients (Angular)

- **Consumer storefront SPA/SSR** — SSR (or equivalent server rendering) is effectively required, not optional: CC-MKT-003/004 demand server-side exclusion of non-veg content from IN responses including sitemaps and structured data, and CC-I18N-004 needs correct `lang`/`hreflang` per rendered locale. Angular Universal is the natural fit; the SSR mechanism is a Known unknown.
- **Wholesale portal** — utilitarian variant of the same design system (DESIGN.md §11).
- **Internal dashboard** — separate origin and session scope from the storefront, network-restricted (SECURITY.md, HTTP boundary rule 8); Pit-themed (DESIGN.md §12).
- All three consume design tokens generated from DESIGN.md §§3–4 (`tokens.json`) and are built to the SECURITY.md client rules (CSP-compatible construction, no raw-HTML sinks, hardened builds).

## Server bounded contexts (each owns its data)

1. **Market & Gating Policy** — per-market policy configuration as data: catalog gating, veg/non-veg exclusion, content-page availability, currency/tax convention, legal content set. The single enforcement point consulted by storefront rendering, search, the B2B API, and sitemap/feed generation (CC-MKT-001–008, CC-SEC-012).
2. **Catalog & Inventory** — SKUs with structured food data; inventory per SKU per regional cold store; three-state availability derived per requesting market; per-market, per-locale search (CC-CAT-001–006).
3. **Pricing & Promotions** — per-SKU per-market prices in integer minor units; promotion rules with market-timezone windows; the order service is final authority on applied promotions (CC-PRC-001–007).
4. **Ordering & Payments** — idempotent order submission, server-side recomputation of all money, audit-logged order state machine, card handling delegated outward to the external processor (CC-ORD-001–008, CC-PRC-005). Processor selection: Known unknowns (must cover per-market local methods, CC-ORD-004).
5. **Fulfillment** — routing to the regional cold store for the delivery address, cross-region override gated by audited dashboard permission; frozen-shipping serviceability checks at checkout (CC-FUL-001/002).
6. **Wholesale & B2B API** — partner tenancy, approval workflow driven from the dashboard, wholesale price lists invisible to consumer sessions, versioned `/v1` REST API (CC-WHS-001–004, CC-API-001–010).
7. **Invoicing** — per-market legal invoice generation, immutable with credit notes; server-rendered PDFs behind authenticated download (CC-INV-001/002).
8. **Back Office** — dashboard modules per CC-DSH-003 with RBAC, append-only audit store, hr-restricted employee records (CC-DSH-001–006).
9. **Identity & Access** — consumer accounts, staff SSO, and the OAuth2 authorization server for B2B clients, per the Authentication model above. Provider selection: Known unknowns.
10. **Content & Localization** — CMS-sourced content rendered through the sanitizing allowlist renderer; ICU MessageFormat string resources; transactional email in all launch locales (CC-CNT-001–006, CC-I18N-001–006).

## Cross-cutting

- **Observability:** structured logs, metrics, and traces from every service; continuous per-market synthetic probes including the production IN gating probe (CC-NFR-003). Tooling: Known unknowns.
- **Data stores:** deliberately unchosen. Constraints any choice must satisfy: exact-decimal/integer money (CC-PRC-003), append-only audit (CC-ORD-006, CC-DSH-004), per-locale search (CC-CAT-005), field-level encryption (CC-DSH-005), automated retention deletion (CC-CMP-003).

## Known unknowns

The single home for open decisions — kept visible, not guessed. Per CLAUDE.md, none of these may be resolved without a human decision.

- Azure as primary cloud (only Key Vault is confirmed); selection of any other Azure services
- Payment processor and per-market local methods (CC-ORD-004)
- Identity provider (consumer + staff + OAuth2 AS for B2B)
- Data store(s)
- Search engine
- CMS
- Email provider
- Region topology and data residency (GDPR/DPDP placement); single- vs multi-region write model
- SSR mechanism for the Angular storefront
- Observability tooling
- Dashboard network-restriction model: VPN vs IP allowlist plus SSO (CC-SEC-011)
- Cold-chain shipping spec (48h transit is an `[ASSUMPTION]` in CC-FUL-002)
- Tax/VAT calculation approach per market
- Carrier integrations
- Dedicated security architecture pass (security posture is provisional per SECURITY.md until it completes)
- Inline `[ASSUMPTION]` tags at individual requirements and rules (e.g., rate-limit defaults CC-API-008, audit retention CC-DSH-004, net-30 terms CC-WHS-004, invoice delivery CC-INV-002, the 12-month dependency-maintenance window in SECURITY.md) — each stays open at its owning location until ratified

## Requirement Traceability

| Architecture element | Requirement groups | Status |
|---|---|---|
| Market & Gating Policy service (policy-as-data, single server-side enforcement point) | CC-MKT-001–008, CC-SEC-012, CC-CNT-005/006, CC-QA-003 | Boundary fixed |
| SSR storefront (Angular) | CC-MKT-003/004, CC-I18N-001–005, CC-NFR-002/005 | SSR mechanism open |
| Catalog & Inventory service | CC-CAT-001–006, CC-FUL-001/002 | Data store open |
| Pricing & Promotions service | CC-PRC-001–007, CC-QA-004 | Tax engine open |
| Ordering & Payments service | CC-ORD-001–008, CC-PRC-005 | Payment processor open |
| Wholesale portal + B2B REST API | CC-WHS-001–004, CC-API-001–010, CC-QA-005 | OAuth2 AS open |
| Invoicing service | CC-INV-001/002 | Legal review required |
| Internal dashboard (separate origin) + Back Office | CC-DSH-001–006, CC-SEC-011 | Network-restriction model open |
| Identity & Access | CC-SEC-005/006, CC-DSH-001, CC-API-002/003 | Provider open |
| Content & Localization pipeline | CC-CNT-001–006, CC-I18N-001–006, CC-SEC-002 | CMS and email provider open |
| Terraform + GitOps delivery, k8s, Key Vault, private endpoints | CC-SEC-008/009, CC-QA-001/002, CC-NFR-001 | Region topology open |
| Observability & synthetic gating probes | CC-NFR-003, CC-SEC-010 | Tooling open |
| Privacy & compliance controls (GDPR, DPDP, APPI, CCPA; retention jobs) | CC-CMP-001–005 | Legal review required |

All "open" statuses are itemized under Known unknowns.

## Dependency Rules

1. **Compliance gating is server-side and upstream of everything user-visible.** Storefront rendering, search, feeds, sitemaps, and the B2B API all depend on the Market & Gating Policy service; nothing may implement its own market conditionals (CC-MKT-003/006). Clients never gate; they display what the server already gated.
2. **Money flows one way.** Clients and caches may *display* prices; only the Ordering service *computes* them, from Pricing as canonical source, at submission time (CC-PRC-005/006). Nothing depends on client-supplied monetary values.
3. **Consumer surfaces must not depend on wholesale data.** Wholesale prices/terms live behind the partner tenancy boundary and are unreachable from consumer session context (CC-WHS-003).
4. **The dashboard is isolated.** Storefront and portal never import dashboard modules or share cookies/tokens with it (SECURITY.md, HTTP boundary rule 8).
5. **Payment card data never enters the dependency graph.** All card handling is delegated outward (CC-ORD-003; SECURITY.md, Secret handling rule 7).
6. **Audit and invoices are append-only sinks.** Corrections are new records, never mutations (CC-INV-001; SECURITY.md, Logging rule 6).
7. **Schemas are the single source of truth at every trust boundary.** API validation, generated docs, and translation-file CI validation all derive from the same published schemas (CC-API-006/010, CC-I18N-002); no hand-maintained parallel definitions.
8. **Design tokens flow from DESIGN.md outward.** All three clients consume the generated `tokens.json`; no client hardcodes brand colors, type, or status vocabulary (DESIGN.md §§3–5).
9. **Shared kernel is minimal:** market/locale identifiers, money type (integer minor units), SKU identity, requirement-tagged test utilities. Everything else stays inside its bounded context.
