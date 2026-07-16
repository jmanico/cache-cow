/**
 * Footer legal-links tests (issue 077 AC-02/AC-05; CC-CNT-005; DESIGN.md
 * §8.4). Requirement tags per REQUIREMENTS.md §17: CC-CNT-005, CC-I18N-001.
 *
 * The per-market legal content SET is authored server-side (issue 023); the
 * footer renders the set the seam resolves and holds no market conditional.
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { ContentApi } from '../../content/content.api';
import { TransactingContext } from '../../core/transacting-context';
import { Footer } from './footer';

async function footerFor(market: string, locale?: string) {
  const fixture = TestBed.createComponent(Footer);
  const context = TestBed.inject(TransactingContext);
  context.setMarket(market);
  if (locale) {
    context.setLocale(locale);
  }
  await fixture.whenStable();
  const host = fixture.nativeElement as HTMLElement;
  const docs = Array.from(host.querySelectorAll<HTMLElement>('[data-legal-doc]')).map(
    (link) => link.dataset['legalDoc'],
  );
  return { host, docs };
}

describe('Footer legal links (issue 077)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Footer],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders the shared legal set in a non-DE market (AC-01)', async () => {
    const { docs } = await footerFor('US');
    expect(docs).toEqual(['privacy', 'terms', 'shipping-returns']);
  });

  it('adds Impressum and Widerrufsbelehrung as FIRST-CLASS footer items in DE (AC-02, DESIGN.md §8.4)', async () => {
    const { host, docs } = await footerFor('DE', 'de-DE');

    expect(docs).toContain('impressum');
    expect(docs).toContain('widerruf');
    // First-class = same list, same level as every other legal item — not
    // nested in a secondary group (DESIGN.md §8.4: not buried).
    const items = Array.from(host.querySelectorAll('.legal-list > .legal-item'));
    expect(items.length).toBe(5);
    for (const id of ['impressum', 'widerruf']) {
      const link = host.querySelector<HTMLElement>(`[data-legal-doc="${id}"]`)!;
      expect(link.tagName).toBe('A');
      expect(link.parentElement?.classList.contains('legal-item')).toBe(true);
      expect(link.getAttribute('href')).toBe(`/legal/${id}`);
    }
    // Statutory German names in every locale.
    expect(host.querySelector('[data-legal-doc="impressum"]')?.textContent).toContain('Impressum');
  });

  it('shows Impressum/Widerruf in NO other market (AC-05 negative, CC-CNT-005)', async () => {
    for (const market of ['US', 'ES', 'MX', 'JP', 'IN']) {
      TestBed.resetTestingModule();
      await TestBed.configureTestingModule({
        imports: [Footer],
        providers: [provideRouter([])],
      }).compileComponents();

      const { host, docs } = await footerFor(market);
      expect(docs).not.toContain('impressum');
      expect(docs).not.toContain('widerruf');
      expect(host.textContent).not.toContain('Impressum');
      expect(host.textContent).not.toContain('Widerrufsbelehrung');
    }
  });

  it('localizes the legal titles for the active locale (CC-I18N-001)', async () => {
    const { host } = await footerFor('ES', 'es-ES');
    expect(host.querySelector('[data-legal-doc="privacy"]')?.textContent).toContain(
      'Política de privacidad',
    );
  });

  it('fails closed to NO legal links when the set cannot be resolved (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Footer],
      providers: [
        provideRouter([]),
        {
          provide: ContentApi,
          useValue: { getLegalDocList: () => throwError(() => new Error('policy unavailable')) },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Footer);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;

    // Never a guessed or another market's set.
    expect(host.querySelector('[data-testid="legal-nav"]')).toBeNull();
    expect(host.textContent).not.toContain('policy unavailable');
    // The rest of the footer still renders.
    expect(host.querySelector('.wordmark')).toBeTruthy();
  });
});
