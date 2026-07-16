/**
 * Legal page tests (issue 077; CC-CNT-005, CC-FUL-003).
 * Requirement tags per REQUIREMENTS.md §17: CC-CNT-005, CC-FUL-003,
 * CC-I18N-001, CC-SEC-002.
 *
 * Scope note: the per-market page-set matrix and the HTTP 404 status for a
 * document outside a market's set are server-side (issues 023/026); these
 * assert the client's rendering of the set the seam resolves. The DE
 * Widerrufsbelehrung's accepted TEXT is not asserted here — the mock carries
 * placeholders, and the accepted text arrives through legal review (issue
 * 077 open question on the authoring source).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { throwError } from 'rxjs';
import { routes } from '../../app.routes';
import { ContentApi } from '../../content/content.api';
import { TransactingContext } from '../../core/transacting-context';
import { Legal } from './legal';

async function openDoc(docId: string) {
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/legal/${docId}`, Legal);
  await harness.fixture.whenStable();
  return harness;
}

describe('Legal (issue 077)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes)],
    });
  });

  it('renders a versioned document with its version and effective date (AC-01/AC-04)', async () => {
    const harness = await openDoc('privacy');
    const host = harness.routeNativeElement as HTMLElement;

    const doc = host.querySelector<HTMLElement>('[data-testid="legal-doc"]')!;
    expect(doc.dataset['doc']).toBe('privacy');
    expect(host.querySelector('[data-testid="legal-title"]')?.textContent).toContain('Privacy policy');
    // The response identifies the version of the text being served.
    expect(host.querySelector('[data-testid="legal-version"]')?.textContent).toContain('1.0.0');
    // Locale-formatted effective date (CC-I18N-003), never hand-formatted.
    expect(host.querySelector('[data-testid="legal-effective-date"]')?.textContent).toContain(
      'July 15, 2026',
    );
    expect(host.querySelectorAll('[data-testid="legal-section"]').length).toBeGreaterThan(0);
  });

  it('serves the shared set (privacy, terms, shipping-returns) in a non-DE market (AC-01)', async () => {
    for (const docId of ['privacy', 'terms', 'shipping-returns']) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
      const harness = await openDoc(docId);
      const host = harness.routeNativeElement as HTMLElement;
      expect(host.querySelector<HTMLElement>('[data-testid="legal-doc"]')?.dataset['doc']).toBe(docId);
    }
  });

  it('serves Impressum and Widerrufsbelehrung in the DE market (AC-02, CC-CNT-005)', async () => {
    for (const docId of ['impressum', 'widerruf']) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
      const harness = await RouterTestingHarness.create();
      TestBed.inject(TransactingContext).setMarket('DE');
      TestBed.inject(TransactingContext).setLocale('de-DE');
      await harness.navigateByUrl(`/legal/${docId}`, Legal);
      await harness.fixture.whenStable();
      const host = harness.routeNativeElement as HTMLElement;

      expect(host.querySelector<HTMLElement>('[data-testid="legal-doc"]')?.dataset['doc']).toBe(docId);
      expect(host.querySelector('[data-testid="legal-not-found"]')).toBeNull();
    }
  });

  it('404s the DE-only documents in a non-DE market — no cross-market legal text (AC-05 negative)', async () => {
    for (const docId of ['impressum', 'widerruf']) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
      // Default market is US.
      const harness = await openDoc(docId);
      const host = harness.routeNativeElement as HTMLElement;

      expect(host.querySelector('[data-testid="legal-not-found"]')).toBeTruthy();
      expect(host.textContent).toContain('Signal lost');
      expect(host.querySelector('[data-testid="legal-doc"]')).toBeNull();
    }
  });

  it('references the perishable-frozen-food withdrawal exemption on the DE Widerruf page (AC-03, CC-FUL-003)', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(TransactingContext).setMarket('DE');
    TestBed.inject(TransactingContext).setLocale('de-DE');
    await harness.navigateByUrl('/legal/widerruf', Legal);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    // The mock carries a PLACEHOLDER scope note, not the accepted legal text
    // (which comes from legal review — issue 077). This asserts the scope is
    // carried through to the page, and pins the placeholder so replacing it
    // with the accepted text is an explicit, reviewed diff.
    expect(host.textContent).toContain('Widerrufsrecht');
    expect(host.textContent).toContain('Tiefkühlware');
  });

  it('serves documents in the active locale (AC-01, CC-I18N-001)', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(TransactingContext).setLocale('ja-JP');
    await harness.navigateByUrl('/legal/privacy', Legal);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="legal-title"]')?.textContent).toContain(
      'プライバシーポリシー',
    );
  });

  it('renders legal body text as inert text, never as markup (AC-07, CC-SEC-002)', async () => {
    const harness = await openDoc('privacy');
    const host = harness.routeNativeElement as HTMLElement;
    const paragraph = host.querySelector<HTMLElement>('.legal-paragraph')!;
    expect(paragraph.querySelector('*')).toBeNull();
    expect(paragraph.innerHTML).not.toContain('<');
  });

  it('carries zero cache/tech puns on legal surfaces (AC-06, DESIGN.md §5.4)', async () => {
    const harness = await openDoc('privacy');
    const text = (harness.routeNativeElement as HTMLElement).textContent ?? '';
    // The brand's pun vocabulary (cache/eviction wording and the stock-badge
    // status language of DESIGN.md §5.2) never appears in legal content.
    // Matched case-insensitively by pattern so this spec does not itself
    // hardcode the badge strings (tokens:check gate, Dependency rule 8).
    for (const pun of [/cache/i, /eviction/i, /signal lost/i, /\bhit\b/i, /\bmiss\b/i, /warming/i]) {
      expect(text).not.toMatch(pun);
    }
  });

  it('fails closed to a generic error rather than another market’s text (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        {
          provide: ContentApi,
          useValue: {
            getLegalDoc: () => throwError(() => new Error('schema violation')),
            getLegalDocList: () => throwError(() => new Error('schema violation')),
          },
        },
      ],
    });
    const harness = await openDoc('privacy');
    const host = harness.routeNativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="legal-error"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="legal-doc"]')).toBeNull();
    expect(host.textContent).not.toContain('schema violation');
  });
});
