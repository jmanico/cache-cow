# 096 · Per-market synthetic probes including IN gating probe

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-NFR-003
- **Title**: Per-market synthetic probes including IN gating probe
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Operational

## Requirement
- **Description**: The platform MUST run continuous per-market synthetic checks via Azure Monitor availability tests, including a production IN-market gating probe that asserts non-veg content is absent from IN responses (CC-MKT-003) and that non-veg product URLs in the IN market return HTTP 404 (CC-MKT-004) (REQUIREMENTS.md CC-NFR-003).
- **Rationale**: CC-NFR-003 requires per-market synthetic checks running continuously, "including an IN-market gating probe asserting CC-MKT-003/004 in production". The IN veg-only rule is a P1 compliance regime (CC-MKT-003; FSSAI context per CC-CMP-004), and the CI test matrix (issue 027) only proves gating pre-merge — a production probe is the last line of detection for regressions introduced by configuration, caching (CC-MKT-009), or deployment drift. Per-market availability probes also evidence the 99.9%/99.5% availability targets (CC-NFR-001). ARCHITECTURE.md ("Cross-cutting", Observability) confirms the mechanism: "synthetic probes via Azure Monitor availability tests", including "the production IN gating probe".
- **Design**: N/A (operational monitoring; no user-facing surface).

