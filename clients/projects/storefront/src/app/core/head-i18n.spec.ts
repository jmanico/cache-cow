/**
 * lang/hreflang emission tests (issue 063 AC-02).
 * Requirement tags: CC-I18N-004, CC-I18N-001 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { TransferState } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HeadI18n } from './head-i18n';
import { LOCALES, TRANSACTING_CONTEXT_KEY, TransactingContext } from './transacting-context';

function hreflangLinks(): Map<string, string> {
  const links = new Map<string, string>();
  for (const link of Array.from(document.head.querySelectorAll('link[data-cc-hreflang]'))) {
    links.set(link.getAttribute('hreflang') ?? '', link.getAttribute('href') ?? '');
  }
  return links;
}

describe('HeadI18n (CC-I18N-004)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('declares the rendered locale as the document lang and emits all alternates', () => {
    TestBed.inject(TransferState).set(TRANSACTING_CONTEXT_KEY, { market: 'DE', locale: 'de-DE' });
    TestBed.inject(HeadI18n);
    TestBed.tick();

    expect(document.documentElement.getAttribute('lang')).toBe('de-DE');
    const links = hreflangLinks();
    // One alternate per launch locale plus x-default.
    for (const locale of LOCALES) {
      expect(links.get(locale), `hreflang ${locale}`).toContain(`locale=${locale}`);
    }
    expect(links.has('x-default')).toBe(true);
    expect(links.size).toBe(LOCALES.length + 1);
  });

  it.each(LOCALES)('declares lang="%s" when that locale is rendered', (locale) => {
    TestBed.inject(TransferState).set(TRANSACTING_CONTEXT_KEY, { market: 'US', locale });
    TestBed.inject(HeadI18n);
    TestBed.tick();
    expect(document.documentElement.getAttribute('lang')).toBe(locale);
  });

  it('updates lang and alternates when the locale changes (and replaces, not accumulates, links)', () => {
    TestBed.inject(HeadI18n);
    TestBed.tick();
    expect(document.documentElement.getAttribute('lang')).toBe('en-US');
    const sizeBefore = hreflangLinks().size;

    TestBed.inject(TransactingContext).setLocale('hi-IN');
    TestBed.tick();

    expect(document.documentElement.getAttribute('lang')).toBe('hi-IN');
    expect(hreflangLinks().size).toBe(sizeBefore);
  });

  it('changing the locale does not change the market (independence, CC-MKT-002)', () => {
    TestBed.inject(TransferState).set(TRANSACTING_CONTEXT_KEY, { market: 'JP', locale: 'ja-JP' });
    TestBed.inject(HeadI18n);
    const context = TestBed.inject(TransactingContext);
    TestBed.tick();

    context.setLocale('en-US');
    TestBed.tick();

    expect(document.documentElement.getAttribute('lang')).toBe('en-US');
    expect(context.market()).toBe('JP');
  });
});
