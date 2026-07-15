/**
 * Token source loading and validation — first-party, zero dependencies
 * (SECURITY.md, Dependency Rules 1).
 *
 * The source file transcribes DESIGN.md sections 3-5 values; this module is
 * the published schema for it (single source of truth for validation,
 * ARCHITECTURE.md, Dependency rule 7). A malformed source fails generation
 * with a clear error and nothing is emitted (issue 005, Failure Behavior).
 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const TOKENS_DIR = dirname(dirname(fileURLToPath(import.meta.url)));
export const SOURCE_PATH = join(TOKENS_DIR, 'tokens.source.json');
export const DIST_DIR = join(TOKENS_DIR, 'dist');

const HEX_RE = /^#[0-9a-fA-F]{6}$/;

/** Resolve a dotted token reference like "color.cache.500" to its hex value. */
export function resolveColorRef(source, ref) {
  const parts = typeof ref === 'string' ? ref.split('.') : [];
  if (parts.length !== 3 || parts[0] !== 'color') {
    throw new Error(`Invalid color token reference: ${String(ref)}`);
  }
  const entry = source.color?.[parts[1]]?.[parts[2]];
  if (!entry || typeof entry.value !== 'string') {
    throw new Error(`Unresolvable color token reference: ${ref}`);
  }
  return entry.value;
}

/** Validate the token source structure; throws an aggregate error on failure. */
export function validateSource(source) {
  const errors = [];

  // Colors: every leaf must be { value: #rrggbb, role: string }.
  if (!source.color || typeof source.color !== 'object') {
    errors.push('missing "color" group');
  } else {
    for (const [family, shades] of Object.entries(source.color)) {
      for (const [shade, entry] of Object.entries(shades)) {
        if (!entry || typeof entry.value !== 'string' || !HEX_RE.test(entry.value)) {
          errors.push(`color.${family}.${shade}: value must be a #rrggbb hex string`);
        }
        if (typeof entry?.role !== 'string' || entry.role.length === 0) {
          errors.push(`color.${family}.${shade}: missing role description`);
        }
      }
    }
  }

  // Typography: roles, scripts, scale (DESIGN.md section 4).
  const typo = source.typography;
  if (!typo || typeof typo !== 'object') {
    errors.push('missing "typography" group');
  } else {
    for (const role of ['display', 'body', 'data']) {
      if (typeof typo.roles?.[role]?.family !== 'string') {
        errors.push(`typography.roles.${role}: missing font family`);
      }
    }
    if (!typo.scripts || typeof typo.scripts !== 'object') {
      errors.push('typography.scripts: missing per-script stacks');
    }
    const steps = typo.scale?.stepsPx;
    if (!Array.isArray(steps) || steps.length === 0) {
      errors.push('typography.scale.stepsPx: must be a non-empty array');
    } else {
      for (let i = 0; i < steps.length; i++) {
        if (!Number.isFinite(steps[i]) || (i > 0 && steps[i] <= steps[i - 1])) {
          errors.push('typography.scale.stepsPx: must be strictly ascending numbers');
          break;
        }
      }
    }
    if (typeof typo.lineHeights?.body !== 'number') {
      errors.push('typography.lineHeights.body: missing');
    }
    for (const w of ['body', 'uiLabel', 'subhead']) {
      if (typeof typo.weights?.[w] !== 'number') {
        errors.push(`typography.weights.${w}: missing`);
      }
    }
  }

  // Status vocabulary (DESIGN.md 5.2): badge + plain line + color ref + state.
  // Status is never color alone (DESIGN.md 13), so the text fields are required.
  if (!source.status || typeof source.status !== 'object') {
    errors.push('missing "status" group');
  } else {
    for (const [key, entry] of Object.entries(source.status)) {
      for (const field of ['badge', 'plainLine', 'state']) {
        if (typeof entry?.[field] !== 'string' || entry[field].length === 0) {
          errors.push(`status.${key}.${field}: missing (status is never color alone, DESIGN.md 13)`);
        }
      }
      try {
        resolveColorRef(source, entry?.colorToken);
      } catch (e) {
        errors.push(`status.${key}.colorToken: ${e.message}`);
      }
    }
  }

  // Contrast pairs (DESIGN.md 3.2): resolvable refs, sane thresholds.
  if (!Array.isArray(source.contrastPairs) || source.contrastPairs.length === 0) {
    errors.push('missing "contrastPairs" array');
  } else {
    for (const pair of source.contrastPairs) {
      const label = pair?.name ?? JSON.stringify(pair);
      for (const side of ['fg', 'bg']) {
        try {
          resolveColorRef(source, pair?.[side]);
        } catch (e) {
          errors.push(`contrastPairs[${label}].${side}: ${e.message}`);
        }
      }
      if (!Number.isFinite(pair?.minRatio) || pair.minRatio < 1 || pair.minRatio > 21) {
        errors.push(`contrastPairs[${label}].minRatio: must be a number in [1, 21]`);
      }
      if (pair?.documentedRatio !== null && !Number.isFinite(pair?.documentedRatio)) {
        errors.push(`contrastPairs[${label}].documentedRatio: must be a number or null`);
      }
    }
  }

  if (errors.length > 0) {
    throw new Error(`tokens.source.json is invalid:\n  - ${errors.join('\n  - ')}`);
  }
  return source;
}

/** Load and validate the token source. */
export function loadSource() {
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(SOURCE_PATH, 'utf8'));
  } catch (e) {
    throw new Error(`Cannot read/parse ${SOURCE_PATH}: ${e.message}`);
  }
  return validateSource(parsed);
}
