# Cache Cow: Threat Model (THREAT_MODEL.md)

Version 1.0 | Date: 2026-07-15 | Classification: Internal

This is the consolidated threat model for the Cache Cow platform, produced by running the
Threat Modeling prompt suite (STRIDE, PASTA, LINDDUN, Attack Trees, FMEA lenses; Repository
Reconnaissance [05]; Document & Architecture Absorption [06]; Consolidator [07]) against the
canonical specifications. It is the source for the requirement, architecture, and security-rule
changes made on 2026-07-15 (REQUIREMENTS.md v1.3, ARCHITECTURE.md v1.1, SECURITY.md v1.1).

The platform is at bootstrap: specifications and logo assets only, no application code. Repository
reconnaissance (05) therefore found no code-level entry points, data stores, or secrets to analyze;
the model is grounded in the documents (06) and the methodology lenses. Every finding below is either
**folded into the canonical documents** (control added) or **flagged as an open decision** for a human
(per CLAUDE.md: flag conflicts and open decisions, never resolve them unilaterally).

> **Supersession note.** This report consolidates and replaces an earlier, incomplete threat-model
> fragment left under `threat-model/THREAT_MODEL.md` by a prior session. That fragment was truncated
> mid-sentence at its tenth finding, its referenced CycloneDX JSON was never produced, and the
> canonical-document edits it described as "Fixed-in-docs" were never actually applied to the working
> tree. Its still-valid findings — wholesale-portal authentication, database-level audit immutability,
> per-context database roles, ingress WAF/DDoS, and image signing — are carried forward here as T-15
> through T-19 and applied to the canonical documents. The orphaned fragment under `threat-model/`
> is superseded by this file and can be deleted.

---

## 1. Scope and method

- **In scope:** the six subsystems in REQUIREMENTS.md §1 — consumer storefront (Angular SSR), market/
  catalog/pricing engine, order/payment/fulfillment services, B2B API + wholesale portal, internal
  operations dashboard, content pages — plus the confirmed stack in ARCHITECTURE.md (ASP.NET Core
  modular monolith on AKS, Azure PostgreSQL, Key Vault, Microsoft Entra, Contentful, Stripe/Razorpay,
  EasyPost, Azure Communication Services).
- **Out of scope (v1):** everything in REQUIREMENTS.md §16. No AI/ML components are specified, so the
  AI/ML lens (04a) is not applicable today — noted as a watch item: if "recommendations" (CC-MKT-003)
  becomes ML-driven, it re-enters scope and inherits market gating.
