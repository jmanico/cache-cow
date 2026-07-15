/**
 * WCAG 2.x contrast math — first-party, zero dependencies
 * (SECURITY.md, Dependency Rules 1: a contrast-ratio computation is a few
 * lines of first-party code; no library added).
 *
 * Formulas per WCAG 2.2 "relative luminance" and "contrast ratio" definitions:
 * https://www.w3.org/TR/WCAG22/#dfn-relative-luminance
 * https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio
 *
 * Implements the CI contrast checks required by DESIGN.md 3.2 / 13 and
 * CC-NFR-004 (WCAG 2.2 AA floor).
 */

const HEX_RE = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/;

/**
 * Parse a #rgb or #rrggbb hex color into [r, g, b] with 0-255 channels.
 * Throws on anything else — invalid tokens fail the build, never pass silently.
 */
export function hexToRgb(hex) {
  if (typeof hex !== 'string' || !HEX_RE.test(hex)) {
    throw new Error(`Invalid hex color: ${String(hex)} (expected #rgb or #rrggbb)`);
  }
  let h = hex.slice(1);
  if (h.length === 3) {
    h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
  }
  return [0, 2, 4].map((i) => Number.parseInt(h.slice(i, i + 2), 16));
}

/** WCAG relative luminance of a hex color, in [0, 1]. */
export function relativeLuminance(hex) {
  const [r, g, b] = hexToRgb(hex).map((channel) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/**
 * WCAG contrast ratio between two hex colors, in [1, 21].
 * Order-independent (the lighter color is always the numerator).
 */
export function contrastRatio(hexA, hexB) {
  const la = relativeLuminance(hexA);
  const lb = relativeLuminance(hexB);
  const lighter = Math.max(la, lb);
  const darker = Math.min(la, lb);
  return (lighter + 0.05) / (darker + 0.05);
}
