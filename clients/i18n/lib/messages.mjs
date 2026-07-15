/**
 * Shared helpers for the ICU MessageFormat resource pipeline (issue 064,
 * client half; CC-I18N-002, CC-QA-006) and the pseudo-localization generator
 * (issue 065, CC-I18N-005).
 *
 * First-party, zero dependencies (SECURITY.md, Dependency Rules 1), mirroring
 * the tokens/ pipeline style. The ICU grammar itself is NOT redefined here:
 * it is imported from the storefront's runtime module (Node 24 native type
 * stripping), so validator and runtime share one published grammar
 * (ARCHITECTURE.md, Dependency rule 7).
 *
 * Translation files are untrusted input (SECURITY.md, Input validation
 * rule 7): they are validated against this schema and REJECTED on any
 * violation — never sanitized into acceptance. Failure output names the
 * file, key, and rule without echoing raw invalid content (Logging rule 5).
 */

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  parseIcu,
  placeholderSignature,
  mapMessageText,
} from '../../projects/storefront/src/app/i18n/icu.ts';

export const CLIENTS_DIR = dirname(dirname(dirname(fileURLToPath(import.meta.url))));
export const I18N_ASSETS_DIR = join(CLIENTS_DIR, 'projects/storefront/src/assets/i18n');
export const PSEUDO_DIR = join(I18N_ASSETS_DIR, 'pseudo');
export const PSEUDO_LOCALE = 'en-XA';
export const PSEUDO_SOURCE_LOCALE = 'en-US';

/** The seven launch locales (CC-I18N-001). */
export const LAUNCH_LOCALES = ['en-US', 'es-ES', 'es-MX', 'de-DE', 'ja-JP', 'en-IN', 'hi-IN'];

/** Published key schema: dotted segments, letters/digits/hyphens. */
const KEY_RE = /^[a-z][a-zA-Z0-9]*(\.[a-zA-Z0-9-]+)+$/;
/** No HTML in string resources — reject on '<' (SECURITY.md, Input validation rule 7). */
const FORBIDDEN_RE = /[<\x00-\x1f]/;

/**
 * Validate one parsed bundle object. Returns a list of error strings
 * ("<file>: <key>: <rule>"); empty list means valid.
 */
export function validateBundle(fileLabel, bundle) {
  const errors = [];
  if (bundle === null || typeof bundle !== 'object' || Array.isArray(bundle)) {
    return [`${fileLabel}: root must be a flat JSON object of string messages`];
  }
  for (const [key, value] of Object.entries(bundle)) {
    if (!KEY_RE.test(key)) {
      errors.push(`${fileLabel}: key '${key}': invalid key (schema: dotted lowerCamel segments)`);
      continue;
    }
    if (typeof value !== 'string' || value.length === 0) {
      errors.push(`${fileLabel}: key '${key}': value must be a non-empty string`);
      continue;
    }
    if (FORBIDDEN_RE.test(value)) {
      errors.push(`${fileLabel}: key '${key}': HTML/control characters are forbidden in string resources`);
      continue;
    }
    try {
      parseIcu(value);
    } catch (e) {
      errors.push(`${fileLabel}: key '${key}': invalid ICU MessageFormat (${e.message})`);
    }
  }
  return errors;
}

/**
 * Validate a full set of bundles: per-bundle schema, key parity across all
 * bundles, and placeholder-signature consistency per key across all bundles.
 * `bundles` is a Map of label -> parsed object. Returns error strings.
 */
export function validateBundleSet(bundles) {
  const errors = [];
  for (const [label, bundle] of bundles) {
    errors.push(...validateBundle(label, bundle));
  }
  if (errors.length > 0) {
    return errors; // schema errors first; parity on malformed data is noise
  }

  // Key parity (CC-I18N-002): every bundle carries exactly the union key set.
  const union = new Set();
  for (const bundle of bundles.values()) {
    for (const key of Object.keys(bundle)) union.add(key);
  }
  for (const [label, bundle] of bundles) {
    for (const key of union) {
      if (!(key in bundle)) errors.push(`${label}: key '${key}': missing (key parity)`);
    }
  }

  // Placeholder consistency (issue 064 AC-04): name/type/option-set per key
  // must be identical in every locale.
  for (const key of union) {
    const signatures = new Map();
    for (const [label, bundle] of bundles) {
      if (key in bundle) {
        signatures.set(label, placeholderSignature(bundle[key]).join(','));
      }
    }
    const distinct = new Set(signatures.values());
    if (distinct.size > 1) {
      const labels = [...signatures.keys()].join(', ');
      errors.push(`key '${key}': placeholder mismatch across ${labels}`);
    }
  }
  return errors;
}

