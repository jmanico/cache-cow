# 064 · ICU MessageFormat resource pipeline with CI validation

Part of the Cache Cow v1 build-out epic.

## Metadata
- **ID**: CC-I18N-002, CC-QA-006
- **Title**: ICU MessageFormat resource pipeline with CI validation
- **Version**: 1.0.0
- **Status**: Draft
- **Author**: Cache Cow spec decomposition
- **Last Updated**: 2026-07-15
- **Priority**: Critical
- **Classification**: Security

## Requirement
- **Description**: All user-facing strings MUST be externalized in ICU MessageFormat resource files with key parity across the seven launch locales validated in CI, and translation files MUST be treated as untrusted input — ICU MessageFormat only, no HTML in string resources, interpolated values escaped by default, and schema validation including placeholder consistency across locales enforced as a blocking CI gate.
- **Rationale**: CC-I18N-002 requires externalized ICU MessageFormat resources with key parity across locales, validated in CI, and delegates security handling to SECURITY.md, Input validation rule 7, which treats translation files as untrusted input: "five languages means five supply chains for strings" — a compromised or malformed translation file is an injection vector into every rendered page. CC-QA-006 makes translation resource schema validation and key parity part of the i18n CI gates. ARCHITECTURE.md Dependency rule 7 requires translation-file CI validation to derive from published schemas — no hand-maintained parallel definitions.
- **Design**: N/A (pipeline and CI infrastructure; consuming surfaces cite DESIGN.md themselves). DESIGN.md §9 governs how copy is written (per-market native copy, not translated puns) but is an editorial rule, not a pipeline rule.

## Scope
- **Applies To**: Both (Angular clients consume the resources; server-rendered surfaces such as SSR output and transactional email templates draw on the same externalized-resource discipline)
- **Components**: String-resource file format and layout for the seven launch locales (en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN); the published JSON Schema the resources validate against; CI validation jobs (schema, key parity, placeholder consistency, no-HTML rule); the runtime message-formatting layer with escape-by-default interpolation. Explicitly excluded: pseudo-localization build and locale visual regression (issue 065), transactional email templates and locale fallback (issue 043, CC-I18N-006), locale-aware number/currency formatting (issue 034), and the storefront shell that consumes the strings (issue 063).
- **Actors**: Translators/localization contributors (untrusted input source), CI system, all client and server rendering surfaces.
- **Data Classification**: Public (UI strings), but handled as untrusted input per SECURITY.md, Input validation rule 7.

## Security Context
- **Defense Layer**: Input Validation
- **Threat(s) Addressed**: Supply-chain injection through translation files — HTML/script smuggled into string resources reaching rendered pages (XSS, CWE-79); placeholder mismatch causing broken or misleading rendered output; OWASP Top Ten A03:2021 Injection (SECURITY.md, Input validation rules 1 and 7).
- **Trust Boundary**: The translation-file ingestion point: every string resource crossing into the build is attacker-controlled until schema-validated (SECURITY.md, Input validation rule 1 lists translation files explicitly).
- **Zero Trust Consideration**: No translation file is trusted by origin: all seven locales' resources pass identical schema validation; invalid input is rejected, never sanitized into acceptance (SECURITY.md, Input validation rule 1); interpolated values are escaped by default at render so even a validated string cannot introduce raw markup.

## Standards Alignment
- **OWASP ASVS**: ASVS 5.0 V1 Encoding and Sanitization (output escaping of interpolated values); V2 Validation and Business Logic (schema validation of inbound resources); platform-wide ASVS 5.0 Level 2 baseline (SECURITY.md, "Baseline")
- **OWASP AISVS**: N/A
- **NIST SP 800-53**: SI-10 (Information Input Validation)
- **NIST SP 800-207**: N/A
- **Regulatory**: N/A
- **Other**: Unicode ICU MessageFormat (format mandated by CC-I18N-002); BCP 47 locale identifiers (REQUIREMENTS.md §2)

## Acceptance Criteria
1. **AC-01**: Given the storefront, portal, and dashboard codebases, when user-facing strings are audited, then they are externalized in ICU MessageFormat resource files — not embedded in components or server code (CC-I18N-002).
2. **AC-02**: Given resource files for the seven launch locales, when a key exists in one locale but is missing in another, then the CI key-parity check fails the build (CC-I18N-002, CC-QA-006).
3. **AC-03**: Given a translation file containing HTML markup in any string resource, when CI validation runs, then the build fails — no HTML is permitted in string resources (SECURITY.md, Input validation rule 7; negative case).
4. **AC-04**: Given a string whose placeholders differ across locales (name, count, or type mismatch against the source locale), when CI validation runs, then the build fails with the offending key and locales identified (SECURITY.md, Input validation rule 7: placeholder consistency; CC-QA-006).
5. **AC-05**: Given any rendered message, when an interpolated value contains markup or control characters (e.g., `<script>alert(1)</script>`), then the value renders inert as text — interpolation escapes by default and there is no opt-out to raw HTML in the message pipeline (SECURITY.md, Input validation rules 5 and 7; negative case).
6. **AC-06**: Given a resource file that is not valid ICU MessageFormat or violates the published schema, when CI validation runs, then the build fails and the file is rejected rather than sanitized into acceptance (SECURITY.md, Input validation rule 1; CC-QA-006).
7. **AC-07**: Given the CI validation, generated documentation of the resource format, and the runtime loader, when their schema sources are compared, then all derive from the same published schema — no hand-maintained parallel definitions (ARCHITECTURE.md, Dependency rule 7).

