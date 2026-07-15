/**
 * Transacting market/locale context (issue 063; CC-MKT-002, CC-I18N-001).
 *
 * The market and the locale are INDEPENDENT user selections (REQUIREMENTS.md
 * §2; DESIGN.md §7): changing one never changes the other, and neither is
 * ever inferred from the other.
 *
 * State flows from the SERVER. During SSR the context is seeded from the
 * server-side transacting state (placeholder provider below — the real
 * resolution/persistence API is issue 024 and is an intentionally open seam)
 * and carried to the browser via Angular TransferState, so hydration renders
 * exactly what the server rendered.
 *
 * SECURITY (CC-SEC-012; SECURITY.md, Authentication and authorization
 * rule 10): client hints MUST NOT drive gating. This service NEVER reads
 * `navigator.language`, `navigator.languages`, `Accept-Language`, or
 * geolocation — a spec asserts that. Geolocation may only *propose* a market
 * server-side (issue 024), and the user's explicit choice overrides it.
 * The client performs no gating at all (ARCHITECTURE.md, Dependency rule 1):
 * these signals only display what the server already gated, and switcher
 * changes express user intent that the server re-validates and persists
 * (issue 024). Unknown values are rejected here and again server-side.
 */

import {
  Injectable,
  InjectionToken,
  PLATFORM_ID,
  Provider,
  TransferState,
  computed,
  inject,
  makeStateKey,
  signal,
} from '@angular/core';
import { isPlatformServer } from '@angular/common';

/** The six launch markets (CC-MKT-001). */
export const MARKETS = ['US', 'ES', 'MX', 'DE', 'JP', 'IN'] as const;
export type Market = (typeof MARKETS)[number];

/** The seven launch locales, BCP 47 (CC-I18N-001). */
export const LOCALES = ['en-US', 'es-ES', 'es-MX', 'de-DE', 'ja-JP', 'en-IN', 'hi-IN'] as const;
export type Locale = (typeof LOCALES)[number];

export interface TransactingContextState {
  readonly market: Market;
  readonly locale: Locale;
}

export function isMarket(value: string): value is Market {
  return (MARKETS as readonly string[]).includes(value);
}

export function isLocale(value: string): value is Locale {
  return (LOCALES as readonly string[]).includes(value);
}

/**
 * Neutral bootstrap default used only until issue 024's server resolution
 * exists. NOT derived from any client hint.
 */
export const DEFAULT_TRANSACTING_CONTEXT: TransactingContextState = {
  market: 'US',
  locale: 'en-US',
};

/** TransferState key carrying the server-resolved context to the browser. */
export const TRANSACTING_CONTEXT_KEY = makeStateKey<TransactingContextState>('cc-transacting-context');

/**
 * Server-side seam for issue 024 ("Transacting market/locale resolution:
 * geo proposal, user override persistence"). The host wiring will replace
 * this provider with the real per-request resolution (persisted explicit
 * user choice first, geolocation only as a proposal — CC-MKT-002). Until
 * then it yields the neutral default. It deliberately takes NO client input.
 */
export const SERVER_TRANSACTING_CONTEXT = new InjectionToken<TransactingContextState>(
  'SERVER_TRANSACTING_CONTEXT',
);

export function provideServerTransactingContext(): Provider {
  return {
    provide: SERVER_TRANSACTING_CONTEXT,
    // Placeholder: issue 024 replaces this factory with real server state.
    useFactory: (): TransactingContextState => DEFAULT_TRANSACTING_CONTEXT,
  };
}

@Injectable({ providedIn: 'root' })
export class TransactingContext {
  private readonly state = signal<TransactingContextState>(this.resolveInitialState());

  /** The transacting market: drives catalog, currency, compliance (server-gated). */
  readonly market = computed(() => this.state().market);

  /** The UI locale: drives strings and formatting only. */
  readonly locale = computed(() => this.state().locale);

  private resolveInitialState(): TransactingContextState {
    const transferState = inject(TransferState);
    if (isPlatformServer(inject(PLATFORM_ID))) {
      const seed =
        inject(SERVER_TRANSACTING_CONTEXT, { optional: true }) ?? DEFAULT_TRANSACTING_CONTEXT;
      // Carry the server-resolved state into hydration (never a client hint).
      transferState.set(TRANSACTING_CONTEXT_KEY, seed);
      return seed;
    }
    // Browser: exactly what the server rendered; on a non-SSR bootstrap
    // (dev serve) fall back to the neutral default — never navigator.*.
    return transferState.get(TRANSACTING_CONTEXT_KEY, DEFAULT_TRANSACTING_CONTEXT);
  }

  /**
   * Explicit user market choice (CC-MKT-002). Changes ONLY the market —
   * the locale is untouched (AC-04). Invalid values are ignored (the server
   * re-validates in issue 024; failure is a no-op, never a fallback to a
   * client-hint-derived market).
   */
  setMarket(market: string): void {
    if (isMarket(market) && market !== this.state().market) {
      this.state.update((s) => ({ ...s, market }));
    }
  }

  /**
   * Explicit user locale choice (CC-I18N-001). Changes ONLY the locale —
   * the market is untouched (AC-04).
   */
  setLocale(locale: string): void {
    if (isLocale(locale) && locale !== this.state().locale) {
      this.state.update((s) => ({ ...s, locale }));
    }
  }
}
