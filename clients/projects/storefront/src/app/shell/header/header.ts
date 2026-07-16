/**
 * Storefront header (issue 063; DESIGN.md §7 "Region and language switcher";
 * navigation placement: issues 074/075, CC-MKT-005).
 *
 * Two INDEPENDENT controls: the market switcher (six markets — drives
 * catalog, currency, compliance) and the language switcher (seven locales —
 * drives strings). Changing one never changes the other, and neither is
 * inferred from the other (CC-MKT-002, CC-I18N-001, AC-04).
 *
 * Native <select> elements: fully keyboard-operable with visible focus
 * (DESIGN.md §13; focus outline in header.css). The visual form of the
 * switchers (dropdown vs. dialog) is an open question in issue 063; native
 * selects are the accessible placeholder.
 *
 * NAVIGATION PLACEMENT IS POLICY DATA, NOT LOGIC (CC-MKT-005, CC-MKT-006).
 * The header renders whatever the NavPolicy seam hands it — which pages sit
 * in primary navigation, which sit under "Our Story", and which exist at all
 * in the transacting market. There is deliberately NO `if (market === 'IN')`
 * here: the India inversion (cows promoted to primary, cuts absent entirely
 * — DESIGN.md §8.1) is expressed in the policy payload. The REAL policy
 * owner is the server-side Market & Gating Policy context (issues 023/025);
 * the mock seam stands in until it lands, and the client never gates
 * (ARCHITECTURE.md, Dependency rule 1).
 *
 * Fail closed: if the policy cannot be resolved, NO navigation renders —
 * never a guessed placement (SECURITY.md, Logging rule 2).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import {
  LOCALES,
  Locale,
  MARKETS,
  Market,
  TransactingContext,
} from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import { NavPolicyApi } from '../../nav/nav-policy.api';
import { NavPage, NavPolicy } from '../../nav/nav-policy.types';

/** Typed key lookup so bundle keys stay compile-checked (no string building). */
const MARKET_NAME_KEYS: Readonly<Record<Market, MessageKey>> = {
  US: 'shell.market.us',
  ES: 'shell.market.es',
  MX: 'shell.market.mx',
  DE: 'shell.market.de',
  JP: 'shell.market.jp',
  IN: 'shell.market.in',
};

const LOCALE_NAME_KEYS: Readonly<Record<Locale, MessageKey>> = {
  'en-US': 'shell.locale.en-US',
  'es-ES': 'shell.locale.es-ES',
  'es-MX': 'shell.locale.es-MX',
  'de-DE': 'shell.locale.de-DE',
  'ja-JP': 'shell.locale.ja-JP',
  'en-IN': 'shell.locale.en-IN',
  'hi-IN': 'shell.locale.hi-IN',
};

/** Typed label + route lookup per nav page (compile-checked; the ROUTES are
 * fixed, the PLACEMENT is policy data). */
const NAV_PAGE_LABEL_KEYS: Readonly<Record<NavPage, MessageKey>> = {
  menu: 'shell.nav.menu',
  chefs: 'shell.nav.chefs',
  cows: 'shell.nav.cows',
  cuts: 'shell.nav.cuts',
  stores: 'shell.nav.stores',
};

const NAV_PAGE_ROUTES: Readonly<Record<NavPage, string>> = {
  menu: '/menu',
  chefs: '/chefs',
  cows: '/cows',
  cuts: '/cuts',
  stores: '/stores',
};

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './header.html',
  styleUrl: './header.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  private readonly navPolicy = inject(NavPolicyApi);
  protected readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);
  protected readonly markets = MARKETS;
  protected readonly locales = LOCALES;

  /**
   * The server-resolved navigation policy for the transacting market.
   * Re-asked whenever the market changes. null = unresolved → no navigation
   * renders (fail closed, never a guessed placement).
   */
  protected readonly policy = toSignal<NavPolicy | null, NavPolicy | null>(
    toObservable(computed(() => this.context.market())).pipe(
      switchMap(() =>
        this.navPolicy.getNavPolicy().pipe(
          map((policy): NavPolicy | null => policy),
          catchError(() => of<NavPolicy | null>(null)),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected marketNameKey(market: Market): MessageKey {
    return MARKET_NAME_KEYS[market];
  }

  protected localeNameKey(locale: Locale): MessageKey {
    return LOCALE_NAME_KEYS[locale];
  }

  protected navLabelKey(page: NavPage): MessageKey {
    return NAV_PAGE_LABEL_KEYS[page];
  }

  protected navRoute(page: NavPage): string {
    return NAV_PAGE_ROUTES[page];
  }

  protected onMarketChange(event: Event): void {
    // Changes ONLY the market; the locale is untouched (DESIGN.md §7).
    this.context.setMarket((event.target as HTMLSelectElement).value);
  }

  protected onLocaleChange(event: Event): void {
    // Changes ONLY the locale; the market is untouched (DESIGN.md §7).
    this.context.setLocale((event.target as HTMLSelectElement).value);
  }
}
