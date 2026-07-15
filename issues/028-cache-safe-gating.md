# 028 · Cache-safe gating: cache keys, no-store personalized responses, gated SSR transfer state

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-MKT-009, CC-SEC-013
- **Title**: Cache-safe gating: cache keys, no-store personalized responses, gated SSR transfer state
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: No SSR, edge, or CDN cache MAY serve a response gated for one market/locale to another: cache keys MUST derive solely from server-side transacting-market/locale state (never from client hints), authenticated/cart/checkout/per-user/personalized responses MUST be `Cache-Control: no-store` and never edge-cached, and SSR transfer/hydration state MUST contain only data already authorized and gated for that exact response (REQUIREMENTS.md CC-MKT-009; SECURITY.md, HTTP boundary rule 10; CC-SEC-013).
- **Rationale**: Added by the 2026-07-15 threat model (THREAT_MODEL.md; REQUIREMENTS.md v1.3): CC-MKT-009 "closes the path where a cached non-veg page is served into IN despite CC-MKT-003" — perfect server-side gating (issue 025) is defeated if a cache stores a US-market page and replays it to an IN session, or if the cache key is built from a client hint an attacker can forge. ARCHITECTURE.md "Edge & SSR caching" calls this "load-bearing given the regional-cache topology" of regional read replicas/edges. The SSR transfer-state clause exists because Angular SSR serializes data into the HTML for hydration: an ungated dataset embedded there leaks even if the rendered view is gated.
- **Design**: N/A (caching and serialization behavior; no user-facing design surface).

## Scope
- **Applies To**: Both
- **Components**: ASP.NET Core response-caching/cache-header emission across storefront SSR, portal, and API responses; Angular SSR (`@angular/ssr`) transfer/hydration state serialization; edge/CDN cache-key configuration for the regional edges (ARCHITECTURE.md, "Edge & SSR caching"); interacts with the `Cache-Control: no-store` on authenticated/sensitive responses baseline (SECURITY.md, HTTP boundary rule 3, issue 017).
- **Actors**: Anonymous consumers (cacheable gated pages); authenticated consumers, cart/checkout sessions (never-cached responses); attackers attempting cache-poisoning/cache-deception via forged hints.
- **Data Classification**: Public market-gated content (cacheable, keyed); Confidential/Restricted-PII for personalized and authenticated responses (never cached).

## Security Context
- **Defense Layer**: Architecture
- **Threat(s) Addressed**: Cross-market cache leakage (cached non-veg page served into IN, CC-MKT-003 bypass); web cache poisoning/deception via unkeyed client-hint-derived content; personal data exposure through a shared cache; ungated-data disclosure via SSR hydration payloads. OWASP Top Ten A01:2021 Broken Access Control; CWE-524 (use of cache containing sensitive information); CWE-444-adjacent cache-poisoning class; STRIDE: Information Disclosure.
- **Trust Boundary**: The cache tiers (SSR output cache, edge, CDN) sit between server and client and replay responses without re-running gating — every cache tier is therefore part of the enforcement surface and must be keyed/bypassed so it cannot widen access; client hints (`Accept-Language`, geolocation, forgeable cookies) remain untrusted input on the client side of the boundary (SECURITY.md, HTTP boundary rule 10; Authentication rule 10).
- **Zero Trust Consideration**: A cache hit is an access decision made without re-evaluating policy; this requirement makes that decision safe by construction: the key embeds the exact server-resolved gating context (transacting market + locale), and anything personalized never enters a shared cache at all.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline") — HTTP/browser security chapter (cache-control on sensitive responses) and Access Control chapter (no control bypass via infrastructure tiers).
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: AC-3 (access enforcement preserved across cache tiers), SC-8 boundary context N/A — primary: AC-3, SI-10 (client hints never trusted as key input)
- **NIST SP 800-207**: Every access mediated by policy — cache replay is engineered so it can never grant what policy evaluation would deny.
- **Regulatory**: Prevents cached non-veg content entering the IN market regime (CC-MKT-003); prevents personalized-response caching that would expose personal data (GDPR/DPDP context, CC-CMP-001/002).
- **Other**: N/A

## Acceptance Criteria
1. **AC-01**: Given a cacheable anonymous storefront response rendered for transacting market DE / locale de-DE, when a request with transacting market IN (any locale) arrives at any cache tier, then the DE-keyed entry is never served — the IN request resolves only to an entry keyed for IN, or misses and renders freshly gated content (CC-MKT-009).
2. **AC-02** (negative): Given two requests for the same URL that differ only in client hints (`Accept-Language`, forged geolocation, a client-forgeable cookie or header), when cache keys are computed, then the keys are identical — client hints contribute nothing to the cache key; only the server-side transacting market + locale state (issue 024) does (SECURITY.md, HTTP boundary rule 10; CC-SEC-013).
3. **AC-03**: Given any authenticated, cart, checkout, per-user, or personalized response, when it is emitted, then it carries `Cache-Control: no-store` and is verifiably absent from every cache tier (SSR/output cache, edge, CDN) (CC-MKT-009; SECURITY.md, HTTP boundary rules 3 and 10).
4. **AC-04** (negative): Given the SSR-rendered HTML for an IN-market page, when the embedded Angular transfer/hydration state is extracted and inspected, then it contains no non-veg SKU data, no ungated catalog dataset, and no other user's data — only data already authorized and gated for that exact response (CC-MKT-009; SECURITY.md, HTTP boundary rule 10; ARCHITECTURE.md, "Edge & SSR caching").
5. **AC-05**: Given a response whose gating context cannot be determined (transacting market/locale resolution failed or ambiguous), when caching is decided, then the response is treated as uncacheable (`no-store`) — never cached under a guessed or default key (SECURITY.md, Logging rule 2 fail-closed principle).
6. **AC-06**: Given the gated 404 responses of CC-MKT-004 (issue 026), when they are cacheable, then they are keyed by transacting market + locale like any gated response, so an IN 404 for a non-veg URL is never replayed to a US session nor vice versa (CC-MKT-009).
7. **AC-07**: Given cache configuration (server output caching and edge/CDN rules), when it is reviewed in CI, then the market+locale key derivation and the no-store classes are asserted by automated tests/config checks, not convention (CC-MKT-006 spirit: policy as verifiable configuration).

