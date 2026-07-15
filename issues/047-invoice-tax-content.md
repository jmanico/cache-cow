# 047 · Per-market invoice tax content

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** ARCHITECTURE.md "Known unknowns" — *Cross-border transfer mechanism for processors*. Tax computation flows through Stripe (Stripe Tax) and Razorpay, both named processors of EU/IN personal data whose lawful transfer basis is not decided or documented (CC-CMP-006). The tax-content model and rendering can proceed; production processor data flows for EU/IN await that human decision. Not resolved here.

## Metadata
- **ID**: CC-INV-001
- **Title**: Per-market invoice tax content
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Compliance

## Requirement
- **Description**: Every invoice MUST carry its market's legally required tax content — US sales tax lines; EU VAT with rates and USt-IdNr.; JP consumption tax with qualified-invoice number; IN GST with GSTIN and HSN codes — rendered onto the immutable invoice core at issuance (REQUIREMENTS.md CC-INV-001).
- **Rationale**: CC-INV-001 mandates invoices "per market legal requirements" with these exact tax elements. Tax computation authority per market is confirmed: Stripe Tax for US/ES/MX/DE/JP; IN GST handled with Razorpay/local accounting rules (ARCHITECTURE.md, "Technology decisions", Payments; decision record "Tax/VAT approach"). The drafted formats were accepted 2026-07-15 with later legal review running against implemented behavior (ARCHITECTURE.md decision record, `[legal review required]` flags). Display conventions interlock: US tax-exclusive with estimated tax at checkout; DE/ES/MX/JP/IN tax-inclusive, with IN carrying an explicit GST line on the invoice and MX IVA-inclusive (CC-PRC-002).
- **Design**: Monetary amounts render locale-formatted per DESIGN.md §4.4 (never hand-formatted — CC-PRC-004); invoice numerals in IBM Plex Mono on UI surfaces (DESIGN.md §4.1). Document layout/delivery is issue 048 (consumer PDF) and issue 086 (dashboard print stylesheet, DESIGN.md §12).

## Scope
- **Applies To**: API
- **Components**: Invoicing bounded context (tax-content composition at issuance, on the issue-046 core); Ordering & Payments (tax amounts from the payment/tax flow: Stripe Tax via issue 039, Razorpay via issue 040); Pricing (tax-inclusion conventions per CC-PRC-002, issue 034).
- **Actors**: Consumer and wholesale invoice recipients; Stripe and Razorpay as external tax/payment processors; finance staff (issue 086).
- **Data Classification**: Restricted/PII and Regulated (tax registration identifiers, buyer identity, legal financial records)

## Security Context
- **Defense Layer**: Strict API (server-side composition from canonical data)
- **Threat(s) Addressed**: STRIDE Tampering — client-influenced tax amounts or identifiers entering a legal document; the server recomputes all prices, discounts, taxes, and totals at order submission from canonical data, ignoring client-supplied values (CC-PRC-005).
- **Trust Boundary**: Processor integration boundary — tax data returned by Stripe/Razorpay enters via the server-to-server integrations (issues 039/040), with payment state driven only by signature-verified webhooks (SECURITY.md, Input validation rule 11); nothing tax-related is accepted from the browser.
- **Zero Trust Consideration**: Tax content on an invoice derives exclusively from server-side canonical order data and server-initiated processor interactions; client-supplied prices or tax figures are ignored (CC-PRC-005; SECURITY.md, Input validation rule 3 — server-controlled fields set from server state only).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline applies in full (SECURITY.md, Baseline); Input Validation and Business Logic chapters (server-side authority over computed values).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (input validation on processor-returned data at the integration boundary)
- **NIST SP 800-207**: N/A
- **Regulatory**: EU VAT (rates and USt-IdNr.), JP consumption tax qualified-invoice number, IN GST (GSTIN, HSN codes), US sales tax — as named in CC-INV-001; IN GST line on invoice per CC-PRC-002. Formats accepted as drafted 2026-07-15; later legal review runs against implemented behavior (ARCHITECTURE.md decision record).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a US-market order, when its invoice is issued, then it carries US sales tax line(s) computed via Stripe Tax, consistent with US tax-exclusive display convention (CC-INV-001; CC-PRC-002; ARCHITECTURE.md, Payments).
2. **AC-02**: Given an ES- or DE-market order, when its invoice is issued, then it carries EU VAT lines with the applied rate(s) and the USt-IdNr. element required by CC-INV-001, with amounts from Stripe Tax (CC-INV-001).
3. **AC-03**: Given a JP-market order, when its invoice is issued, then it carries JP consumption tax with the qualified-invoice number (CC-INV-001), amounts in JPY zero-decimal minor units (CC-PRC-003; CC-QA-004).
4. **AC-04**: Given an IN-market order, when its invoice is issued, then it carries GST lines with GSTIN and per-line HSN codes, with the GST line explicit on the tax-inclusive invoice (CC-INV-001; CC-PRC-002), computed via Razorpay/local accounting rules (ARCHITECTURE.md, Payments).
5. **AC-05**: Given any invoice, when tax amounts are computed and stored, then they are server-recomputed from canonical data in integer minor units with overflow-checked arithmetic; client-supplied values are ignored (CC-PRC-005; CC-PRC-003).
6. **AC-06**: Given any invoice presentation, when amounts render, then formatting is locale-aware (server equivalent of `Intl.NumberFormat`) including INR lakh/crore grouping and JPY no-decimals — hand-formatted currency strings are a defect (CC-PRC-004; DESIGN.md §4.4).
7. **AC-07**: Negative: tax content MUST NOT be added, changed, or recomputed on an invoice after issuance — corrections go through credit notes on the issue-046 core (CC-INV-001; ARCHITECTURE.md, Dependency rule 6).

