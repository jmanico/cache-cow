# 029 · SKU domain model with structured food data

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-CAT-001, CC-CAT-004, CC-CMP-004
- **Title**: SKU domain model with structured food data
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional, Compliance

## Requirement
- **Description**: Every SKU MUST be modeled as structured data carrying a unique ID, localized name, veg/non-veg classification, cut/category, net weight, serving estimate, ingredients, allergens, nutrition per market format, storage and reheat instructions, and per-market availability flags, and allergen/nutrition information MUST render only from these structured fields, never from free-text CMS content (REQUIREMENTS.md CC-CAT-001, CC-CAT-004).
- **Rationale**: The veg/non-veg classification is the datum every market-gating decision keys on (REQUIREMENTS.md CC-MKT-003; enforcement point in issue 025) — an unstructured or missing classification collapses the IN compliance regime. Allergen and nutrition data are regulated food-safety information: structured fields are the single source for food-information compliance in every market — EU FIC, FSSAI (IN), US FDA, and JP labeling (REQUIREMENTS.md CC-CMP-004, CC-CAT-004). Free-text CMS content cannot be validated, localized reliably, or proven compliant.
- **Design**: N/A for this server-side domain model. Presentation of these fields is owned by downstream issues: veg marking per DESIGN.md §3.3 and product detail per DESIGN.md §10 (issues 066/067).

