# 022 · Structured security logging, PII redaction, log-injection prevention, alerting

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-010 (with CC-NFR-003 observability clause) — authored in SECURITY.md, Logging and error handling rules 3, 4, 5, and 8
- **Title**: Structured security logging, PII redaction, log-injection prevention, alerting
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: The platform MUST log security events (authentication successes/failures, authorization denials, validation rejections, admin actions) as structured logs to centralized monitoring with alerting, MUST never log credentials, tokens, passwords, connection strings, or PANs, MUST redact PII and filter it from telemetry pipelines, MUST use structured log templates (never string interpolation into log messages), MUST encode or sanitize user-supplied values before they enter log entries, and MUST alert on Key Vault access denials and authentication-failure spikes (SECURITY.md, Logging rules 3–5 and 8; CC-SEC-010, CC-NFR-003).
- **Rationale**: Security logging is the detection layer for every other control: brute force against OTP login (CC-SEC-016), webhook signature-verification failures (SECURITY.md, Secret handling rule 9), authz denial spikes, and admin actions are only visible if events are structured, centralized, and alerted (CC-SEC-010, CC-NFR-003). The log store is itself a target and a leak vector: secrets/tokens in logs turn a log-read into a compromise (capability tokens and OTP codes are explicitly never logged — SECURITY.md, Authentication rules 13–14), PII in telemetry violates the residency/retention regime (CC-CMP-003; Logging rule 9), and unencoded user input enables log-injection forgery of entries (Logging rule 5). Structured templates rather than interpolation keep fields queryable and prevent user data from being parsed as message structure.
- **Design**: N/A (non-UI infrastructure; the dashboard consumes analytics, not raw security logs).

## Scope
- **Applies To**: Both
- **Components**: Shared logging/telemetry plumbing of the modular monolith (all ten bounded contexts); OpenTelemetry export to Azure Monitor / Application Insights / Log Analytics (ARCHITECTURE.md, Observability); PII-redaction/telemetry filters; alert rules (Key Vault access denials, authentication-failure spikes); Angular client error reporting arrives via issue 021's server-side logging
- **Actors**: All principals generate events (consumers, guests, portal buyers, B2B clients, staff); consumers of logs are operations/security staff; attackers attempt log forgery, secret harvesting from logs, and detection evasion
- **Data Classification**: Confidential (security telemetry), with strict exclusion of Restricted/PII and secrets from log content; log data is itself in scope for retention and residency rules (CC-CMP-003, CC-CMP-006)

## Security Context
- **Defense Layer**: Architecture (detection and response substrate)
- **Threat(s) Addressed**: Undetected attacks via missing security telemetry (OWASP Top Ten A09:2021 Security Logging and Monitoring Failures); credential/token/PII disclosure through logs (CWE-532); log injection/forging (CWE-117); STRIDE: Repudiation (missing/forgeable events), Information Disclosure.
- **Trust Boundary**: Every value entering a log entry from user-influenced input crosses a trust boundary and is encoded/sanitized first (SECURITY.md, Logging rule 5); the telemetry pipeline to Azure Monitor is a data-egress boundary that PII must not cross unredacted (Logging rule 8).
- **Zero Trust Consideration**: User-supplied strings are hostile even inside log messages; template parameters keep them as data, never as message structure. No event's absence is trusted: security-relevant paths emit events on both success and failure so gaps are detectable.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V16 Security Logging and Error Handling (chapter-level, per SECURITY.md's ASVS 5.0 L2 baseline)
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AU-2 (Event Logging), AU-3 (Content of Audit Records), AU-6 (Audit Record Review, Analysis, and Reporting), AU-9 (Protection of Audit Information — full WORM/INSERT-only enforcement is issue 081), SI-4 (System Monitoring)
- **NIST SP 800-207**: Continuous monitoring of access decisions as a first-class signal
- **Regulatory**: GDPR/DPDP/APPI data-minimization applies to log content (CC-CMP-003; logs are in scope for retention and deletion — SECURITY.md, Logging rule 9, owned by issue 090); residency of aggregated telemetry is an open decision (see Dependencies)
- **Other**: N/A