## Failure Behavior
- **On Invalid Input**: A translation file failing schema, key-parity, no-HTML, or placeholder validation blocks the merge (blocking CI gate, no warn-only mode); the failure output names the file, key, and rule without echoing raw invalid content into logs unencoded (SECURITY.md, Logging rule 5).
- **On System Error**: Fail closed: a validation-job error is a build failure, never a skip; at runtime, a message-format error must never fall through to rendering raw/unescaped input (SECURITY.md, Logging rule 2 posture applied to the rendering path).
- **Alerting**: CI failure on the merge request is the primary alert; repeated validation failures on translation contributions are visible in CI history (no runtime alerting surface is defined for this pipeline in the specs).

## Test Strategy
- **Unit Tests**: Validator tests for each rule (missing key, extra key, HTML in resource, placeholder name/count/type mismatch, malformed ICU syntax) across all seven locales; escape-by-default interpolation tests with hostile values.
- **Integration Tests**: End-to-end build test proving a seeded violation in any single locale fails CI; round-trip test that a valid resource set renders correctly in an Angular component and in SSR output.
- **Security Tests**: Injection corpus through interpolated parameters asserting inert rendering (composes with the raw-HTML-sink CI grep gate, SECURITY.md Deployment rule 7); fuzzing of ICU syntax edge cases into the validator (reject, never crash-open).
- **Compliance Tests**: CI artifacts retained as evidence that schema validation and key parity ran on every merge (CC-QA-006; CC-I18N-002 "validated in CI").
- **Coverage Target**: ≥ 80% per package (CC-QA-001); tests tagged CC-I18N-002, CC-QA-006 (REQUIREMENTS.md §17).

## Dependencies
- **Upstream**: 004 "Angular workspace scaffold" (workspaces the resources live in); 006 "CI merge gates" (this issue's validators run as blocking gates alongside them).
- **Downstream**: 063 "Storefront SSR shell" (all shell strings); 065 "Locale layout resilience" (pseudo-localization builds on this pipeline); 043 "Transactional order emails in all locales" (CC-I18N-006 templates use the same externalization discipline); 066–078 (every string-bearing surface); 034 "Locale-aware price formatting" (numbers/currency formatting is separate but renders inside these messages).
- **External**: None. (Contentful CMS content is a separate untrusted pipeline through the sanitizing allowlist renderer — issue 072; CMS rich text never enters string resources.)

## Implementation Notes
- **Constraints**: ICU MessageFormat only (CC-I18N-002; SECURITY.md, Input validation rule 7); seven launch locales with key parity (CC-I18N-001/002); validation schema is the single published source of truth (ARCHITECTURE.md, Dependency rule 7); Angular clients built AOT with strictTemplates (SECURITY.md, Deployment rule 9).
- **Anti-Patterns**: MUST NOT put HTML in string resources or render messages through raw-HTML sinks (`bypassSecurityTrust*`, unsanitized `[innerHTML]` — banned with CI grep gate, SECURITY.md, Input validation rule 5); MUST NOT sanitize invalid resources into acceptance — reject them (Input validation rule 1); MUST NOT hand-maintain a second definition of the resource schema (Dependency rule 7); MUST NOT bake translated taglines into logo assets (DESIGN.md §2.3 — translated taglines are separate text).
- **AI Development Guidance**: AI-generated code (including AI-drafted translations, which are still untrusted translation input subject to every gate above) passes the identical merge gates — tests green, coverage per CC-QA-001, lint clean, SAST/SCA/secret-scan/raw-HTML-sink gates — plus mandatory human review, no auto-merge (CC-QA-002; SECURITY.md, Deployment rule 7).

## Open Questions
- Runtime behavior for a missing key that escapes CI (e.g., key added at runtime boundary): the specs define CI-time parity but no runtime fallback rule for UI strings (CC-I18N-006 defines fallback for email templates only — the market's primary language). Behavior on a runtime miss is unspecified.
- The source-of-truth locale for parity comparison (presumably en-US) is not named in the specs.
- The specs do not name a validation/formatting library; SECURITY.md Dependency Rules 1–8 (minimal, actively maintained, pinned) govern any choice.
