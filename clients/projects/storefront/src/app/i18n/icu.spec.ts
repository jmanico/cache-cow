/**
 * ICU formatter tests (issue 064, client half).
 * Requirement tags: CC-I18N-002, CC-I18N-003, CC-QA-006 (REQUIREMENTS.md §17).
 */

import { formatIcu, parseIcu, placeholderSignature, IcuSyntaxError } from './icu';

describe('formatIcu (CC-I18N-002)', () => {
  it('formats literal text and simple placeholders', () => {
    expect(formatIcu('Hello {name}', 'en-US', { name: 'Jo' })).toBe('Hello Jo');
  });

  it('formats plural via Intl.PluralRules with # as the locale-formatted operand', () => {
    const message = '{count, plural, one {# item} other {# items}}';
    expect(formatIcu(message, 'en-US', { count: 1 })).toBe('1 item');
    expect(formatIcu(message, 'en-US', { count: 2 })).toBe('2 items');
    // Locale-aware number formatting inside messages (CC-I18N-003):
    expect(formatIcu(message, 'de-DE', { count: 1234 })).toBe('1.234 items');
  });

  it('supports exact matches (=0) ahead of plural categories', () => {
    const message = '{count, plural, =0 {none} one {# item} other {# items}}';
    expect(formatIcu(message, 'en-US', { count: 0 })).toBe('none');
  });

  it('formats select with other-fallback', () => {
    const message = '{kind, select, veg {Vegetarian} other {Standard}}';
    expect(formatIcu(message, 'en-US', { kind: 'veg' })).toBe('Vegetarian');
    expect(formatIcu(message, 'en-US', { kind: 'anything' })).toBe('Standard');
  });

  it('locale-formats numeric simple arguments (CC-I18N-003)', () => {
    // INR lakh/crore grouping comes from the locale, never hand-formatted
    // (DESIGN.md 4.4).
    expect(formatIcu('{n}', 'hi-IN', { n: 1249000 })).toBe('12,49,000');
  });

  it('renders hostile interpolated values inert as text — no markup interpretation (064 AC-05)', () => {
    const hostile = '<script>alert(1)</script>';
    const out = formatIcu('Hi {name}', 'en-US', { name: hostile });
    // Escape-by-default: the value passes through as literal text for text
    // binding; when bound via textContent it cannot become markup.
    expect(out).toBe(`Hi ${hostile}`);
    const div = document.createElement('div');
    div.textContent = out;
    expect(div.querySelector('script')).toBeNull();
    expect(div.innerHTML).not.toContain('<script>');
  });

  it('fails closed on missing values instead of rendering raw placeholders', () => {
    expect(() => formatIcu('Hi {name}', 'en-US')).toThrow(IcuSyntaxError);
  });
});

describe('parseIcu rejects invalid messages (fail closed, 064 AC-06)', () => {
  it.each([
    ['{unclosed', 'unterminated argument'],
    ['stray } brace', 'unbalanced close'],
    ['{n, plural, one {x}}', 'plural without other'],
    ['{n, select, a {x}}', 'select without other'],
    ['{n, number}', 'unsupported argument type'],
    ['{n, plural, one {x} one {y} other {z}}', 'duplicate selector'],
  ])('rejects %s (%s)', (message) => {
    expect(() => parseIcu(message)).toThrow(IcuSyntaxError);
  });
});

describe('placeholderSignature', () => {
  it('is stable across formatting-irrelevant differences and detects mismatches', () => {
    expect(placeholderSignature('Hello {name}')).toEqual(placeholderSignature('{name}, hi!'));
    expect(placeholderSignature('Hola {nombre}')).not.toEqual(placeholderSignature('Hello {name}'));
    expect(placeholderSignature('{c, plural, one {#} other {#}}')).not.toEqual(
      placeholderSignature('{c} items'),
    );
  });
});
