# 038 · Per-market address capture and validation

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-ORD-002
- **Title**: Per-market address capture and validation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: Address capture MUST use per-market address formats and validation, including Japanese address structure and Indian PIN codes, with all validation enforced server-side against explicit schemas (REQUIREMENTS.md CC-ORD-002; SECURITY.md, Input validation rule 1).
- **Rationale**: Frozen product with a ratified 48-hour maximum transit window (CC-FUL-002) leaves no slack for undeliverable addresses: a malformed Japanese address block or invalid Indian PIN code is a spoiled shipment, a refund, and a compliance-relevant customer harm. Address data also drives cold-store routing (CC-FUL-001) and serviceability checks (CC-FUL-002), which need structured, validated fields to key on. Every input crossing the trust boundary is attacker-controlled and is validated server-side against explicit schemas, rejecting invalid input rather than sanitizing it into acceptance (SECURITY.md, Input validation rule 1).
- **Design**: Checkout renders per-market address formats (DESIGN.md §10, "Checkout": "address formats per market"); JP checkout includes precise delivery-window selection per Japanese logistics norms (DESIGN.md §8.5) — the checkout UI itself is issue 069. This issue owns the server-side address model, per-market schemas, and validation.

## Scope
- **Applies To**: Both (consumer checkout submission; wholesale/B2B order addresses flow through the same per-market validation)
- **Components**: Ordering & Payments bounded context (address capture at submission; ARCHITECTURE.md, "Server bounded contexts" #4); consumed by Fulfillment (#5) for routing and serviceability
- **Actors**: Guest consumer, authenticated consumer, B2B partner clients (delivery addresses on wholesale orders)
- **Data Classification**: Restricted/PII (delivery addresses are personal data under GDPR/DPDP/APPI/CCPA — CC-CMP-001/002)

## Security Context
- **Defense Layer**: Input Validation
- **Threat(s) Addressed**: Injection via free-text address fields reaching downstream systems (SQL — SECURITY.md, Input validation rule 4; log injection — Logging rule 5; carrier/label pipelines); malformed structured fields defeating routing and serviceability logic; CWE-20 (improper input validation)
- **Trust Boundary**: Client–server edge at order submission and address-entry endpoints; address bodies from B2B API clients are equally untrusted (SECURITY.md, Input validation rule 1)
- **Zero Trust Consideration**: Client-side format hints (the per-market form) are UX only; the server independently validates every address against the schema for the order's transacting market, regardless of what surface submitted it. Locale/market for format selection comes from server-side transacting-market state, never client hints (SECURITY.md, Authentication rule 10).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V2 (Validation and Business Logic — input validation)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: Address data handled as personal data under GDPR (ES/DE), DPDP (IN), APPI (JP), CCPA/CPRA (US) — REQUIREMENTS.md CC-CMP-001/002; data minimization per CC-CMP-003
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given each of the six launch markets (CC-MKT-001), when an address is submitted, then it is validated server-side against that market's explicit address schema, and a valid address for the transacting market is accepted (CC-ORD-002).
2. **AC-02**: Given the JP market, when an address is submitted, then the Japanese address structure is captured and validated (structured fields per Japanese addressing, not a generic street/city template) (CC-ORD-002).
3. **AC-03**: Given the IN market, when an address is submitted with a PIN code that fails PIN-code validation, then the submission is rejected with HTTP 400 and RFC 9457 problem details, and no order processing occurs (CC-ORD-002; SECURITY.md, Input validation rule 1).
4. **AC-04**: Given any market, when an address payload contains unknown fields, fields violating type/length constraints, or is missing schema-required fields, then it is rejected — invalid input is never sanitized into acceptance (SECURITY.md, Input validation rules 1–2) (negative case).
5. **AC-05**: Given an address that passes only client-side form validation but fails the server schema (crafted direct API request bypassing the form), when submitted, then the server rejects it — client-side validation is never the sole enforcement (SECURITY.md, Input validation rule 1) (negative case).
6. **AC-06**: Given a validated address, when persisted or passed to fulfillment, then it is stored as structured fields (not a free-text blob), binding via dedicated DTOs with explicit source attributes (SECURITY.md, Input validation rule 3), so routing (CC-FUL-001) and serviceability checks (CC-FUL-002, issue 045) can key on discrete fields.
7. **AC-07**: Given address values containing markup or control characters, when they later appear in logs or downstream output, then they are encoded/sanitized for that context — address free-text never reaches SQL by concatenation or logs unencoded (SECURITY.md, Input validation rules 4–5; Logging rule 5).

## Failure Behavior
- **On Invalid Input**: Reject with HTTP 400 and RFC 9457 problem details identifying the invalid field(s) generically; log a structured validation-rejection event with correlation ID; do not echo internal validation internals or disclose stack traces (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed — if the address-validation path errors, the submission is rejected rather than accepted unvalidated (SECURITY.md, Logging rule 2).
- **Alerting**: Sustained spikes in address-validation rejections (form-bypass probing) surface via structured security logging and centralized monitoring (SECURITY.md, Logging rule 3).

## Test Strategy
- **Unit Tests**: Per-market schema tests — valid and invalid fixtures for each of US, ES, MX, DE, JP, IN, including JP structured-address cases and IN PIN-code boundary cases; DTO binding rejects unknown/over-posted fields.
- **Integration Tests**: ASP.NET Core integration tests submitting addresses through the real endpoint per market; direct-API bypass of client validation is rejected; validated address round-trips as structured fields to the fulfillment consumer.
- **Security Tests**: Fuzzing input class: address fields (injection payloads, oversized values, control characters, mixed scripts) — server rejects or safely encodes; SAST/parameterized-query verification per SECURITY.md, Input validation rule 4.
- **Compliance Tests**: Evidence that address data is stored in the documented PII data class for retention/deletion (CC-CMP-003 schedule is issue 090).
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged `CC-ORD-002` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 (scaffold), 003 (Market identity types), 021 (RFC 9457 error handling), 024 (transacting-market resolution — selects which schema applies)
- **Downstream**: 036 (order submission consumes validated addresses), 044 (cold-store routing keys on the delivery address, CC-FUL-001), 045 (serviceable-postal-code and transit checks, CC-FUL-002 — distinct scope: 038 validates format, 045 validates serviceability), 069 (checkout UI renders per-market forms), 053/055 (B2B order addresses)
- **External**: None named in the specs for address validation (EasyPost is the confirmed carrier aggregator for fulfillment, but the specs do not assign it address-validation duty — see Open Questions)

## Implementation Notes
- **Constraints**: .NET 10 / ASP.NET Core; per-market address schemas as explicit, versionable validation definitions consistent with the schemas-as-single-source rule (ARCHITECTURE.md, Dependency rule 7); dedicated address DTOs with `[FromBody]` binding, never entity models (SECURITY.md, Input validation rule 3); parameterized persistence only (Input validation rule 4). Address fields are PII: redact in logs and telemetry (SECURITY.md, Logging rules 4, 8), and remember CC-ORD-007 forbids full addresses in email bodies (emails are issue 043). Localized field labels/order for the checkout form come through the ICU MessageFormat pipeline (issue 064) — no hardcoded per-locale strings.
- **Anti-Patterns**: MUST NOT rely on client-side validation as sole enforcement (SECURITY.md, Input validation rule 1). MUST NOT flatten addresses into a single free-text field. MUST NOT "sanitize into acceptance" — invalid input is rejected. MUST NOT concatenate address input into SQL or log messages (Input validation rule 4; Logging rule 5). MUST NOT infer the address format from `Accept-Language` or geolocation instead of the transacting market (SECURITY.md, Authentication rule 10).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, ≥ 80% coverage, lint, SAST/SCA/secret-scan — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- CC-ORD-002 names only two concrete validations (Japanese address structure, Indian PIN codes). The precise validation rules for the other markets (e.g., US ZIP vs. ZIP+4, DE PLZ length, ES/MX postal-code formats, required-field sets per market) are not enumerated in the specs and need definition before schemas are final.
- Whether address validation is purely format/schema-based or should verify deliverability against an external source is unspecified; no address-verification vendor is named in ARCHITECTURE.md, and EasyPost's confirmed role is carrier aggregation (CC-FUL-002), not address validation. Do not adopt a vendor without a human decision.
- The exact structured field set for the Japanese address model (e.g., postal code, prefecture, municipality, block/building, recipient) is not specified beyond "Japanese address structure"; the field inventory needs ratification.
