# CLAUDE.md

Cache Cow — see REQUIREMENTS.md §1 for what the platform is.

**Repo status: bootstrap.** This repository contains specification documents and logo assets only. No application code, build system, or CI exists yet — do not improvise build, test, or run commands, or branching and PR conventions; all are undefined until scaffolding lands.

## Canonical documents

These are imported and binding. Each owns one domain; do not restate their rules elsewhere; do not contradict them.

@REQUIREMENTS.md
@ARCHITECTURE.md
@SECURITY.md
@DESIGN.md

REQUIREMENTS.md defines the testable requirements (CC-* IDs). ARCHITECTURE.md fixes boundaries and the stack, and tracks all open decisions under "Known unknowns". SECURITY.md authors every security requirement and control, and governs all future code, including AI-generated code. DESIGN.md governs all user-facing surfaces.

## GitHub issues

Every new GitHub issue MUST follow [REQUIREMENT_TEMPLATE.md](REQUIREMENT_TEMPLATE.md) so each issue is a structured, testable requirement — with metadata, RFC 2119 description, security context, standards alignment, acceptance criteria, and failure behavior filled in. Issues that are not structured requirements (e.g., doc typos) still use the template, marking non-applicable sections N/A.

## Working rules

- **Never resolve an open decision yourself.** Items marked `[ASSUMPTION]` or `TO BE CONFIRMED`, and everything listed in ARCHITECTURE.md "Known unknowns", stay open until a human decides. If work depends on one, stop and surface it.
- **Flag conflicts, don't pick a side.** New conflicts between documents get raised, not silently resolved.
- **Changes to rules go into the owning canonical document**, not into this file or scattered notes. This file holds only repo-operational guidance.
- **Cite requirement IDs.** Work items, PRs, and tests reference the CC-* requirements they implement or verify (REQUIREMENTS.md §17).
- **When code exists**, every change — AI-generated included — passes the merge gates in CC-QA-002 and SECURITY.md (Deployment, rule 7).
