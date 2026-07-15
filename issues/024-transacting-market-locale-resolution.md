# 024 · Transacting market/locale resolution: geo proposal, user override persistence

Part of the Cache Cow v1 build-out epic.

> **AT RISK:** Data residency vs. "single primary write region" (ARCHITECTURE.md, "Known unknowns"). Guest-side persistence of the market/locale choice can proceed; server-side persistence of the choice against an EU or India user account is a write of personal data and may be affected by the unresolved write-region/residency conflict.

## Metadata
- **ID**: CC-MKT-002, CC-I18N-001, CC-SEC-012
- **Title**: Transacting market/locale resolution: geo proposal, user override persistence
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Functional

## Requirement
- **Description**: The platform MUST propose a market from IP geolocation as untrusted, user-overridable personalization data, MUST persist the user's explicit market choice across sessions, MUST offer locale selection independent of market (seven launch locales: en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN), and MUST key every market/compliance gating decision exclusively off the resulting server-side transacting-market state — never off geolocation, `Accept-Language`, or any client-supplied locale hint (REQUIREMENTS.md CC-MKT-002, CC-I18N-001, CC-SEC-012; SECURITY.md, Authentication and authorization rule 10).
- **Rationale**: Market drives catalog, currency, tax, and compliance regime; locale drives strings and formatting — they are independent user selections ("a user MAY shop the DE market in English", REQUIREMENTS.md §2). IP geolocation is spoofable and wrong at borders, so it may only *propose* a default; if gating keyed off geolocation or client hints, an attacker (or a misclassified user) could pull gated content into the wrong compliance regime — the reason SECURITY.md Authentication rule 10 exists (CC-SEC-012). The server-side transacting-market state established here is also what cache keys must derive from (CC-MKT-009, issue 028).
- **Design**: DESIGN.md §7 "Region and language switcher" — two independent header controls: market (catalog, currency, compliance) and language (strings); "Never infer one from the other silently." Switcher UI itself is built in issue 063; this issue provides the server-side resolution and persistence it drives.

## Scope
- **Applies To**: Both
- **Components**: Market & Gating Policy bounded context (transacting-market/locale state resolution); storefront SSR request pipeline (ASP.NET Core middleware establishing per-request market/locale state); Identity & Access context only insofar as authenticated users' persisted preference is stored.
- **Actors**: Anonymous consumer, authenticated consumer; all downstream server modules as readers of resolved state.
- **Data Classification**: Internal; the persisted preference tied to an account is Restricted/PII in GDPR/DPDP terms (see AT RISK note).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Gating bypass via forged client hints (spoofed `Accept-Language`, forged geo/IP headers, tampered locale cookies) causing gated content to be served into the wrong market (CC-MKT-003 evasion); OWASP Top Ten A01:2021 Broken Access Control; STRIDE: Spoofing (client masquerading as another market), Elevation of Privilege (accessing market-gated content).
- **Trust Boundary**: Client–server edge: everything arriving with the request (IP-derived geolocation, headers, cookies) is attacker-controlled input (SECURITY.md, Input validation rule 1); the trustworthy artifact is the server-side transacting-market/locale state derived and held by the server.
- **Zero Trust Consideration**: IP geolocation is explicitly untrusted personalization data — proposal only (SECURITY.md, Authentication rule 10). Client-supplied market/locale identifiers are validated against the closed set of six markets and seven locales (issue 003 identity types) and rejected otherwise; no client value flows into a gating decision except through the validated, server-held transacting-market state.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — Access Control chapter (server-side enforcement decisions from trusted state), Session Management chapter (server-associated per-user state).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement from server-side state), SI-10 (validation of client-supplied market/locale identifiers)
- **NIST SP 800-207**: Never trust client-asserted attributes for policy decisions; policy inputs come from the server's own resolved state.
- **Regulatory**: The transacting-market state is the key for per-market compliance regimes (IN veg-only per CC-MKT-003; per-market legal content per CC-CNT-005). GDPR applies to the persisted preference of EU users (CC-CMP-001).
- **Other**: BCP 47 locale strings (REQUIREMENTS.md §2, "Locale").

