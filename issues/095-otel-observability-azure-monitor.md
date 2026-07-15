# 095 · OpenTelemetry observability to Azure Monitor

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Telemetry & backup residency is an open decision (ARCHITECTURE.md, "Known unknowns"): the Azure Monitor / Log Analytics aggregation region topology is undecided and entangled with the data-residency vs. single-primary-write-region conflict (CC-CMP-003/006). Instrumentation, pipeline shape, PII filtering, and retention wiring can proceed; the region/workspace topology for log and telemetry storage MUST NOT be fixed until a human decides.

## Metadata
- **ID**: CC-NFR-003
- **Title**: OpenTelemetry observability to Azure Monitor
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: High
- **Classification**: Operational

## Requirement
- **Description**: All services MUST emit structured logs, metrics, and traces via OpenTelemetry ingestion into Azure Monitor with Application Insights and Log Analytics, with PII filtered from the telemetry pipelines and all logs subject to the documented retention schedule and automated deletion jobs (REQUIREMENTS.md CC-NFR-003; SECURITY.md, Logging rules 8–9).
- **Rationale**: CC-NFR-003 requires structured logs, metrics, and traces from every service as the substrate for the availability and performance targets (CC-NFR-001/002), the per-market synthetic checks (issue 096), and security-event alerting (SECURITY.md, Logging rule 3). ARCHITECTURE.md ("Cross-cutting", Observability; decision record 2026-07-15) confirms the tooling: Azure Monitor with Application Insights and Log Analytics, via OpenTelemetry ingestion. SECURITY.md Logging rule 8 makes the pipeline itself a privacy boundary ("Filter PII from telemetry pipelines; alert on Key Vault access denials and authentication failure spikes"), and Logging rule 9 puts logs in scope for the CC-CMP-003 retention schedule.
- **Design**: N/A (server-side/operational; no user-facing surface).

