# 043 · Transactional order emails in all locales

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Cross-border transfer mechanism for processors (ARCHITECTURE.md, "Known unknowns"; CC-CMP-006 names Azure Communication Services). Templates and sending logic can be built and tested, but production sending of EU/India customer personal data through ACS depends on the documented lawful transfer basis (issue 094). Not proposing a resolution.

## Metadata
- **ID**: CC-ORD-007, CC-I18N-006
- **Title**: Transactional order emails in all locales
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Functional

## Requirement
- **Description**: Customers MUST receive order confirmation and shipment notification emails in their locale via Azure Communication Services, from templates existing in all seven launch locales with fallback to the market's primary language (never a broken template), containing no more personal data than necessary (CC-ORD-007, CC-I18N-006).
- **Rationale**: CC-ORD-007 mandates locale-correct confirmation and shipment notifications with data minimization (no full address in the email body) because order and shipment mail is a prime phishing lure and a PII-disclosure channel (SECURITY.md, Email and messaging security rule 1). CC-I18N-006 mandates template coverage in all launch locales with primary-language fallback so no market ever receives a broken template. ARCHITECTURE.md confirms Azure Communication Services as the email provider (Technology decisions; decision record 2026-07-15).
- **Design**: DESIGN.md §9 (voice and microcopy: plain verbs, sentence case, active voice; translated copy written per market by native speakers, not translated puns); DESIGN.md §5.4 (comedy never touches money movement); typography/locale conventions per DESIGN.md §4 where templates carry formatted prices or dates (locale-formatted per CC-PRC-004/CC-I18N-003).

## Scope
- **Applies To**: Both (server-triggered; consumer-received)
- **Components**: Content & Localization bounded context (transactional email in all launch locales — ARCHITECTURE.md, Server bounded contexts #10); Ordering & Payments context (state transitions that trigger sends); Azure Communication Services (delivery)
- **Actors**: Consumer (guest or authenticated) in any of the six markets / seven locales; Ordering service as trigger; Azure Communication Services as external processor
- **Data Classification**: Restricted/PII (customer name, email address, order summary)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: PII over-disclosure in email bodies (CC-ORD-007); capability-token or secret leakage through logged email headers/metadata (SECURITY.md, Email and messaging security rule 1; Logging rule 4); phishing lures against Cache Cow customers — sender-domain authentication itself (SPF/DKIM/DMARC `p=reject`, CC-SEC-018) is issue 093, a hard companion control
- **Trust Boundary**: Platform ↔ Azure Communication Services (external processor); email content entering the customer's untrusted inbox environment
- **Zero Trust Consideration**: Email bodies are treated as a disclosure surface: minimum-necessary PII only, invoice data delivered as an authenticated link rather than attached content (CC-INV-002); translation resources feeding templates are untrusted input — ICU MessageFormat only, no HTML in string resources, interpolated values escaped by default (SECURITY.md, Input validation rule 7).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); data-protection chapter (minimizing sensitive data in communications)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-6 (least privilege for the ACS sending identity via managed identity), AU-2 (send/failure event logging)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR data minimization for ES/DE customers (CC-CMP-001); DPDP (IN) and APPI (JP) obligations per CC-CMP-002; transfer-basis documentation for ACS per CC-CMP-006 (issue 094)
- **Other**: SPF/DKIM/DMARC per SECURITY.md, Email and messaging security rule 1 — enforced by issue 093, referenced here as a dependency

## Acceptance Criteria
1. **AC-01**: Given an order reaches the `confirmed` state (CC-ORD-006), when the transition is processed, then an order-confirmation email is sent via Azure Communication Services in the customer's locale (CC-ORD-007).
2. **AC-02**: Given an order reaches the `shipped` state, when the transition is processed, then a shipment-notification email is sent in the customer's locale (CC-ORD-007).
3. **AC-03**: Given templates are inventoried in CI, when the check runs, then confirmation and shipment templates exist for all seven launch locales (en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN — CC-I18N-001) and the build fails on a missing template (CC-I18N-006).
4. **AC-04**: Given a customer locale whose template is unavailable at send time, when the email is generated, then it falls back to the market's primary language — a broken or partially-rendered template is never sent (CC-I18N-006).
5. **AC-05**: Given any confirmation or shipment email body, when inspected in tests, then it contains no full delivery address and no more personal data than necessary (CC-ORD-007). [Negative]
6. **AC-06**: Given logged email headers/metadata in any telemetry or ACS pipeline, when inspected, then they never contain capability tokens or secrets (SECURITY.md, Email and messaging security rule 1; Logging rule 4). [Negative]
7. **AC-07**: Given a consumer invoice email, when it is generated, then it delivers a link to authenticated download — never an attachment containing full address data (CC-INV-002; PDF rendering and the download endpoint are issue 048). [Negative for attachments]
8. **AC-08**: Given email template strings, when they are authored and built, then they are externalized ICU MessageFormat resources with no HTML in string resources and interpolated values escaped by default, schema-validated in CI (CC-I18N-002; SECURITY.md, Input validation rule 7).

