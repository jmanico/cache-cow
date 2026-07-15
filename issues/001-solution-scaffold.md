# 001 · Solution scaffold: .NET 10 modular monolith with enforced bounded-context boundaries

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: N/A — no single CC-* ID; implements ARCHITECTURE.md "Technology decisions" (Packaging: modular monolith), "Server bounded contexts" (all ten), and Dependency rule 9 (minimal shared kernel)
- **Title**: Solution scaffold: .NET 10 modular monolith with enforced bounded-context boundaries
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Operational

## Requirement
- **Description**: The repository MUST contain a single deployable .NET Core 10 / ASP.NET Core application organized as a modular monolith with one module per bounded context defined in ARCHITECTURE.md "Server bounded contexts", and automated architecture tests MUST fail the build on any cross-context reference that does not go through the shared kernel.
- **Rationale**: ARCHITECTURE.md ("Technology decisions", Packaging) confirms a modular monolith — one deployable ASP.NET Core application with enforced internal module boundaries along the bounded contexts, so contexts may split into services later along the same seams. ARCHITECTURE.md Dependency rule 9 keeps the shared kernel minimal (market/locale identifiers, money type, SKU identity, requirement-tagged test utilities) so everything else stays inside its bounded context. Without mechanical enforcement, the module boundary erodes and later controls that assume it (e.g., per-context database roles, SECURITY.md, Secret handling rule 10) lose their code-level counterpart.
- **Design**: N/A (non-UI; server solution layout only).

