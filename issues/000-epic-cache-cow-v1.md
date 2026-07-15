# 000 · Epic: Cache Cow v1 platform build-out

## Objective

Build the Cache Cow platform defined in REQUIREMENTS.md §1: a multi-market direct-to-consumer and B2B commerce platform for frozen BBQ products with regional catalogs and pricing across six launch markets (US, ES, MX, DE, JP, IN) and seven locales, grocery wholesale distribution, a versioned B2B ordering API, and an internal operations dashboard covering sales, orders, invoices, and employee management — implemented on the confirmed stack (ASP.NET Core / .NET 10 modular monolith on AKS, Angular clients with SSR, Azure PostgreSQL Flexible Server, Microsoft Entra, Stripe/Razorpay, per ARCHITECTURE.md), under the OWASP ASVS 5.0 Level 2 baseline and controls authored in SECURITY.md, and the design language of DESIGN.md. This epic decomposes the ratified v1.3 requirements (every CC-* ID) into the single-responsibility sub-issues below; each sub-issue cites the CC-* requirements it implements per REQUIREMENTS.md §17.

## Metadata
- **ID**: CC-EPIC-001
- **Title**: Cache Cow v1 platform build-out
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The platform MUST implement all ratified v1.3 requirements in REQUIREMENTS.md (CC-MKT, CC-CAT, CC-PRC, CC-ORD, CC-FUL, CC-WHS, CC-API, CC-I18N, CC-CNT, CC-DSH, CC-INV, CC-SEC, CC-CMP, CC-NFR, CC-QA), within the boundaries fixed by ARCHITECTURE.md and the controls authored in SECURITY.md.
- **Rationale**: REQUIREMENTS.md v1.3 is ratified (all open assumptions resolved 2026-07-15 except the reopened items listed under Open Questions below); this epic is the traceable work breakdown required by REQUIREMENTS.md §17.
- **Design**: DESIGN.md governs all user-facing surfaces; each UI sub-issue cites its sections.

## Scope
- **Applies To**: Both
- **Components**: All ten bounded contexts (ARCHITECTURE.md, "Server bounded contexts"), all three Angular clients, infrastructure and CI/CD.
- **Actors**: Consumers (incl. guests), wholesale partner buyers, B2B API clients, internal staff (sales-viewer, ops-agent, finance, hr-admin, admin).
- **Data Classification**: Restricted/PII and Regulated (PCI DSS SAQ A scope, GDPR, DPDP, APPI, CCPA/CPRA)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: THREAT_MODEL.md (2026-07-15) findings, folded into SECURITY.md v1.1 / REQUIREMENTS.md v1.3 (CC-SEC-013–022).
- **Trust Boundary**: All boundaries fixed in ARCHITECTURE.md: public gateways (storefront, portal, B2B API), inbound processor-webhook receiver, CMS/translation/carrier inputs, dashboard origin.
- **Zero Trust Consideration**: Per SECURITY.md Input validation rule 1, every input crossing a trust boundary is treated as attacker-controlled; sub-issues enforce this per surface.

## Standards Alignment
- **OWASP ASVS**: 5.0 Level 2 baseline, platform-wide (SECURITY.md, Baseline).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: N/A (per-sub-issue)
- **NIST SP 800-207**: N/A (per-sub-issue)
- **Regulatory**: PCI DSS (SAQ A delegation), GDPR, DPDP Act 2023, APPI, CCPA/CPRA, EU FIC, FSSAI, Preisangabenverordnung — per sub-issue.
- **Other**: RFC 9700, RFC 8705, RFC 9449, RFC 9457, RFC 8058, FIDO2/WebAuthn, BCP 47, ICU MessageFormat.

## Acceptance Criteria

1. **AC-01**: Given the sub-issue list below, when all sub-issues are closed, then every CC-* requirement in REQUIREMENTS.md v1.3 is implemented or explicitly tracked as blocked under an Open Question, with the requirements-to-tests coverage report (REQUIREMENTS.md §17) showing no unimplemented ratified requirement.
2. **AC-02**: Given any merged change under this epic, when the merge gates run, then CC-QA-001/002 and SECURITY.md Deployment rule 7 gates pass (tests, ≥80% coverage, lint, SAST/SCA/secret-scan, no raw-HTML sinks, human review).
3. **AC-03**: No sub-issue resolves an item listed in ARCHITECTURE.md "Known unknowns" — those remain open until a human decides (CLAUDE.md working rules).

## Failure Behavior
- **On Invalid Input**: N/A (epic; per sub-issue)
- **On System Error**: N/A (epic; per sub-issue — fail closed on authorization, gating, and money paths per SECURITY.md Logging rule 2)
- **Alerting**: N/A (epic)

## Test Strategy
- **Unit Tests**: Per sub-issue; ≥80% line coverage per package (CC-QA-001).
- **Integration Tests**: Market-gating matrix (CC-QA-003), money-path suite (CC-QA-004), cross-tenant/cross-role authz suite (CC-QA-005) run on every merge.
- **Security Tests**: SECURITY.md Deployment rules 7–8; DAST per release and annual pentest (CC-QA-007).
- **Compliance Tests**: i18n CI (CC-QA-006); requirement-tag coverage report (REQUIREMENTS.md §17).
- **Coverage Target**: ≥ 80% per package (CC-QA-001)

