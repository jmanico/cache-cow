# 040 · Razorpay payment integration for IN (UPI + cards)

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Cross-border transfer mechanism for processors (ARCHITECTURE.md, "Known unknowns"; CC-CMP-006 names Razorpay). The integration can be built and tested against sandbox environments, but production processing of India personal data depends on the documented lawful transfer basis (issue 094) and the unresolved data-residency/write-region decision. Not proposing a resolution.

## Metadata
- **ID**: CC-ORD-004, CC-ORD-003
- **Title**: Razorpay payment integration for IN (UPI + cards)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The platform MUST process all IN-market consumer payments (UPI and cards, per CC-ORD-004) exclusively through Razorpay via hosted fields or redirect, such that payment card data never touches Cache Cow systems (CC-ORD-003).
- **Rationale**: CC-ORD-003 delegates all card handling to an external PCI DSS Level 1 processor, keeping the platform out of PCI scope (SAQ A per SECURITY.md, Secret handling rule 7 — no service may accept, log, or store primary account numbers). The processor choice is ratified (2026-07-15 decision record, ARCHITECTURE.md): Razorpay for the IN market covering UPI + cards, with IN GST handled with Razorpay/local accounting rules (ARCHITECTURE.md, Technology decisions — Payments). ARCHITECTURE.md Dependency Rule 5: payment card data never enters the dependency graph.
- **Design**: DESIGN.md §10 (Checkout page: straight voice, locale tax/units); DESIGN.md §5.4 (pun budget: zero puns inside checkout, payment, and error recovery); price display per DESIGN.md §4.4 (hi-IN/en-IN INR formatting from the locale, never hand-formatted).

## Scope
- **Applies To**: Both
- **Components**: Ordering & Payments bounded context (ARCHITECTURE.md, Server bounded contexts #4); storefront checkout payment step (Angular SSR)
- **Actors**: IN-market consumer (guest or authenticated account); Razorpay (external processor)
- **Data Classification**: Regulated (PCI-DSS — delegated outward; card data never in-system) / Restricted-PII (order and payer data)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: PCI scope expansion / card-data exposure (CC-ORD-003; SECURITY.md, Secret handling rule 7); client-tampered payment amounts (CC-PRC-005); forged payment success via redirect parameters (CC-ORD-009 — enforcement authority lives in issue 041); CSP weakening via wildcard processor origins (SECURITY.md, HTTP boundary rule 2)
- **Trust Boundary**: Client-server edge at checkout; platform ↔ Razorpay (hosted fields/redirect on the client side; server-initiated API calls and the inbound webhook boundary of issue 041 on the server side)
- **Zero Trust Consideration**: No client-supplied monetary value or payment outcome is trusted: amounts sent to Razorpay come only from the server-side recomputation of CC-PRC-005, and payment confirmation is never taken from the browser return (CC-ORD-009, issue 041).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); data protection and secure communication chapters for the payment hand-off
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-8 (transmission confidentiality — all Razorpay exchanges over TLS), SI-10 (validation of processor responses at the trust boundary)
- **NIST SP 800-207**: Payment outcome verified server-to-server, never inferred from the client channel
- **Regulatory**: PCI DSS — Level 1 processor delegation, SAQ A scope (CC-ORD-003; SECURITY.md, Secret handling rule 7); India DPDP Act 2023 obligations for the IN market (CC-CMP-002); transfer-basis documentation per CC-CMP-006 (issue 094)
- **Other**: RFC 9457 problem details for error responses (SECURITY.md, Logging rule 1)

## Acceptance Criteria
1. **AC-01**: Given an IN-market checkout, when the payment step renders, then UPI and card payment methods are offered and both are fulfilled through Razorpay hosted fields or redirect — no Cache Cow-rendered card input fields exist (CC-ORD-004, CC-ORD-003).
2. **AC-02**: Given any Cache Cow endpoint (checkout, API, logs, database), when integration tests and CI scans inspect request DTOs, persisted data, and log output, then no primary account number or card data field is accepted, stored, or logged anywhere in the system (CC-ORD-003; SECURITY.md, Secret handling rule 7). [Negative]
3. **AC-03**: Given the checkout page CSP, when the Razorpay payment surface loads, then `form-action`, `frame-src`, and `connect-src` allowlist only the exact external origins Razorpay and UPI require — specific origins, never wildcards or suffix matches — with all other directives default-deny (SECURITY.md, HTTP boundary rule 2; delivered with issue 017).
4. **AC-04**: Given a payment order created with Razorpay, when the server initiates the creation call, then the amount is the server-recomputed order total (CC-PRC-005) expressed in integer minor units (paise, CC-PRC-003) with overflow-checked arithmetic, and any client-supplied amount is ignored. [Negative for client-supplied amounts]
5. **AC-05**: Given a consumer double-submits an IN order (same idempotency key), when the second submission arrives, then no duplicate Razorpay order or charge is created (CC-ORD-005; idempotency service of issue 037).
6. **AC-06**: Given the browser returns from a Razorpay redirect with success parameters, when the storefront handles the return, then the order is NOT confirmed and funds are NOT treated as captured on that basis alone — confirmation authority is exclusively the signature-verified webhook + reconciliation path of issue 041 (CC-ORD-009). [Negative]
7. **AC-07**: Given the Ordering & Payments service starts, when it needs Razorpay API credentials, then they are sourced from Azure Key Vault via managed identity only — never from source, config files, environment variables, or client bundles (SECURITY.md, Secret handling rules 1–2).

