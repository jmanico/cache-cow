# 077 · Legal pages per market (versioned; DE Impressum/Widerruf)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CNT-005, CC-FUL-003
- **Title**: Legal pages per market (versioned; DE Impressum/Widerruf)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Compliance

## Requirement
- **Description**: The platform MUST serve versioned legal pages per market — privacy policy, terms, and shipping and returns in every market, plus Impressum and Widerrufsbelehrung in DE — in the active locale, and the DE withdrawal text MUST accurately state the perishable-frozen-food exemption from the standard 14-day withdrawal right.
- **Rationale**: CC-CNT-005 [P1] mandates the per-market legal page set, served per locale and versioned. CC-FUL-003 requires the DE Widerrufsrecht exception to be stated accurately: perishable frozen food is exempt from the standard 14-day withdrawal right, with legal texts per legal review. The drafted legal texts were accepted 2026-07-15 with later legal review to run against implemented behavior (ARCHITECTURE.md, "Decision record (2026-07-15)": the five `[legal review required]` flags including CC-FUL-003 "accepted as drafted"). DESIGN.md §8.4 makes Impressum, Widerrufsbelehrung, and detailed allergen/nutrition links first-class DE footer items, not buried. Each market's legal content set is part of the Market & Gating Policy configuration (ARCHITECTURE.md, bounded context 1: "currency/tax convention, legal content set").
- **Design**: DESIGN.md §8.4 (DE: formal "Sie" address in commerce and legal; Impressum/Widerrufsbelehrung as first-class footer items); §10 (page inventory: "Contact, FAQ, shipping policy, legal — Standard; DE per 8.4"); §5.4 (pun budget: zero puns in legal content); §9 (voice); §3.1 (Paper background for long-form reading).

## Scope
- **Applies To**: Web App
- **Components**: Storefront legal pages (privacy policy, terms, shipping and returns; DE additionally Impressum and Widerrufsbelehrung); per-market legal-content-set resolution via the Market & Gating Policy configuration (ARCHITECTURE.md, bounded context 1); content rendering via the Content & Localization module (bounded context 10) through the sanitizing allowlist renderer (issue 072); legal-content versioning. Explicitly excluded: cookie/consent management UI (issue 088), data-subject rights endpoints (issue 089), marketing-consent flows (issue 092), the footer/navigation frame itself (issue 063 — this issue supplies the DE footer-item requirement it must satisfy), authoring the legal texts (accepted as drafted; legal review is external).
- **Actors**: Consumer storefront visitors in all six markets; legal/compliance staff maintaining texts
- **Data Classification**: Public (published legal content)

## Security Context
- **Defense Layer**: Architecture (per-market policy-driven content set; versioned immutable-by-reference legal text)
- **Threat(s) Addressed**: Compliance failure from serving the wrong market's legal set or an outdated/altered legal text (GDPR transparency obligations, DE Impressum/Widerruf statutory duties per CC-CNT-005/CC-FUL-003); stored XSS via legal content if authored through the CMS (CWE-79, mitigated by issue 072's allowlist renderer)
- **Trust Boundary**: Server-side rendering boundary — the market's legal content set is resolved from server-side transacting-market state (CC-SEC-012; SECURITY.md, Authentication rule 10), never from client hints
- **Zero Trust Consideration**: Legal content, wherever authored, crosses the CMS/content trust boundary and renders only through the sanitizing allowlist renderer (SECURITY.md, Input validation rules 1 and 5); versioning ensures the served text is an identifiable, unaltered version.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V4 Access Control (server-side market-scoped content resolution); V1 Encoding and Sanitization (inherited via issue 072); platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline"
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: CM-3 (configuration change control — versioned legal content)
- **NIST SP 800-207**: N/A
- **Regulatory**: GDPR (CC-CMP-001 — privacy policy for ES/DE and EU visitors); DE Widerrufsrecht perishable-goods exemption (CC-FUL-003); market privacy regimes referenced by CC-CMP-002 (DPDP, APPI, CCPA/CPRA) insofar as their notices are carried by these pages
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given each of the six markets, when its legal pages are requested, then privacy policy, terms, and shipping and returns are all served, in the active locale, with content belonging to that market's legal content set (CC-CNT-005; ARCHITECTURE.md, bounded context 1).
2. **AC-02**: Given the DE market, when the storefront footer renders, then Impressum, Widerrufsbelehrung, and detailed allergen/nutrition links are present as first-class footer items, and both pages are served (CC-CNT-005; DESIGN.md §8.4).
3. **AC-03**: Given the DE Widerrufsbelehrung, when its content is compared against the accepted drafted text, then it states the perishable-frozen-food exemption from the 14-day withdrawal right exactly as accepted 2026-07-15 (CC-FUL-003; ARCHITECTURE.md decision record).
4. **AC-04**: Given any legal page, when it is served, then the response identifies the version of the legal text being served, and publishing a change produces a new version rather than silently mutating the served text (CC-CNT-005: "versioned").
5. **AC-05**: Given a session whose transacting market is US (or any non-DE market), when legal navigation renders, then Impressum and Widerrufsbelehrung do not appear, and no market is served another market's legal content set (CC-CNT-005). (Negative case.)
6. **AC-06**: Given DE legal and commerce copy, when it is reviewed, then it uses formal address ("Sie") throughout (DESIGN.md §8.4), and given any legal page in any locale, then it contains zero cache/tech puns (DESIGN.md §5.4). (Negative case.)
7. **AC-07**: Given legal content containing markup outside the allowlist, when a legal page renders, then that markup never reaches the response — all legal content renders through issue 072's sanitizing allowlist renderer (CC-SEC-002). (Negative case.)
8. **AC-08**: Given any cacheable legal page, when it is cached at SSR/edge/CDN, then the cache key derives from server-side transacting market + locale so one market's legal text is never served to another (CC-MKT-009; SECURITY.md, HTTP boundary rule 10).