## Dependencies
- **Upstream**: None (root epic).
- **Downstream**: All sub-issues below.
- **External**: Stripe, Razorpay, EasyPost, Contentful, Microsoft Entra, Azure (AKS, Key Vault, PostgreSQL Flexible Server, Communication Services, Monitor).

## Implementation Notes
- **Constraints**: Repo is bootstrap — no code, build system, or CI exists yet (CLAUDE.md); foundation issues 001–015 land first. Every PR references the CC-* IDs it implements (REQUIREMENTS.md §17).
- **Anti-Patterns**: Resolving `[ASSUMPTION]`/"Known unknowns" items without a human decision; restating canonical-document rules outside their owning document; scattered market conditionals instead of the single gating enforcement point (ARCHITECTURE.md Dependency rule 1).
- **AI Development Guidance**: All AI-generated code passes the identical merge gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md Deployment rule 7).

## Sub-issues

Suggested implementation order follows the numbering: foundations (001–015), platform security (016–022), gating (023–028), domain services (029–062), user surfaces (063–087), compliance and quality (088–100).

<!-- SUBISSUE-TASKLIST -->

## Open Questions

These stay open until a human decides (CLAUDE.md working rules; ARCHITECTURE.md "Known unknowns" is the single home for open decisions). Sub-issues that depend on one are marked BLOCKED or AT RISK.

Each sub-issue additionally carries its own finer-grained `## Open Questions` section; the items below are the cross-cutting ones.

From ARCHITECTURE.md "Known unknowns" (reopened 2026-07-15 by the threat model):
1. **Data residency vs. single primary write region** — unresolved conflict; blocks production persistence topology for EU/India personal data. AT RISK: issues 015, 024, 044, 046, 048, 087, 089, 090.
2. **Cross-border transfer mechanism for processors** (CC-CMP-006) — blocks issue 094 entirely; AT RISK: 040, 043, 045, 047, 048.
3. **Telemetry & backup residency** (CC-CMP-003/006) — AT RISK: issues 022, 081, 090, 095.
4. **Wholesale-portal identity provider** (CC-SEC-019, CC-WHS-005) — blocks issue 051; AT RISK: 052.

Details the specs do not provide (surfaced here, not guessed; new decisions belong in ARCHITECTURE.md "Known unknowns"):
5. **No delivery/security tooling is named anywhere in the specs**: CI platform, Terraform state backend, policy-as-code engine (SECURITY.md says only "e.g., OPA"), IaC scanner, GitOps reconciler, image-signing/provenance toolchain, admission controller, container registry, SAST/SCA/secret-scan/DAST tools, mutation-testing and a11y tooling (issues 006, 008–012, 098–100).
6. The Azure WAF/DDoS service fronting public ingress is not specified (issue 013), nor is any edge/CDN product despite the regional-cache topology ARCHITECTURE.md describes (issue 028).
7. The WORM/retention-locked storage service for the audit/invoice stream is not specified (issue 081).
8. CC-ORD-004 names minimum payment methods for US/DE/ES/JP/IN but none for MX (issue 039), and CC-INV-001 enumerates invoice tax content for US/EU/JP/IN but not MX (issue 047).
9. Legal entities per market, needed for sequential invoice numbering (CC-INV-001), are not enumerated (issues 046, 047).
10. The CAPTCHA-equivalent abuse control for the contact form is not specified, and any third-party script must be reconciled with the CSP and the ban on third-party runtime CDNs (issue 076).
11. The store-locator map provider and partner-location data source are not specified; map tiles/scripts are in tension with SECURITY.md Deployment rule 10 — the map is excluded from issue 078's acceptance criteria pending a decision.
12. DESIGN.md §11 mentions wholesale "PO upload" but no CC-* requirement authors it (issue 052).
13. The mapping from the internal order state machine (CC-ORD-006) plus carrier events to the five consumer tracking stages (CC-ORD-008, DESIGN.md §7) — especially which states produce "Smoked" and "Frozen" — is defined nowhere (issue 070).
14. No bounded context in ARCHITECTURE.md owns the cart (issue 068), and CC-MKT-003 gates "recommendations" as an IN-exclusion surface although no recommendation feature is specified anywhere (issues 025, 071).
15. PostgreSQL FTS is confirmed, but no ja-JP/hi-IN tokenization mechanism is named despite CC-CAT-005's quality bar (issue 031).
16. No requirement defines a consumer account surface beyond authentication and object-level access to own orders (e.g., an order-history page) — deliberately excluded from storefront issues rather than invented.
17. The CC-DSH-002 role–permission matrix content itself (which role holds which module permission, e.g. partner approval, cross-region fulfillment override) is not authored anywhere and needs human authoring under issue 080.

Cross-document tensions to flag (per CLAUDE.md: raised, not silently resolved):
18. CC-PRC-003 permits "integer minor units **or** exact decimal type" while ARCHITECTURE.md Dependency rule 9 fixes the shared-kernel money type to "integer minor units" (issue 002).
19. CC-MKT-007 requires the full catalog only for US/ES/DE, while DESIGN.md §8.5 describes JP as "full catalog" (issue 025 applies REQUIREMENTS.md precedence and flags the discrepancy).
20. SECURITY.md HTTP boundary rules 4 and 6 and Authentication rule 1 have no dedicated CC-SEC-* pointer in REQUIREMENTS.md §12 (issues 018, 020 anchor to the nearest existing IDs and flag the traceability gap).
