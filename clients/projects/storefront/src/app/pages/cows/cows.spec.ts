/**
 * Meet our Cows page tests (issue 074; CC-CNT-002, CC-MKT-005).
 * Requirement tags per REQUIREMENTS.md §17: CC-CNT-002, CC-MKT-005,
 * CC-I18N-001, CC-SEC-002.
 *
 * The navigation-placement half of CC-MKT-005 is asserted in the header spec
 * (shell/header/header.spec.ts) — placement is policy data consumed by the
 * shell, not by this page.
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { ContentApi } from '../../content/content.api';
import { MARKETS, TransactingContext } from '../../core/transacting-context';
import { Cows } from './cows';

function hrefs(host: HTMLElement): string[] {
  return Array.from(host.querySelectorAll('a')).map((a) => a.getAttribute('href') ?? '');
}

async function createCows() {
  const fixture = TestBed.createComponent(Cows);
  await fixture.whenStable();
  return fixture;
}

describe('Cows (issue 074)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Cows],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders cow cards with illustration, name, role and one-line bio (AC-05, DESIGN.md §7)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    const card = host.querySelector<HTMLElement>('[data-cow="daisy"]')!;
    expect(card.querySelector('[data-testid="cow-illustration"]')).toBeTruthy();
    expect(card.querySelector('[data-testid="cow-name"]')?.textContent).toContain('Daisy');
    expect(card.querySelector('[data-testid="cow-role"]')?.textContent).toContain('Head of Grazing');
    expect(card.querySelector('[data-testid="cow-bio"]')?.textContent?.trim()).not.toBe('');
  });

  it('differentiates each cow by blaze shape and names it in the alt text (AC-05, DESIGN.md §7/§13)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;

    const blazes = Array.from(host.querySelectorAll<HTMLElement>('[data-testid="cow-card"]')).map(
      (card) => card.dataset['blaze'],
    );
    expect(blazes).toEqual(['database', 'lightning', 'heart']);
    // Each blaze renders its own distinct geometry.
    expect(host.querySelector('[data-testid="blaze-database"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="blaze-lightning"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="blaze-heart"]')).toBeTruthy();

    // The illustration is named for assistive technology, and the name
    // carries the blaze differentiator.
    const daisy = host.querySelector('[data-cow="daisy"] [data-testid="cow-illustration"]')!;
    expect(daisy.getAttribute('role')).toBe('img');
    expect(daisy.getAttribute('aria-label')).toContain('Daisy');
    expect(daisy.getAttribute('aria-label')).toContain('database-cylinder blaze');
  });

  it('contains ZERO product links in EVERY market (AC-03, CC-CNT-002 negative)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    const context = TestBed.inject(TransactingContext);

    for (const market of MARKETS) {
      context.setMarket(market);
      await fixture.whenStable();
      expect(host.querySelectorAll('[data-testid="cow-card"]').length).toBeGreaterThan(0);
      // No PDP link of any kind — not just no non-veg PDP link.
      const productHrefs = hrefs(host).filter((href) => href.includes('/product/'));
      expect(productHrefs).toEqual([]);
      // Nothing in the rendered markup references a PDP path at all.
      expect(host.innerHTML).not.toContain('/product/');
    }
  });

  it('carries no butchery content and no link to the Cuts experience (AC-04, DESIGN.md §8.1)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    // The separation rule applies to this page's own content; the shell's
    // global navigation legitimately carries the Cuts link under Our Story
    // in non-IN markets (CC-MKT-005) — see the note in cows.ts.
    expect(hrefs(host).filter((href) => href.includes('/cuts'))).toEqual([]);
    expect(host.innerHTML).not.toContain('/cuts');
  });

  it('renders the same herd in every market (DESIGN.md §2.3 — only the menu changes)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    const ids = () =>
      Array.from(host.querySelectorAll<HTMLElement>('[data-testid="cow-card"]')).map(
        (card) => card.dataset['cow'],
      );
    const inUs = ids();

    TestBed.inject(TransactingContext).setMarket('IN');
    await fixture.whenStable();
    expect(ids()).toEqual(inUs);
  });

  it('localizes roles and bios per locale (CC-I18N-001)', async () => {
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    const role = () => host.querySelector('[data-cow="clover"] [data-testid="cow-role"]')?.textContent ?? '';
    // The English-only pun role (DESIGN.md §7 example).
    expect(role()).toContain('Chief Cud Officer');

    TestBed.inject(TransactingContext).setLocale('de-DE');
    await fixture.whenStable();
    // Untranslatable puns are cut: other locales get a plain description (§9).
    expect(role()).toContain('Wiederkäuen');
    expect(role()).not.toContain('Chief Cud Officer');
    expect(host.textContent).toContain('Unsere Kühe');
  });

  it('fails closed to a generic error state with no mascot in it (Failure Behavior, DESIGN.md §9)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Cows],
      providers: [
        provideRouter([]),
        {
          provide: ContentApi,
          useValue: { getCowHerd: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const fixture = await createCows();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="cows-error"]')).toBeTruthy();
    expect(host.querySelectorAll('[data-testid="cow-card"]').length).toBe(0);
    // No mascots in error states (DESIGN.md §9).
    expect(host.querySelector('[data-testid="cow-illustration"]')).toBeNull();
    expect(host.textContent).not.toContain('schema violation');
  });
});
