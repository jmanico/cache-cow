# 013 · Ingress WAF and DDoS protection

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-SEC-022 (authoring control: SECURITY.md, HTTP boundary rule 11; protects CC-NFR-001 and the CC-FUL-002 cold-chain deadline)
- **Title**: Ingress WAF and DDoS protection
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Medium
- **Classification**: Security

## Requirement
- **Description**: All public ingress — limited to the storefront, wholesale portal, and API gateways — MUST sit behind DDoS protection and a Web Application Firewall as defense in depth on top of application rate limits.
- **Rationale**: SECURITY.md, HTTP boundary rule 11 authors this control (CC-SEC-022, added by the 2026-07-15 threat model): application rate limits (HTTP boundary rule 7; CC-API-008) do not absorb network- or volumetric-layer floods, so ingress needs DDoS protection and a WAF so that the 99.9% storefront/API availability target (CC-NFR-001) and the 48-hour frozen-transit deadline (CC-FUL-002) survive a flood or an application-layer attack that slips a single rate limiter. An outage is not merely lost revenue here — frozen orders already in the cold chain spoil if operations stall. ARCHITECTURE.md ("Technology decisions") and SECURITY.md, HTTP boundary rule 9 fix the protected surface: public ingress is limited to the storefront, portal, and API gateways; everything else is private endpoints.
- **Design**: N/A (no user-facing surface; error/blocking pages, if any, follow DESIGN.md §9 voice rules — no puns in error recovery, DESIGN.md §5.4).

