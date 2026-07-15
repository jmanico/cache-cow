# 094 · Cross-border transfer mechanism documentation

Part of the Cache Cow v1 build-out epic.

> **BLOCKED:** The lawful cross-border transfer basis for each processor is an open human decision — ARCHITECTURE.md, "Known unknowns": *Cross-border transfer mechanism for processors*, entangled with *Telemetry & backup residency* and *Data residency vs. "single primary write region"*. This issue specifies the deliverable's shape only; no transfer mechanism may be chosen or documented as decided until a human resolves those decisions. Do not propose a resolution.

## Metadata
- **ID**: CC-CMP-006
- **Title**: Cross-border transfer mechanism documentation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The cross-border transfer mechanism for every processor handling EU or India personal data (Stripe, Razorpay, EasyPost, Contentful, Microsoft Azure/Entra, Azure Communication Services) MUST be assessed and documented — adequacy decision, Standard Contractual Clauses, EU–US Data Privacy Framework, or equivalent — and telemetry/log and backup data residency MUST be reconciled with the EU-in-EU and India-in-India residency rules (REQUIREMENTS.md CC-CMP-006).
- **Rationale**: CC-CMP-006 was added by the 2026-07-15 threat model (THREAT_MODEL.md): the confirmed processors move EU/IN personal data across borders, and the lawful transfer basis is not decided or documented (ARCHITECTURE.md, "Known unknowns"). The requirement also forces reconciliation of telemetry/log and backup residency with the confirmed residency rules (EU data resident in EU, India data resident in India — ARCHITECTURE.md, Technology decisions; SECURITY.md, Secret handling rule 6), which exposed the unresolved primary-write-region-vs-residency conflict tracked in ARCHITECTURE.md "Known unknowns".
- **Design**: N/A (documentation deliverable).

## Scope
- **Applies To**: Both (documentation covering every data flow that sends EU or India personal data to a processor)
- **Components**: Documentation covering all processor integrations: Ordering & Payments (Stripe, Razorpay), Fulfillment (EasyPost), Content & Localization (Contentful, Azure Communication Services), Identity & Access (Microsoft Entra), observability and data-store infrastructure (Azure Monitor/Log Analytics, PostgreSQL backups/replicas)
- **Actors**: Compliance/legal reviewers (the human decision-makers for the transfer bases), spec maintainers, external processors
- **Data Classification**: Restricted/PII (the flows under assessment); the document itself is Internal

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Unlawful cross-border transfer of EU/India personal data (GDPR Chapter V-class and DPDP transfer exposure as encoded by CC-CMP-006); residency bypass via telemetry aggregation and backups (SECURITY.md, Secret handling rule 6 — a backup or replica is never a residency loophole)
- **Trust Boundary**: Outbound platform-to-processor boundaries — every integration where personal data leaves Cache Cow's runtime for a processor, plus intra-cloud flows (telemetry aggregation, backup/replica placement) that cross residency zones
- **Zero Trust Consideration**: N/A for the document itself; the deliverable maps each boundary crossing explicitly so no flow is assumed compliant without a documented basis.

## Standards Alignment
- **OWASP ASVS**: N/A (documentation deliverable; ASVS 5.0 Level 2 governs the implementing integrations per SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR (ES/DE and EU visitors — CC-CMP-001) and India DPDP (CC-CMP-002) transfer obligations as encoded by CC-CMP-006; candidate mechanisms named by the requirement: adequacy decision, Standard Contractual Clauses, EU–US Data Privacy Framework, or equivalent — selection is the blocked human decision
- **Other**: N/A

## Acceptance Criteria

All criteria describe the deliverable's required shape; completing them requires the blocked human decisions to land first.

1. **AC-01**: Given the deliverable, when it is reviewed, then it contains one entry per processor named by CC-CMP-006 (Stripe, Razorpay, EasyPost, Contentful, Microsoft Azure/Entra, Azure Communication Services), each recording: the personal-data categories transferred, the source residency zone (EU / India), the destination(s), and the documented lawful transfer mechanism (adequacy / SCCs / EU–US DPF / equivalent) once decided (CC-CMP-006).
2. **AC-02**: Given the deliverable, when it is reviewed, then it reconciles telemetry/log residency (Azure Monitor / Log Analytics aggregation) and backup/replica/snapshot residency (PostgreSQL Flexible Server) against the EU-in-EU and India-in-India rules, recording the topology once the open residency decisions are made (CC-CMP-006, CC-CMP-003; SECURITY.md, Secret handling rule 6; ARCHITECTURE.md, "Known unknowns").
3. **AC-03**: Given any processor entry whose transfer basis has not been humanly decided, when the document is published, then that entry is explicitly marked undecided/blocked with a pointer to ARCHITECTURE.md "Known unknowns" — it MUST NOT record a mechanism as decided (negative case; CLAUDE.md, Working rules).
4. **AC-04**: Given the deliverable, when cross-checked against the DPA documentation from issue 088 (CC-CMP-001) and the erasure-propagation processor list (CC-CMP-003, issues 089/090), then the processor inventories are consistent — no processor handling EU/IN personal data is absent from the transfer documentation (CC-CMP-006).
5. **AC-05**: Given a future new processor or a change in an existing processor's data flow, when it is introduced, then the document's maintenance rule requires a transfer-mechanism entry before the integration ships — the document is versioned and kept current, not a one-time artifact (CC-CMP-006).
6. **AC-06**: Given the document's treatment of the primary-write-region-vs-residency conflict, when it is reviewed, then the conflict is cited as tracked in ARCHITECTURE.md "Known unknowns" and is NOT resolved, restated as decided, or worked around in the documentation (negative case; CLAUDE.md, Working rules — flag conflicts, don't pick a side).

