# 097 · Real-user monitoring and performance budgets

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-NFR-002
- **Title**: Real-user monitoring and performance budgets
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Performance

## Requirement
- **Description**: The platform MUST enforce performance budgets — storefront LCP under 2.5s at p75 per market measured by real-user monitoring, and API p95 latency under 300ms for reads and under 800ms for order creation — with alerting when any budget is breached (REQUIREMENTS.md CC-NFR-002).
- **Rationale**: CC-NFR-002 sets the performance budgets and explicitly requires real-user monitoring per market for the storefront LCP target. The worldwide multi-region topology (ARCHITECTURE.md, Technology decisions: regional clusters/edges "to meet the per-market latency budgets (CC-NFR-002)") exists to serve these budgets, so per-market measurement is what proves the topology works. Measurement and alerting run on the confirmed observability stack: Azure Monitor with Application Insights and Log Analytics (ARCHITECTURE.md, "Cross-cutting", Observability).
- **Design**: N/A (operational measurement; no new user-facing surface — the RUM beacon must not alter the design surfaces it measures).

## Scope
- **Applies To**: Both
- **Components**: Storefront Angular SSR client (RUM/web-vitals instrumentation); B2B API and storefront-serving API endpoints (server-side latency metrics); Application Insights ingestion; Azure Monitor alert rules and per-market dashboards/queries.
- **Actors**: Real consumer sessions across the six markets (passive telemetry sources); B2B API clients (server-measured); SRE/on-call staff consuming alerts.
- **Data Classification**: Internal (performance telemetry) — MUST NOT contain Restricted/PII after filtering (SECURITY.md, Logging rule 8).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Primarily operational (budget regressions, degraded availability trending toward CC-NFR-001 breach); secondarily, PII leakage through the RUM telemetry pipeline (STRIDE: Information Disclosure — SECURITY.md, Logging rule 8) and latency anomalies as an early signal of application-layer floods that slip the rate limiter (SECURITY.md, HTTP boundary rule 11 context).
- **Trust Boundary**: RUM beacons are client-submitted data crossing the client-server edge and are attacker-controlled input: validated server-side against an explicit schema, size-capped and rate-limited like any public input (SECURITY.md, Input validation rule 1; HTTP boundary rule 7). Server-side API latency metrics are measured in-process and do not trust client-reported timings.
- **Zero Trust Consideration**: Client-reported metrics are used only for aggregate performance measurement, never for any authorization, gating, or pricing decision; API latency budgets are verified from server-side measurements, treating client-supplied timing as unverifiable.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Input Validation chapter for the beacon endpoint; Logging and Error Handling chapter for telemetry content.
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-4 (system monitoring), CA-7 (continuous monitoring), SI-10 (information input validation, for the beacon endpoint)
- **NIST SP 800-207**: Client-submitted telemetry treated as untrusted; no policy decision derives from it.
- **Regulatory**: GDPR/DPDP data minimization applies to RUM data (REQUIREMENTS.md CC-CMP-001/002/003); see Open Questions on consent classification.
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given real consumer traffic in a market, when storefront pages load, then RUM captures LCP (and the data needed to compute p75) dimensioned by market, and the per-market p75 LCP is queryable in Azure Monitor/Application Insights (CC-NFR-002).
2. **AC-02**: Given server-side API metrics, when read endpoints and order-creation endpoints are exercised, then p95 latency is computed per endpoint class from server-side measurements, evaluated against 300ms (reads) and 800ms (order creation) (CC-NFR-002).
3. **AC-03**: Given a budget breach — per-market storefront p75 LCP ≥ 2.5s, or API read p95 ≥ 300ms, or order-creation p95 ≥ 800ms — when the evaluation window closes, then an Azure Monitor alert fires to the configured destination identifying the market/endpoint class and the measured value (CC-NFR-002, CC-NFR-003).
4. **AC-04** (negative): Given budget evaluation, when one market misses its LCP budget while the global aggregate passes, then the breach MUST still be detected and alerted — budgets are per market, and a global average MUST NOT mask a per-market miss (CC-NFR-002).
5. **AC-05** (negative): Given RUM beacon payloads, when they are ingested, then they MUST NOT contain PII, credentials, tokens, or capability tokens (guest capability tokens are explicitly kept out of analytics query strings), and payloads failing schema validation are rejected without processing (SECURITY.md, Logging rules 4 and 8; Authentication rule 14; Input validation rule 1).
6. **AC-06**: Given the RUM beacon ingestion endpoint, when it receives traffic, then it enforces request-size caps and rate limits with 429 + `Retry-After` like any public endpoint (SECURITY.md, HTTP boundary rule 7).