## Failure Behavior
- **On Invalid Input**: Processor-returned tax data failing schema validation at the integration boundary is rejected; issuance does not proceed with partial or unvalidated tax content (SECURITY.md, Input validation rule 1); RFC 9457 problem details on API-facing failures (issue 021).
- **On System Error**: Fail closed — if tax content cannot be composed (processor error, missing registration identifier for the legal entity), the invoice is not issued; no invoice may be issued missing its market's mandatory tax elements (CC-INV-001; SECURITY.md, Logging rule 2 for money paths).
- **Alerting**: Tax-composition failures and processor integration errors logged as structured events with correlation IDs and alerted (SECURITY.md, Logging rules 1, 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests per market composing tax content from canonical fixtures: US lines, EU VAT rate/USt-IdNr. presence, JP qualified-invoice number presence, IN GSTIN/HSN presence; rounding in all five currencies including JPY zero-decimal and INR grouping (CC-QA-004). Tagged CC-INV-001 (REQUIREMENTS.md §17).
- **Integration Tests**: Stubbed Stripe Tax / Razorpay integration tests asserting server-recomputation authority (client-supplied tax ignored — CC-PRC-005) and fail-closed issuance when tax data is unavailable.
- **Security Tests**: Attempted client-side injection of tax amounts/identifiers into order submission is ignored/rejected (CC-PRC-005; SECURITY.md, Input validation rule 3).
- **Compliance Tests**: Golden-file assertions per market that every CC-INV-001 mandatory tax element is present on issued test invoices — automated evidence for the later legal review against implemented behavior (ARCHITECTURE.md decision record).
- **Coverage Target**: ≥ 80% per package (CC-QA-001); mutation testing SHOULD run on this money code (CC-QA-001); no floating-point money in tests (CC-PRC-003).

## Dependencies
- **Upstream**: 002 (Money type), 032 (per-SKU per-market price model), 034 (locale-aware formatting and tax-display conventions), 036 (server-side money recomputation), 039 (Stripe integration incl. Stripe Tax), 040 (Razorpay integration), 046 (invoice core — this issue renders onto it).
- **Downstream**: 048 (invoice PDF rendering), 086 (dashboard invoice management), 052 (wholesale invoice history).
- **External**: Stripe (Stripe Tax, US/ES/MX/DE/JP), Razorpay (IN GST) — confirmed vendors (ARCHITECTURE.md, Payments).
- **Open decision**: AT RISK on the cross-border transfer mechanism for Stripe/Razorpay (CC-CMP-006; ARCHITECTURE.md, "Known unknowns") — see blockquote.

## Implementation Notes
- **Constraints**: Composition happens inside the Invoicing bounded context at issuance time, reading canonical order/pricing data — money flows one way; only the Ordering service computes it from Pricing as canonical source (ARCHITECTURE.md, Dependency rule 2). All amounts via the shared Money type (integer minor units, overflow-checked — CC-PRC-003; issue 002). Processor calls authenticate with secrets from Key Vault only (SECURITY.md, Secret handling rules 1–5). Tax content is stored structured (typed fields per market), not as free text, so downstream rendering (048/086) works from structured data (consistent with CC-INV-002 and the CC-CAT-004 structured-data principle).
- **Anti-Patterns**: MUST NOT accept client-supplied tax amounts or registration identifiers (CC-PRC-005); MUST NOT hand-format currency strings (CC-PRC-004); MUST NOT mutate issued invoices to fix tax content (ARCHITECTURE.md, Dependency rule 6); MUST NOT perform runtime FX conversion of consumer prices (CC-PRC-001); MUST NOT let "Eviction Specials" presentation naming reach line-item legal descriptions (CC-PRC-007).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-INV-001 (REQUIREMENTS.md §17).

## Open Questions
- **MX invoice tax content is not enumerated.** MX is a launch market with IVA-inclusive pricing (CC-PRC-002) and Stripe Tax coverage (ARCHITECTURE.md), but CC-INV-001's tax-element list (US, EU VAT, JP, IN) names no MX invoice requirements (e.g., IVA lines, CFDI). What MX invoices must carry is unspecified.
- **HSN code source is unspecified.** CC-INV-001 requires per-line HSN codes for IN, but the SKU field list in CC-CAT-001 does not include an HSN code field; where HSN codes live and who maintains them is undefined.
- **Seller tax registration identifiers are not enumerated.** The USt-IdNr., JP qualified-invoice registration number, and GSTIN values belong to legal entities, which are themselves not enumerated in the specs (see issue 046's open question).
- CC-INV-001 says "EU VAT with rates and USt-IdNr." — whether ES invoices carry a Spanish VAT identifier (the spec names only the German USt-IdNr.) is unspecified.
- Whether the seller's GSTIN, the buyer's GSTIN (for wholesale partners, captured per CC-WHS-002), or both must appear on IN invoices is not stated.
