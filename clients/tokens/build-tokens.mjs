#!/usr/bin/env node
/**
 * Design-token generator (issue 005).
 *
 * Reads tokens.source.json (values transcribed from DESIGN.md sections 3-5)
 * and emits:
 *   - dist/tokens.json  — for design tools and code (DESIGN.md 15)
 *   - dist/tokens.css   — CSS custom properties consumed by all three Angular
 *                         clients (ARCHITECTURE.md, Dependency rule 8)
 *
 * Deterministic output, zero dependencies (SECURITY.md, Dependency Rules 1).
 * Fails closed: an invalid source throws before anything is written; both
 * artifacts are fully rendered in memory before either file is emitted, so a
 * validation failure never leaves a partial dist/ (issue 005, Failure Behavior).
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { DIST_DIR, loadSource, resolveColorRef } from './lib/source.mjs';

/** camelCase / dotted names to css-var-safe kebab-case. */
function kebab(name) {
  return name
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/[^a-zA-Z0-9]+/g, '-')
    .toLowerCase();
}

function renderCss(source) {
  const lines = [];
  lines.push('/*');
  lines.push(' * GENERATED FILE — do not edit. Run `npm run tokens:build`.');
  lines.push(' * Source: tokens/tokens.source.json (transcribed from DESIGN.md sections 3-5).');
  lines.push(' * All three clients consume brand color/type/status values only from here');
  lines.push(' * (ARCHITECTURE.md, Dependency rule 8).');
  lines.push(' * Font families are named only; font files are self-hosted in a later issue,');
  lines.push(' * never loaded from a third-party runtime CDN (SECURITY.md, Deployment rule 10;');
  lines.push(' * CC-NFR-005).');
  lines.push(' */');
  lines.push(':root {');

  // Colors (DESIGN.md 3.1)
  for (const [family, shades] of Object.entries(source.color)) {
    for (const [shade, entry] of Object.entries(shades)) {
      lines.push(`  --cc-color-${kebab(family)}-${shade}: ${entry.value.toUpperCase()}; /* ${entry.role} */`);
    }
  }

  // Typography roles (DESIGN.md 4.1)
  for (const [role, entry] of Object.entries(source.typography.roles)) {
    lines.push(`  --cc-font-${kebab(role)}: "${entry.family}";`);
  }

  // Per-script stacks (DESIGN.md 4.2); latin is the default stack above.
  for (const [script, entry] of Object.entries(source.typography.scripts)) {
    if (script === 'latin') continue;
    const s = kebab(script);
    lines.push(`  --cc-font-display-${s}: "${entry.display.family}";`);
    lines.push(`  --cc-font-body-${s}: "${entry.body.family}";`);
    if (typeof entry.display.weight === 'number') {
      lines.push(`  --cc-weight-display-${s}: ${entry.display.weight};`);
    }
    if (typeof entry.body.lineHeight === 'number') {
      lines.push(`  --cc-type-line-height-body-${s}: ${entry.body.lineHeight};`);
    }
  }

  // Modular scale (DESIGN.md 4.3): --cc-type-size-1 .. -N, ascending.
  source.typography.scale.stepsPx.forEach((px, i) => {
    lines.push(`  --cc-type-size-${i + 1}: ${px}px;`);
  });
  lines.push(`  --cc-type-line-height-body: ${source.typography.lineHeights.body};`);
  for (const [name, weight] of Object.entries(source.typography.weights)) {
    lines.push(`  --cc-weight-${kebab(name)}: ${weight};`);
  }

  // Cache-status vocabulary colors (DESIGN.md 5.2). The badge/plain-line text
  // lives in tokens.json — status is never color alone (DESIGN.md 13).
  for (const [key, entry] of Object.entries(source.status)) {
    const ref = entry.colorToken.split('.');
    lines.push(`  --cc-status-${kebab(key)}-color: var(--cc-color-${kebab(ref[1])}-${ref[2]});`);
  }

  lines.push('}');
  return lines.join('\n') + '\n';
}

function renderJson(source) {
  // tokens.json mirrors the validated source (minus the source-file preamble),
  // with resolved hex values added to status entries for design-tool use.
  const { $description, ...rest } = source;
  const out = {
    $generated:
      'GENERATED FILE - do not edit. Run `npm run tokens:build`. Source: tokens/tokens.source.json, transcribed from DESIGN.md sections 3-5.',
    ...rest,
    status: Object.fromEntries(
      Object.entries(source.status).map(([key, entry]) => [
        key,
        { ...entry, colorValue: resolveColorRef(source, entry.colorToken) },
      ]),
    ),
  };
  return JSON.stringify(out, null, 2) + '\n';
}

function main() {
  const source = loadSource();
  // Render both artifacts fully before writing either (fail closed).
  const css = renderCss(source);
  const json = renderJson(source);
  mkdirSync(DIST_DIR, { recursive: true });
  writeFileSync(join(DIST_DIR, 'tokens.json'), json);
  writeFileSync(join(DIST_DIR, 'tokens.css'), css);
  console.log(`tokens:build OK -> ${join(DIST_DIR, 'tokens.json')}, ${join(DIST_DIR, 'tokens.css')}`);
}

try {
  main();
} catch (e) {
  console.error(`tokens:build FAILED: ${e.message}`);
  process.exit(1);
}
