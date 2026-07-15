# Cache Cow: Architecture (ARCHITECTURE.md)

Version 1.1 | Status: Confirmed with open items — the 2026-07-15 resolution round closed all then-open decisions, but the 2026-07-15 threat model (THREAT_MODEL.md) reopened three residency/transfer decisions now tracked under "Known unknowns". v1.1 also adds the inbound payment-webhook boundary and edge/SSR cache-gating notes.

This document owns HOW the platform is built: components, interfaces, data flow, trust boundaries, and technology choices. WHAT the system must do is REQUIREMENTS.md; security rules are authored in SECURITY.md; the design language is DESIGN.md.

---

## Technology decisions (confirmed)

- **Server:** .NET Core 10 / C# (ASP.NET Core) on Kubernetes, all private endpoints — confirmed to mean: intra-platform services and data stores are reachable only on private networking, with public ingress limited to the storefront, portal, and API gateways.
- **Clients:** Angular for all three surfaces (storefront, wholesale portal, internal dashboard).
- **API style:** versioned REST (CC-API-001).
- **Cloud:** Azure confirmed as the primary cloud. Confirmed Azure services: Key Vault (secrets), Azure Database for PostgreSQL Flexible Server (data store), Azure Communication Services (email), Azure Monitor + Application Insights + Log Analytics (observability).
- **Delivery:** infrastructure provisioned by Terraform; application delivery via GitOps (declarative manifests reconciled to the clusters). CI enforces the CC-QA gates and the SECURITY.md deployment rules.
- **Packaging:** modular monolith — one deployable ASP.NET Core application with enforced internal module boundaries along the bounded contexts below; contexts may split into services later along the same seams.
- **Scale:** worldwide — six launch markets across the Americas, Europe, and Asia. Multi-region deployment confirmed: Kubernetes clusters/regional edges in at least Americas, Europe, and Asia to meet the per-market latency budgets (CC-NFR-002), with a single primary write region and regional read replicas/edges. Data residency: EU data resident in EU regions (GDPR); India data resident in India (DPDP).
- **Data store:** Azure Database for PostgreSQL Flexible Server for all bounded contexts (exact-decimal/integer money, append-only audit tables, field-level encryption with Key Vault-managed keys, automated retention deletion).
- **Search:** PostgreSQL full-text search, per market and per locale; ja-JP and hi-IN analysis quality must still satisfy CC-CAT-005.
- **CMS:** Contentful (managed headless CMS); all CMS content renders through the sanitizing allowlist renderer.
- **Email:** Azure Communication Services for transactional and marketing email.
- **Payments:** Stripe for US, ES, MX, DE, JP — including Stripe Tax; Razorpay for the IN market (UPI + cards; IN GST handled with Razorpay/local accounting rules). Local methods: DE PayPal + SEPA, JP konbini, all via Stripe (CC-ORD-004).
- **Carriers:** EasyPost multi-carrier aggregator; per-market carriers selected behind it under the ratified 48-hour max frozen transit constraint (CC-FUL-002).
- **SSR:** Angular SSR (`@angular/ssr`) with hydration, running on AKS.

## Authentication model

- **Consumers:** optional accounts with WebAuthn passkeys and email-code login (CC-SEC-005; policy in SECURITY.md, Authentication rules 3–4). Provider: Microsoft Entra External ID.
- **Staff and admins:** SSO with mandatory passkeys/WebAuthn (CC-DSH-001). Provider: Microsoft Entra ID.
- **B2B API clients:** OAuth 2.0 client credentials (CC-API-002/003; policy in SECURITY.md, Authentication rules 5–7). Authorization server: Microsoft Entra ID.

## Clients (Angular)

- **Consumer storefront SPA/SSR** — SSR (or equivalent server rendering) is effectively required, not optional: CC-MKT-003/004 demand server-side exclusion of non-veg content from IN responses including sitemaps and structured data, and CC-I18N-004 needs correct `lang`/`hreflang` per rendered locale. SSR mechanism confirmed: Angular SSR (`@angular/ssr`) with hydration.
- **Wholesale portal** — utilitarian variant of the same design system (DESIGN.md §11).
- **Internal dashboard** — separate origin and session scope from the storefront, network-restricted (SECURITY.md, HTTP boundary rule 8); Pit-themed (DESIGN.md §12).
- All three consume design tokens generated from DESIGN.md §§3–4 (`tokens.json`) and are built to the SECURITY.md client rules (CSP-compatible construction, no raw-HTML sinks, hardened builds).

## Server bounded contexts (each owns its data)