## Failure Behavior
- **On Invalid Input**: Template-resolution failure falls back to the market's primary language (CC-I18N-006); if no valid template can be resolved, the send is aborted (never a broken template), logged with correlation ID, and alerted — the order state machine is unaffected.
- **On System Error**: ACS send failures are logged as structured events with correlation IDs and do not roll back or block the underlying order state transition; no PII or secrets in the failure logs (SECURITY.md, Logging rules 3–4). Fail-closed applies to content: when in doubt, send nothing rather than an over-disclosing or broken message.
- **Alerting**: Alert on send-failure spikes and on any template-resolution failure in production (SECURITY.md, Logging rule 3; CC-NFR-003 structured logs/metrics).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for template selection (locale → template, fallback to market primary language), ICU MessageFormat rendering per locale, and PII-minimization of the composed body (no full address); tagged `CC-ORD-007`, `CC-I18N-006`.
- **Integration Tests**: ASP.NET Core integration tests with an ACS stub: state-transition triggers (confirmed → confirmation mail, shipped → shipment mail), guest vs. account orders, and invoice mail carrying a link and no attachment (CC-INV-002).
- **Security Tests**: Log-content assertions that tokens/secrets never appear in email send logs or headers metadata (SECURITY.md, Email rule 1; Logging rule 4); translation-resource schema validation and placeholder-consistency checks in CI (CC-QA-006; SECURITY.md, Input validation rule 7).
- **Compliance Tests**: CI evidence of template presence across all seven locales (CC-I18N-006); body-content scan for address fields (CC-ORD-007).
- **Coverage Target**: ≥ 80% per package (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold; 014 Azure Key Vault / managed identity (ACS client auth per SECURITY.md, Secret handling rule 2); 022 Structured security logging; 035 Order state machine with audited transitions (send triggers); 041 Inbound processor webhook verification (verified events drive the state changes that trigger sends); 042 Guest order capability tokens (guest links embedded in mail); 064 ICU MessageFormat resource pipeline with CI validation. CC-I18N-001/002.
- **Downstream**: 048 Invoice PDF rendering and authenticated link-only delivery (invoice mail links into it); 093 Sender domain authentication (SPF/DKIM/DMARC `p=reject`, CC-SEC-018) — required before production sending; 092 Marketing email consent (separate scope: marketing mail is NOT this issue).
- **External**: Azure Communication Services.

## Implementation Notes
- **Constraints**: ACS client authenticated with managed identity (`DefaultAzureCredential`), no embedded keys or credentialed connection strings (SECURITY.md, Secret handling rule 2); email lives in the Content & Localization bounded context with its own least-privilege database role (SECURITY.md, Secret handling rule 10); prices/dates in templates use locale-aware formatting, never hand-formatted strings (CC-PRC-004, CC-I18N-003); user input never reaches SMTP headers (email header injection — SECURITY.md, Input validation rule 10).
- **Anti-Patterns**: MUST NOT include the full delivery address in any email body (CC-ORD-007); MUST NOT attach invoice PDFs (CC-INV-002); MUST NOT send a broken or partially-localized template (CC-I18N-006); MUST NOT put capability tokens or secrets in logged headers/metadata (SECURITY.md, Email rule 1); MUST NOT embed HTML in translation string resources or bypass ICU escaping (SECURITY.md, Input validation rule 7); MUST NOT hand-format currency in templates (CC-PRC-004).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-ORD-007/CC-I18N-006 (REQUIREMENTS.md §17).

## Open Questions
- The source of "their locale" (CC-ORD-007) is not defined: the locale selected at checkout vs. an account preference vs. the market's primary language for guests who never chose one.
- Whether the order-confirmation email is the delivery channel for the guest capability-token link (CC-ORD-010, issue 042) is implied but not explicitly stated in the specs.
- Retry/queueing policy for failed ACS sends is not specified (only logging/alerting behavior follows from SECURITY.md Logging rules).
- Per-market sender addresses/domains are not specified; they are entangled with issue 093 (SPF/DKIM/DMARC) and CC-CMP-006 residency for ACS.