## Scope
- **Applies To**: Both
- **Components**: Catalog & Inventory bounded context (ARCHITECTURE.md, "Server bounded contexts" #2): SKU entity, persistence schema, and the typed read model consumed by storefront rendering, search, the B2B API, and feeds. Excludes: inventory levels and availability derivation (issue 030), search (issue 031), pricing (issue 032), gating enforcement (issue 025), and all UI rendering (issues 066/067).
- **Actors**: Catalog service (internal), storefront SSR, B2B API (`catalog:read` scope), dashboard inventory module (read).
- **Data Classification**: Internal (catalog master data); allergen/nutrition fields are Regulated (food-information law: EU FIC, FSSAI, FDA, JP labeling per CC-CMP-004).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Tampering/integrity loss on regulated food data (STRIDE: Tampering); market-gating failure via missing or free-text classification (OWASP Top Ten A01:2021 Broken Access Control, as the classification feeds the CC-MKT-003 gating path); injection of unvalidated CMS rich text into safety-critical surfaces (OWASP Top Ten A03:2021).
- **Trust Boundary**: CMS content (Contentful) is attacker-controlled input per SECURITY.md, Input validation rule 1; structured SKU food data is deliberately kept out of that channel. Consumer/B2B read surfaces sit behind the gating enforcement point (ARCHITECTURE.md Dependency rule 1).
- **Zero Trust Consideration**: Allergen/nutrition/classification values are typed, schema-validated fields (SECURITY.md, Input validation rule 1); clients render food data only from typed, validated responses. No renderer may fall back to CMS free text for these fields.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); V2 Validation and Business Logic (typed schema validation of catalog data at trust boundaries).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (information input validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: EU Food Information to Consumers (FIC); FSSAI labeling (IN); US FDA labeling; JP food labeling — per REQUIREMENTS.md CC-CMP-004.
- **Other**: BCP 47 locale identifiers for localized fields (REQUIREMENTS.md §2).

## Acceptance Criteria
1. **AC-01**: Given the SKU schema, when a SKU is created or updated, then it MUST carry all CC-CAT-001 fields — unique ID, localized name, veg/non-veg classification, cut/category, net weight, serving estimate, ingredients, allergens, nutrition per market format, storage and reheat instructions, per-market availability flags — and persistence MUST reject a SKU missing any required field (CC-CAT-001).
2. **AC-02**: Given a SKU write with no veg/non-veg classification or a value outside the closed enumeration, when validation runs, then the write is rejected (400 via the RFC 9457 handler, issue 021) and no partial record is stored (CC-CAT-001, CC-MKT-003 dependency).
3. **AC-03**: Given any locale of the seven launch locales, when allergen or nutrition data is requested for rendering, then the response is produced from structured fields only, localized for that locale (CC-CAT-004).
4. **AC-04**: Given a SKU with associated CMS rich-text content (e.g., marketing description), when the typed catalog read model is serialized, then allergen, nutrition, ingredients, and classification values MUST NOT be sourced from, overridden by, or concatenated with CMS free text (CC-CAT-004 negative case).
5. **AC-05**: Given per-market availability flags, when the catalog read model is queried for a market, then only that market's flag set is exposed, keyed by the Market identity type from issue 003 (CC-CAT-001).
6. **AC-06**: Given the nutrition structure, when a market is specified, then the nutrition representation for that market's format is resolvable from structured data as the single compliance source (CC-CMP-004).
7. **AC-07**: Given the API documentation pipeline, when the catalog schema changes, then the published JSON Schema used for validation and docs is the same artifact (ARCHITECTURE.md, Dependency rule 7; CC-API-010) — no hand-maintained parallel definition.

## Failure Behavior
- **On Invalid Input**: Reject writes failing schema validation with HTTP 400 and an RFC 9457 problem-details body (SECURITY.md, Logging rule 1); log a structured validation-rejection event with correlation ID (SECURITY.md, Logging rule 3); never store partially valid food data.
- **On System Error**: Fail closed (SECURITY.md, Logging rule 2): if classification or a required food field cannot be read, the SKU is treated as not renderable/not offerable rather than rendered with defaults; an exception in this path is a denial, never a bypass.
- **Alerting**: Validation-rejection spikes and any read-path fallback attempt on food fields alert via centralized monitoring (SECURITY.md, Logging rule 3; Azure Monitor per ARCHITECTURE.md).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the SKU entity invariants (required fields, closed classification enumeration, per-market flag keying, localized-field fallback rules); property-style tests that no constructor/factory path yields a SKU without classification.
- **Integration Tests**: ASP.NET Core integration tests against PostgreSQL Flexible Server (per-context schema, issue 015) covering persistence constraints (NOT NULL / CHECK on classification), and read-model serialization per market/locale.
- **Security Tests**: Test asserting CMS-sourced content can never populate allergen/nutrition/classification fields (compile-time type separation plus runtime test); SAST clean per SECURITY.md Deployment rule 7.
- **Compliance Tests**: Automated evidence that every launch-market nutrition format renders from structured fields for a sample SKU set (CC-CMP-004); test tagging per REQUIREMENTS.md §17.
- **Coverage Target**: ≥ 80% per CC-QA-001; tests tagged CC-CAT-001, CC-CAT-004, CC-CMP-004.

## Dependencies
- **Upstream**: 001 Solution scaffold (bounded-context boundaries); 003 Shared kernel: Market, Locale, and SKU identity types; 015 PostgreSQL Flexible Server (Catalog schema and least-privilege role, SECURITY.md Secret handling rule 10); 021 RFC 9457 error handling.
- **Downstream**: 025 Server-side gating enforcement (consumes classification); 030 Inventory and availability; 031 Catalog search; 032 Per-SKU per-market price model; 033 Promotion engine (per-category scope); 066 Menu page; 067 Product detail page (FSSAI mark, allergens); 071 SEO surfaces; 072 Contentful integration (must not carry food data); 084 Dashboard inventory module.
- **External**: Contentful — explicitly *not* a source for allergen/nutrition/classification data (CC-CAT-004).

## Implementation Notes
- **Constraints**: Implement inside the Catalog & Inventory module of the modular monolith with its own PostgreSQL schema and least-privilege role over TLS (ARCHITECTURE.md, "Cross-cutting"; SECURITY.md, Secret handling rule 10). Localized name and other localized fields keyed by BCP 47 locale (REQUIREMENTS.md §2). Classification modeled as a closed enum (veg/non-veg), database-constrained. The catalog read model is the single schema source for API validation and generated docs (ARCHITECTURE.md, Dependency rule 7).
- **Anti-Patterns**: MUST NOT render allergen/nutrition from CMS free text (CC-CAT-004); MUST NOT scatter market conditionals in catalog code — gating consults the Market & Gating Policy service only (CC-MKT-006; ARCHITECTURE.md, Dependency rule 1); MUST NOT bind writes to entity/domain models directly — dedicated DTOs with explicit source attributes only (SECURITY.md, Input validation rule 3); MUST NOT concatenate input into SQL — parameterized/LINQ only (SECURITY.md, Input validation rule 4).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, ≥80% coverage, lint, SAST/SCA/secret scan, no raw-HTML sinks — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must cite CC-CAT-001/004, CC-CMP-004 (REQUIREMENTS.md §17).

## Open Questions
- The exact per-market nutrition panel schema (field list, units, rounding) for EU FIC, FDA, FSSAI, and JP labeling is not enumerated in the specs — CC-CMP-004 requires the formats be "assessed and implemented" but the field-level definitions need a compliance input.
- Semantics of "serving estimate" (per person, per package, per 100 g?) are unspecified in CC-CAT-001.
- The cut/category taxonomy is not defined anywhere in the specs (it also feeds the Cuts diagram, issue 075, and per-category promotion scope, issue 033).
- Which fields beyond name carry per-locale localized variants (ingredients? storage/reheat instructions?) is implied by CC-CAT-004's "in every locale" but not explicitly enumerated.
