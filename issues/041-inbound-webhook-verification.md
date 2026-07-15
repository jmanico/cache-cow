# 041 · Inbound processor webhook verification and payment authority

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-009, CC-SEC-014
- **Title**: Inbound processor webhook verification and payment authority
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: Payment confirmation, funds capture, and order-state advancement MUST be driven only by inbound processor callbacks (Stripe/Razorpay) whose sender signature is verified over the raw request body before parsing and reconciled against a server-initiated status check, and a browser redirect back from a payment flow MUST NOT by itself confirm payment, capture funds, or advance an order (CC-ORD-009, CC-SEC-014).
- **Rationale**: Threat-model-derived control (THREAT_MODEL.md, folded into the specs 2026-07-15): if payment state can be advanced by client-supplied redirect parameters or unverified callbacks, an attacker forges a "paid" return and receives free goods (SECURITY.md, Input validation rule 11). ARCHITECTURE.md (Server bounded contexts #4) designates the inbound processor-webhook receiver as a distinct untrusted trust boundary alongside the gateway. The same rule names other inbound third-party callbacks — Contentful publish events and carrier/EasyPost events — as the same untrusted boundary, so the verification framework covers them too.
- **Design**: N/A (server-side boundary; no user-facing surface).

## Scope
- **Applies To**: API
- **Components**: Ordering & Payments bounded context — inbound webhook receiver endpoints (ARCHITECTURE.md, Server bounded contexts #4); shared raw-body signature-verification framework reused for Contentful publish events (Content & Localization context) and EasyPost carrier events (Fulfillment context) (SECURITY.md, Input validation rule 11)
- **Actors**: Stripe, Razorpay, Contentful, EasyPost (external senders); attacker forging or replaying callbacks; consumer browser returning from a payment redirect
- **Data Classification**: Confidential (payment/order events); Restricted (verification secrets)

## Security Context
- **Defense Layer**: Input Validation
- **Threat(s) Addressed**: Forged payment confirmation → free goods (SECURITY.md, Input validation rule 11); webhook replay; STRIDE Spoofing and Tampering at the callback boundary; secret exposure of signing material (SECURITY.md, Secret handling rule 9)
- **Trust Boundary**: The inbound processor-webhook receiver, a distinct untrusted trust boundary alongside the public gateway (ARCHITECTURE.md, Server bounded contexts #4)
- **Zero Trust Consideration**: Every callback is treated as attacker-controlled until its signature is verified over the raw (unparsed) body; even a verified event is not sufficient authority for funds capture — it is reconciled against a server-initiated confirmation call to the processor before payment/order state moves (CC-ORD-009).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); input validation and API/web-service chapters for boundary validation of inbound callbacks
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation at the boundary), AU-2 (security event logging of verification failures), SC-8 (TLS on the receiver, SECURITY.md HTTP boundary rule 1)
- **NIST SP 800-207**: No implicit trust by network position or claimed sender identity; cryptographic verification per request plus independent server-initiated reconciliation
- **Regulatory**: N/A
- **Other**: RFC 9457 problem details for rejections (SECURITY.md, Logging rule 1)

## Acceptance Criteria
1. **AC-01**: Given a Stripe or Razorpay webhook with a valid signature, when it arrives at the receiver, then the signature is verified over the raw request body before any parsing/model binding, and only then is the event processed and the order state machine advanced per CC-ORD-006 (CC-ORD-009, CC-SEC-014).
2. **AC-02**: Given a webhook with a missing or invalid signature, when it arrives, then it is rejected before parsing, no payment or order state changes, and the failure is logged and alerted as a security event (SECURITY.md, Input validation rule 11; Secret handling rule 9; Logging rule 3). [Negative]
3. **AC-03**: Given a previously delivered webhook replayed outside the timestamp/nonce replay bounds, when it arrives with a valid signature, then it is rejected and no state changes (CC-SEC-014). [Negative]
4. **AC-04**: Given a signature-verified payment event, when the platform confirms payment or captures funds, then it first reconciles the event against a server-initiated status check to the processor; state advances only when both agree (CC-ORD-009).
5. **AC-05**: Given a browser redirect back from a Stripe or Razorpay payment flow carrying success parameters, when the storefront or API handles the return, then no payment is confirmed, no funds are captured, and no order state advances on that basis (CC-ORD-009). [Negative]
6. **AC-06**: Given the receiver needs verification secrets, when it loads them, then Stripe/Razorpay signing secrets come from Azure Key Vault only, scoped per environment, and rotation on the processor's schedule is supported without downtime (SECURITY.md, Secret handling rule 9; Secret handling rules 1–2, 5).
7. **AC-07**: Given a Contentful publish event or an EasyPost carrier event, when it arrives at its receiver, then the same raw-body signature verification and replay-bound enforcement applies before any processing (SECURITY.md, Input validation rule 11).
8. **AC-08**: Given any exception inside the verification or reconciliation path, when it occurs, then the request is denied and no state changes — verification failures never degrade to acceptance (SECURITY.md, Logging rule 2). [Negative]

## Failure Behavior
- **On Invalid Input**: Reject missing/invalid-signature and out-of-replay-bounds deliveries with HTTP 400 and a generic RFC 9457 body disclosing no verification internals; log a structured security event with correlation ID (SECURITY.md, Logging rules 1, 3, 5).
- **On System Error**: Fail closed (SECURITY.md, Logging rule 2): Key Vault unavailability, verification exceptions, or reconciliation-call failures result in denial/no state change; processors retry per their own delivery semantics.
- **Alerting**: Every signature-verification failure is logged and alerted as a security event (SECURITY.md, Secret handling rule 9); alert on failure spikes per SECURITY.md, Logging rule 8's spirit of anomaly alerting (authentication-failure spike alerting analog).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for raw-body HMAC/signature verification (valid, invalid, missing, tampered-body, tampered-signature), timestamp/nonce replay-bound logic, and per-sender scheme adapters (Stripe, Razorpay, Contentful, EasyPost); tagged `CC-ORD-009`, `CC-SEC-014`.
- **Integration Tests**: ASP.NET Core integration tests proving: parsing occurs only after verification (raw request body preserved, no premature model binding); verified event + successful reconciliation advances the order state machine; verified event + failed reconciliation does not; redirect-return endpoints never move state (CC-ORD-009 negative).
- **Security Tests**: Forged-callback and replay fuzzing against the receiver in DAST/staging (CC-QA-007); secret-scan and SAST merge gates (SECURITY.md, Deployment rule 7); test that secrets never appear in logs (Logging rule 4).
- **Compliance Tests**: Automated evidence that every order state transition originating from a webhook has a corresponding verified-event audit record (CC-ORD-006; SECURITY.md, Logging rule 6).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this money/authz-adjacent code (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold; 014 Azure Key Vault (Workload Identity, rotation); 016 TLS/HSTS and middleware ordering; 021 RFC 9457 error handling and fail-closed behavior; 022 Structured security logging and alerting; 035 Order state machine; 039 Stripe payment integration; 040 Razorpay payment integration. CC-ORD-006.
- **Downstream**: 043 Transactional order emails (shipment notification triggered by verified state changes/carrier events); 048 Invoice PDF rendering (invoicing on confirmed orders); 070 Order tracking UI (carrier events); 072 Contentful integration (publish-event receiver consumes this framework); 057 Outbound partner webhooks (separate, outbound-direction issue — not this scope).
- **External**: Stripe, Razorpay, Contentful, EasyPost, Azure Key Vault.

## Implementation Notes
- **Constraints**: ASP.NET Core receiver endpoints must read and retain the raw request body for signature computation before any deserialization (no `[FromBody]` model binding ahead of verification); constant-time signature comparison; receivers sit behind the public gateway as a distinct untrusted boundary (ARCHITECTURE.md, Server bounded contexts #4); method/media-type allowlisting and body-size caps apply (SECURITY.md, HTTP boundary rules 6–7).
- **Anti-Patterns**: MUST NOT confirm payment, capture funds, or advance orders from client redirect parameters or success URLs (CC-ORD-009); MUST NOT parse or act on a body before signature verification; MUST NOT store signing secrets outside Key Vault or log them (SECURITY.md, Secret handling rules 1, 9; Logging rule 4); MUST NOT treat a verified webhook alone as funds-capture authority without the server-initiated reconciliation call; MUST NOT let verification exceptions fall through to processing (SECURITY.md, Logging rule 2).
- **AI Development Guidance**: AI-generated code passes the identical merge gates (SAST/SCA/secret-scan, tests, coverage, lint) plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-ORD-009/CC-SEC-014 (REQUIREMENTS.md §17).

## Open Questions
- The specs mandate "timestamp/nonce replay bounds" but do not set the window duration or the retention period for processed event IDs/nonces.
- The reconciliation policy (timing, retry/backoff, behavior when the server-initiated status check is temporarily unavailable) is not specified beyond "reconciled against a server-initiated confirmation call".
- The HTTP status code for rejected signatures is not mandated by the specs (400 chosen here as a validation rejection per SECURITY.md, Input validation rule 1); confirm whether processors' retry semantics warrant a different code.
- Contentful and EasyPost signature schemes/verification material are not detailed in the specs beyond being named at the untrusted boundary (SECURITY.md, Input validation rule 11).
