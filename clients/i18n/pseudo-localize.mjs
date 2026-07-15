#!/usr/bin/env node
/**
 * Pseudo-localization generator (issue 065; CC-I18N-005, CC-QA-006) — run as
 * `npm run i18n:pseudo`.
 *
 * Generates the en-XA pseudo-locale bundle from the en-US bundle: literal
 * text is accented and expanded to at least 130% of the source length and the
 * whole message is wrapped in ⟦…⟧ markers, while placeholders and ICU
 * structure are preserved. The output is written under
 * projects/storefront/src/assets/i18n/pseudo/ and immediately re-validated by
 * the SAME untrusted-input pipeline as real translations (issue 064;
 * SECURITY.md, Input validation rules 1 and 7) — generated resources are not
 * hand-trusted. Generation fails closed: nothing is written if the source
 * bundle or the generated bundle fails validation.
 *
 * Note: locale visual-regression tooling (screenshots/diffs over the top-20
 * templates) is an OPEN question in issue 065 — no screenshot library is
 * chosen or added here (SECURITY.md, Dependency Rules).
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  I18N_ASSETS_DIR,
  PSEUDO_DIR,
  PSEUDO_LOCALE,
  PSEUDO_SOURCE_LOCALE,
  pseudoLocalizeBundle,
  readBundleFile,
  validateBundle,
  validateBundleSet,
} from './lib/messages.mjs';

function main() {
  const sourcePath = join(I18N_ASSETS_DIR, `${PSEUDO_SOURCE_LOCALE}.json`);
  const { bundle: source, error } = readBundleFile(sourcePath);
  if (error) {
    throw new Error(error);
  }
  const sourceErrors = validateBundle(`${PSEUDO_SOURCE_LOCALE}.json`, source);
  if (sourceErrors.length > 0) {
    throw new Error(`source bundle invalid: ${sourceErrors.join('; ')}`);
  }

  const pseudo = pseudoLocalizeBundle(source);

  // The generated bundle passes the identical validation as real translations
  // (schema, no-HTML, ICU grammar, parity + placeholder consistency vs. source).
  const pairErrors = validateBundleSet(
    new Map([
      [`${PSEUDO_SOURCE_LOCALE}.json`, source],
      [`pseudo/${PSEUDO_LOCALE}.json`, pseudo],
    ]),
  );
  if (pairErrors.length > 0) {
    throw new Error(`generated pseudo bundle failed validation: ${pairErrors.join('; ')}`);
  }

  mkdirSync(PSEUDO_DIR, { recursive: true });
  const outPath = join(PSEUDO_DIR, `${PSEUDO_LOCALE}.json`);
  writeFileSync(outPath, JSON.stringify(pseudo, null, 2) + '\n');
  console.log(`i18n:pseudo OK -> ${outPath} (${Object.keys(pseudo).length} keys, >=130% expansion)`);
}

try {
  main();
} catch (e) {
  console.error(`i18n:pseudo FAILED: ${e.message}`);
  process.exit(1);
}
