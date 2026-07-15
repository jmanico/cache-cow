/**
 * Node tests for the i18n resource pipeline (issue 064 client half, issue 065
 * pseudo-localization). Requirement tags: CC-I18N-002, CC-I18N-005, CC-QA-006
 * (REQUIREMENTS.md §17).
 *
 * Run: npm run i18n:test
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import {
  EXPANSION_FACTOR,
  LAUNCH_LOCALES,
  loadAllBundles,
  pseudoLocalizeBundle,
  pseudoLocalizeMessage,
  validateBundle,
  validateBundleSet,
} from '../lib/messages.mjs';
import { parseIcu, placeholderSignature, formatIcu } from '../../projects/storefront/src/app/i18n/icu.ts';

const VALID = {
  'shell.greeting': 'Hello {name}',
  'shell.items': '{count, plural, one {# item} other {# items}}',
  'shell.plain': 'Plain text',
};

/* ------------------------- schema validation (CC-I18N-002) ------------------------- */

test('a valid bundle passes validation', () => {
  assert.deepEqual(validateBundle('en-US.json', VALID), []);
});

test('HTML in a string resource is rejected — AC-03 (SECURITY.md, Input validation rule 7)', () => {
  const errors = validateBundle('de-DE.json', { 'shell.plain': 'ok <b>bold</b>' });
  assert.equal(errors.length, 1);
  assert.match(errors[0], /HTML\/control characters are forbidden/);
  // The raw invalid content is not echoed back (Logging rule 5).
  assert.ok(!errors[0].includes('<b>'));
});

test('control characters in a string resource are rejected', () => {
  const errors = validateBundle('en-US.json', { 'shell.plain': 'bad' + String.fromCharCode(7) + 'bell' });
  assert.equal(errors.length, 1);
  assert.match(errors[0], /forbidden/);
});

test('malformed ICU syntax is rejected, not sanitized into acceptance — AC-06', () => {
  for (const bad of ['{unclosed', 'text } stray', '{n, plural, one {x}}', '{n, number}', '{n, select, a {x}}']) {
    const errors = validateBundle('x.json', { 'shell.plain': bad });
    assert.equal(errors.length, 1, `expected rejection for: ${JSON.stringify(bad)}`);
  }
});

test('non-string and empty values are rejected', () => {
  assert.equal(validateBundle('x.json', { 'shell.plain': 42 }).length, 1);
  assert.equal(validateBundle('x.json', { 'shell.plain': '' }).length, 1);
  assert.equal(validateBundle('x.json', ['nope']).length, 1);
});

test('invalid keys are rejected by the published key schema', () => {
  assert.equal(validateBundle('x.json', { 'no-dots': 'x' }).length, 1);
  assert.equal(validateBundle('x.json', { 'Shell.upper': 'x' }).length, 1);
});

/* ------------------------- parity and placeholders (AC-02, AC-04) ------------------------- */

test('a key missing in one locale fails key parity — AC-02', () => {
  const errors = validateBundleSet(
    new Map([
      ['en-US.json', VALID],
      ['de-DE.json', { 'shell.greeting': 'Hallo {name}', 'shell.items': '{count, plural, one {# Artikel} other {# Artikel}}' }],
    ]),
  );
  assert.equal(errors.length, 1);
  assert.match(errors[0], /de-DE\.json: key 'shell\.plain': missing \(key parity\)/);
});

test('placeholder name mismatch across locales fails with key and locales named — AC-04', () => {
  const errors = validateBundleSet(
    new Map([
      ['en-US.json', { 'shell.greeting': 'Hello {name}' }],
      ['es-ES.json', { 'shell.greeting': 'Hola {nombre}' }],
    ]),
  );
  assert.equal(errors.length, 1);
  assert.match(errors[0], /key 'shell\.greeting': placeholder mismatch across en-US\.json, es-ES\.json/);
});

test('placeholder type mismatch (arg vs plural) fails — AC-04', () => {
  const errors = validateBundleSet(
    new Map([
      ['en-US.json', { 'shell.items': '{count, plural, one {# item} other {# items}}' }],
      ['ja-JP.json', { 'shell.items': '{count} 個' }],
    ]),
  );
  assert.equal(errors.length, 1);
  assert.match(errors[0], /placeholder mismatch/);
});

/* ------------------------- the real committed bundles ------------------------- */

test('the seven launch-locale bundles exist and pass the full validation set', () => {
  const { bundles, errors } = loadAllBundles();
  assert.deepEqual(errors, []);
  for (const locale of LAUNCH_LOCALES) {
    assert.ok(bundles.has(`${locale}.json`), `missing bundle for ${locale}`);
  }
  assert.deepEqual(validateBundleSet(bundles), []);
});

/* ------------------------- pseudo-localization (issue 065, CC-I18N-005) ------------------------- */

function literalTextLength(message) {
  let length = 0;
  const walk = (nodes) => {
    for (const node of nodes) {
      if (node.kind === 'text') length += node.value.length;
      else if (node.kind === 'plural' || node.kind === 'select') {
        for (const body of node.options.values()) walk(body);
      }
    }
  };
  walk(parseIcu(message));
  return length;
}

test('pseudo-localization expands literal text to >= 130% and wraps in markers', () => {
  const source = 'Regional BBQ, delivered frozen';
  const pseudo = pseudoLocalizeMessage(source);
  assert.ok(pseudo.startsWith('⟦') && pseudo.endsWith('⟧'));
  assert.ok(
    literalTextLength(pseudo) >= Math.ceil(source.length * EXPANSION_FACTOR),
    'expanded literal text must meet the 130% budget (CC-I18N-005)',
  );
});

test('pseudo-localization preserves placeholder signatures (issue 064 validation still passes)', () => {
  const source = 'Hi {name}, {count, plural, =0 {none} one {# item} other {# items}}';
  const pseudo = pseudoLocalizeMessage(source);
  assert.deepEqual(placeholderSignature(pseudo), placeholderSignature(source));
  // And the pseudo message still formats.
  const out = formatIcu(pseudo, 'en-XA', { name: 'Jo', count: 2 });
  assert.ok(out.includes('2'));
  assert.ok(out.includes('Jo'));
});

test('the generated pseudo bundle passes the SAME parity validation as real translations', () => {
  const { bundles } = loadAllBundles();
  const source = bundles.get('en-US.json');
  const pseudo = pseudoLocalizeBundle(source);
  const errors = validateBundleSet(
    new Map([
      ['en-US.json', source],
      ['pseudo/en-XA.json', pseudo],
    ]),
  );
  assert.deepEqual(errors, []);
  // Every message meets the expansion budget.
  for (const [key, message] of Object.entries(source)) {
    const min = Math.ceil(literalTextLength(message) * EXPANSION_FACTOR);
    assert.ok(
      literalTextLength(pseudo[key]) >= min,
      `key '${key}' must expand to >= 130% of source literal text`,
    );
  }
});

test('pseudo output never introduces HTML or ICU syntax characters in padding', () => {
  const pseudo = pseudoLocalizeMessage('Sign in to continue');
  assert.ok(!pseudo.includes('<'));
  assert.deepEqual(validateBundle('pseudo/en-XA.json', { 'shell.plain': pseudo }), []);
});