## Failure Behavior
- **On Invalid Input**: Reject with HTTP 400 and an RFC 9457 problem-details body containing no processor internals, stack traces, or internal identifiers (SECURITY.md, Logging rule 1); log with correlation ID.
- **On System Error**: Fail closed (SECURITY.md, Logging rule 2): if the Razorpay API call fails or times out, the order remains unconfirmed in `received`, no funds movement is assumed, and the consumer sees a generic retryable error. No exception path may mark an order paid.
- **Alerting**: Structured security/payment events (payment-creation failures, unexpected processor responses) to centralized monitoring with alerting (SECURITY.md, Logging rule 3); webhook signature-failure alerting is issue 041's scope.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the Razorpay client adapter (amount-in-paise mapping, overflow-checked arithmetic per CC-PRC-003, server-total sourcing per CC-PRC-005), tagged `CC-ORD-003`, `CC-ORD-004`, `CC-PRC-003`, `CC-PRC-005`.
- **Integration Tests**: ASP.NET Core integration tests against a Razorpay stub: order creation, idempotent resubmission (CC-ORD-005), redirect-return does not advance state (CC-ORD-009 negative). Angular component tests for the IN payment step (methods offered, no card inputs rendered).
- **Security Tests**: SAST + CI checks asserting no card-data fields in DTOs or log templates; CSP allowlist verified exact-origin (no wildcard) in integration tests; secret-scan gate (SECURITY.md, Deployment rule 7).
- **Compliance Tests**: Automated evidence that money-path tests cover INR grouping and recomputation authority (CC-QA-004); log-absence check for PANs.
- **Coverage Target**: ≥ 80% per package (CC-QA-001), with mutation testing on money-path code (CC-QA-001 SHOULD).

## Dependencies
- **Upstream**: 001 Solution scaffold; 002 Shared kernel: Money type; 014 Azure Key Vault; 017 CSP and security headers with payment-processor origin allowlists; 035 Order state machine; 036 Order submission (server-side money recomputation); 037 Idempotency service. CC-PRC-003/005, CC-ORD-005/006.
- **Downstream**: 041 Inbound processor webhook verification and payment authority (confirmation authority for every Razorpay payment); 043 Transactional order emails; 047 Per-market invoice tax content (IN GST, CC-INV-001); 069 Checkout UI per market. AT RISK per header: 094 Cross-border transfer mechanism documentation [BLOCKED] must complete before production IN personal-data processing (CC-CMP-006).
- **External**: Razorpay; Azure Key Vault.

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core modular monolith — Razorpay integration lives inside the Ordering & Payments bounded context with its own least-privilege PostgreSQL role/schema (SECURITY.md, Secret handling rule 10). Razorpay SDK/client library selection must pass the SECURITY.md Dependency Rules (maintained within 6 months, pinned exact version, transitive audit). API p95 latency budget for order creation is 800ms (CC-NFR-002).
- **Anti-Patterns**: MUST NOT render first-party card input fields or proxy card data (CC-ORD-003); MUST NOT trust client-supplied amounts or redirect success parameters (CC-PRC-005, CC-ORD-009); MUST NOT use binary floating point for money anywhere including tests (CC-PRC-003); MUST NOT widen CSP with wildcard or suffix-matched processor origins (SECURITY.md, HTTP boundary rule 2); MUST NOT place secrets in config/env/Terraform (SECURITY.md, Secret handling rule 1); no cache/tech puns in the payment flow (DESIGN.md §5.4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates (tests green, ≥80% coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks) plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-ORD-003/004 (REQUIREMENTS.md §17).

## Open Questions
- The exact Razorpay (and UPI-flow) external origins to allowlist in CSP are not enumerated in the specs; they must be pinned during implementation of issue 017 with this issue.
- SECURITY.md Secret handling rule 7 permits "hosted fields or redirect"; the specs do not choose between Razorpay's hosted checkout and redirect flows for UPI vs. cards.
- The division of IN GST responsibilities between "Razorpay/local accounting rules" (ARCHITECTURE.md) and the Invoicing context's GST invoice content (CC-INV-001, issue 047) is not detailed in the specs.