/** Read and parse one bundle file; JSON parse failures become errors, not throws. */
export function readBundleFile(path) {
  try {
    return { bundle: JSON.parse(readFileSync(path, 'utf8')), error: null };
  } catch (e) {
    return { bundle: null, error: `${basename(path)}: unreadable or invalid JSON (${e.message})` };
  }
}

/**
 * Load the launch-locale bundle set (strict: exactly the seven files, no
 * strays) plus the generated pseudo bundle when present.
 */
export function loadAllBundles() {
  const errors = [];
  const bundles = new Map();
  const present = new Set(
    readdirSync(I18N_ASSETS_DIR, { withFileTypes: true })
      .filter((entry) => entry.isFile() && entry.name.endsWith('.json'))
      .map((entry) => entry.name),
  );
  for (const locale of LAUNCH_LOCALES) {
    const name = `${locale}.json`;
    if (!present.has(name)) {
      errors.push(`${name}: missing launch-locale bundle`);
      continue;
    }
    present.delete(name);
    const { bundle, error } = readBundleFile(join(I18N_ASSETS_DIR, name));
    if (error) errors.push(error);
    else bundles.set(name, bundle);
  }
  for (const stray of present) {
    errors.push(`${stray}: unexpected file in i18n resources (only launch locales belong here)`);
  }
  const pseudoPath = join(PSEUDO_DIR, `${PSEUDO_LOCALE}.json`);
  if (existsSync(pseudoPath)) {
    const { bundle, error } = readBundleFile(pseudoPath);
    if (error) errors.push(error);
    else bundles.set(`pseudo/${PSEUDO_LOCALE}.json`, bundle);
  }
  return { bundles, errors };
}

/* ------------------------------------------------------------------ *
 * Pseudo-localization (issue 065, CC-I18N-005)
 * ------------------------------------------------------------------ */

const ACCENT_MAP = {
  a: 'á', b: 'ƀ', c: 'ç', d: 'ð', e: 'é', f: 'ƒ', g: 'ĝ', h: 'ĥ', i: 'í',
  j: 'ĵ', k: 'ķ', l: 'ļ', m: 'ɱ', n: 'ñ', o: 'ö', p: 'þ', q: 'ǫ', r: 'ŕ',
  s: 'š', t: 'ŧ', u: 'ü', v: 'ṽ', w: 'ŵ', x: 'ẋ', y: 'ý', z: 'ž',
  A: 'Á', B: 'Ɓ', C: 'Ç', D: 'Ð', E: 'É', F: 'Ƒ', G: 'Ĝ', H: 'Ĥ', I: 'Í',
  J: 'Ĵ', K: 'Ķ', L: 'Ļ', M: 'Ṁ', N: 'Ñ', O: 'Ö', P: 'Þ', Q: 'Ǫ', R: 'Ŕ',
  S: 'Š', T: 'Ŧ', U: 'Ü', V: 'Ṽ', W: 'Ŵ', X: 'Ẍ', Y: 'Ý', Z: 'Ž',
};

/** Expansion factor: literal text grows to >= 130% of source length (CC-I18N-005). */
export const EXPANSION_FACTOR = 1.3;

function accent(text) {
  return [...text].map((ch) => ACCENT_MAP[ch] ?? ch).join('');
}

/**
 * Pseudo-localize one ICU message: accent literal text, pad it to at least
 * EXPANSION_FACTOR of the source length, and wrap the whole message in
 * ⟦…⟧ markers. Placeholders and ICU structure are preserved so the result
 * passes the same schema, parity, and placeholder validation as any real
 * translation (issue 065, Zero Trust Consideration). The padding characters
 * never include '<', '{', '}', or '#'.
 */
export function pseudoLocalizeMessage(message) {
  const transformed = mapMessageText(message, (text) => {
    const padding = '·'.repeat(Math.max(1, Math.ceil(text.length * (EXPANSION_FACTOR - 1))));
    return `${accent(text)}${padding}`;
  });
  return `⟦${transformed}⟧`;
}

/** Pseudo-localize a whole bundle (sorted keys for deterministic output). */
export function pseudoLocalizeBundle(bundle) {
  const out = {};
  for (const key of Object.keys(bundle).sort()) {
    out[key] = pseudoLocalizeMessage(bundle[key]);
  }
  return out;
}