## Scope
- **Applies To**: Both
- **Components**: All ten bounded contexts of the .NET 10 modular monolith (ARCHITECTURE.md, "Server bounded contexts"); Angular SSR host; OpenTelemetry SDK/exporter configuration; Azure Monitor, Application Insights, and Log Analytics resources (Terraform-provisioned, issue 008); telemetry PII-filtering processors.
- **Actors**: Platform/SRE engineers and on-call staff (telemetry consumers); all services (telemetry producers). No end-user actor.
- **Data Classification**: Internal (telemetry content) — MUST NOT contain Restricted/PII after filtering (SECURITY.md, Logging rules 4 and 8).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Undetected attacks and outages from missing/unstructured telemetry (supports OWASP Top Ten A09:2021 Security Logging and Monitoring Failures); PII/secret leakage through telemetry pipelines (STRIDE: Information Disclosure); retention-schedule violations when logs escape the CC-CMP-003 deletion jobs.
- **Trust Boundary**: Telemetry egress from application services to Azure Monitor ingestion. Telemetry values that originated as user input are attacker-influenced and are encoded/sanitized before entering log entries (SECURITY.md, Logging rule 5; content rules owned by issue 022).
- **Zero Trust Consideration**: The pipeline assumes emitted telemetry may contain data that must not persist: PII filters and redaction run in the pipeline regardless of what upstream code logged (defense in depth over Logging rule 4's never-log rule). Azure SDK clients authenticate with managed identity, never embedded keys (SECURITY.md, Secret handling rule 2).

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Logging and Error Handling chapter (protection and content of log data).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-2 (event logging), AU-6 (audit record review/analysis via centralized monitoring), AU-9 (protection of audit information), SI-4 (system monitoring)
- **NIST SP 800-207**: Continuous monitoring of all resource activity as input to policy decisions; telemetry collected from every workload, not only the perimeter.
- **Regulatory**: GDPR / DPDP data-minimization and retention applied to telemetry (REQUIREMENTS.md CC-CMP-003; CC-CMP-006 requires telemetry/log residency reconciliation — open decision, see AT RISK).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given any of the ten bounded contexts handling a request, when the request completes, then structured logs, metrics, and a distributed trace span are emitted via OpenTelemetry and are queryable in Log Analytics / Application Insights, correlated by trace/correlation ID across module boundaries (CC-NFR-003).
2. **AC-02**: Given the telemetry pipeline, when a log record or telemetry item containing PII (e.g., email address, delivery address) reaches the pipeline's filtering stage, then the PII is redacted or dropped before persistence in Azure Monitor (SECURITY.md, Logging rules 4 and 8; CC-NFR-003).
3. **AC-03** (negative): Given any telemetry export, when its persisted content is inspected, then it MUST NOT contain credentials, tokens (including guest-order capability tokens), OTP codes, passwords, connection strings, or PANs (SECURITY.md, Logging rule 4; Authentication rules 13–14; Secret handling rule 7).
4. **AC-04**: Given Key Vault access denials or an authentication-failure spike, when the events reach Azure Monitor, then an alert fires to the configured destination (SECURITY.md, Logging rule 8; security-event content and structured-log format are owned by issue 022).
5. **AC-05**: Given the documented retention schedule per data class (CC-CMP-003), when telemetry/log data in Log Analytics exceeds its class's retention period, then automated retention enforcement deletes it — logs are explicitly in scope for the retention schedule (SECURITY.md, Logging rule 9).
6. **AC-06** (negative): Given telemetry emission fails (exporter outage, Azure Monitor unreachable), when application requests are processed, then request processing continues (telemetry is not on the request critical path) but the export failure itself is surfaced as an operational alert — silent telemetry loss MUST NOT occur unnoticed (CC-NFR-003, CC-NFR-001).

## Failure Behavior
- **On Invalid Input**: Malformed/oversized telemetry payloads are dropped or truncated by the pipeline without disclosing internal state; user-derived values are sanitized before entering log entries (SECURITY.md, Logging rule 5).
- **On System Error**: Telemetry export failure fails open for request processing (observability must not take down the storefront/API against CC-NFR-001) but fails loudly: exporter errors raise operational alerts. Any exception inside an authorization or gating path remains a denial regardless of telemetry state (SECURITY.md, Logging rule 2).
- **Alerting**: Exporter/ingestion failure alerts; Key Vault access-denial alerts; authentication-failure-spike alerts (SECURITY.md, Logging rule 8) — all routed through Azure Monitor alerting (ARCHITECTURE.md, "Cross-cutting").

## Test Strategy
- **Unit Tests**: .NET 10 unit tests for the PII-filtering/redaction processors (known-PII fixtures redacted; secrets/token patterns dropped) and for structured-log template usage. Tagged CC-NFR-003 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests asserting a request through each bounded context produces correlated logs/metrics/traces via the OpenTelemetry exporters (in-memory/test exporter); Angular SSR host emits request telemetry.
- **Security Tests**: Pipeline test injecting credential/token/PII strings into log calls and asserting none persist (AC-03); SAST/secret-scan gates per SECURITY.md, Deployment rule 7.
- **Compliance Tests**: Automated evidence that Log Analytics retention configuration matches the CC-CMP-003 retention schedule per data class; alert-rule presence validation (AC-04).
- **Coverage Target**: ≥ 80% line coverage for the telemetry/filtering module (CC-QA-001).

## Dependencies
- **Upstream**: 001 Solution scaffold (bounded contexts to instrument); 008 Terraform bootstrap (Azure Monitor/Log Analytics/Application Insights provisioning); 010 GitOps application delivery to AKS; 014 Azure Key Vault (managed identity pattern for Azure SDK clients).
- **Downstream**: 022 Structured security logging (emits through this pipeline); 090 Retention schedule and automated deletion jobs (logs in scope, Logging rule 9); 096 Per-market synthetic probes; 097 Real-user monitoring and performance budgets.
- **External**: Azure Monitor, Application Insights, Log Analytics (ARCHITECTURE.md, decision record 2026-07-15).
- **AT RISK**: Telemetry & backup residency (ARCHITECTURE.md, "Known unknowns") — Log Analytics workspace region topology cannot be finalized until the residency decision is made; do not resolve here.

## Implementation Notes
- **Constraints**: .NET OpenTelemetry SDK with Azure Monitor ingestion (ARCHITECTURE.md, "Cross-cutting": "Azure Monitor with Application Insights and Log Analytics (OpenTelemetry ingestion)"); Azure SDK clients authenticate via managed identity / `DefaultAzureCredential` (SECURITY.md, Secret handling rule 2) under narrow data-plane RBAC (rule 3); structured log templates only, never string interpolation into log messages (SECURITY.md, Logging rule 4); Angular client errors route through interceptors/global ErrorHandler with details logged server-side only (SECURITY.md, Logging rule 7 — client wiring owned by issues 004/022).
- **Anti-Patterns**: MUST NOT log credentials, tokens, capability tokens, OTP codes, or PANs (SECURITY.md, Logging rules 4, Authentication rules 13–14); MUST NOT embed instrumentation keys/connection strings in source, config, or client bundles (Secret handling rule 1); MUST NOT interpolate user input into log message strings (Logging rules 4–5); MUST NOT create a telemetry store outside the retention schedule (Logging rule 9); MUST NOT pin telemetry regions ahead of the open residency decision.
- **AI Development Guidance**: AI-generated instrumentation code passes the identical merge gates (SAST/SCA/secret scan, tests green, coverage, lint) plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The Log Analytics workspace/region topology for telemetry is an open decision (ARCHITECTURE.md, "Known unknowns" — telemetry & backup residency, entangled with the write-region conflict); recorded here as AT RISK, not resolved.
- The specs mandate alerting destinations only generically ("centralized monitoring with alerting", SECURITY.md Logging rule 3); the concrete on-call routing (action groups, escalation) is unspecified.
- Retention periods per data class are required to exist (CC-CMP-003) but their concrete values for the "logs" class are not stated in the specs; AC-05 tests conformance to the documented schedule, whatever its ratified values.
