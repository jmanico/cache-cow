/**
 * Locale price-formatting tests (issues 066/067; CC-PRC-003/004; DESIGN.md
 * §4.4 worked examples). Requirement tags: CC-PRC-003, CC-PRC-004.
 */

import { formatMinorUnits } from './format-price';

describe('formatMinorUnits (Intl only, integer minor units in)', () => {
  it('formats en-US USD with two decimals (DESIGN.md 4.4)', () => {
    expect(formatMinorUnits(14900, 'USD', 'en-US')).toBe('$149.00');
  });

  it('formats de-DE EUR with comma decimals and trailing symbol', () => {
    const text = formatMinorUnits(14900, 'EUR', 'de-DE');
    expect(text).toContain('149,00');
    expect(text).toContain('€');
  });

  it('formats ja-JP JPY zero-decimal (CC-QA-004)', () => {
    const text = formatMinorUnits(14900, 'JPY', 'ja-JP');
    expect(text).toContain('14,900');
    expect(text).not.toContain('.');
    expect(text).not.toContain('149.00');
  });

  it('formats hi-IN INR with lakh/crore grouping from the locale (CC-QA-004)', () => {
    const text = formatMinorUnits(124900000, 'INR', 'hi-IN');
    // ₹12,49,000.00 — grouping comes from the locale, never hand-formatted.
    expect(text).toContain('12,49,000');
  });

  it('keeps exactness for amounts that are inexact in binary floating point', () => {
    // 0.1 + 0.2 style hazard: 10 minor units must be exactly 0.10.
    expect(formatMinorUnits(10, 'USD', 'en-US')).toBe('$0.10');
  });
});
