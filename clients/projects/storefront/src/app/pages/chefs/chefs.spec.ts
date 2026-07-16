/**
 * Meet our Chefs page tests (issue 073; CC-CNT-001).
 * Requirement tags per REQUIREMENTS.md §17: CC-CNT-001, CC-I18N-001,
 * CC-SEC-002.
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { ContentApi } from '../../content/content.api';
import { TransactingContext } from '../../core/transacting-context';
import { Chefs } from './chefs';

function chefIds(host: HTMLElement): string[] {
  return Array.from(host.querySelectorAll<HTMLElement>('[data-testid="chef-card"]')).map(
    (card) => card.dataset['chef'] ?? '',
  );
}

async function createChefs() {
  const fixture = TestBed.createComponent(Chefs);
  await fixture.whenStable();
  return fixture;
}

describe('Chefs (issue 073)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Chefs],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders the shared roster with card composition per DESIGN.md §7 (AC-01)', async () => {
    const fixture = await createChefs();
    const host = fixture.nativeElement as HTMLElement;
    expect(chefIds(host).length).toBeGreaterThan(0);

    const card = host.querySelector<HTMLElement>('[data-chef="marisol-vega"]')!;
    // Portrait placeholder, name, pit specialty, market flag(s).
    expect(card.querySelector('[data-testid="chef-portrait"]')).toBeTruthy();
    expect(card.querySelector('[data-testid="chef-name"]')?.textContent).toContain('Marisol Vega');
    expect(card.querySelector('[data-testid="chef-specialty"]')?.textContent?.trim()).not.toBe('');
    expect(card.querySelector('[data-testid="chef-bio"]')?.textContent?.trim()).not.toBe('');
    const markets = Array.from(card.querySelectorAll('[data-testid="chef-market"]')).map((m) =>
      m.textContent?.trim(),
    );
    expect(markets).toEqual(['United States', 'Mexico']);
  });

  it('renders bios localized per locale (CC-CNT-001, CC-I18N-001)', async () => {
    const fixture = await createChefs();
    const host = fixture.nativeElement as HTMLElement;
    const bio = () => host.querySelector('[data-chef="marisol-vega"] [data-testid="chef-bio"]')?.textContent ?? '';
    expect(bio()).toContain('patience is the only real recipe');

    TestBed.inject(TransactingContext).setLocale('de-DE');
    await fixture.whenStable();
    expect(bio()).toContain('Geduld ist das einzige echte Rezept');
    // Page chrome localizes too (strings from the bundles, CC-I18N-002).
    expect(host.textContent).toContain('Unsere Chefs');

    TestBed.inject(TransactingContext).setLocale('ja-JP');
    await fixture.whenStable();
    expect(bio()).toContain('本当のレシピは忍耐だけ');
  });

  it('keeps the roster identical across markets — shared, not gated (CC-CNT-001)', async () => {
    const fixture = await createChefs();
    const host = fixture.nativeElement as HTMLElement;
    const inUs = chefIds(host);

    TestBed.inject(TransactingContext).setMarket('IN');
    await fixture.whenStable();
    expect(chefIds(host)).toEqual(inUs);
  });

  it('renders bio text as inert text, never as markup (CC-SEC-002)', async () => {
    const fixture = await createChefs();
    const host = fixture.nativeElement as HTMLElement;
    const bio = host.querySelector<HTMLElement>('[data-testid="chef-bio"]')!;
    // Plain-text binding only: no element ever gets built from content.
    expect(bio.querySelector('*')).toBeNull();
    expect(bio.innerHTML).not.toContain('<');
  });

  it('fails closed to a generic error state when the seam rejects the response (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Chefs],
      providers: [
        provideRouter([]),
        {
          provide: ContentApi,
          useValue: { getChefRoster: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const fixture = await createChefs();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="chefs-error"]')).toBeTruthy();
    expect(host.querySelectorAll('[data-testid="chef-card"]').length).toBe(0);
    // Generic message only — never the raw error body (SECURITY.md, Logging rule 7).
    expect(host.textContent).not.toContain('schema violation');
  });
});
