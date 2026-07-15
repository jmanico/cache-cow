/**
 * TransactingContext tests (issue 063).
 * Requirement tags: CC-MKT-002, CC-I18N-001, CC-SEC-012 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { TransferState } from '@angular/core';
import {
  DEFAULT_TRANSACTING_CONTEXT,
  LOCALES,
  MARKETS,
  TRANSACTING_CONTEXT_KEY,
  TransactingContext,
} from './transacting-context';

describe('TransactingContext', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('exposes the six launch markets and seven launch locales (CC-MKT-001, CC-I18N-001)', () => {
    expect(MARKETS).toEqual(['US', 'ES', 'MX', 'DE', 'JP', 'IN']);
    expect(LOCALES).toEqual(['en-US', 'es-ES', 'es-MX', 'de-DE', 'ja-JP', 'en-IN', 'hi-IN']);
  });

  it('is seeded from server-provided transfer state, not client hints (CC-SEC-012)', () => {
    TestBed.inject(TransferState).set(TRANSACTING_CONTEXT_KEY, { market: 'DE', locale: 'ja-JP' });
    const context = TestBed.inject(TransactingContext);
    expect(context.market()).toBe('DE');
    expect(context.locale()).toBe('ja-JP');
  });

  it('falls back to the neutral default without transfer state (non-SSR bootstrap)', () => {
    const context = TestBed.inject(TransactingContext);
    expect(context.market()).toBe(DEFAULT_TRANSACTING_CONTEXT.market);
    expect(context.locale()).toBe(DEFAULT_TRANSACTING_CONTEXT.locale);
  });

  it('never reads navigator.language/languages — client hints must not drive gating (CC-SEC-012)', () => {
    const proto = Object.getPrototypeOf(navigator) as object;
    const accessed: string[] = [];
    const originals = new Map<string, PropertyDescriptor | undefined>();
    for (const property of ['language', 'languages'] as const) {
      originals.set(property, Object.getOwnPropertyDescriptor(proto, property));
      Object.defineProperty(proto, property, {
        configurable: true,
        get() {
          accessed.push(property);
          return property === 'language' ? 'xx-XX' : ['xx-XX'];
        },
      });
    }
    try {
      const context = TestBed.inject(TransactingContext);
      // Exercise the full read/write surface of the service.
      expect(context.market()).toBe('US');
      expect(context.locale()).toBe('en-US');
      context.setMarket('DE');
      context.setLocale('ja-JP');
      expect(context.market()).toBe('DE');
      expect(context.locale()).toBe('ja-JP');
      expect(accessed).toEqual([]);
    } finally {
      for (const [property, descriptor] of originals) {
        if (descriptor) {
          Object.defineProperty(proto, property, descriptor);
        } else {
          delete (proto as Record<string, unknown>)[property];
        }
      }
    }
  });

  describe('independence of the two selections (CC-MKT-002, CC-I18N-001, 063 AC-04)', () => {
    it('setMarket changes only the market; the locale is untouched', () => {
      const context = TestBed.inject(TransactingContext);
      const localeBefore = context.locale();
      context.setMarket('JP');
      expect(context.market()).toBe('JP');
      expect(context.locale()).toBe(localeBefore);
    });

    it('setLocale changes only the locale; the market is untouched', () => {
      const context = TestBed.inject(TransactingContext);
      const marketBefore = context.market();
      context.setLocale('hi-IN');
      expect(context.locale()).toBe('hi-IN');
      expect(context.market()).toBe(marketBefore);
    });

    it('never infers market from locale: shopping the DE market in English is valid (REQUIREMENTS.md §2)', () => {
      const context = TestBed.inject(TransactingContext);
      context.setMarket('DE');
      context.setLocale('en-US');
      expect(context.market()).toBe('DE');
      expect(context.locale()).toBe('en-US');
    });
  });

  it('rejects unknown market/locale values (server re-validates in issue 024)', () => {
    const context = TestBed.inject(TransactingContext);
    context.setMarket('XX');
    context.setLocale('tlh-QO');
    expect(context.market()).toBe('US');
    expect(context.locale()).toBe('en-US');
  });
});
