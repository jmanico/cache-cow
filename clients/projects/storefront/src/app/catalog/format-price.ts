/**
 * Locale-aware price display formatting (issues 066/067; CC-PRC-004,
 * CC-PRC-003; DESIGN.md §4.4).
 *
 * The ONLY way price strings are produced in this client: Intl.NumberFormat
 * with the transacting locale and the server-supplied ISO 4217 currency.
 * Hand-formatted currency strings are a defect (CC-PRC-004); grouping (INR
 * lakh/crore) and decimals (JPY zero-decimal) come from the locale/currency
 * data, never from code here.
 *
 * No monetary arithmetic and no binary floating point (CC-PRC-003;
 * ARCHITECTURE.md, Dependency rule 2): the integer minor units are converted
 * to a DECIMAL STRING by digit slicing and handed to Intl as a string
 * numeric literal (ECMA-402: format accepts exact decimal strings), so no
 * float ever carries a monetary value. This is display formatting of a
 * server-computed value — the server remains the only computer of money
 * (CC-PRC-005).
 */

import { Locale } from '../core/transacting-context';

/** Exact decimal string for `amountMinor` at the currency's minor-unit scale. */
function minorUnitsToDecimalString(amountMinor: number, fractionDigits: number): string {
  const digits = String(amountMinor).padStart(fractionDigits + 1, '0');
  if (fractionDigits === 0) {
    return digits;
  }
  return `${digits.slice(0, -fractionDigits)}.${digits.slice(-fractionDigits)}`;
}

/**
 * Format integer minor units as a locale currency string
 * (e.g. en-US/USD 14900 -> "$149.00"; ja-JP/JPY 14900 -> "￥14,900";
 * hi-IN/INR 124900000 -> "₹12,49,000.00" — grouping from the locale).
 */
export function formatMinorUnits(amountMinor: number, currency: string, locale: Locale): string {
  const formatter = new Intl.NumberFormat(locale, { style: 'currency', currency });
  const fractionDigits = formatter.resolvedOptions().maximumFractionDigits ?? 2;
  const decimal = minorUnitsToDecimalString(amountMinor, fractionDigits);
  // ECMA-402 string numeric literals keep the value exact end to end; the
  // lib.d.ts overload set for format() lags the spec, hence the narrow cast.
  return (formatter.format as unknown as (value: string) => string)(decimal);
}