## Failure Behavior
- **On Invalid Input**: N/A for visitor input (read-only pages). A request for a legal page not in the transacting market's legal content set returns HTTP 404 (hardening default per SECURITY.md, Authentication rule 9; semantics per issue 026).
- **On System Error**: Fail closed — if the market's legal content set or the versioned text cannot be resolved, the page returns a generic RFC 9457 error (issue 021) rather than falling back to another market's text or an unversioned draft (SECURITY.md, Logging rule 2 posture for gating-path failures).
- **Alerting**: Content-resolution failures on legal routes log as structured events to centralized monitoring (SECURITY.md, Logging rule 3); per-market synthetic checks (CC-NFR-003, issue 096) can assert legal-page availability per market.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for legal-content-set resolution per market (correct page set in, wrong-market page → not found); version resolution (latest version served, version identifier present).
- **Integration Tests**: SSR integration tests across all six markets × launch locales: page-set matrix (AC-01/02/05), DE footer items, version identifier presence, market/locale cache-key behavior with issue 028's patterns.
- **Security Tests**: Hostile-markup payload tests through the renderer path (AC-07); the raw-HTML-sink CI grep gate (issue 006).
- **Compliance Tests**: Snapshot test pinning the DE Widerrufsbelehrung to the accepted drafted text (AC-03) so any change is an explicit, reviewed diff; evidence retained for the later legal review against implemented behavior (ARCHITECTURE.md decision record).
- **Coverage Target**: ≥ 80% (CC-QA-001); tests tagged CC-CNT-005, CC-FUL-003 per REQUIREMENTS.md §17.

## Dependencies
- **Upstream**: 023 "Market & Gating Policy: policy-as-data model" (the per-market legal content set); 024 "Transacting market/locale resolution"; 072 "Contentful integration and sanitizing allowlist renderer" (rendering path); 063 "Storefront SSR shell" (footer frame carrying the DE first-class items); 064 "ICU MessageFormat resource pipeline" (page chrome strings).
- **Downstream**: 088 "EU consent management" and 092 "Marketing email consent" (link to/depend on the privacy policy these pages serve); 069 "Checkout UI per market" (DE checkout references Widerrufsbelehrung content); 096 "Per-market synthetic probes" (may assert legal-page availability).
- **External**: Contentful (if legal texts are CMS-authored — see Open Questions); external legal review runs later against implemented behavior (ARCHITECTURE.md decision record).

## Implementation Notes
- **Constraints**: The market→legal-content-set mapping lives in Market & Gating Policy configuration data, not scattered conditionals (CC-MKT-006 pattern; ARCHITECTURE.md, bounded context 1); rendering goes through the Content & Localization module and issue 072's allowlist renderer; long-form reading surfaces use Paper background (DESIGN.md §3.1); issued legal versions should be treated like other immutable records — corrections are new versions, never mutations (consistent with ARCHITECTURE.md, Dependency rule 6's append-only posture).
- **Anti-Patterns**: Silently editing a published legal text in place (defeats "versioned", CC-CNT-005); serving a fallback market's legal text on resolution failure; burying the DE Impressum/Widerrufsbelehrung in secondary navigation (DESIGN.md §8.4); free-text improvisation of the Widerruf exemption wording instead of the accepted drafted text (CC-FUL-003); puns or informal address in DE legal content (DESIGN.md §5.4, §8.4); raw-HTML sinks (SECURITY.md, Input validation rule 5).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). AI MUST NOT draft or alter legal text content; texts are accepted as drafted and changes go through legal review (ARCHITECTURE.md decision record).

## Open Questions
- The authoring source for legal texts is unspecified: ARCHITECTURE.md puts the "legal content set" in Market & Gating Policy configuration while CMS content belongs to Contentful — whether legal texts live in Contentful, in repo-versioned resources, or elsewhere needs a decision.
- The versioning mechanism and its user-facing surface (effective-date display? archive of prior versions accessible to users? retention of superseded versions) are not specified beyond "versioned" (CC-CNT-005).
- Which locales each market's legal texts must be served in, and which language version is legally binding per market, is unspecified (CC-CNT-005 says "served per locale"; the later legal review presumably resolves binding-language questions).
- Whether the privacy policy content must enumerate the CC-CMP-006 cross-border transfer mechanisms is entangled with the open Known-unknown decisions on transfer mechanism and residency (ARCHITECTURE.md, "Known unknowns"); page plumbing here does not depend on them, but final privacy-policy content may — content updates would land as new versions once issue 094's blocked decision resolves.
