/**
 * Locale-aware money display for dashboard tables (issue 082; CC-PRC-004,
 * CC-PRC-003; DESIGN.md §12 — every number in Plex Mono via the .cc-num /
 * .cc-mono utilities; this helper only produces the string).
 *
 * Mirrors the storefront's digit-slicing discipline WITHOUT importing it
 * (ARCHITECTURE.md, Dependency rule 4): integer minor units are converted
 * to an exact decimal STRING and handed to Intl.NumberFormat as a string
 * numeric literal, so no binary float ever carries a monetary value and no
 * monetary arithmetic happens client-side (CC-PRC-003; ARCHITECTURE.md,
 * Dependency rule 2). Hand-formatted currency strings are a defect
 * (CC-PRC-004).
 *
 * Locale is the dashboard's en-US staff-tooling baseline — the dashboard
 * locale scope is a flagged open question (i18n/i18n.service.ts header).
 */

const DASHBOARD_LOCALE = 'en-US';

/** Exact decimal string for `amountMinor` at the currency's minor-unit scale. */
function minorUnitsToDecimalString(amountMinor: number, fractionDigits: number): string {
  const digits = String(amountMinor).padStart(fractionDigits + 1, '0');
  if (fractionDigits === 0) {
    return digits;
  }
  return `${digits.slice(0, -fractionDigits)}.${digits.slice(-fractionDigits)}`;
}

/**
 * Format server-computed integer minor units as a currency string
 * (e.g. USD 14900 -> "$149.00"; JPY 14900 -> "¥14,900" — zero-decimal from
 * the currency data, never from code here).
 */
export function formatMinorUnits(amountMinor: number, currency: string): string {
  const formatter = new Intl.NumberFormat(DASHBOARD_LOCALE, { style: 'currency', currency });
  const fractionDigits = formatter.resolvedOptions().maximumFractionDigits ?? 2;
  const decimal = minorUnitsToDecimalString(amountMinor, fractionDigits);
  // ECMA-402 string numeric literals keep the value exact end to end; the
  // lib.d.ts overload set for format() lags the spec, hence the narrow cast.
  return (formatter.format as unknown as (value: string) => string)(decimal);
}

/**
 * Format an Intl-parseable timestamp for dashboard tables (dates in tables
 * are Plex Mono per DESIGN.md §12; the class comes from the template).
 */
export function formatTimestamp(iso: string): string {
  return new Intl.DateTimeFormat(DASHBOARD_LOCALE, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(new Date(iso));
}
