/**
 * I18nService tests (issue 064, client half).
 * Requirement tags: CC-I18N-002, CC-I18N-001 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';
import { MESSAGES } from './messages';
import { LOCALES, TransactingContext } from '../core/transacting-context';

describe('I18nService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('formats messages for the current locale and re-resolves after a locale change', () => {
    const i18n = TestBed.inject(I18nService);
    const context = TestBed.inject(TransactingContext);

    expect(i18n.t('shell.header.marketLabel')).toBe('Market');
    context.setLocale('de-DE');
    expect(i18n.t('shell.header.marketLabel')).toBe('Markt');
    context.setLocale('hi-IN');
    expect(i18n.t('shell.header.marketLabel')).toBe('बाज़ार');
  });

  it('interpolates values (footer legal line with {year})', () => {
    const i18n = TestBed.inject(I18nService);
    // Numbers are locale-formatted; a year must not be grouped, so it is
    // passed as a string by the footer.
    expect(i18n.t('shell.footer.legal', { year: '2026' })).toContain('2026');
  });

  it('fails closed to the key on a formatting error — never raw fallthrough (064 Failure Behavior)', () => {
    const i18n = TestBed.inject(I18nService);
    // Missing required placeholder value.
    expect(i18n.t('shell.footer.legal')).toBe('shell.footer.legal');
  });

  it('has a complete bundle for every launch locale (CC-I18N-001/002)', () => {
    const referenceKeys = Object.keys(MESSAGES['en-US']).sort();
    for (const locale of LOCALES) {
      expect(Object.keys(MESSAGES[locale]).sort(), locale).toEqual(referenceKeys);
    }
  });

  it('no message in any bundle contains HTML (SECURITY.md, Input validation rule 7)', () => {
    for (const locale of LOCALES) {
      for (const [key, message] of Object.entries(MESSAGES[locale])) {
        expect(message.includes('<'), `${locale}:${key}`).toBe(false);
      }
    }
  });
});