- **Perspective limitation:** this pass was produced by a single security-analyst perspective against
  documentation. Findings touching payments, privacy law, and cold-chain operations should be
  validated by payments/finance, privacy/legal, and SRE/operations respectively before launch
  (cross-functional coverage gap, per the suite's Perspective Coverage requirement).

## 2. Actors

| Actor | Trust | Notes |
|---|---|---|
| Anonymous / guest shopper | Untrusted | Guest checkout (CC-ORD-001); no session identity — see T-02 |
| Registered consumer | Low | Optional accounts; passkeys or email-code login |
| B2B partner client | Medium, tenant-scoped | OAuth2 client credentials; per-tenant data isolation |
| Staff (sales-viewer, ops-agent, finance, hr-admin, admin) | High, role-scoped | SSO + passkeys, VPN-only dashboard |
| Payment processor (Stripe, Razorpay) | External, semi-trusted | Sends inbound webhooks — see T-01 |
| CMS (Contentful), carrier (EasyPost), email (ACS), identity (Entra) | External processors | Cross-border data processing — see T-08 |
| External attacker / fraudster / malicious insider | Threat agent | — |

## 3. Trust boundaries

1. Internet → public gateway (storefront / portal / API) — the primary boundary.
2. **Processor → inbound webhook receiver** — a distinct untrusted boundary that the pre-model docs
   did not name (T-01).
3. Consumer session ↔ wholesale/partner tenancy (CC-WHS-003).
4. Storefront/portal ↔ isolated internal dashboard (separate origin, VPN, distinct session — CC-SEC-011).
5. Application ↔ data store / Key Vault (private endpoints, managed identity).
6. **Edge/SSR cache ↔ origin** — cache boundary where gating can be defeated if cache keys are wrong (T-03).
7. Region ↔ region (data residency; EU-in-EU, India-in-India) — see T-07 (open).

## 4. Threat inventory

Severity is the consolidated rating (Consolidator §4 scale). "Disposition" is what this model did about it.

| ID | Threat | Lens | Severity | Disposition |
|---|---|---|---|---|
| **T-01** | Forged/replayed payment confirmation. Order fulfillment or funds capture driven by a client redirect (`success_url`) or an unverified webhook lets an attacker claim "paid" and receive free goods, or replay a real webhook. | STRIDE Spoofing/Tampering; Attack Tree (free-goods goal); FMEA security | **Critical** | **Fixed** — CC-ORD-009; SECURITY.md Input validation r11, Secret handling r9 (CC-SEC-014). Payment/order state moves only on signature-verified server-to-server callback reconciled with a server-initiated confirmation. |
| **T-02** | Guest order/invoice BOLA. Order status, tracking, and invoice download reachable via a guessable order-number + email pair enumerates other customers' orders, addresses, and invoices. | STRIDE Info Disclosure; LINDDUN Identifiability/Disclosure; Attack Tree | **High** | **Fixed** — CC-ORD-010, CC-INV-002 clause; SECURITY.md Authentication r14 (CC-SEC-017). Unguessable, expiring, revocable per-order capability token. |
| **T-03** | Cache defeats gating. An SSR/edge/CDN cache keyed without market+locale (or on a client hint) serves a cached non-veg page into the IN market, or serves personalized/authenticated content to the wrong user. On-theme given the regional-cache topology. | STRIDE Info Disclosure/Tampering; FMEA config | **High** | **Fixed** — CC-MKT-009; SECURITY.md HTTP boundary r10 (CC-SEC-013). Cache-key discipline, no personalized caching, gated SSR transfer state. |
| **T-04** | Email-code (OTP) login abuse. Weak/long-lived codes, no throttling, or enumerable responses allow account takeover by brute force or user enumeration. | STRIDE Spoofing; Attack Tree (ATO) | **High** | **Fixed** — SECURITY.md Authentication r13 (CC-SEC-016). Entropy, ≤10-min single-use codes, per-account/IP throttling, constant-time, enumeration-safe. |
| **T-05** | Idempotency-key collision/replay. Keys not scoped to tenant/session, or not bound to a request fingerprint, allow cross-tenant confusion or serving a stale result for a different order. | STRIDE Tampering; FMEA integrity | **Medium** | **Fixed** — SECURITY.md Input validation r12 (CC-SEC-015). Tenant/session scoping + fingerprint binding; same key + different body → 409. |
| **T-06** | Payment CSP breakage / over-broad allowance. `form-action 'self'` blocks redirect-based methods (PayPal/SEPA/konbini/UPI); a lazy fix widens CSP to wildcards and reopens injection/exfil paths. | STRIDE Tampering; FMEA config | **Medium** | **Fixed** — SECURITY.md HTTP boundary r2 clause. Allowlist the exact processor origins in `form-action`/`frame-src`/`connect-src`, no wildcards. |
| **T-07** | Data residency vs. single write region — **conflict**. "Single primary write region" cannot keep EU-only and India-only personal data in-region for writes; residency (GDPR/DPDP) is violated or the topology must change. | LINDDUN Non-compliance; PASTA Stage 7 | **High** | **Flagged (open)** — ARCHITECTURE.md "Known unknowns" (reopened). Requires a human topology decision. Not resolved. |
| **T-08** | Undocumented cross-border transfers. Stripe/Contentful/EasyPost/Azure process EU/IN personal data across borders with no documented transfer mechanism (adequacy/SCCs/DPF). | LINDDUN Non-compliance | **Medium** | **Flagged + required** — CC-CMP-006 requires the assessment; mechanism choice is an open decision (ARCHITECTURE.md). |
| **T-09** | Backups/replicas as a residency & erasure loophole; erasure vs. legal-hold. Deletion jobs that skip backups/replicas/processors leave PII; erasure requests conflict with immutable invoices / 7-yr audit if the exception is undocumented. | LINDDUN Non-compliance/Unawareness; FMEA | **Medium** | **Fixed** — CC-CMP-003 clause; SECURITY.md Secret handling r6 (CC-SEC-018 area). Deletion propagates; legal-hold exception documented. |
| **T-10** | Email sender spoofing. No SPF/DKIM/DMARC on ACS-sent domains lets attackers spoof Cache Cow order/OTP mail for phishing. | STRIDE Spoofing; PASTA | **Medium** | **Fixed** — SECURITY.md Email and messaging security r1 (CC-SEC-018). DMARC `p=reject`. |
| **T-11** | Money arithmetic overflow. Attacker-influenced quantity × price, in large-grouping currencies (INR/JPY), wraps and underprices. | FMEA integrity; STRIDE Tampering | **Low–Med** | **Fixed** — CC-PRC-003 clause: overflow-checked arithmetic, fail closed. |
| **T-12** | Inventory oversell / preorder race. Concurrent decrements on restocking/preorder stock oversell frozen inventory. | FMEA integrity/availability | **Low–Med** | **Watch** — recommend concurrency-controlled inventory reservation at implementation (Catalog & Inventory context); not yet a written requirement. |
| **T-13** | Refund fraud by insider. A malicious ops-agent issues refunds to an attacker-controlled instrument. Re-auth (Auth r2) and audit (r6) partially mitigate. | PASTA Stage 4/7; FMEA | **Low–Med** | **Watch** — recommend refund bounded to captured amount + reason recorded, consider dual control on large refunds; partially covered by existing re-auth/audit. |
| **T-14** | Promo abuse. No per-customer promo redemption limits / code brute-force protection. | PASTA; FMEA | **Low** | **Watch** — recommend redemption limits + rate limiting on CC-PRC-006 promotions. |
| **T-15** | Wholesale-portal human auth unspecified. The auth model covered consumers, staff, and B2B API clients but not the human partner buyers who see wholesale prices and place orders — an unspecified surface defaults to passwords. | STRIDE Spoofing/EoP; Attack Tree | **High** | **Fixed** — CC-WHS-005; SECURITY.md Authentication r15 (CC-SEC-019). Phishing-resistant MFA, tenant-scoped. Portal IdP flagged open. |
| **T-16** | Audit/invoice mutable at DB level. "Append-only" asserted but unenforced; ordinary Postgres tables are mutable by any role with UPDATE/DELETE — a compromised app credential or DBA can erase 7-yr financial history. | STRIDE Repudiation/Tampering; FMEA | **Medium** | **Fixed** — SECURITY.md Logging r6 (CC-SEC-020). INSERT-only roles + WORM replication. |
| **T-17** | Shared-DB cross-context blast radius. One Flexible Server + one role means an injection/logic flaw in any module (e.g., contact form) reaches employee comp, partner terms, audit; TLS-to-DB unstated. | STRIDE EoP/Info Disclosure | **Medium** | **Fixed** — SECURITY.md Secret handling r10 (CC-SEC-021). Per-context least-privilege roles, own schema, TLS required. |
| **T-18** | No WAF/DDoS at ingress. App rate limits don't absorb volumetric/L7 floods; 99.9% availability + 48-hr transit deadline exposed. | STRIDE DoS; FMEA availability | **Medium** | **Fixed** — SECURITY.md HTTP boundary r11 (CC-SEC-022). DDoS protection + WAF. |
| **T-19** | Unsigned container images. Image scanning finds CVEs but not a substituted/tampered image; nothing verifies the artifact that actually runs. | Attack Tree (supply chain); STRIDE Tampering | **Medium** | **Fixed** — SECURITY.md Deployment r11 (CC-SEC-022). Signed-image admission + provenance. |

Findings T-12 through T-14 are recorded as watch items rather than folded into the canonical docs:
they are real but lower-severity and closer to implementation-time decisions; surfacing them here
avoids silently dropping them.

## 5. Compliance mapping (summary)

- **PCI DSS:** SAQ A maintained — card data never enters the system (CC-ORD-003; Secret handling r7).
  T-01 strengthens payment integrity around the delegated processor.
- **GDPR / DPDP / APPI:** T-07 (residency) and T-08 (transfers) are the material open gaps; CC-CMP-006
  and the reopened Known Unknowns track them. Erasure/retention reconciled in T-09.
- **OWASP ASVS 5.0 L2 / API Top 10:** T-02 (BOLA), T-04 (auth), T-05 (idempotency/business logic),
  T-03 (mass caching / gating) close common API-Top-10 gaps.

## 6. CycloneDX Threat Modeling Blueprint 2.0 (fragment)

```json
{
  "$schema": "https://github.com/CycloneDX/specification/blob/2.0-dev-threatmodeling/schema/2.0/model/cyclonedx-blueprint-2.0.schema.json",
  "modelTypes": ["architecture", "dataFlow", "threat", "risk"],
  "methodologies": ["STRIDE", "PASTA", "LINDDUN", "Attack Tree", "FMEA", "Document Analysis", "Automated Repository Analysis"],
  "metadata": { "title": "Cache Cow Threat Model", "version": "1.0", "timestamp": "2026-07-15", "classification": "Internal" },
  "assets": [
    { "id": "asset-storefront", "name": "Angular SSR storefront", "type": "web-application" },
    { "id": "asset-market-gating", "name": "Market & Gating Policy service", "type": "service" },
    { "id": "asset-ordering", "name": "Ordering & Payments service", "type": "service" },
    { "id": "asset-webhook-receiver", "name": "Inbound processor webhook receiver", "type": "service" },
    { "id": "asset-b2b-api", "name": "B2B REST API", "type": "api" },
    { "id": "asset-dashboard", "name": "Internal operations dashboard", "type": "web-application" },
    { "id": "asset-db", "name": "Azure PostgreSQL Flexible Server", "type": "datastore" },
    { "id": "asset-edge-cache", "name": "Regional read replicas / SSR-CDN cache", "type": "cache" }
  ],
  "actors": [
    { "id": "actor-guest", "name": "Guest shopper", "trustLevel": "untrusted" },
    { "id": "actor-partner", "name": "B2B partner", "trustLevel": "tenant-scoped" },
    { "id": "actor-staff", "name": "Staff/admin", "trustLevel": "role-scoped" },
    { "id": "actor-processor", "name": "Payment processor", "trustLevel": "external" },
    { "id": "actor-attacker", "name": "External attacker / malicious insider", "trustLevel": "adversary" }
  ],
  "boundaries": [
    { "id": "tb-internet-gw", "name": "Internet to public gateway" },
    { "id": "tb-webhook", "name": "Processor to inbound webhook receiver" },
    { "id": "tb-tenant", "name": "Consumer session to partner tenancy" },
    { "id": "tb-dashboard", "name": "Storefront to isolated dashboard" },
    { "id": "tb-cache", "name": "Edge/SSR cache to origin" },
    { "id": "tb-db", "name": "Application to shared PostgreSQL (per-context roles)" },
    { "id": "tb-region", "name": "Region to region (data residency)" }
  ],
  "threats": [
    { "id": "T-01", "title": "Forged/replayed payment confirmation", "categories": ["Spoofing", "Tampering"], "severity": "Critical", "boundary": "tb-webhook", "status": "mitigated", "control": "CC-ORD-009 / CC-SEC-014" },
    { "id": "T-02", "title": "Guest order/invoice BOLA", "categories": ["Information Disclosure", "Identifiability"], "severity": "High", "status": "mitigated", "control": "CC-ORD-010 / CC-SEC-017" },
    { "id": "T-03", "title": "Cache defeats market gating", "categories": ["Information Disclosure", "Tampering"], "severity": "High", "boundary": "tb-cache", "status": "mitigated", "control": "CC-MKT-009 / CC-SEC-013" },
    { "id": "T-04", "title": "Email-code login abuse", "categories": ["Spoofing"], "severity": "High", "status": "mitigated", "control": "CC-SEC-016" },
    { "id": "T-05", "title": "Idempotency-key collision/replay", "categories": ["Tampering"], "severity": "Medium", "status": "mitigated", "control": "CC-SEC-015" },
    { "id": "T-06", "title": "Payment CSP breakage / over-broad allowance", "categories": ["Tampering"], "severity": "Medium", "status": "mitigated", "control": "SECURITY.md HTTP boundary r2" },
    { "id": "T-07", "title": "Data residency vs single write region", "categories": ["Non-compliance"], "severity": "High", "boundary": "tb-region", "status": "open", "control": "ARCHITECTURE Known unknowns / CC-CMP-006" },
    { "id": "T-08", "title": "Undocumented cross-border transfers", "categories": ["Non-compliance"], "severity": "Medium", "status": "open", "control": "CC-CMP-006" },
    { "id": "T-09", "title": "Backup residency & erasure-vs-legal-hold", "categories": ["Non-compliance", "Unawareness"], "severity": "Medium", "status": "mitigated", "control": "CC-CMP-003 / Secret handling r6" },
    { "id": "T-10", "title": "Email sender spoofing", "categories": ["Spoofing"], "severity": "Medium", "status": "mitigated", "control": "CC-SEC-018" },
    { "id": "T-11", "title": "Money arithmetic overflow", "categories": ["Tampering"], "severity": "Low-Medium", "status": "mitigated", "control": "CC-PRC-003" },
    { "id": "T-12", "title": "Inventory oversell race", "categories": ["Integrity", "Availability"], "severity": "Low-Medium", "status": "watch" },
    { "id": "T-13", "title": "Insider refund fraud", "categories": ["Elevation of Privilege"], "severity": "Low-Medium", "status": "watch" },
    { "id": "T-14", "title": "Promotion abuse", "categories": ["Business Logic"], "severity": "Low", "status": "watch" },
    { "id": "T-15", "title": "Wholesale-portal human auth unspecified", "categories": ["Spoofing", "Elevation of Privilege"], "severity": "High", "boundary": "tb-tenant", "status": "mitigated", "control": "CC-WHS-005 / CC-SEC-019" },
    { "id": "T-16", "title": "Audit/invoice mutable at DB level", "categories": ["Repudiation", "Tampering"], "severity": "Medium", "status": "mitigated", "control": "CC-SEC-020" },
    { "id": "T-17", "title": "Shared-DB cross-context blast radius", "categories": ["Elevation of Privilege", "Information Disclosure"], "severity": "Medium", "boundary": "tb-db", "status": "mitigated", "control": "CC-SEC-021" },
    { "id": "T-18", "title": "No WAF/DDoS at ingress", "categories": ["Denial of Service"], "severity": "Medium", "boundary": "tb-internet-gw", "status": "mitigated", "control": "CC-SEC-022" },
    { "id": "T-19", "title": "Unsigned container images", "categories": ["Tampering"], "severity": "Medium", "status": "mitigated", "control": "CC-SEC-022" }
  ],
  "coverageGaps": [
    "Single-analyst perspective: payments/finance, privacy/legal, and SRE validation recommended before launch.",
    "No AI/ML components today; re-run 04a if recommendations become model-driven."
  ],
  "assumptions": [
    "Confirmed stack in ARCHITECTURE.md is authoritative.",
    "Card data remains out of scope via delegated PCI Level 1 processors (SAQ A)."
  ]
}
```

## 7. Change log into the canonical documents (2026-07-15)

- **REQUIREMENTS.md v1.3:** CC-MKT-009, CC-ORD-009, CC-ORD-010, CC-WHS-005, CC-CMP-006,
  CC-SEC-013–022; clauses added to CC-PRC-003, CC-INV-002, CC-CMP-003.
- **SECURITY.md v1.1:** HTTP boundary r2 (payment-origin CSP), new r10 (cache/SSR gating), new r11
  (WAF/DDoS); Authentication r13 (OTP), r14 (guest capability tokens), r15 (wholesale-portal MFA);
  Input validation r11 (inbound webhook verification/payment authority), r12 (idempotency scoping);
  Secret handling r6 (backups), new r9 (inbound webhook secrets), new r10 (per-context DB roles/TLS);
  Logging r6 (DB-enforced audit immutability); Deployment r11 (signed-image admission); new "Email
  and messaging security" r1 (SPF/DKIM/DMARC).
- **ARCHITECTURE.md v1.1:** Ordering & Payments inbound-webhook boundary; Cross-cutting edge/SSR
  cache-gating, backup-residency, and per-context DB-role notes; "Known unknowns" reopened with the
  residency conflict, cross-border transfers, telemetry/backup residency, and portal-IdP decisions.
