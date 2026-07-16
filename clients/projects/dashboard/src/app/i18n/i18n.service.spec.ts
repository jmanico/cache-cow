/**
 * Dashboard i18n tests (issue 079 shell scope): typed keys, plain-text
 * interpolation, fail-closed lookup, no HTML in the bundle
 * (SECURITY.md, Input validation rules 5 and 7).
 */

import { TestBed } from '@angular/core/testing';
import enUS from './en-US.json';
import { DashboardI18n, DashboardMessageKey } from './i18n.service';

describe('DashboardI18n', () => {
  let i18n: DashboardI18n;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    i18n = TestBed.inject(DashboardI18n);
  });

  it('returns the message for a known key', () => {
    expect(i18n.t('gate.title')).toBe('Sign-in required');
  });

  it('interpolates placeholders as plain text', () => {
    expect(i18n.t('shell.header.signedInAs', { name: 'Ada' })).toBe('Signed in as Ada');
    expect(i18n.t('module.placeholder.pending', { issue: '082' })).toContain('issue 082');
  });

  it('leaves unknown placeholders intact rather than emitting undefined', () => {
    expect(i18n.t('shell.header.signedInAs')).toBe('Signed in as {name}');
  });

  it('fails closed to the key for unknown keys', () => {
    expect(i18n.t('nope.missing' as DashboardMessageKey)).toBe('nope.missing');
  });

  it('bundle contains no HTML (Input validation rule 7: plain text only)', () => {
    for (const value of Object.values(enUS)) {
      expect(value).not.toMatch(/[<>]/);
    }
  });

  it('interpolated values are not treated as markup', () => {
    const out = i18n.t('shell.header.signedInAs', { name: '<img src=x>' });
    // The service returns plain text; templates bind it via text
    // interpolation only, so this string is never parsed as HTML.
    expect(out).toBe('Signed in as <img src=x>');
  });
});
