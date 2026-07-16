/**
 * Money and percentage formatting tests (issues 082/084).
 * Requirement tags: CC-PRC-003, CC-PRC-004, CC-DSH-006 (REQUIREMENTS.md §17).
 *
 * NO FLOAT ANYWHERE, INCLUDING IN THESE TESTS (CC-PRC-003): every expected
 * value below is written as integer minor units in, exact string out. There
 * is not a single decimal literal carrying money in this file.
 */

import { formatMinorUnits, formatTimestamp } from './format-minor-units';
import { formatBasisPoints } from './format-percent';

describe('formatMinorUnits (CC-PRC-003, CC-PRC-004)', () => {
  it('formats two-decimal currencies from integer minor units', () => {
    expect(formatMinorUnits(14_900, 'USD')).toBe('$149.00');
    expect(formatMinorUnits(8_950, 'EUR')).toBe('€89.50');
  });

  it('formats JPY as zero-decimal — from the currency data, not from code', () => {
    // 14900 JPY minor units are 14900 yen, NOT 149.00 (CC-QA-004).
    expect(formatMinorUnits(14_900, 'JPY')).toBe('¥14,900');
  });

  it('formats INR with locale grouping (CC-QA-004)', () => {
    expect(formatMinorUnits(419_600, 'INR')).toBe('₹4,196.00');
  });

  it('handles sub-unit and zero amounts without losing the minor digits', () => {
    expect(formatMinorUnits(5, 'USD')).toBe('$0.05');
    expect(formatMinorUnits(50, 'USD')).toBe('$0.50');
    expect(formatMinorUnits(0, 'USD')).toBe('$0.00');
  });

  it('stays exact at magnitudes where binary float would drift', () => {
    // 8_100_000_000_000_05 minor units: the cent digits must survive intact.
    expect(formatMinorUnits(810_000_000_000_005, 'USD')).toBe('$8,100,000,000,000.05');
  });
});

describe('formatTimestamp', () => {
  it('renders an ISO instant in UTC for staff tables', () => {
    expect(formatTimestamp('2026-07-12T14:05:00Z')).toBe('Jul 12, 2026, 2:05 PM');
  });
});

describe('formatBasisPoints (CC-DSH-006 service level)', () => {
  it('formats integer basis points as a percentage', () => {
    expect(formatBasisPoints(9_860)).toBe('98.6%');
    expect(formatBasisPoints(7_425)).toBe('74.3%');
  });

  it('formats the range ends exactly', () => {
    expect(formatBasisPoints(10_000)).toBe('100%');
    expect(formatBasisPoints(0)).toBe('0%');
  });
});
