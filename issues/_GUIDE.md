# Issue Drafting Guide (internal — not itself an issue)

You are drafting GitHub issue bodies for the Cache Cow project, a spec-only repository at
`/Users/jmanico/Dropbox/github/cache-cow`. The canonical specs (REQUIREMENTS.md, ARCHITECTURE.md,
SECURITY.md, DESIGN.md) are loaded in your project context via CLAUDE.md; THREAT_MODEL.md exists at
the repo root if you need it. Before writing anything, Read
`/Users/jmanico/Dropbox/github/cache-cow/REQUIREMENT_TEMPLATE.md` — every issue must follow it
exactly.

## Output

Write each assigned issue as one markdown file in `/Users/jmanico/Dropbox/github/cache-cow/issues/`
named `NNN-<short-kebab-slug>.md` (NNN = the 3-digit index number you were assigned; pick a short
slug from the title).

File format:
- Line 1: `# NNN · <exact assigned title>` — this line becomes the GitHub issue title; everything
  after it becomes the issue body.
- Line 2: blank.
- Line 3: `Part of the Cache Cow v1 build-out epic.`
- Then the full REQUIREMENT_TEMPLATE.md structure — every section present, in order
  (Metadata, Requirement, Scope, Security Context, Standards Alignment, Acceptance Criteria,
  Failure Behavior, Test Strategy, Dependencies, Implementation Notes). Mark genuinely
  inapplicable fields `N/A`.
- End with `## Open Questions` — list missing/ambiguous spec details, or `None.`

## Binding rules

1. **Source of truth is ONLY the canonical docs.** Do not invent requirements, endpoints, vendors,
   tools, or scope not present in them. Precedence: REQUIREMENTS.md > ARCHITECTURE.md > SECURITY.md
   > DESIGN.md.
2. **Cite spec sections precisely** everywhere relevant, e.g. `REQUIREMENTS.md CC-ORD-009`,
   `SECURITY.md, Input validation rule 11`, `ARCHITECTURE.md, "Edge & SSR caching"`,
   `DESIGN.md §5.2`.
3. **Never resolve open decisions.** ARCHITECTURE.md "Known unknowns" is the authoritative list
   (data residency vs. single primary write region; cross-border transfer mechanism; telemetry &
   backup residency; wholesale-portal identity provider). If your issue depends on one, put a
   blockquote at the top of the body — `> **BLOCKED:** <which decision>` if the issue cannot be
   implemented at all, or `> **AT RISK:** <which decision>` if part of it can proceed — repeat it
   in Dependencies, and do NOT propose a resolution.
4. **Do not guess missing details.** If a detail needed for implementation is absent or ambiguous
   in the specs, exclude it from acceptance criteria and record it under the file's
   `## Open Questions` section.
5. **Metadata fields:** ID = the primary CC-* requirement ID(s) this issue implements (e.g.
   `CC-ORD-009, CC-SEC-014`); Version `1.0.0`; Status `Draft`; Author `Cache Cow spec
   decomposition`; Last Updated `2026-07-15`; Priority = Critical for P1 items on security,
   gating, or money paths, High for other P1, Medium for P2; Classification per the template's
   options.
6. **Requirement section:** Description is a single RFC 2119 sentence. Rationale gives the
   business/regulatory/threat justification found in the specs (cite the threat-model-derived
   controls where SECURITY.md/REQUIREMENTS.md note them). Design cites concrete DESIGN.md
   sections, or `N/A` for non-UI issues.