1. **Market & Gating Policy** — per-market policy configuration as data: catalog gating, veg/non-veg exclusion, content-page availability, currency/tax convention, legal content set. The single enforcement point consulted by storefront rendering, search, the B2B API, and sitemap/feed generation (CC-MKT-001–008, CC-SEC-012).
2. **Catalog & Inventory** — SKUs with structured food data; inventory per SKU per regional cold store; three-state availability derived per requesting market; per-market, per-locale search (CC-CAT-001–006).
3. **Pricing & Promotions** — per-SKU per-market prices in integer minor units; promotion rules with market-timezone windows; the order service is final authority on applied promotions (CC-PRC-001–007).
4. **Ordering & Payments** — idempotent order submission, server-side recomputation of all money, audit-logged order state machine, card handling delegated outward to the external processor (CC-ORD-001–008, CC-PRC-005). Processors: Stripe (US/ES/MX/DE/JP) and Razorpay (IN), covering the per-market local methods in CC-ORD-004. Payment/order state advances only on signature-verified inbound processor webhooks reconciled with a server-initiated confirmation call — a client redirect never confirms payment or moves an order (CC-ORD-009; SECURITY.md, Input validation rule 11). The inbound processor-webhook receiver is a distinct untrusted trust boundary alongside the gateway; guest order/invoice access uses per-order capability tokens (CC-ORD-010).
5. **Fulfillment** — routing to the regional cold store for the delivery address, cross-region override gated by audited dashboard permission; frozen-shipping serviceability checks at checkout (CC-FUL-001/002).
6. **Wholesale & B2B API** — partner tenancy, approval workflow driven from the dashboard, wholesale price lists invisible to consumer sessions, versioned `/v1` REST API (CC-WHS-001–004, CC-API-001–010).
7. **Invoicing** — per-market legal invoice generation, immutable with credit notes; server-rendered PDFs behind authenticated download (CC-INV-001/002).
8. **Back Office** — dashboard modules per CC-DSH-003 with RBAC, append-only audit store, hr-restricted employee records (CC-DSH-001–006).
9. **Identity & Access** — consumer accounts, staff SSO, and the OAuth2 authorization server for B2B clients, per the Authentication model above. Provider: Microsoft Entra (External ID for consumers; Entra ID for staff SSO and the B2B OAuth2 authorization server).
10. **Content & Localization** — CMS-sourced content rendered through the sanitizing allowlist renderer; ICU MessageFormat string resources; transactional email in all launch locales (CC-CNT-001–006, CC-I18N-001–006).

## Cross-cutting

- **Observability:** structured logs, metrics, and traces from every service; continuous per-market synthetic probes including the production IN gating probe (CC-NFR-003). Tooling: Azure Monitor with Application Insights and Log Analytics (OpenTelemetry ingestion); synthetic probes via Azure Monitor availability tests.
- **Data stores:** Azure Database for PostgreSQL Flexible Server, satisfying: exact-decimal/integer money (CC-PRC-003), append-only audit (CC-ORD-006, CC-DSH-004), per-locale search via PostgreSQL full-text (CC-CAT-005), field-level encryption (CC-DSH-005), automated retention deletion (CC-CMP-003). Backups, snapshots, and read replicas inherit encryption, residency, and retention from their source (SECURITY.md, Secret handling rule 6). Although one Flexible Server backs all ten contexts, each context connects with its own least-privilege role confined to its own schema over TLS, so the code-level module boundary does not collapse at the connection string (SECURITY.md, Secret handling rule 10); audit and issued-invoice tables are INSERT-only at the database-privilege level with WORM retention (SECURITY.md, Logging rule 6).
- **Edge & SSR caching:** the regional read replicas/edges and any SSR or CDN cache MUST key on server-side transacting market + locale and MUST NOT cache personalized or authenticated responses; SSR transfer/hydration state carries only data already gated for that response. This makes caching incapable of defeating market gating (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary rule 10) — load-bearing given the regional-cache topology.

## Known unknowns

The single home for open decisions — kept visible, not guessed. Per CLAUDE.md, none of these may be resolved without a human decision.

The 2026-07-15 resolution round closed every item then listed (record below). The 2026-07-15 threat model (THREAT_MODEL.md) then surfaced three residency/transfer decisions that the prior round did not reconcile; they are open and awaiting a human decision.

### Open decisions (reopened 2026-07-15 — threat model)

- **Data residency vs. "single primary write region" — a conflict, not yet resolved.** Technology decisions state "a single primary write region with regional read replicas/edges" *and* "EU data resident in EU regions (GDPR); India data resident in India (DPDP)." For **writes** of personal data these contradict: one global write region cannot keep EU-only and India-only personal data in-region. Either residency is violated for the non-home regions, or the topology must change (e.g., per-residency-zone write regions / residency-sharded primaries, or narrowing what personal data each region persists). This blocks any implementation that persists EU or India personal data and must be decided by a human (CC-CMP-001/002/006). Flagged, not resolved.
- **Cross-border transfer mechanism for processors.** Stripe, Contentful, EasyPost, and Microsoft/Azure services process EU/IN personal data across borders; the lawful transfer basis (adequacy / SCCs / EU–US DPF) is not decided or documented (CC-CMP-006).
- **Telemetry & backup residency.** Azure Monitor / Log Analytics aggregation and PostgreSQL backups/snapshots must be pinned to the correct residency zone; the region topology for logs and backups is undecided and is entangled with the write-region conflict above (CC-CMP-003/006).
- **Wholesale-portal identity provider.** Human partner buyers need phishing-resistant authentication (CC-WHS-005) but are not assigned to an identity provider: Entra External ID (as consumers) vs. per-partner federation is undecided (CC-SEC-019).

