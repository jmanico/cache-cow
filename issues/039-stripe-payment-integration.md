# 039 · Stripe payment integration (US/ES/MX/DE/JP incl. DE PayPal/SEPA, JP konbini)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-003, CC-ORD-004
- **Title**: Stripe payment integration (US/ES/MX/DE/JP incl. DE PayPal/SEPA, JP konbini)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Payment processing for the US, ES, MX, DE, and JP markets MUST be delegated to Stripe (a PCI DSS Level 1 processor) via hosted fields or redirect so that card data never touches Cache Cow systems, offering at minimum per market: US cards; ES cards; DE cards plus PayPal and SEPA; JP cards plus konbini — with Stripe Tax used for these markets (REQUIREMENTS.md CC-ORD-003, CC-ORD-004; SECURITY.md, Secret handling rule 7; ARCHITECTURE.md, "Technology decisions" — Payments).
- **Rationale**: In-house payment processing is explicitly out of scope (REQUIREMENTS.md §16); delegating all card handling to a PCI DSS Level 1 processor keeps Cache Cow in SAQ A scope — no service may accept, log, or store primary account numbers (CC-ORD-003; SECURITY.md, Secret handling rule 7; ARCHITECTURE.md, Dependency rule 5: payment card data never enters the dependency graph). The per-market local methods (DE PayPal/SEPA, JP konbini) are launch minimums (CC-ORD-004, processors ratified 2026-07-15). The 2026-07-15 threat model made payment authority explicit: a browser redirect back from Stripe never confirms payment (CC-ORD-009) — that verification boundary is issue 041.
- **Design**: Checkout is a no-pun zone — comedy never touches money movement (DESIGN.md §5.4); checkout page anatomy per DESIGN.md §10 (UI is issue 069). Price display conventions at payment time per DESIGN.md §4.4 / REQUIREMENTS.md CC-PRC-002 (issue 034).