7. **Standards Alignment:** cite OWASP ASVS 5.0 at chapter/section granularity (e.g. "V4 Access
   Control") unless you are certain of an exact control ID — never fabricate precise control
   numbers. NIST SP 800-53 at control-ID level only for well-known controls (AC-3, AC-6, SI-10,
   AU-9, SC-8, ...). Include RFC / FIDO2 / PCI DSS / GDPR / DPDP / APPI references only where the
   specs name them. OWASP AISVS: `N/A`.
8. **Acceptance Criteria:** 4–8 Given/When/Then criteria, each independently testable, each
   traceable to a cited CC-* ID; include at least one negative case (what MUST NOT happen). Stay
   inside your assigned scope — each issue is sized 0.5–2 engineer-days (< ~1500 LOC); do not
   absorb a neighboring issue's scope (the full index below shows where neighboring scope lives).
9. **Failure Behavior:** fail closed for anything touching authorization, gating, or money
   (SECURITY.md, Logging rule 2); give concrete status codes and logging/alerting behavior per
   SECURITY.md Logging rules.
10. **Test Strategy:** concrete test types against the confirmed stack (.NET 10 / ASP.NET Core
    unit + integration tests, Angular component tests, CI gates); coverage ≥ 80% per CC-QA-001;
    tests tagged with the CC-* IDs they verify (REQUIREMENTS.md §17).
11. **Dependencies:** Upstream/Downstream reference other planned issues by index number and
    title from the full index below, plus CC-* IDs. External names confirmed vendors only
    (Stripe, Razorpay, EasyPost, Contentful, Microsoft Entra, Azure services per ARCHITECTURE.md).
12. **Implementation Notes:** constraints from the confirmed stack (ASP.NET Core middleware/
    attributes, Angular SSR, PostgreSQL Flexible Server constructs, Key Vault, Entra); explicit
    anti-patterns the specs prohibit; AI Development Guidance referencing CC-QA-002 and
    SECURITY.md Deployment rule 7 merge gates (AI-generated code passes identical gates plus
    mandatory human review, no auto-merge).
13. **Technical depth:** name the actual mechanisms (middleware ordering, `[Authorize]` policies,
    INSERT-only grants, `Intl.NumberFormat`/ICU MessageFormat, JSON Schema validation, raw-body
    HMAC verification, etc.) so an AI agent can implement without clarification — but only
    mechanisms the specs mandate or that follow directly from the confirmed stack.

## Full issue index (for Dependencies cross-references)

001 Solution scaffold: .NET 10 modular monolith with enforced bounded-context boundaries
002 Shared kernel: Money type (integer minor units, overflow-checked)
003 Shared kernel: Market, Locale, and SKU identity types
004 Angular workspace scaffold: storefront (SSR), portal, dashboard with hardened builds
005 Design token pipeline: tokens.json from DESIGN.md with CI contrast checks
006 CI merge gates: tests, coverage, lint, SAST/SCA/secret-scan/raw-HTML-sink gate
007 Requirement traceability: PR references, test tagging, coverage report
008 Terraform bootstrap: encrypted remote state, pinned versions, golden modules
009 IaC policy-as-code, scanning, and drift detection
010 GitOps application delivery to AKS
011 AKS workload hardening: pod security, default-deny network policies, image scanning
012 Container image signing and admission verification
013 Ingress WAF and DDoS protection
014 Azure Key Vault: Workload Identity, CSI driver, TTL caching, rotation
015 PostgreSQL Flexible Server: per-context schemas/roles, TLS, encryption at rest
016 TLS/HSTS enforcement and security middleware ordering
017 CSP and security headers with payment-processor origin allowlists
018 CORS, HTTP method/media-type allowlists, request size caps
019 Baseline rate-limiting middleware (429 + Retry-After)
020 Deny-by-default authorization fallback policy
021 RFC 9457 error handling and fail-closed authorization/gating behavior
022 Structured security logging, PII redaction, log-injection prevention, alerting
023 Market & Gating Policy: policy-as-data model and configuration schema
024 Transacting market/locale resolution: geo proposal, user override persistence
025 Server-side gating enforcement API with IN veg-only exclusion
026 404 semantics for market-gated resources
027 Market-gating CI test matrix
028 Cache-safe gating: cache keys, no-store personalized responses, gated SSR transfer state
029 SKU domain model with structured food data
030 Inventory per SKU per regional cold store with three-state availability
031 Per-market per-locale catalog search with vegetarian filter
032 Per-SKU per-market price model
033 Promotion engine: timezone windows, scope, stacking rules
034 Locale-aware price formatting and market tax-display conventions
035 Order state machine with audited transitions
036 Order submission: guest checkout and server-side money recomputation
037 Idempotency service: scoped keys with request-fingerprint binding
038 Per-market address capture and validation
039 Stripe payment integration (US/ES/MX/DE/JP incl. DE PayPal/SEPA, JP konbini)
040 Razorpay payment integration for IN (UPI + cards)
041 Inbound processor webhook verification and payment authority
042 Guest order capability tokens
043 Transactional order emails in all locales
044 Regional cold-store order routing with audited cross-region override
045 Checkout serviceability: postal codes and 48-hour frozen transit
046 Invoice core: sequential numbering, immutability, credit notes
047 Per-market invoice tax content
048 Invoice PDF rendering and authenticated link-only delivery
049 Partner tenancy and onboarding approval workflow
050 Wholesale price lists isolated from consumer sessions
051 Wholesale portal buyer authentication [BLOCKED: IdP undecided]
052 Wholesale portal UI: case-quantity ordering and history
053 B2B API scaffold: /v1 versioning, schema validation, docs from schemas
054 B2B OAuth2 client-credentials authentication and token validation
055 B2B scope and tenant enforcement with IN gating parity
056 B2B per-client rate limits
057 Outbound partner webhooks: HMAC signing, SSRF-safe registration
058 Consumer authentication: passkeys and email-code via Entra External ID
059 Email OTP hardening
060 Staff SSO with mandatory WebAuthn and step-up re-authentication
061 Session cookie policy and CSRF protection
062 Object-level authorization and cross-tenant/IDOR test suite
063 Storefront SSR shell: hydration, switchers, lang/hreflang
064 ICU MessageFormat resource pipeline with CI validation
065 Locale layout resilience: expansion budget, pseudo-localization, visual regression
066 Menu page: product cards, cache-status badges, filters
067 Product detail page: structured food data, FSSAI mark, price display
068 Cart with cross-market preservation rules
069 Checkout UI per market
070 Order tracking UI (five stages)
071 SEO surfaces: gated sitemaps, hreflang, structured data, feeds
072 Contentful integration and sanitizing allowlist renderer
073 Meet our Chefs page
074 Meet our Cows page with IN navigation promotion
075 Meet our Cuts interactive diagram with accessible fallback
076 Contact form with abuse controls
077 Legal pages per market (versioned; DE Impressum/Widerruf)
078 Store locator
079 Dashboard shell: separate origin, VPN-restricted, Pit theme
080 Dashboard RBAC: role-permission matrix and enforcement
081 Append-only audit store with WORM retention
082 Dashboard order management module
083 Dashboard sales analytics module
084 Dashboard inventory-by-cold-store module
085 Dashboard partner management module
086 Dashboard invoice management module
087 Dashboard employee management (HR restriction, compensation encryption)
088 EU consent management
089 Data-subject rights endpoints
090 Retention schedule and automated deletion jobs
091 DPDP/APPI/CCPA obligations assessment
092 Marketing email consent and one-click unsubscribe
093 Sender domain authentication (SPF/DKIM/DMARC)
094 Cross-border transfer mechanism documentation [BLOCKED]
095 OpenTelemetry observability to Azure Monitor
096 Per-market synthetic probes including IN gating probe
097 Real-user monitoring and performance budgets
098 Accessibility gates (WCAG 2.2 AA)
099 Money-path and mutation-testing suite
100 DAST and penetration-test cadence

## When done

Reply with one line per file written (`NNN <filename> — OK`) plus a short list of any spec
ambiguities you recorded as Open Questions.
