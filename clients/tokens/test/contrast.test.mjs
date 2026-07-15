/**
 * Unit tests for the WCAG contrast math and token pipeline (issue 005).
 * Requirement tags (REQUIREMENTS.md section 17): CC-NFR-004.
 *
 * Runs on node:test — zero dependencies (SECURITY.md, Dependency Rules 1).
 */

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { contrastRatio, hexToRgb, relativeLuminance } from '../lib/contrast.mjs';
import { loadSource, resolveColorRef, validateSource } from '../lib/source.mjs';

// [CC-NFR-004] hex parsing
test('hexToRgb parses 6-digit hex', () => {
  assert.deepEqual(hexToRgb('#221812'), [0x22, 0x18, 0x12]);
  assert.deepEqual(hexToRgb('#FCF7F0'), [0xfc, 0xf7, 0xf0]);
});

test('hexToRgb expands 3-digit hex', () => {
  assert.deepEqual(hexToRgb('#fff'), [255, 255, 255]);
  assert.deepEqual(hexToRgb('#000'), [0, 0, 0]);
});

test('hexToRgb rejects invalid input', () => {
  for (const bad of ['221812', '#22181', '#GGGGGG', '', null, 42, '#12345678']) {
    assert.throws(() => hexToRgb(bad), /Invalid hex color/);
  }
});

// [CC-NFR-004] relative luminance boundary values per WCAG 2.2 definition
test('relative luminance of white is 1 and black is 0', () => {
  assert.equal(relativeLuminance('#FFFFFF'), 1);
  assert.equal(relativeLuminance('#000000'), 0);
});

// [CC-NFR-004] contrast ratio properties
test('contrast ratio of black on white is 21:1', () => {
  assert.equal(contrastRatio('#000000', '#FFFFFF'), 21);
});

test('contrast ratio of a color with itself is 1:1', () => {
  assert.equal(contrastRatio('#E04E1B', '#E04E1B'), 1);
});

test('contrast ratio is order-independent', () => {
  assert.equal(contrastRatio('#221812', '#FCF7F0'), contrastRatio('#FCF7F0', '#221812'));
});

test('known WCAG reference: #767676 on white is ~4.54:1 (AA normal text boundary)', () => {
  const r = contrastRatio('#767676', '#FFFFFF');
  assert.ok(Math.abs(r - 4.54) < 0.01, `expected ~4.54, got ${r}`);
});

// [CC-NFR-004] DESIGN.md 3.2 pairs as fixtures: every declared pair must meet
// its enforced WCAG threshold with the real token values.
test('every declared DESIGN.md 3.2 contrast pair meets its enforced threshold', () => {
  const source = loadSource();
  for (const pair of source.contrastPairs) {
    const ratio = contrastRatio(
      resolveColorRef(source, pair.fg),
      resolveColorRef(source, pair.bg),
    );
    assert.ok(
      ratio >= pair.minRatio,
      `${pair.name}: computed ${ratio.toFixed(2)}:1 < required ${pair.minRatio}:1`,
    );
  }
});

test('Char on Paper and Char on Butcher pass WCAG AAA for all text (DESIGN.md 3.2)', () => {
  const source = loadSource();
  const char = resolveColorRef(source, 'color.char.900');
  assert.ok(contrastRatio(char, resolveColorRef(source, 'color.paper.100')) >= 7);
  assert.ok(contrastRatio(char, resolveColorRef(source, 'color.butcher.300')) >= 7);
});

test('ember.500 on Paper is large-text-only: passes 3:1, fails 4.5:1 body threshold (DESIGN.md 3.2)', () => {
  const source = loadSource();
  const r = contrastRatio(
    resolveColorRef(source, 'color.ember.500'),
    resolveColorRef(source, 'color.paper.100'),
  );
  assert.ok(r >= 3, 'must pass AA large text');
  assert.ok(r < 4.5, 'DESIGN.md 3.2: body text is never ember.500 on Paper');
});

test('ember.700 on Paper passes 4.5:1 for colored body-size text (DESIGN.md 3.2)', () => {
  const source = loadSource();
  const r = contrastRatio(
    resolveColorRef(source, 'color.ember.700'),
    resolveColorRef(source, 'color.paper.100'),
  );
  assert.ok(r >= 4.5, `expected >= 4.5, got ${r.toFixed(2)}`);
});

// Source validation fails closed (issue 005, Failure Behavior).
test('validateSource rejects a broken token source', () => {
  const source = loadSource();
  const broken = structuredClone(source);
  broken.color.char['900'].value = 'not-a-hex';
  assert.throws(() => validateSource(broken), /char\.900/);

  const missingStatus = structuredClone(source);
  delete missingStatus.status;
  assert.throws(() => validateSource(missingStatus), /status/);

  const badPair = structuredClone(source);
  badPair.contrastPairs[0].fg = 'color.nope.123';
  assert.throws(() => validateSource(badPair), /Unresolvable/);
});

// Token completeness against DESIGN.md 3.1 (all ten core color tokens, exact hex).
test('token source carries all ten DESIGN.md 3.1 core color tokens with exact values', () => {
  const source = loadSource();
  const expected = {
    'color.char.900': '#221812',
    'color.bark.700': '#4A3226',
    'color.smoke.400': '#A79A8F',
    'color.butcher.300': '#F0C39B',
    'color.paper.100': '#FCF7F0',
    'color.ember.500': '#E04E1B',
    'color.ember.700': '#B23A12',
    'color.cache.500': '#1FA860',
    'color.pit.950': '#16110E',
    'color.pitpaper.100': '#E9DED4',
  };
  for (const [ref, hex] of Object.entries(expected)) {
    assert.equal(resolveColorRef(source, ref).toUpperCase(), hex, ref);
  }
});

// Status vocabulary mappings per DESIGN.md 5.2 (issue 005 AC-01).
test('status vocabulary maps per DESIGN.md 5.2 and carries plain text (never color alone)', () => {
  const source = loadSource();
  assert.equal(source.status.cacheHit.badge, 'CACHE HIT');
  assert.equal(source.status.cacheHit.colorToken, 'color.cache.500');
  assert.equal(source.status.warming.badge, 'WARMING');
  assert.equal(source.status.warming.colorToken, 'color.ember.500');
  assert.equal(source.status.cacheMiss.badge, 'CACHE MISS');
  assert.equal(source.status.cacheMiss.colorToken, 'color.smoke.400');
  for (const entry of Object.values(source.status)) {
    assert.ok(entry.plainLine.length > 0, 'status is never color alone (DESIGN.md 13)');
  }
});

// Typography scale per DESIGN.md 4.3.
test('typography scale is 1.250 modular from a 17px base: 17/21/27/34/42/53/66', () => {
  const source = loadSource();
  assert.equal(source.typography.scale.basePx, 17);
  assert.equal(source.typography.scale.ratio, 1.25);
  assert.deepEqual(source.typography.scale.stepsPx, [17, 21, 27, 34, 42, 53, 66]);
});
