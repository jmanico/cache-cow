/**
 * Header switcher tests (issue 063 AC-03/AC-04/AC-08).
 * Requirement tags: CC-MKT-001, CC-MKT-002, CC-I18N-001 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Header } from './header';
import { LOCALES, MARKETS, TransactingContext } from '../../core/transacting-context';

function select(host: HTMLElement, testId: string): HTMLSelectElement {
  const element = host.querySelector<HTMLSelectElement>(`[data-testid="${testId}"]`);
  if (!element) {
    throw new Error(`missing ${testId}`);
  }
  return element;
}

function choose(element: HTMLSelectElement, value: string): void {
  element.value = value;
  element.dispatchEvent(new Event('change'));
}

describe('Header (region and language switcher, DESIGN.md §7)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Header],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders two separate controls with all six markets and all seven locales (AC-03)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const market = select(host, 'market-switcher');
    const locale = select(host, 'locale-switcher');
    expect(market).not.toBe(locale);
    expect(Array.from(market.options).map((o) => o.value)).toEqual([...MARKETS]);
    expect(Array.from(locale.options).map((o) => o.value)).toEqual([...LOCALES]);
  });

  it('changing the market emits a market-only change; the locale is unchanged (AC-04)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const context = TestBed.inject(TransactingContext);
    const localeBefore = context.locale();

    choose(select(fixture.nativeElement as HTMLElement, 'market-switcher'), 'DE');
    await fixture.whenStable();

    expect(context.market()).toBe('DE');
    expect(context.locale()).toBe(localeBefore);
  });

  it('changing the language emits a locale-only change; the market is unchanged (AC-04)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const context = TestBed.inject(TransactingContext);
    const marketBefore = context.market();

    choose(select(fixture.nativeElement as HTMLElement, 'locale-switcher'), 'ja-JP');
    await fixture.whenStable();

    expect(context.locale()).toBe('ja-JP');
    expect(context.market()).toBe(marketBefore);
  });

  it('re-renders its own labels when the locale changes (strings from bundles, CC-I18N-002)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('Market');

    choose(select(host, 'locale-switcher'), 'de-DE');
    await fixture.whenStable();

    expect(host.textContent).toContain('Markt');
    expect(host.textContent).toContain('Sprache');
  });

  it('rejects unknown switcher values without falling back to a client hint (Failure Behavior)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const context = TestBed.inject(TransactingContext);

    const market = select(fixture.nativeElement as HTMLElement, 'market-switcher');
    const rogue = document.createElement('option');
    rogue.value = 'XX';
    market.appendChild(rogue);
    choose(market, 'XX');
    await fixture.whenStable();

    expect(context.market()).toBe('US');
  });

  it('uses natively keyboard-operable controls with label association (AC-08, DESIGN.md §13)', async () => {
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    for (const testId of ['market-switcher', 'locale-switcher']) {
      const control = select(host, testId);
      expect(control.tagName).toBe('SELECT'); // native = keyboard-operable
      expect(control.closest('label')?.querySelector('.switcher-label')).toBeTruthy();
    }
  });
});