## Failure Behavior
- **On Invalid Input**: N/A (documentation deliverable; no runtime input).
- **On System Error**: N/A at runtime. Process failure mode: if a processor's transfer posture cannot be established, the entry is recorded as undecided and escalated to the human decision process — never guessed or omitted (CLAUDE.md, Working rules).
- **Alerting**: N/A at runtime. Gaps surface through the human review process; implementation issues that depend on an undecided entry inherit BLOCKED status.

## Test Strategy
- **Unit Tests**: N/A (no code).
- **Integration Tests**: N/A (no code).
- **Security Tests**: N/A (no code).
- **Compliance Tests**: CI presence/structure check for the document: all six named processors present, required fields per entry (data categories, source zone, destination, mechanism-or-blocked marker), telemetry/backup reconciliation section present; consistency check against the issue-088 DPA inventory.
- **Coverage Target**: N/A (no code; CC-QA-001 applies to implementing work that follows the decisions).

## Dependencies
- **Upstream**: **BLOCKED (repeat):** human decisions in ARCHITECTURE.md "Known unknowns" — *Cross-border transfer mechanism for processors*, *Telemetry & backup residency*, and *Data residency vs. "single primary write region"* — must land before any mechanism is documented as decided. Inputs: 088 EU consent management (DPA documentation, CC-CMP-001); 091 DPDP/APPI/CCPA obligations assessment (India transfer findings, CC-CMP-002); 090 Retention schedule (backup/telemetry data-class inventory, CC-CMP-003).
- **Downstream**: 089 Data-subject rights endpoints (processor propagation legs reference the documented mechanisms); any processor-integration issue touching EU/IN personal data (039 Stripe, 040 Razorpay, 057 Outbound partner webhooks, 043 Transactional emails, 072 Contentful integration, 095 OpenTelemetry observability) gains a documented transfer basis from this deliverable.
- **External**: Stripe, Razorpay, EasyPost, Contentful, Microsoft Azure/Entra, Azure Communication Services — the exact processor list named by REQUIREMENTS.md CC-CMP-006.

## Implementation Notes
- **Constraints**: Deliverable is a versioned repository document. Ownership boundaries per CLAUDE.md: the decision itself is recorded in ARCHITECTURE.md's decision record when made; this document carries the per-processor assessment and reconciliation detail that CC-CMP-006 requires. The residency rules it reconciles against are fixed inputs (EU-in-EU, India-in-India — ARCHITECTURE.md, Technology decisions; SECURITY.md, Secret handling rule 6), not variables this work may relax.
- **Anti-Patterns**: MUST NOT select a transfer mechanism for any processor (blocked human decision). MUST NOT resolve or paper over the write-region-vs-residency conflict. MUST NOT restate rules owned by other canonical documents — cite them (CLAUDE.md, Canonical documents). MUST NOT omit intra-cloud flows (telemetry, backups) on the grounds that they stay "inside Azure" — CC-CMP-006 names them explicitly.
- **AI Development Guidance**: If AI-drafted, the document gets mandatory human review before merge (CC-QA-002; SECURITY.md, Deployment rule 7), and no entry moves from blocked to decided without an explicit human decision recorded in ARCHITECTURE.md "Known unknowns"/decision record.

## Open Questions
- The lawful transfer basis per processor is the blocked open decision (ARCHITECTURE.md, "Known unknowns") — deliberately not answered here.
- Whether the deliverable also constitutes (or feeds) formal transfer-impact assessments is not specified by CC-CMP-006; the required depth per entry needs human definition.
- The document's repository location and its relationship to the issue-088 DPA records (single combined register vs. separate documents) is unspecified.
- Whether carrier data flows beyond EasyPost itself (EasyPost's downstream per-market carriers) fall within "processor handling EU or India personal data" needs human/legal confirmation; CC-CMP-006 names EasyPost only.