## Scope
- **Applies To**: Both
- **Components**: Azure Monitor availability tests (Terraform-provisioned, issue 008); probe definitions per market (US, ES, MX, DE, JP, IN) against production storefront and B2B API gateway endpoints; alert rules on probe failure.
- **Actors**: Synthetic probe agents (unauthenticated, public-ingress clients); SRE/on-call staff consuming alerts.
- **Data Classification**: Public (probes exercise public, unauthenticated surfaces only).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Undetected production compliance-gating regression — non-veg content served into the IN market via config, cache, or deploy drift (CC-MKT-003/009; OWASP Top Ten A01:2021 Broken Access Control, as market gating is an access decision over market-restricted content); undetected availability loss against CC-NFR-001 (supports A09:2021 Security Logging and Monitoring Failures).
- **Trust Boundary**: Probes traverse the real public ingress (gateway, WAF/DDoS layer per SECURITY.md HTTP boundary rule 11) exactly as an end user would, so they validate the externally observable behavior of the boundary, not internal state.
- **Zero Trust Consideration**: The probe verifies rather than trusts deployment: gating enforcement is asserted continuously in production against server-observed responses, independent of CI results. The probe establishes IN transacting-market state the same way a user does (server-side market state, not client hints — SECURITY.md, Authentication rule 10), so it measures the real gating path.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Logging and Error Handling / monitoring practices; Access Control chapter (continuous verification of an access-control decision).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-4 (system monitoring), CA-7 (continuous monitoring), AU-6 (analysis and reporting)
- **NIST SP 800-207**: Continuous verification of policy enforcement — enforcement is monitored in production, never assumed from build-time checks.
- **Regulatory**: IN market vegetarian-only compliance regime and FSSAI context motivating the gating probe (REQUIREMENTS.md CC-MKT-003, CC-CNT-006, CC-CMP-004).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given production, when the synthetic-check suite runs, then Azure Monitor availability tests execute continuously for every one of the six launch markets (US, ES, MX, DE, JP, IN) against the storefront, and results (success, latency) are recorded in Azure Monitor per market (CC-NFR-003, CC-NFR-001).
2. **AC-02**: Given the IN-market gating probe with IN transacting-market state established, when it fetches catalog listing, product-detail, and search responses in production, then it asserts no non-veg SKU appears in any response body, and a violation marks the probe failed (CC-MKT-003, CC-NFR-003).
3. **AC-03**: Given the IN-market gating probe, when it requests a known non-veg product URL in the IN market, then it asserts the response is exactly HTTP 404 — a 200, 403, or any redirect (including to the product in another market) marks the probe failed (CC-MKT-004).
4. **AC-04**: Given any per-market probe failure, when the failure is recorded, then an alert fires to the configured destination as a structured event; an IN gating-probe failure alerts as a security/compliance event, not merely an availability event (SECURITY.md, Logging rule 3; CC-NFR-003).
5. **AC-05** (negative): Given a probe execution that cannot complete its assertions (timeout, malformed response, probe-infrastructure error), when results are reported, then the run MUST NOT be reported as passing — indeterminate is treated as failure (fail closed on a gating verification path, SECURITY.md, Logging rule 2).
6. **AC-06** (negative): Given probe traffic, when it exercises production, then it MUST NOT use authenticated sessions or mutate state (no orders, no cart persistence beyond the probe's own session) — probes are read-only against public surfaces.

## Failure Behavior
- **On Invalid Input**: N/A for inbound input (probes are outbound-only clients); unexpected response shapes are assertion failures, reported as probe failures per AC-05.
- **On System Error**: Fail closed: probe errors and indeterminate runs count as failures and alert; a broken probe never silently suppresses gating verification (SECURITY.md, Logging rule 2 applied to the verification path).
- **Alerting**: Probe-failure alert per market via Azure Monitor alerting; IN gating-probe failure escalated as a security/compliance alert (SECURITY.md, Logging rule 3); sustained failures feed the CC-NFR-001 availability measurement.

## Test Strategy
- **Unit Tests**: Unit tests for probe assertion logic (non-veg-SKU detection in response fixtures; 404-exactness check rejecting 200/403/redirect fixtures). Tagged CC-NFR-003, CC-MKT-003, CC-MKT-004 (REQUIREMENTS.md §17).
- **Integration Tests**: Staging run of the full probe suite against a deployed environment, including a seeded regression (non-veg SKU exposed to IN in staging) asserting the probe detects it and the alert rule fires (AC-02–AC-04).
- **Security Tests**: The IN gating probe is itself a continuous production security control; its detection capability is verified by the seeded-regression staging test each release.
- **Compliance Tests**: Automated evidence collection: probe definitions and alert rules exist in Terraform state for all six markets; probe result history retained in Log Analytics per the retention schedule (SECURITY.md, Logging rule 9).
- **Coverage Target**: ≥ 80% line coverage for probe/assertion code (CC-QA-001).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (availability tests and alert rules as IaC); 095 OpenTelemetry observability to Azure Monitor (alerting/monitoring substrate); 024 Transacting market/locale resolution (probes must establish IN market state the way users do); 025 Server-side gating enforcement API and 026 404 semantics (the behaviors under test); 063 Storefront SSR shell (probe target).
- **Downstream**: None planned — this issue is a leaf verification control (complements, and does not replace, 027 Market-gating CI test matrix).
- **External**: Azure Monitor availability tests (ARCHITECTURE.md, decision record 2026-07-15).

## Implementation Notes
- **Constraints**: Probes defined declaratively and provisioned via Terraform in CI/CD (SECURITY.md, Deployment rules 1–5); probes run against public ingress only — all other endpoints are private (ARCHITECTURE.md, Technology decisions); the gating probe must assert on server-rendered response bodies (Angular SSR output), since CC-MKT-003 requires server-side exclusion and client-side hiding is non-compliant; probe assertions need a known non-veg SKU URL and known catalog fixtures as stable references.
- **Anti-Patterns**: MUST NOT rely on client-side JavaScript evaluation to conclude content is "hidden" (CC-MKT-003: client-side hiding is non-compliant); MUST NOT accept 403 or a redirect as satisfying the 404 requirement (CC-MKT-004); MUST NOT treat an indeterminate probe run as a pass (AC-05); MUST NOT embed credentials or secrets in probe definitions (SECURITY.md, Secret handling rule 1).
- **AI Development Guidance**: AI-generated probe/IaC code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); IaC scanning and policy-as-code gates apply to probe Terraform (Deployment rule 4).

## Open Questions
- Probe frequency ("continuously") is not quantified in the specs — no interval is stated for the availability tests or the IN gating probe.
- The exact set of IN probe targets beyond catalog/product-detail/search (e.g., whether the probe also asserts sitemap/structured-data/feed exclusion per CC-MKT-003, which issue 071 owns serving) is not enumerated by CC-NFR-003; the seeded list of "known non-veg product URLs" needs an owner and a maintenance process.
- Whether the B2B API requires its own synthetic availability probes per market (CC-NFR-001 sets an API availability target; CC-NFR-003 says "per-market synthetic checks" without naming API probes) is ambiguous.
