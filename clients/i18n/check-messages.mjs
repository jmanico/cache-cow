#!/usr/bin/env node
/**
 * ICU MessageFormat resource validator (issue 064, client half;
 * CC-I18N-002, CC-QA-006) — run as `npm run i18n:check`.
 *
 * Blocking gate, no warn-only mode: validates the seven launch-locale bundles
 * (plus the generated pseudo-locale bundle when present) for
 *   - JSON shape and key schema,
 *   - no HTML ('<') or control characters in any string resource
 *     (SECURITY.md, Input validation rule 7),
 *   - valid ICU MessageFormat (grammar imported from the runtime module —
 *     single source of truth, ARCHITECTURE.md, Dependency rule 7),
 *   - key parity across all locales,
 *   - placeholder name/type/option consistency across all locales.
 *
 * Fails closed: any violation (or any error in this job) exits non-zero.
 * Output names file, key, and rule only — raw invalid content is not echoed
 * (SECURITY.md, Logging rule 5).
 */

import { loadAllBundles, validateBundleSet } from './lib/messages.mjs';

function main() {
  const { bundles, errors } = loadAllBundles();
  errors.push(...validateBundleSet(bundles));
  if (errors.length > 0) {
    for (const error of errors) {
      console.error(`i18n:check: ${error}`);
    }
    console.error(`i18n:check FAILED: ${errors.length} violation(s)`);
    process.exit(1);
  }
  const keyCount = Object.keys(bundles.values().next().value ?? {}).length;
  console.log(`i18n:check OK — ${bundles.size} bundle(s), ${keyCount} keys, parity and placeholders consistent`);
}

try {
  main();
} catch (e) {
  // Fail closed: a validator error is a build failure, never a skip.
  console.error(`i18n:check FAILED: ${e.message}`);
  process.exit(1);
}
