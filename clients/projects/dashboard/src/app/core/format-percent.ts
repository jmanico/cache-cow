/**
 * Locale-aware percentage display for dashboard tables (issue 084;
 * CC-DSH-006, CC-PRC-004 formatting discipline; DESIGN.md §12 — numbers in
 * Plex Mono via .cc-num, this helper only produces the string).
 *
 * Service levels arrive from the server as INTEGER BASIS POINTS (0..10000),
 * not as a float ratio: the exact-integer transport keeps the wire contract
 * validatable (`requireIntInRange`) and keeps a rounding decision from
 * silently happening in transit. A service level is not money, so CC-PRC-003
 * does not bind it — but the same integer-in / exact-string-out discipline
 * costs nothing here and keeps one formatting idiom across the module pages
 * (see core/format-minor-units.ts).
 *
 * The client performs NO arithmetic on the metric: it converts an integer to
 * an exact decimal string and hands that string to Intl.NumberFormat. What
 * the rate measures (hits over what denominator, over what window) is
 * SERVER-defined — the client never computes or recomputes it
 * (ARCHITECTURE.md, Dependency rule 1).
 *
 * Locale is the dashboard's en-US staff-tooling baseline — the dashboard
 * locale scope is a flagged open question (i18n/i18n.service.ts header).
 */

const DASHBOARD_LOCALE = 'en-US';

/**
 * Format integer basis points as a percentage string
 * (9820 -> "98.2%"; 10000 -> "100%").
 *
 * Intl's `style: 'percent'` takes a ratio, so the basis points become the
 * exact decimal string `0.9820` — never `bp / 10000` in binary floating
 * point, which is the same slice-the-digits move format-minor-units.ts makes
 * for money.
 */
export function formatBasisPoints(basisPoints: number): string {
  const digits = String(basisPoints).padStart(5, '0');
  const ratio = `${digits.slice(0, -4)}.${digits.slice(-4)}`;
  const formatter = new Intl.NumberFormat(DASHBOARD_LOCALE, {
    style: 'percent',
    maximumFractionDigits: 1,
  });
  // ECMA-402 string numeric literals keep the value exact end to end; the
  // lib.d.ts overload set for format() lags the spec, hence the narrow cast.
  return (formatter.format as unknown as (value: string) => string)(ratio);
}