## Scope
- **Applies To**: Web App (consumer checkout payment initiation; server-side Stripe session/intent creation)
- **Components**: Ordering & Payments bounded context (ARCHITECTURE.md, "Server bounded contexts" #4) — payment initiation against Stripe for US/ES/MX/DE/JP; Stripe Tax for these markets' tax computation (ARCHITECTURE.md, "Technology decisions" — Payments)
- **Actors**: Guest and authenticated consumers in US/ES/MX/DE/JP; Stripe as external processor. (IN/Razorpay is issue 040; inbound webhook verification and payment authority is issue 041.)
- **Data Classification**: Regulated (PCI-DSS — kept out-of-system by design; SAQ A scope) and Restricted/PII (payer context)

## Security Context
- **Defense Layer**: Architecture (delegation outward; card data never enters the trust boundary)
- **Threat(s) Addressed**: Cardholder-data exposure (PCI scope creep) — closed by hosted fields/redirect (CC-ORD-003); forged payment confirmation via client redirect parameters (CC-ORD-009 / CC-SEC-014 — closed in issue 041, but this issue must not create the vulnerable path); secret leakage of Stripe API credentials (SECURITY.md, Secret handling rules 1–2)
- **Trust Boundary**: Outbound server-to-Stripe API calls over HTTPS; the browser's excursion to Stripe-hosted surfaces (hosted fields/redirect) happens outside Cache Cow's boundary; the return redirect is untrusted client input (SECURITY.md, Input validation rule 11)
- **Zero Trust Consideration**: Nothing returned through the browser (redirect query parameters, success URLs) is trusted for payment state — payment confirmation comes only from signature-verified server-to-server webhooks reconciled with a server-initiated status check (CC-ORD-009; issue 041). Amounts sent to Stripe are the server-recomputed totals from issue 036, never client-supplied values (CC-PRC-005).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V12 (Secure Communication — outbound TLS); ASVS 5.0 V13 (Configuration — secret management)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-8 (transmission confidentiality and integrity)
- **NIST SP 800-207**: N/A
- **Regulatory**: PCI DSS — delegation to a PCI DSS Level 1 processor with hosted fields or redirect, keeping Cache Cow in SAQ A scope (CC-ORD-003; SECURITY.md, Secret handling rule 7)
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a submitted order (issue 036) in each of US, ES, MX, DE, JP, when payment is initiated, then a Stripe payment flow is created server-side using the server-recomputed total in the market's currency (USD, EUR, MXN, EUR, JPY per CC-PRC-001), via hosted fields or redirect only (CC-ORD-003/004).
2. **AC-02**: Given the DE market, when a consumer reaches payment, then cards, PayPal, and SEPA are offered; given the JP market, cards and konbini are offered; given US and ES, cards are offered (CC-ORD-004 minimums).
3. **AC-03**: Given any request or response handled by Cache Cow code, when inspected (including logs and telemetry), then no primary account number or card data is ever accepted, processed, logged, or stored — card entry occurs exclusively on Stripe-hosted surfaces (CC-ORD-003; SECURITY.md, Secret handling rule 7; Logging rule 4) (negative case).
4. **AC-04**: Given a consumer returns from a Stripe redirect with success-indicating parameters, when the return is processed, then the order does NOT advance and funds are NOT considered captured on that basis alone — payment state changes only via the signature-verified webhook path of issue 041 (CC-ORD-009; SECURITY.md, Input validation rule 11) (negative case, boundary with issue 041).
5. **AC-05**: Given tax computation for US/ES/MX/DE/JP orders, when totals are computed at submission, then Stripe Tax provides the tax amounts per the confirmed decision (ARCHITECTURE.md, "Technology decisions" — Payments; CC-PRC-002 display conventions are issue 034).
6. **AC-06**: Given Stripe API credentials, when the service authenticates to Stripe, then secrets are sourced exclusively from Azure Key Vault (never source, config, client bundles, or environment variables), reached via Workload Identity/CSI on AKS (SECURITY.md, Secret handling rules 1, 4).
7. **AC-07**: Given the payment pages, when CSP is enforced, then `form-action`, `frame-src`, and `connect-src` allowlist the exact external origins Stripe and the local methods (PayPal, SEPA, konbini) require — specific origins only, never wildcards or suffix matches — coordinated with issue 017 (SECURITY.md, HTTP boundary rule 2).
8. **AC-08**: Given a duplicate payment-initiation attempt for the same idempotent order submission, when processed, then no duplicate charge is created (CC-ORD-005; idempotency service is issue 037).

## Failure Behavior
- **On Invalid Input**: Redirect-return parameters are treated as untrusted display context only; malformed or tampered return parameters never mutate payment/order state (CC-ORD-009). Invalid payment-initiation requests → HTTP 400 RFC 9457 problem details, logged with correlation ID (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed — if Stripe session/intent creation fails, the order remains unpaid in its current state with no false progress; errors surface to the consumer as generic messages with correlation IDs only (SECURITY.md, Logging rules 2, 7). Error-recovery copy contains no puns (DESIGN.md §5.4).
- **Alerting**: Payment-initiation failure spikes and any detection of card-data-shaped input reaching Cache Cow endpoints alert via centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Per-market payment-method selection matrix (US/ES/MX/DE/JP); amount/currency mapping from the shared Money type (integer minor units — JPY zero-decimal correctness, CC-QA-004); construction of Stripe requests from server state only.
- **Integration Tests**: ASP.NET Core integration tests against Stripe test mode/stubs: initiation per market and method; redirect-return handling asserts no state change (AC-04); duplicate initiation under one idempotency key creates one charge.
- **Security Tests**: CI grep/SAST assertions that no code path binds card fields; log-scan test asserting no PAN patterns in output (SECURITY.md, Logging rule 4); secret-scan gate clean (Deployment rule 7); CSP allowlist verified against issue 017's enforced policy (DAST per CC-QA-007 covers header presence).
- **Compliance Tests**: Automated evidence of SAQ A posture — no card-data ingress endpoints exist; Key Vault sourcing of Stripe secrets verified by configuration validation.
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged `CC-ORD-003`, `CC-ORD-004` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 036 (order submission — server-recomputed totals are the only amounts sent to Stripe), 037 (idempotency — no duplicate charges), 014 (Key Vault via Workload Identity/CSI for Stripe secrets), 017 (CSP and security headers — payment-processor origin allowlists; interplay per SECURITY.md, HTTP boundary rule 2), 002 (Money type), 021 (RFC 9457 errors)
- **Downstream**: 041 (Inbound processor webhook verification and payment authority — the ONLY mechanism that confirms payment and advances orders; this issue must leave the redirect path inert), 043 (order confirmation email fires only after webhook-confirmed payment), 046/047 (invoicing consumes confirmed payment and tax data), 069 (checkout UI)
- **External**: Stripe (confirmed processor for US/ES/MX/DE/JP, including DE PayPal + SEPA and JP konbini, and Stripe Tax — ARCHITECTURE.md decision record 2026-07-15); Azure Key Vault; Microsoft Entra (workload identity)

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core in the Ordering & Payments context; outbound calls to Stripe over HTTPS/TLS 1.2+ (SECURITY.md, HTTP boundary rule 1); Stripe SDK adoption goes through the dependency policy — justified in the PR, actively maintained, latest stable major, pinned with lockfile (SECURITY.md, Dependency rules 2–4, 7). Stripe secrets per environment in Key Vault with TTL-honoring caching and rotation reaction (Secret handling rules 1, 5; the *webhook signing* secrets are issue 041's scope per Secret handling rule 9). Payment initiation amounts come exclusively from the persisted, server-recomputed order (ARCHITECTURE.md, Dependency rule 2).
- **Anti-Patterns**: MUST NOT accept, transit, log, or store PANs or any card data — no card input fields on Cache Cow origins (CC-ORD-003; SECURITY.md, Secret handling rule 7). MUST NOT confirm payment, capture funds, or advance an order from redirect/success-URL parameters (CC-ORD-009; SECURITY.md, Input validation rule 11). MUST NOT widen CSP with wildcard or suffix-matched processor origins (HTTP boundary rule 2). MUST NOT embed Stripe keys in client bundles, config, or environment variables (Secret handling rule 1).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests, coverage, lint, SAST/SCA/secret-scan clean — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); money-path tests run on every merge (Deployment rule 8).

## Open Questions
- **MX payment methods**: CC-ORD-004 lists minimum methods for US, DE, ES, JP, and IN but names none for MX; ARCHITECTURE.md confirms only that Stripe serves the MX market. Which methods MX must offer (beyond what "Stripe for MX" implies) is unspecified — not invented here; needs a human decision before the MX method matrix is final.
- The specific Stripe integration surface (hosted fields vs. redirect — e.g., Stripe-hosted checkout page vs. embedded hosted fields) is not chosen in the specs; SECURITY.md, Secret handling rule 7 permits either. The choice affects CSP directives in issue 017 and should be decided with it.
- The exact Stripe/PayPal/konbini external origins for the CSP allowlist are not enumerated in the specs; issue 017 owns the enforced list, but the source of truth for "the exact origins each processor requires" (SECURITY.md, HTTP boundary rule 2) must be established during implementation and kept specific-origins-only.
- Whether Stripe Tax is invoked at quote time (checkout display) in addition to submission-time computation is not specified (CC-PRC-002 requires US estimated tax at checkout; the call pattern against Stripe Tax is undefined).
