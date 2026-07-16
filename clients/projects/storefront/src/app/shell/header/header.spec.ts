/**
 * Header switcher tests (issue 063 AC-03/AC-04/AC-08) and navigation
 * placement tests (issues 074/075 — CC-MKT-005 "Meet our Cows" in primary
 * navigation in IN, under Our Story elsewhere; "Meet our Cuts" absent in IN).
 * Requirement tags: CC-MKT-001, CC-MKT-002, CC-MKT-005, CC-MKT-006,
 * CC-I18N-001 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { Header } from './header';
import { NavPolicyApi } from '../../nav/nav-policy.api';
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

/**
 * Navigation placement (issues 074/075; CC-MKT-005/006). Placement is policy
 * DATA consumed from the NavPolicy seam — the header holds no market
 * conditional. The REAL policy owner is the server-side Market & Gating
 * Policy service (issues 023/025); these tests assert the client renders the
 * placement it is handed.
 */
describe('Header navigation placement (CC-MKT-005)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Header],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  async function navFor(market: string) {
    const fixture = TestBed.createComponent(Header);
    TestBed.inject(TransactingContext).setMarket(market);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const pages = (testId: string) =>
      Array.from(host.querySelectorAll<HTMLElement>(`[data-testid="${testId}"] [data-nav]`)).map(
        (link) => link.dataset['nav'],
      );
    return { host, primary: pages('primary-nav'), ourStory: pages('our-story-nav') };
  }

  it('promotes Meet our Cows to primary navigation in IN, with Cuts absent entirely (AC-01, AC-03)', async () => {
    const { host, primary, ourStory } = await navFor('IN');

    expect(primary).toContain('cows');
    expect(ourStory).not.toContain('cows');
    // The Cuts experience does not exist in IN: not placed, not linked.
    expect(primary).not.toContain('cuts');
    expect(ourStory).not.toContain('cuts');
    expect(host.querySelector('[data-nav="cuts"]')).toBeNull();
    expect(host.innerHTML).not.toContain('/cuts');
  });

  it('places Meet our Cows under Our Story with Cuts present in US and DE (AC-02)', async () => {
    for (const market of ['US', 'DE']) {
      TestBed.resetTestingModule();
      await TestBed.configureTestingModule({
        imports: [Header],
        providers: [provideRouter([])],
      }).compileComponents();

      const { host, primary, ourStory } = await navFor(market);
      expect(ourStory).toContain('cows');
      expect(primary).not.toContain('cows');
      expect(ourStory).toContain('cuts');
      expect(host.querySelector('[data-nav="cuts"]')?.getAttribute('href')).toBe('/cuts');
      expect(host.querySelector('[data-nav="cows"]')?.getAttribute('href')).toBe('/cows');
    }
  });

  it('renders navigation as named landmarks with real links (DESIGN.md §13)', async () => {
    const { host } = await navFor('US');
    const primaryNav = host.querySelector('[data-testid="primary-nav"]')!;
    expect(primaryNav.tagName).toBe('NAV');
    expect(primaryNav.getAttribute('aria-label')).toBe('Primary');
    const ourStoryNav = host.querySelector('[data-testid="our-story-nav"]')!;
    expect(ourStoryNav.getAttribute('aria-label')).toBe('Our Story');
    for (const link of Array.from(host.querySelectorAll('[data-nav]'))) {
      expect(link.tagName).toBe('A'); // real link, keyboard-operable
      expect(link.getAttribute('href')).toBeTruthy();
    }
  });

  it('fails closed to NO navigation when the policy cannot be resolved (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Header],
      providers: [
        provideRouter([]),
        {
          provide: NavPolicyApi,
          useValue: { getNavPolicy: () => throwError(() => new Error('policy unavailable')) },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Header);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;

    // Never a guessed placement.
    expect(host.querySelector('[data-testid="primary-nav"]')).toBeNull();
    expect(host.querySelector('[data-testid="our-story-nav"]')).toBeNull();
    expect(host.textContent).not.toContain('policy unavailable');
    // The switchers still work: gating failure never blanks the shell.
    expect(host.querySelector('[data-testid="market-switcher"]')).toBeTruthy();
  });
});
