#!/usr/bin/env node
/**
 * Automated contrast + hardcoded-brand-value check (issue 005; CC-NFR-004).
 *
 * 1. Verifies every declared token pair from tokens.source.json
 *    ("contrastPairs", transcribed from DESIGN.md 3.2) against its enforced
 *    WCAG 2.2 threshold (minRatio). Any pair below threshold FAILS the build
 *    (DESIGN.md 3.2, 13; CC-NFR-004).
 *    Where the ratio DESIGN.md 3.2 documents differs from the computed WCAG
 *    value, a WARNING is printed: per CLAUDE.md, conflicts between documents
 *    and math are flagged for a human, not silently resolved here.
 *
 * 2. Scans the three Angular apps' sources for hardcoded brand hex values or
 *    status badge labels duplicating tokens — clients consume generated
 *    artifacts only (ARCHITECTURE.md, Dependency rule 8). Violations FAIL.
 *
 * Zero dependencies (SECURITY.md, Dependency Rules 1). Fails closed: any
 * error exits non-zero (SECURITY.md, Deployment rule 7 gate posture).
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { contrastRatio } from './lib/contrast.mjs';
import { TOKENS_DIR, loadSource, resolveColorRef } from './lib/source.mjs';

const CLIENTS_DIR = dirname(TOKENS_DIR);

function checkContrastPairs(source) {
  let failures = 0;
  let warnings = 0;
  for (const pair of source.contrastPairs) {
    const fg = resolveColorRef(source, pair.fg);
    const bg = resolveColorRef(source, pair.bg);
    const ratio = contrastRatio(fg, bg);
    const rounded = Math.round(ratio * 100) / 100;

    if (ratio < pair.minRatio) {
      failures++;
      console.error(
        `FAIL  ${pair.name}: computed ${rounded}:1 is below the required ${pair.minRatio}:1 (${pair.usage})`,
      );
      continue;
    }
    console.log(`ok    ${pair.name}: ${rounded}:1 (required >= ${pair.minRatio}:1)`);

    // Documented-vs-computed discrepancy: flag, never fail and never "fix"
    // DESIGN.md from here (CLAUDE.md: flag conflicts, don't pick a side).
    if (
      pair.documentedRatio !== null &&
      Math.abs(Math.round(ratio * 10) / 10 - pair.documentedRatio) > 0.05
    ) {
      warnings++;
      console.warn(
        `WARN  ${pair.name}: DESIGN.md 3.2 documents ${pair.documentedRatio}:1 but WCAG math gives ${rounded}:1 — DESIGN.md may need human review`,
      );
    }
  }
  return { failures, warnings };
}

/** Recursively list files under dir, skipping build output and node_modules. */
function listFiles(dir, out = []) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'dist' || name.startsWith('.')) continue;
    const p = join(dir, name);
    if (statSync(p).isDirectory()) {
      listFiles(p, out);
    } else if (/\.(ts|html|css|scss|json)$/.test(name) && !name.endsWith('favicon.ico')) {
      out.push(p);
    }
  }
  return out;
}

function checkNoHardcodedBrandValues(source) {
  const brandHexes = [];
  for (const [family, shades] of Object.entries(source.color)) {
    for (const [shade, entry] of Object.entries(shades)) {
      brandHexes.push({ label: `color.${family}.${shade}`, needle: entry.value.toLowerCase() });
    }
  }
  const badges = Object.values(source.status).map((s) => ({
    label: `status badge "${s.badge}"`,
    needle: s.badge,
  }));

  const projectsDir = join(CLIENTS_DIR, 'projects');
  let failures = 0;
  for (const file of listFiles(projectsDir)) {
    const content = readFileSync(file, 'utf8');
    const lower = content.toLowerCase();
    for (const { label, needle } of brandHexes) {
      if (lower.includes(needle)) {
        failures++;
        console.error(
          `FAIL  hardcoded brand value ${label} (${needle}) in ${relative(CLIENTS_DIR, file)} — consume tokens instead (ARCHITECTURE.md, Dependency rule 8)`,
        );
      }
    }
    for (const { label, needle } of badges) {
      if (content.includes(needle)) {
        failures++;
        console.error(
          `FAIL  hardcoded ${label} in ${relative(CLIENTS_DIR, file)} — consume tokens instead (ARCHITECTURE.md, Dependency rule 8)`,
        );
      }
    }
  }
  return failures;
}

try {
  const source = loadSource();
  const { failures: contrastFailures, warnings } = checkContrastPairs(source);
  const hardcodeFailures = checkNoHardcodedBrandValues(source);
  const failures = contrastFailures + hardcodeFailures;
  if (failures > 0) {
    console.error(`tokens:check FAILED: ${failures} violation(s)`);
    process.exit(1);
  }
  console.log(
    `tokens:check OK: ${source.contrastPairs.length} contrast pair(s) verified, no hardcoded brand values` +
      (warnings > 0 ? ` (${warnings} documented-ratio warning(s) for human review)` : ''),
  );
} catch (e) {
  console.error(`tokens:check FAILED: ${e.message}`);
  process.exit(1);
}