## Acceptance Criteria
1. **AC-01**: Given a first-time visitor with no persisted choice, when a request arrives, then the server proposes a market from IP geolocation, exposes it as an overridable proposal, and never blocks the user from selecting any of the six markets (CC-MKT-002; SECURITY.md, Authentication rule 10).
2. **AC-02**: Given a user who explicitly selected market DE, when the user returns in a later session from an IP that geolocates to the US, then the transacting market remains DE — the explicit choice persists across sessions and geolocation does not silently override it (CC-MKT-002).
3. **AC-03**: Given a user in market ES, when the user selects locale de-DE, then the transacting market remains ES and only the UI locale changes — market and locale are independent selections across all seven launch locales (CC-I18N-001; REQUIREMENTS.md §2; DESIGN.md §7).
4. **AC-04** (negative): Given any gating decision (e.g., IN catalog exclusion, CC-MKT-003), when the request carries `Accept-Language`, a forged geolocation, or a client-tampered locale/market hint that conflicts with the server-side transacting-market state, then the gating outcome is determined exclusively by the server-side state and does not change (CC-SEC-012; SECURITY.md, Authentication rule 10).
5. **AC-05** (negative): Given a request presenting a market or locale identifier outside the closed launch sets (six markets, seven locales), when the server resolves state, then the value is rejected — it never becomes the transacting market/locale — and the event is a validation rejection per SECURITY.md, Logging rule 3 (SECURITY.md, Input validation rule 1).
6. **AC-06**: Given the resolution middleware, when any downstream module (rendering, search, API, sitemap generation) reads market/locale, then it reads the single server-resolved transacting-market/locale state — there is no alternative resolution path (ARCHITECTURE.md, Dependency rule 1; CC-SEC-012).
7. **AC-07**: Given a signed-out user whose explicit choice was persisted, when any market/locale state is persisted client-side, then the server still validates it against the closed sets on every request before it becomes transacting state (never trusted raw) (CC-SEC-012).

## Failure Behavior
- **On Invalid Input**: Invalid market/locale identifiers rejected server-side; request proceeds without adopting the invalid value; structured validation-rejection log with correlation ID; no internal state disclosed (SECURITY.md, Logging rules 1, 3).
- **On System Error**: Fail closed for gating: any exception while resolving transacting-market state must not resolve to an ungated or broader market context (SECURITY.md, Logging rule 2). Geolocation-lookup failure degrades the *proposal* only (see Open Questions for the unspecified default), never gating correctness.
- **Alerting**: Spikes in market/locale validation rejections logged as security events to Azure Monitor with alerting (SECURITY.md, Logging rule 3; CC-NFR-003).

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on the resolution logic: proposal-vs-override precedence, persistence round-trip, independence of market and locale, closed-set validation, fail-closed on resolution errors. Tagged CC-MKT-002, CC-I18N-001, CC-SEC-012 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests driving the middleware pipeline with forged `Accept-Language`/geo inputs and asserting gating outcomes key only off server state (AC-04); cross-session persistence tests for guest and authenticated flows.
- **Security Tests**: Header/cookie tampering test class (forged hints never alter gating) — feeds the authz suite pattern of CC-QA-005; DAST per CC-QA-007 exercises hint spoofing against staging.
- **Compliance Tests**: These resolution guarantees are asserted market-by-market by the CI gating matrix (issue 027, CC-QA-003) and in production by the IN gating probe (issue 096, CC-NFR-003).
- **Coverage Target**: ≥ 80% line coverage (CC-QA-001); mutation testing SHOULD cover this gating-input code (CC-QA-001).

## Dependencies
- **Upstream**: 003 Shared kernel: Market, Locale, and SKU identity types; 023 Market & Gating Policy: policy-as-data model; 016 security middleware ordering (resolution middleware placement); 058 Consumer authentication (only for account-persisted preference); 061 Session cookie policy (if persistence is cookie-based).
- **Downstream**: 025 Server-side gating enforcement API; 026 404 semantics; 028 Cache-safe gating (cache keys derive from this state); 063 Storefront SSR shell (switcher UI); 068 Cart cross-market rules; 034 price formatting.
- **External**: None confirmed. No geolocation provider is named in the canonical docs (see Open Questions).
- **AT RISK**: Data residency vs. single primary write region (ARCHITECTURE.md, "Known unknowns") for account-side persistence of the preference (EU/India personal data writes).

## Implementation Notes
- **Constraints**: Implement resolution as ASP.NET Core middleware early in the pipeline (after the security middleware of SECURITY.md, HTTP boundary rule 5) so every downstream module sees one resolved state per request. Locale identifiers are BCP 47 (REQUIREMENTS.md §2). The resolved state is what cache keys must derive from (SECURITY.md, HTTP boundary rule 10; issue 028) and what `lang`/`hreflang` rendering keys off (CC-I18N-004, issue 063). Any persistence cookie follows SECURITY.md, Authentication rule 11 (HttpOnly, Secure, SameSite) — noting the market/locale value itself is still re-validated server-side every request (AC-07).
- **Anti-Patterns**: MUST NOT key any gating decision off geolocation, `Accept-Language`, or client-supplied hints (CC-SEC-012); MUST NOT silently infer locale from market or market from locale (DESIGN.md §7); MUST NOT silently override a persisted explicit choice with geolocation (CC-MKT-002); MUST NOT trust a client-stored preference without server-side validation against the closed sets.
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Persistence mechanism for the explicit choice is unspecified: cookie for guests vs. account-profile storage for authenticated users (and precedence between the two when both exist). Account-side storage intersects the open residency decision (AT RISK above).
- Default/fallback market when IP geolocation fails, or when the IP maps to a country outside the six launch markets, is unspecified.
- The IP-geolocation data source/provider is not named in any canonical doc; if it is a third-party dependency it falls under SECURITY.md Dependency Rules.
- How the proposal is surfaced (auto-apply with banner vs. interstitial prompt) is not specified in DESIGN.md.