> **AT RISK:** Telemetry & backup residency (ARCHITECTURE.md, "Known unknowns") — the Azure Monitor / Log Analytics region topology for aggregated logs is undecided. The in-process logging mechanics here can proceed; the workspace/region placement of the centralized store awaits a human decision and MUST NOT be resolved by this issue.

## Acceptance Criteria
1. **AC-01**: Given a security-relevant action (authentication success or failure, authorization denial, validation rejection, admin action), when it occurs in any bounded context, then a structured event with typed fields (event class, actor identifier per policy, outcome, correlation ID, timestamp) is emitted to centralized monitoring (SECURITY.md, Logging rule 3; CC-SEC-010, CC-NFR-003).
2. **AC-02**: Negative — given any log or telemetry output across the platform, then it contains no credentials, tokens (including session cookies, JWTs, guest capability tokens per Authentication rule 14, OTP codes per Authentication rule 13), passwords, connection strings, or PANs (which never exist in-system per Secret handling rule 7); an automated log-content scan over the integration-test corpus fails on any match (SECURITY.md, Logging rule 4).
3. **AC-03**: Given a log statement anywhere in the codebase, when CI static analysis runs, then string interpolation/concatenation into log message text fails the build — only structured templates with named parameters are accepted (SECURITY.md, Logging rule 4).
4. **AC-04**: Given a user-supplied value (e.g., a search term or header containing CR/LF, ANSI escapes, or template syntax) that is logged, when the entry is written, then the value is encoded/sanitized so it cannot forge additional log entries or alter entry structure — the stored event remains a single record with the hostile input inert as field data (SECURITY.md, Logging rule 5).
5. **AC-05**: Given the telemetry pipeline to Azure Monitor/Application Insights, when events pass through, then PII fields are redacted/filtered per the redaction policy before export (SECURITY.md, Logging rules 4 and 8; CC-CMP-003).
6. **AC-06**: Given a Key Vault access denial or an authentication-failure spike above the configured threshold, when it occurs, then an alert fires to the operations destination via Azure Monitor (SECURITY.md, Logging rule 8).
7. **AC-07**: Negative — a failure of the logging/telemetry pipeline MUST NOT expose log content to clients or crash request processing, and security-event emission failures in authorization/gating paths MUST be visible operationally (the denial still stands per issue 021; the emission failure itself is alertable).

