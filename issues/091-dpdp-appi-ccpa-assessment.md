# 091 · DPDP/APPI/CCPA obligations assessment

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CMP-002
- **Title**: DPDP/APPI/CCPA obligations assessment
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The platform team MUST produce a written assessment of India DPDP Act 2023 and Japan APPI obligations for their markets and of US state privacy law obligations (CCPA/CPRA at minimum) for US consumers, whose resulting requirements are ratified into REQUIREMENTS.md by a human before implementation (REQUIREMENTS.md CC-CMP-002; CLAUDE.md, Working rules).
- **Rationale**: CC-CMP-002 requires these obligations to be "assessed and implemented"; this issue is the assessment leg. Per CLAUDE.md working rules, new requirements cannot be silently added — conflicts and new rules are raised for human decision and land in the owning canonical document. The deliverable is therefore a written assessment plus proposed CC-* requirement drafts for human ratification into REQUIREMENTS.md, not direct implementation. (The 2026-07-15 decision record notes CC-CMP-002's `[legal review required]` flag was accepted as drafted, with later legal review running against implemented behavior — ARCHITECTURE.md, Decision record.)
- **Design**: N/A (documentation deliverable; any user-facing surfaces it proposes will cite DESIGN.md in their own ratified requirements).

## Scope
- **Applies To**: Both
- **Components**: Assessment covers all subsystems processing IN, JP, or US consumer personal data: consumer storefront, Ordering & Payments, Identity & Access, Content & Localization (marketing/email), Back Office, observability pipeline
- **Actors**: Compliance/legal reviewers (human decision-makers), spec maintainers; indirectly: IN/JP/US consumers whose rights the obligations concern
- **Data Classification**: Restricted/PII (the assessment's subject matter); the document itself is Internal

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Regulatory exposure from unassessed obligations in three launch jurisdictions; scope creep / unratified requirements entering the codebase (REQUIREMENTS.md §17 treats unreferenced code paths as scope creep)
- **Trust Boundary**: N/A (documentation deliverable; it maps where personal data crosses trust boundaries per jurisdiction rather than enforcing one)
- **Zero Trust Consideration**: N/A for the document itself; the assessment MUST evaluate each obligation against the platform's existing zero-trust controls (SECURITY.md) rather than assuming compliance.

## Standards Alignment
- **OWASP ASVS**: N/A (documentation deliverable; implementing issues that result will cite ASVS 5.0 Level 2 per SECURITY.md, Baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A
- **NIST SP 800-207**: N/A
- **Regulatory**: India DPDP Act 2023; Japan APPI; CCPA/CPRA at minimum for US consumers (REQUIREMENTS.md CC-CMP-002); intersects India data residency (ARCHITECTURE.md, Technology decisions: India data resident in India) and FSSAI/food labeling handled separately under CC-CMP-004
- **Other**: N/A

## Acceptance Criteria

1. **AC-01**: Given the assessment deliverable, when the issue closes, then a versioned written assessment exists in the repository covering, per jurisdiction (IN DPDP 2023, JP APPI, US CCPA/CPRA), the obligations applicable to Cache Cow's processing in that market, mapped to the platform's data classes and bounded contexts (CC-CMP-002; ARCHITECTURE.md, Server bounded contexts).
2. **AC-02**: Given the assessment, when it identifies an obligation not already satisfied by an existing CC-* requirement, then it records a proposed requirement draft (RFC 2119 form, proposed CC-* placement) marked as awaiting human ratification — and does NOT modify REQUIREMENTS.md or any canonical document itself (CLAUDE.md, Working rules).
3. **AC-03**: Given the assessment, when it maps existing coverage, then each covered obligation cites the specific existing CC-* ID or SECURITY.md rule that satisfies it (e.g., data-subject rights mechanics in CC-CMP-001/issue 089, retention in CC-CMP-003/issue 090), so no duplicate requirement is invented (CLAUDE.md, Canonical documents: each owns one domain).
4. **AC-04**: Given the assessment encounters a dependency on an open decision (e.g., India data residency vs. the single primary write region, ARCHITECTURE.md "Known unknowns"), when it documents that obligation, then it flags the dependency and does NOT propose a resolution (negative case; CLAUDE.md, Working rules — never resolve an open decision).
5. **AC-05**: Given the DPDP assessment, when it addresses India data residency, then it reconciles against the confirmed "India data resident in India" rule and the open write-region conflict, citing CC-CMP-006 and ARCHITECTURE.md "Known unknowns" rather than restating or resolving them.
6. **AC-06**: Given human ratification of the assessment's proposed requirements, when they are accepted, then they are added to REQUIREMENTS.md by that human decision process with new CC-* IDs, and follow-up implementation issues reference those IDs (REQUIREMENTS.md §17) — the assessment itself MUST NOT be treated as authorization to implement (negative case).

## Failure Behavior
- **On Invalid Input**: N/A (documentation deliverable; no runtime input).
- **On System Error**: N/A at runtime. Process failure mode: if an obligation cannot be conclusively assessed, it is recorded as an open question for legal review — never silently omitted or guessed (CLAUDE.md, Working rules; the specs' `[legal review required]` convention).
- **Alerting**: N/A at runtime. The assessment's unresolved items are surfaced to the human decision process via ARCHITECTURE.md "Known unknowns" conventions (raised, not resolved).

## Test Strategy
- **Unit Tests**: N/A (no code).
- **Integration Tests**: N/A (no code).
- **Security Tests**: N/A (no code).
- **Compliance Tests**: CI presence/structure check for the assessment document (each jurisdiction section present; every proposed requirement in RFC 2119 form with a ratification-status field; every coverage claim citing a CC-* ID or SECURITY.md rule). Traceability review: proposed requirements cross-referenced against the issue index so downstream issues (089, 090, 092, 094) can cite them once ratified.
- **Coverage Target**: N/A (no code; CC-QA-001 applies to the implementing issues that follow ratification).

## Dependencies
- **Upstream**: None hard; informed by 088 EU consent management and 089 Data-subject rights endpoints (CC-CMP-001 mechanics the assessment maps against) and by CC-CMP-003's data-class inventory (issue 090).
- **Downstream**: 090 Retention schedule and automated deletion jobs (jurisdictional retention obligations feed the schedule); 092 Marketing email consent and one-click unsubscribe (per-market consent law, CC-CMP-005); 094 Cross-border transfer mechanism documentation (India transfer/residency findings, CC-CMP-006); any new implementation issues created after human ratification.
- **External**: None (assessment references the confirmed processors — Stripe, Razorpay, EasyPost, Contentful, Microsoft Entra, Azure services — as processing context only).

## Implementation Notes
- **Constraints**: Deliverable is a repository document, versioned like the canonical specs; changes to rules go into the owning canonical document only via human ratification (CLAUDE.md, Working rules). The assessment must respect document ownership: privacy requirements belong in REQUIREMENTS.md §13, security controls in SECURITY.md — it proposes placements, never edits.
- **Anti-Patterns**: MUST NOT silently add, modify, or resolve requirements in any canonical document (CLAUDE.md, Working rules). MUST NOT resolve ARCHITECTURE.md "Known unknowns" (notably the India-residency/write-region conflict). MUST NOT duplicate or restate rules owned by another document — cite them (CLAUDE.md, Canonical documents). MUST NOT treat the accepted-as-drafted legal-review flag (ARCHITECTURE.md, Decision record) as license to skip the assessment.
- **AI Development Guidance**: If AI-drafted, the assessment gets mandatory human review before merge like all AI-generated work (CC-QA-002; SECURITY.md, Deployment rule 7), and its proposed requirements additionally require explicit human ratification into REQUIREMENTS.md before any implementation begins.

## Open Questions
- CC-CMP-002 says obligations are "assessed and implemented"; the split between this assessment issue and the follow-on implementation issues (created post-ratification) means implementation scope is intentionally undefined here — the ratification round must size it.
- Who the human ratifier is (legal counsel vs. spec owner) is not defined in the specs; ARCHITECTURE.md's decision record notes later legal review runs against implemented behavior, but the ratification workflow for new requirements needs an owner.
- Whether "US state privacy laws (CCPA/CPRA at minimum)" requires assessing additional state laws beyond CCPA/CPRA at launch is left to the assessment itself — the specs set only the floor.
- The DPDP assessment will intersect the open India-residency/write-region conflict (ARCHITECTURE.md, "Known unknowns"); its conclusions may be partially blocked until that human decision lands.
