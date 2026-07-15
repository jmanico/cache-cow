# 093 · Sender domain authentication (SPF/DKIM/DMARC)

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-018
- **Title**: Sender domain authentication (SPF/DKIM/DMARC)
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: Every domain that sends platform mail through Azure Communication Services MUST publish and enforce SPF, DKIM, and DMARC with policy `p=reject`, with aggregate reports monitored (SECURITY.md, Email and messaging security rule 1; REQUIREMENTS.md CC-SEC-018).
- **Rationale**: Order, shipment, OTP, and marketing mail are prime phishing lures; an unauthenticated sender domain lets an attacker spoof Cache Cow to its own customers (SECURITY.md, Email and messaging security rule 1 — added by the 2026-07-15 threat model, THREAT_MODEL.md, as CC-SEC-018). DMARC at `p=reject` with monitored aggregate reports makes spoofed mail rejectable by receivers and detectable by Cache Cow.
- **Design**: N/A (infrastructure/DNS issue; no user-facing surface).

## Scope
- **Applies To**: Both (protects all platform mail: transactional, OTP, and marketing sent via Azure Communication Services)
- **Components**: DNS zones for all sending domains (Terraform-managed), Azure Communication Services sender configuration (DKIM signing, verified domains), aggregate-report ingestion/monitoring
- **Actors**: Receiving mail systems (SPF/DKIM/DMARC evaluators), platform operations (report monitoring), attackers attempting sender spoofing
- **Data Classification**: Internal (DNS records are public by nature; aggregate reports are internal operational data)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Sender spoofing / phishing of Cache Cow customers using the platform's own domains (threat-model-derived, CC-SEC-018); STRIDE: Spoofing
- **Trust Boundary**: The public email ecosystem — receiving mail servers verify that mail claiming a Cache Cow sending domain actually originated from authorized infrastructure
- **Zero Trust Consideration**: Receivers do not trust the `From:` claim; SPF authorizes sending hosts, DKIM cryptographically binds the message to the domain, and DMARC `p=reject` instructs rejection on alignment failure. Cache Cow independently verifies the posture via monitored aggregate reports rather than assuming the records remain correct.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, Baseline); email sender authentication is an infrastructure control elaborated by SECURITY.md, Email and messaging security rule 1
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-8 (transmission integrity/authenticity of platform mail)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: SPF, DKIM, and DMARC as named by SECURITY.md, Email and messaging security rule 1 (RFC 8058 unsubscribe is issue 092's scope)

## Acceptance Criteria

1. **AC-01**: Given every domain that sends platform mail through Azure Communication Services, when its DNS zone is queried, then a valid SPF record authorizing only the platform's sending infrastructure is published (CC-SEC-018).
2. **AC-02**: Given mail sent through Azure Communication Services from any sending domain, when a receiver evaluates it, then it carries a valid DKIM signature that verifies against the published selector keys for that domain (CC-SEC-018).
3. **AC-03**: Given every sending domain, when its DMARC record is queried, then policy is `p=reject` (not `p=none` or `p=quarantine` as an end state) with an aggregate-report (`rua`) destination that the platform actually ingests (CC-SEC-018).
4. **AC-04**: Given a message spoofing a Cache Cow sending domain from unauthorized infrastructure, when a DMARC-enforcing receiver evaluates it, then it fails SPF/DKIM alignment and is rejected per `p=reject` — spoofed mail MUST NOT pass as authenticated (negative case, CC-SEC-018).
5. **AC-05**: Given aggregate DMARC reports arrive, when they are ingested, then they feed centralized monitoring with alerting on spikes of unauthenticated mail claiming platform domains (CC-SEC-018; SECURITY.md, Logging rule 3; Azure Monitor per ARCHITECTURE.md, Cross-cutting Observability).
6. **AC-06**: Given the DNS records are provisioned, when their definitions are inspected, then they are managed as Terraform code in CI/CD — not manually created — with drift detection covering them (SECURITY.md, Deployment rules 1, 4).
7. **AC-07**: Given transactional mail headers/metadata that may be logged, when OTP or guest-capability mail is sent, then no capability tokens or secrets appear in logged headers/metadata (SECURITY.md, Email and messaging security rule 1; Logging rule 4) — this issue's monitoring MUST NOT ingest message bodies or secrets.

## Failure Behavior
- **On Invalid Input**: N/A (no public request endpoint; aggregate-report ingestion validates report format and discards malformed reports with a logged warning — reports are untrusted third-party input per SECURITY.md, Input validation rule 1).
- **On System Error**: Fail closed at the ecosystem level by design: with `p=reject` published, alignment failures result in rejection by receivers regardless of platform availability. Report-ingestion outages alert; they do not weaken the published policy.
- **Alerting**: Alert on DMARC aggregate-report spikes of failing/unauthenticated volume, on DNS drift from the Terraform-declared records, and on DKIM verification failures reported for legitimate sending paths (SECURITY.md, Logging rule 3; Deployment rule 4).

## Test Strategy
- **Unit Tests**: .NET 10 xUnit tests for the aggregate-report parser (malformed-report rejection, alignment-failure aggregation) if ingestion is first-party code.
- **Integration Tests**: Pipeline checks that resolve SPF/DKIM/DMARC records for every configured sending domain and assert policy content (`p=reject`, valid rua, DKIM selectors present); end-to-end send through Azure Communication Services asserting DKIM signature verification on the received message in a test harness.
- **Security Tests**: Negative test sending unaligned mail (unauthorized source claiming the domain) to a DMARC-evaluating test receiver and asserting rejection disposition; IaC scanning of the Terraform DNS module (SECURITY.md, Deployment rule 4).
- **Compliance Tests**: Continuous synthetic check (Azure Monitor availability-test style, CC-NFR-003 pattern) that re-queries the DNS records and alerts on regression from `p=reject`; evidence archive of aggregate-report monitoring.
- **Coverage Target**: ≥ 80% for any first-party ingestion code (CC-QA-001); tests tagged CC-SEC-018 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (DNS records as reviewed, pinned IaC); 009 IaC policy-as-code, scanning, and drift detection; 095 OpenTelemetry observability to Azure Monitor (report-monitoring alert destination).
- **Downstream**: 043 Transactional order emails (CC-ORD-007); 059 Email OTP hardening (CC-SEC-016 — OTP mail is a named phishing lure); 092 Marketing email consent and one-click unsubscribe (CC-CMP-005 — marketing sends should not begin before enforcement).
- **External**: Azure Communication Services (confirmed email provider — domain verification and DKIM signing configuration); DNS hosting for the sending domains (managed via Terraform per SECURITY.md, Deployment rule 1).

## Implementation Notes
- **Constraints**: All records provisioned through Terraform executed in CI/CD with short-lived least-privilege credentials; no manual DNS edits (SECURITY.md, Deployment rules 1–4). Azure Communication Services domain verification and DKIM key configuration follow the provider's managed flow; any signing material or API credentials live in Key Vault only (SECURITY.md, Secret handling rules 1–2). Rollout may pass through a monitoring phase, but the ratified end state is `p=reject` — treat anything weaker as an open finding, mirroring the CSP Report-Only-then-enforce pattern (SECURITY.md, HTTP boundary rule 2).
- **Anti-Patterns**: MUST NOT leave DMARC at `p=none`/`p=quarantine` as a permanent state (CC-SEC-018 requires `p=reject`). MUST NOT publish permissive SPF mechanisms that authorize the world (e.g., a bare `+all`). MUST NOT let aggregate-report ingestion or logging capture message bodies, tokens, or secrets (SECURITY.md, Logging rule 4; Email rule 1). MUST NOT hand-manage DNS outside Terraform (SECURITY.md, Deployment rule 1).
- **AI Development Guidance**: AI-generated code and IaC pass the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7). PR must reference CC-SEC-018 (REQUIREMENTS.md §17).

## Open Questions
- The inventory of sending domains/subdomains (e.g., separate transactional vs. marketing subdomains) is not defined in the specs; the domain plan needs a human decision before records are authored.
- Whether aggregate-report ingestion is first-party code or an operational process/tool is unspecified; SECURITY.md only requires that reports be "monitored".
- DKIM key rotation cadence is not specified for Azure Communication Services-managed signing; confirm whether the provider-managed rotation satisfies the rule or a schedule must be documented.