## Scope
- **Applies To**: Both (the single ASP.NET Core host serves web-facing and API-facing modules)
- **Components**: Solution/project layout; one ASP.NET Core host project; one module (project or project group) per bounded context: 1. Market & Gating Policy, 2. Catalog & Inventory, 3. Pricing & Promotions, 4. Ordering & Payments, 5. Fulfillment, 6. Wholesale & B2B API, 7. Invoicing, 8. Back Office, 9. Identity & Access, 10. Content & Localization (ARCHITECTURE.md, "Server bounded contexts"); a SharedKernel project; an architecture-test project. Explicitly excluded: business logic, data access, infrastructure (Terraform/GitOps/AKS — issues 008–012), CI pipeline definition (issue 006).
- **Actors**: Platform engineers; CI system (executes the architecture tests)
- **Data Classification**: Internal (source layout only; no runtime data)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Erosion of module isolation that later security controls depend on — e.g., a logic or injection flaw in one module reaching another context's data is contained by the module boundary plus per-context database roles (SECURITY.md, Secret handling rule 10 / CC-SEC-021); scope creep via unreferenced code paths (REQUIREMENTS.md §17)
- **Trust Boundary**: Internal module boundaries between bounded contexts (code-level counterpart of the per-context schema/role boundary)
- **Zero Trust Consideration**: Each bounded context owns its data and exposes no internals to peers; cross-context coupling is denied by default and only the minimal shared kernel is mutually visible (ARCHITECTURE.md, Dependency rule 9).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V15 Secure Coding and Architecture (architectural segregation; the platform-wide ASVS 5.0 Level 2 baseline per SECURITY.md "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SA-8 (security engineering principles — modularity and layering)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the repository, when the solution is built, then it produces exactly one deployable ASP.NET Core application (ARCHITECTURE.md, "Technology decisions": Packaging), targeting .NET Core 10 / C#.
2. **AC-02**: Given the solution layout, when its projects are enumerated, then there is exactly one module per bounded context for all ten contexts named in ARCHITECTURE.md "Server bounded contexts", plus a SharedKernel project and an architecture-test project.
3. **AC-03**: Given the architecture-test suite, when module A references internals of module B (any dependency that is not the shared kernel), then the architecture tests fail and the build is red (negative case: a cross-context reference MUST NOT compile/pass CI).
4. **AC-04**: Given the SharedKernel project, when its contents are reviewed against ARCHITECTURE.md Dependency rule 9, then it contains only placeholders/namespaces for market/locale identifiers, the money type, SKU identity, and requirement-tagged test utilities (types themselves land in issues 002 and 003); an architecture test MUST fail if SharedKernel references any bounded-context module.
5. **AC-05**: Given the storefront/portal/dashboard Angular clients (issue 004), when the server solution is inspected, then no client code lives inside server modules and no server module depends on client artifacts (clients are separate surfaces per ARCHITECTURE.md, "Clients").
6. **AC-06**: Given the scaffold, when it is reviewed, then it contains no business logic, no data-store access, and no infrastructure code (those belong to later issues; unreferenced code paths are scope creep per REQUIREMENTS.md §17).

## Failure Behavior
- **On Invalid Input**: N/A (no runtime input; this issue produces build-time structure only)
- **On System Error**: Fail closed at build time — any architecture-test failure or boundary violation breaks the build; there is no warn-only mode (consistent with SECURITY.md, Logging rule 2's fail-closed posture and Deployment rule 7's blocking gates).
- **Alerting**: CI failure on the merge request is the alert; no runtime alerting applies.

## Test Strategy
- **Unit Tests**: N/A beyond the architecture-test suite (no business logic in scope).
- **Integration Tests**: A smoke test that the single host boots and responds (empty pipeline), proving one-deployable packaging.
- **Security Tests**: Architecture tests asserting (a) no cross-context references except via SharedKernel, (b) SharedKernel references no context, (c) every context module is referenced only by the host and its own tests. These run in the merge gates (SECURITY.md, Deployment rule 7).
- **Compliance Tests**: Architecture-test output retained as CI evidence that the modular-monolith boundary of ARCHITECTURE.md is enforced.
- **Coverage Target**: ≥ 80% per package where measurable (CC-QA-001); the scaffold's testable surface is the architecture-test suite itself, tagged with the ARCHITECTURE.md sections it verifies (REQUIREMENTS.md §17 tagging discipline).

## Dependencies
- **Upstream**: None (first implementation issue).
- **Downstream**: 002 "Shared kernel: Money type" and 003 "Shared kernel: Market, Locale, and SKU identity types" (live in SharedKernel); 006 "CI merge gates" (runs the architecture tests); 015 "PostgreSQL Flexible Server: per-context schemas/roles" (mirrors these module boundaries at the database, CC-SEC-021); 023 "Market & Gating Policy: policy-as-data model", 029 "SKU domain model", 032 "Per-SKU per-market price model", 035 "Order state machine", 046 "Invoice core", 049 "Partner tenancy", 053 "B2B API scaffold", 072 "Contentful integration" (each fills its context module).
- **External**: None.

## Implementation Notes
- **Constraints**: .NET Core 10 / C# / ASP.NET Core on Kubernetes with all private endpoints (ARCHITECTURE.md, "Technology decisions"); one deployable application — module boundaries are internal, not service boundaries; boundaries must sit on the same seams as the eventual per-context PostgreSQL schemas/roles (SECURITY.md, Secret handling rule 10) so contexts can split into services later.
- **Anti-Patterns**: No shared "common"/"utils" dumping ground beyond the Dependency-rule-9 shared kernel; no context reaching into another context's namespace, entities, or (later) schema; no market/gating conditionals scattered outside the Market & Gating Policy context (ARCHITECTURE.md, Dependency rule 1); no business logic smuggled into the scaffold (REQUIREMENTS.md §17 treats unreferenced paths as scope creep).
- **AI Development Guidance**: AI-generated code passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The specs do not name the architecture-test tooling or mechanism for enforcing module boundaries (dedicated test library vs. compiler-level project references vs. analyzer rules); the choice is left to implementation and review.
- The specs do not define solution/project naming conventions or folder layout; none are asserted in acceptance criteria.
- Whether each bounded context is one project or a small project group (e.g., separate contract/implementation assemblies) is unspecified.