## Failure Behavior
- **On Invalid Input**: Hostile input never breaks logging — it is encoded and stored as inert field data (AC-04); malformed values never cause an entry to be dropped silently.
- **On System Error**: Request processing does not fail open or closed on logging availability (logging is detection, not authorization — authz/gating fail-closed is issue 021's scope); telemetry export failures are buffered/retried per the OpenTelemetry pipeline and alert if sustained. No security event is silently discarded without an operational signal.
- **Alerting**: Key Vault access denials; authentication-failure spikes; sustained telemetry-export failure; webhook signature-verification failures arrive through this pipeline as security events (SECURITY.md, Secret handling rule 9, emitted by issue 041). Thresholds are configuration (see Open Questions); destination is Azure Monitor alerting per ARCHITECTURE.md Observability.

## Test Strategy
- **Unit Tests**: Redaction filters (PII field masking); log-sanitization encoder against CR/LF, ANSI, and structure-breaking payloads; event-model field completeness. ≥ 80% coverage (CC-QA-001).
- **Integration Tests**: End-to-end event emission for each event class in AC-01 through a test sink; log-content scan of the full integration-test log corpus for secret/token patterns (AC-02); injection-payload round-trip asserting single-record integrity (AC-04); alert-rule wiring validated against the Log Analytics/Azure Monitor configuration (deployed via Terraform, issue 008).
- **Security Tests**: SAST/analyzer rule for interpolated log calls (AC-03) wired into the issue 006 merge gates; secret-scan patterns extended to captured log output; manual pentest checklist: log-injection attempts via user-input fields.
- **Compliance Tests**: Automated evidence of event presence for each security event class (SECURITY.md, Logging rule 3); redaction-policy configuration snapshot per release; retention applicability recorded for issue 090 (Logging rule 9).
- **Coverage Target**: ≥ 80% branch coverage for the logging/redaction module; tests tagged `CC-SEC-010`, `CC-NFR-003` (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 001 Solution scaffold; 095 OpenTelemetry observability to Azure Monitor (export pipeline and workspace — mechanics here can land against a test sink first); 014 Azure Key Vault integration (source of the access-denial signal); 021 RFC 9457 error handling (server-side logging of full error details, correlation IDs)
- **Downstream**: 019 Baseline rate limiting (rejection events, throttle-spike signal); 041 Inbound processor webhook verification (signature-failure security events per Secret handling rule 9); 058/059/060 authentication issues (authn success/failure events, OTP throttle events — codes never logged); 062 Object-level authorization suite (denial events); 081 Append-only audit store with WORM retention (the separate, database-enforced audit stream per Logging rule 6 — distinct from operational security logging and NOT absorbed here); 090 Retention schedule and automated deletion jobs (logs in scope per Logging rule 9); 096 Per-market synthetic probes (consume the same monitoring plane)
- **External**: Microsoft Azure — Azure Monitor, Application Insights, Log Analytics (ARCHITECTURE.md, Observability); Azure Key Vault (denial events)
- **AT RISK**: Telemetry & backup residency open decision (ARCHITECTURE.md, "Known unknowns") governs where the centralized log store may live for EU/IN-origin events (CC-CMP-003/006).

## Implementation Notes
- **Constraints**: .NET 10 `ILogger` structured templates with OpenTelemetry export to Azure Monitor (ARCHITECTURE.md, Observability) — first-party stack, no new logging frameworks without the SECURITY.md Dependency Rules justification; redaction implemented as a telemetry processor/enricher so it applies uniformly to all exporters; correlation IDs from W3C trace context shared with issue 021; the append-only *audit* store (order transitions, privileged dashboard actions — SECURITY.md, Logging rule 6) is a separate database-enforced system owned by issue 081: this issue's operational security logging complements it and must not be conflated with it.
- **Anti-Patterns**: MUST NOT interpolate or concatenate user data into log message strings (`$"..."`/`string.Format` into the message — analyzer-blocked); MUST NOT log request bodies or headers wholesale (they can contain tokens, addresses, and payment-flow parameters); MUST NOT log capability tokens, OTP codes, `Authorization` headers, or Set-Cookie values under any log level including Debug; MUST NOT treat Application Insights defaults as compliant — PII filtering is explicit (Logging rule 8); MUST NOT write security events only on failure paths (successes are required for authn per rule 3).
- **AI Development Guidance**: AI-generated code frequently logs entire request/exception objects — reviewers specifically check log statements for secret/PII exposure and interpolation. All AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- The authentication-failure spike threshold (rate, window) and alert destinations/on-call routing are not specified in the specs; they need an operational decision.
- The concrete PII redaction policy (which fields count as PII per data class, hashing vs. masking vs. dropping, whether IP addresses are retained for rate-limiting forensics) is not defined; it must align with the CC-CMP-003 retention/minimization schedule owned by issues 090/091.
- Whether actor identifiers in security events use stable pseudonymous IDs versus raw account identifiers (an enumeration/minimization trade-off) is unspecified.
- Log retention duration for operational security logs (as distinct from the 7-year financial audit stream, CC-DSH-004) is not specified; it belongs to the CC-CMP-003 retention schedule (issue 090).
- The AT-RISK telemetry-residency decision above (ARCHITECTURE.md, "Known unknowns") — not to be resolved here.