## Failure Behavior
- **On Invalid Input**: Forged client hints never reach key computation (AC-02); requests with unresolvable market/locale state produce uncacheable responses (AC-05). No error detail beyond RFC 9457 generic bodies (SECURITY.md, Logging rule 1).
- **On System Error**: Fail closed: any exception in cache-key derivation or gating-context resolution results in cache bypass/no-store and a freshly gated render — never serving a cached entry whose gating context is uncertain (SECURITY.md, Logging rule 2).
- **Alerting**: Cache-context resolution failures logged as structured security events (SECURITY.md, Logging rule 3); the production IN gating probe (issue 096, CC-NFR-003) exercises cached paths and alerts on any CC-MKT-003/004 violation that would indicate cross-market cache reuse.

## Test Strategy
- **Unit Tests**: .NET 10 unit tests on cache-key derivation (market+locale only; hint-insensitivity), no-store classification of response types, fail-closed behavior on unresolved context. Tagged CC-MKT-009, CC-SEC-013 (REQUIREMENTS.md §17).
- **Integration Tests**: ASP.NET Core integration tests through the real middleware/output-caching pipeline: cross-market replay attempts (AC-01), hint-variation key equality (AC-02), header assertions on every personalized response class (AC-03); Angular SSR tests extracting serialized TransferState from rendered HTML per market and asserting gated content only (AC-04).
- **Security Tests**: Cache-poisoning/deception test class: attacker-controlled hints attempting to poison a shared key or coax a personalized response into cache; DAST against staging includes cached-path probing per market (CC-QA-007).
- **Compliance Tests**: The market-gating CI matrix (issue 027) runs its IN assertions against responses served through the caching pipeline so cache tiers cannot silently reintroduce leakage; production synthetic probes (issue 096) provide continuous evidence.
- **Coverage Target**: ≥ 80% line coverage (CC-QA-001); mutation testing SHOULD cover this gating-adjacent code (CC-QA-001).

## Dependencies
- **Upstream**: 024 Transacting market/locale resolution (the only permitted key input); 025 Server-side gating enforcement API (produces the gated data that transfer state may carry); 017 CSP and security headers (`no-store` baseline per SECURITY.md, HTTP boundary rule 3); 016 middleware ordering; 063 Storefront SSR shell (hydration/transfer-state mechanics); 023 policy-as-data model.
- **Downstream**: 026 404 semantics (cached-404 keying, AC-06); 066/067 menu and PDP pages (cacheable gated pages); 071 SEO surfaces; 096 synthetic probes; 097 RUM/performance budgets (caching is how CC-NFR-002 is met without sacrificing gating).
- **External**: Azure edge/CDN infrastructure per the confirmed multi-region topology (ARCHITECTURE.md, "Technology decisions") — the specific edge/CDN product is not named (see Open Questions).

## Implementation Notes
- **Constraints**: Cache keys derive exclusively from the server-resolved transacting market + locale of issue 024 — never `Vary: Accept-Language`, never geolocation, never a client-forgeable cookie value used raw (SECURITY.md, HTTP boundary rule 10). Personalized classes (authenticated, cart, checkout, per-user) get `Cache-Control: no-store` per SECURITY.md HTTP boundary rules 3 and 10 and must be excluded from edge caching by configuration, not convention. For Angular SSR, transfer/hydration state must be populated only from the already-gated response data of issue 025 — never from a shared ungated catalog object. This applies to the regional read replicas/edges of the confirmed multi-region topology (ARCHITECTURE.md, "Edge & SSR caching": "This makes caching incapable of defeating market gating").
- **Anti-Patterns**: MUST NOT key any cache on client hints (`Accept-Language`, geolocation headers, forgeable cookies) (CC-MKT-009); MUST NOT edge-cache personalized or authenticated responses (SECURITY.md, HTTP boundary rule 10); MUST NOT embed the ungated catalog, another market's SKUs (e.g., non-veg in IN), or another user's data in SSR transfer state (ARCHITECTURE.md, "Edge & SSR caching"); MUST NOT cache under a default key when gating context is unresolved (fail closed).
- **AI Development Guidance**: AI-generated code passes the identical merge gates plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7); caching code sits on the gating path and is in scope for the mutation-testing recommendation of CC-QA-001.

## Open Questions
- The concrete edge/CDN product for the regional edges is not named in ARCHITECTURE.md (Azure is the confirmed cloud, but no specific CDN/Front Door-class service appears in the decision record); edge-tier key-configuration specifics cannot be finalized until it is chosen.
- Whether currency/price display ever varies within a single market+locale pair in a way that needs an additional cache-key dimension is unaddressed by the specs (prices are per-market, CC-PRC-001, which suggests market+locale suffices); recorded for confirmation.
- Cache TTLs and invalidation strategy for gated pages (e.g., on policy or promotion change — CC-PRC-006 notes expired promotions must not apply "even if cached UI still displays them", with the order service as final authority) are unspecified; this issue enforces gating safety, not freshness policy.