## Scope
- **Applies To**: Both (public ingress for the consumer storefront web app, wholesale portal, and B2B API gateways, in all regions with public ingress)
- **Components**: Public ingress path in front of the storefront, portal, and API gateways; DDoS protection layer; WAF layer and rule policy
- **Actors**: Anonymous internet clients (consumers, attackers), wholesale portal buyers, B2B API clients, platform/security engineers (policy owners)
- **Data Classification**: Internal (WAF policy); WAF/DDoS logs may contain client IPs and request data (Restricted/PII handling per SECURITY.md, Logging rules 4 and 9)

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Volumetric and protocol-layer DDoS (STRIDE Denial of Service); application-layer floods and common web attack classes (OWASP Top Ten A03:2021 Injection, A05:2021 Security Misconfiguration exploitation attempts) filtered before origin; availability loss breaching CC-NFR-001 and stalling the CC-FUL-002 cold chain
- **Trust Boundary**: The internet-to-platform edge — the outermost trust boundary, in front of the storefront/portal/API gateways (SECURITY.md, HTTP boundary rule 9)
- **Zero Trust Consideration**: Edge filtering is defense in depth only: it never replaces origin-side authentication, authorization, input validation (SECURITY.md, Input validation rule 1), or application rate limiting (HTTP boundary rule 7). Origin services continue to treat every request as untrusted even when it has passed the WAF.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 (Level 2 baseline applies in full, per SECURITY.md); V13 Configuration; the WAF supplements but never substitutes for the validation controls of V2 Validation and Business Logic
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SC-5 (denial-of-service protection), SC-7 (boundary protection), SI-4 (system monitoring)
- **NIST SP 800-207**: Edge controls add depth without conferring implicit trust on traffic that passes them
- **Regulatory**: N/A
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given the deployed network topology, when public entry points are enumerated, then exactly the storefront, wholesale portal, and B2B API gateways are publicly reachable, and each sits behind both DDoS protection and a WAF (SECURITY.md, HTTP boundary rules 9 and 11; ARCHITECTURE.md, "Technology decisions").
2. **AC-02** (negative): Given any other platform component (dashboard, data stores, intra-platform services, webhook receivers' internal processing), when reached from the public internet other than via the protected gateways, then the connection fails — any exposure beyond the gateways is a security defect (SECURITY.md, HTTP boundary rule 9; dashboard is VPN-only per rule 8).
3. **AC-03**: Given a volumetric or protocol-layer flood against a public entry point, when DDoS protection engages, then legitimate traffic continues to be served within the availability target (CC-NFR-001; SECURITY.md, HTTP boundary rule 11).
4. **AC-04**: Given an application-layer attack pattern matching WAF rules (e.g., injection payloads), when it hits a public entry point, then the WAF blocks it before origin processing and emits a structured security event (SECURITY.md, Logging rule 3).
5. **AC-05** (negative): Given the WAF is in place, when origin-side controls are reviewed, then application rate limits (429 + `Retry-After`, HTTP boundary rule 7), input validation, and authorization remain fully enforced at the origin — the WAF MUST NOT be relied on as the sole enforcement for any origin-side control (SECURITY.md, HTTP boundary rule 11 is explicitly defense in depth).
6. **AC-06**: Given WAF/DDoS telemetry, when an attack is mitigated or a rule blocks traffic at anomalous rates, then alerts reach centralized security monitoring, and logs follow the PII-redaction and retention rules (SECURITY.md, Logging rules 3, 4, 9; CC-CMP-003).
7. **AC-07**: Given the WAF and DDoS configuration, when it is provisioned or changed, then the change flows through Terraform/GitOps with the policy-as-code gates (issues 008–010) — no console-only configuration.

## Failure Behavior
- **On Invalid Input**: Requests matching attack signatures are blocked at the edge with a generic error response that discloses no internal state (SECURITY.md, Logging rule 1); blocked events are logged with correlation identifiers.
- **On System Error**: The edge layer must not become the outage: degradation of WAF inspection is alerted and risk-accepted behavior must be decided by a human runbook — while origin controls (rate limiting, validation, authz) remain independently enforced so the platform never depends on the WAF alone (defense in depth, HTTP boundary rule 11). Anything touching authorization or gating at origin continues to fail closed (SECURITY.md, Logging rule 2).
- **Alerting**: Alert on DDoS mitigation activation, sustained WAF block-rate spikes, and WAF/DDoS service health degradation, to centralized monitoring (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: IaC-level assertions that every public entry point has WAF + DDoS attached and that no other component is publicly exposed (composes with issue 009 policy-as-code).
- **Integration Tests**: Staging probes: benign traffic passes; representative attack-class payloads are blocked at the edge; direct-access attempts to non-gateway components fail (AC-02).
- **Security Tests**: DAST against staging traverses the WAF per release (CC-QA-007); external penetration test validates edge filtering and confirms origin controls hold independently of the WAF; verify WAF logs redact per Logging rule 4.
- **Compliance Tests**: Automated evidence: edge configuration inventory per release (protected endpoints, policy versions), mitigation-event log retention per CC-CMP-003.
- **Coverage Target**: ≥ 80% for first-party test/tooling code per CC-QA-001; tests tagged CC-SEC-022/CC-NFR-001 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 008 Terraform bootstrap (edge resources provisioned via golden modules); 009 IaC policy-as-code (public-exposure policies).
- **Downstream**: 019 Baseline rate-limiting middleware (origin-side layer this edge control backs up); 056 B2B per-client rate limits; 096 Per-market synthetic probes (availability monitoring that would surface edge failures); 016–018 (origin HTTP boundary hardening that must hold independently of the WAF).
- **External**: Microsoft Azure (confirmed primary cloud — the concrete WAF/DDoS service is an Open Question below).

## Implementation Notes
- **Constraints**: The protected surface is fixed by ARCHITECTURE.md: public ingress only for storefront, portal, and API gateways; multi-region ingress (Americas/Europe/Asia) means edge protection must cover every region's public entry. WAF/DDoS logs enter the observability stack (Azure Monitor / Log Analytics per ARCHITECTURE.md "Cross-cutting: Observability") and are subject to the telemetry-residency open decision only insofar as log storage is concerned (tracked in ARCHITECTURE.md "Known unknowns" — telemetry & backup residency; this issue does not decide log-region placement).
- **Anti-Patterns**: MUST NOT expose any endpoint beyond the three gateway classes (HTTP boundary rule 9); MUST NOT treat the WAF as a substitute for origin-side validation, authz, or rate limiting (rule 11 is defense in depth); MUST NOT configure edge services outside Terraform/GitOps; MUST NOT log unredacted PII or credentials at the edge (Logging rule 4).
- **AI Development Guidance**: AI-generated edge IaC and policy passes the identical blocking gates plus mandatory human review; no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- **The specific Azure WAF/DDoS services are not named in the specs.** SECURITY.md, HTTP boundary rule 11 mandates "DDoS protection and a WAF" generically; the concrete Azure service selection (and tier) is an open implementation choice requiring human ratification.
- WAF rule-set baseline (managed rule set vs. custom rules), tuning process, and false-positive handling workflow are not specified.
- Whether the WAF layer also fronts the inbound processor-webhook receiver path (a distinct untrusted boundary per ARCHITECTURE.md, bounded context 4) — and how to avoid interfering with raw-body signature verification (SECURITY.md, Input validation rule 11) — is not specified.
- Residency/routing of edge logs is entangled with the open telemetry-residency decision (ARCHITECTURE.md, "Known unknowns"); not resolved here.