### Decision record (2026-07-15)

- Azure confirmed as primary cloud; confirmed services: Key Vault, Kubernetes (AKS), Azure Database for PostgreSQL Flexible Server, Azure Communication Services, Azure Monitor + Application Insights + Log Analytics
- Payment processors: Stripe for US/ES/MX/DE/JP with local methods DE PayPal + SEPA and JP konbini; Razorpay for IN (UPI + cards) (CC-ORD-004)
- Identity provider: Microsoft Entra — External ID (consumers), Entra ID (staff SSO), Entra ID as the OAuth2 authorization server (B2B)
- Data store: Azure Database for PostgreSQL Flexible Server
- Search engine: PostgreSQL full-text search
- CMS: Contentful
- Email provider: Azure Communication Services
- Region topology: multi-region (Americas/Europe/Asia), single primary write region with regional read replicas/edges; EU data resident in EU, India data resident in India
- SSR mechanism: Angular SSR (`@angular/ssr`) with hydration
- Observability tooling: Azure Monitor + Application Insights + Log Analytics
- Dashboard network restriction: VPN required, plus SSO with mandatory passkeys (CC-SEC-011)
- Cold-chain shipping spec: 48-hour max carrier transit ratified (CC-FUL-002)
- Tax/VAT approach: Stripe Tax for US/ES/MX/DE/JP; IN GST handled with Razorpay/local accounting rules
- Carrier integrations: EasyPost multi-carrier aggregator
- Packaging: modular monolith
- Dedicated security architecture pass: satisfied by this resolution round; SECURITY.md status updated accordingly
- Former inline `[ASSUMPTION]` tags ratified at their owning locations: CC-API-008 rate limits (600 req/min, 60 order-creations/min), CC-DSH-004 audit retention (7 years, financial actions), CC-WHS-004 wholesale terms (changed to net-60), CC-INV-002 link-only invoice delivery, SECURITY.md dependency-maintenance window (tightened from 12 to 6 months), JWT clock skew ≤ 2 minutes
- The five `[legal review required]` flags (CC-PRC-002, CC-INV-001, CC-CMP-002, CC-CMP-004, CC-FUL-003) accepted as drafted; later legal review runs against implemented behavior

## Requirement Traceability

| Architecture element | Requirement groups | Status |
|---|---|---|
| Market & Gating Policy service (policy-as-data, single server-side enforcement point) | CC-MKT-001–008, CC-SEC-012, CC-CNT-005/006, CC-QA-003 | Boundary fixed |
| SSR storefront (Angular) | CC-MKT-003/004, CC-I18N-001–005, CC-NFR-002/005 | Confirmed: Angular SSR (`@angular/ssr`) |
| Catalog & Inventory service | CC-CAT-001–006, CC-FUL-001/002 | Confirmed: Azure PostgreSQL |
| Pricing & Promotions service | CC-PRC-001–007, CC-QA-004 | Confirmed: Stripe Tax (IN GST via Razorpay) |
| Ordering & Payments service | CC-ORD-001–008, CC-PRC-005 | Confirmed: Stripe + Razorpay (IN) |
| Wholesale portal + B2B REST API | CC-WHS-001–004, CC-API-001–010, CC-QA-005 | Confirmed: Entra ID OAuth2 AS |
| Invoicing service | CC-INV-001/002 | Confirmed: drafted formats accepted 2026-07-15 |
| Internal dashboard (separate origin) + Back Office | CC-DSH-001–006, CC-SEC-011 | Confirmed: VPN + SSO/passkeys |
| Identity & Access | CC-SEC-005/006, CC-DSH-001, CC-API-002/003 | Confirmed: Microsoft Entra |
| Content & Localization pipeline | CC-CNT-001–006, CC-I18N-001–006, CC-SEC-002 | Confirmed: Contentful + Azure Communication Services |
| Terraform + GitOps delivery, k8s, Key Vault, private endpoints | CC-SEC-008/009, CC-QA-001/002, CC-NFR-001 | Confirmed: multi-region, single write region |
| Observability & synthetic gating probes | CC-NFR-003, CC-SEC-010 | Confirmed: Azure Monitor |
| Privacy & compliance controls (GDPR, DPDP, APPI, CCPA; retention jobs) | CC-CMP-001–005 | Confirmed: drafted scope accepted 2026-07-15 |

All decisions above were ratified 2026-07-15; see the decision record under "Known unknowns".

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
