# CLAUDE.md

Cache Cow — multi-market (US, ES, MX, DE, JP, IN) direct-to-consumer and B2B commerce platform for frozen BBQ: web storefront, wholesale portal, versioned REST B2B API, internal dashboard.

**Repo status: bootstrap.** This repository contains specification documents and logo assets only. No application code, build system, or CI exists yet.

## Canonical documents

These are imported and binding. Do not restate their rules elsewhere; do not contradict them.

@REQUIREMENTS.md
@ARCHITECTURE.md
@SECURITY.md
@DESIGN.md

REQUIREMENTS.md defines the testable requirements (CC-* IDs). ARCHITECTURE.md fixes boundaries and the stack. SECURITY.md governs all future code, including AI-generated code. DESIGN.md governs all user-facing surfaces.

## GitHub issues

Every new GitHub issue MUST follow [REQUIREMENT_TEMPLATE.md](REQUIREMENT_TEMPLATE.md) so each issue is a structured, testable requirement — with metadata, RFC 2119 description, security context, standards alignment, acceptance criteria, and failure behavior filled in. Issues that are not structured requirements (e.g., doc typos) still use the template, marking non-applicable sections N/A.

## Working rules

- **Never resolve an open decision yourself.** Items marked `TO BE DECIDED`, `UNKNOWN`, or `[ASSUMPTION]` in the canonical documents (payment processor, identity provider, data stores, search, CMS, email, region topology, observability, SSR mechanism, consumer auth posture) stay open until a human decides. If work depends on one, stop and surface it.
- **Flag conflicts, don't pick a side.** Known conflicts (e.g., consumer SMS-MFA vs CC-SEC-005) are tracked deliberately; new conflicts between documents get raised, not silently resolved.
- **Changes to rules go into the owning canonical document**, not into this file or scattered notes. This file holds only repo-operational guidance.
- **Cite requirement IDs.** Work items, PRs, and tests reference the CC-* requirements they implement or verify (REQUIREMENTS.md §17).
- **When code exists**, SECURITY.md's rules and the CC-QA merge gates apply to every change, AI-generated included — mandatory human review, no auto-merge.

## Workflow (not yet defined — do not improvise)

- Build / test / lint commands: `[TBD — no code exists yet]`
- Branching and PR conventions: `[TBD]`
- CI pipeline and enforcement of CC-QA gates: `[TBD — gates are specified in REQUIREMENTS.md §15 but not yet implemented]`
- Project layout / package structure: `[TBD — follows ARCHITECTURE.md bounded contexts when scaffolded]`