## Failure Behavior
- **On Invalid Input**: Malformed or oversized beacon payloads rejected (400/413 per case) without processing; no internal state disclosed (RFC 9457 generic errors, SECURITY.md, Logging rule 1); rejections logged as validation events (Logging rule 3).
- **On System Error**: Fail open for the user experience — RUM collection failure never degrades or blocks page loads or API requests (CC-NFR-001/002 take precedence over measurement); fail loudly operationally — ingestion outages alert so budget evaluation gaps are visible.
- **Alerting**: Budget-breach alerts per AC-03; RUM ingestion-failure alerts; both via Azure Monitor alerting (ARCHITECTURE.md, "Cross-cutting"; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for beacon schema validation (reject unknown fields, PII-shaped fields, oversize payloads) and percentile/budget evaluation logic (p75/p95 windows, per-market dimensioning). Angular component/unit tests for the web-vitals instrumentation hook. Tagged CC-NFR-002 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests asserting server-side latency metrics are emitted per endpoint class and dimensioned correctly; end-to-end test that a synthetic breach (injected slow metric series) triggers the alert rule in a test workspace.
- **Security Tests**: Fuzzing of the beacon endpoint (malformed JSON, oversized bodies, injection payloads in string fields — log-injection prevention per SECURITY.md, Logging rule 5); rate-limit behavior test (429 + `Retry-After`).
- **Compliance Tests**: Automated evidence that RUM pipeline PII filters are configured (SECURITY.md, Logging rule 8) and that alert rules exist for every budget × market combination.
- **Coverage Target**: ≥ 80% line coverage for beacon validation and budget-evaluation modules (CC-QA-001).

## Dependencies
- **Upstream**: 095 OpenTelemetry observability to Azure Monitor (ingestion, alerting, PII-filtering substrate); 063 Storefront SSR shell (surface to instrument); 053 B2B API scaffold (endpoints to measure); 019 Baseline rate-limiting middleware (beacon endpoint limits); 008 Terraform bootstrap (alert rules as IaC).
- **Downstream**: None planned — leaf measurement/alerting control (synthetic availability measurement lives in 096).
- **External**: Azure Monitor, Application Insights (ARCHITECTURE.md, decision record 2026-07-15).

## Implementation Notes
- **Constraints**: RUM instrumentation must be CSP-compatible and self-hosted — no third-party runtime CDN scripts (SECURITY.md, HTTP boundary rule 2; Deployment rule 10), so the beacon reports to a first-party endpoint allowlisted in `connect-src`; server-side latency measured in ASP.NET Core middleware/metrics on the confirmed OpenTelemetry pipeline (issue 095); per-market dimension must come from the server-side transacting-market state, consistent with how gating and budgets are defined (SECURITY.md, Authentication rule 10); instrumentation overhead must not itself endanger the LCP budget (defer beacon work off the critical rendering path).
- **Anti-Patterns**: MUST NOT load RUM scripts from third-party CDNs (SECURITY.md, Deployment rule 10); MUST NOT put tokens or PII in beacon query strings (SECURITY.md, Authentication rule 14; Logging rule 4); MUST NOT derive any gating, pricing, or authorization behavior from client-reported telemetry; MUST NOT report a passing global aggregate over a failing market (AC-04).
- **AI Development Guidance**: AI-generated instrumentation and alert-rule code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Whether RUM beacons are classified as "non-essential analytics" requiring consent in GDPR markets (CC-CMP-001 requires consent management for non-essential cookies/analytics) is not settled in the specs; if so, per-market RUM sampling would depend on consent state and issue 088 (EU consent management).
- Alert thresholds' evaluation windows (how long a p75/p95 breach must persist before alerting) and alert destinations are not specified beyond "alerting" (CC-NFR-002/003).
- CC-NFR-002 states API p95 budgets without a per-market qualifier (unlike LCP); whether API latency budgets are evaluated globally or per market/region is ambiguous.
