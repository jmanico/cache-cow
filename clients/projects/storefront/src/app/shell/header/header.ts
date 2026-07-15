/**
 * Storefront header (issue 063; DESIGN.md §7 "Region and language switcher").
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
 * No gating here (ARCHITECTURE.md, Dependency rule 1): the controls only
 * express user intent to TransactingContext; server-side re-validation and
 * persistence are issue 024.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  LOCALES,
  Locale,
  MARKETS,
  Market,
  TransactingContext,
} from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';

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

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './header.html',
  styleUrl: './header.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  protected readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);
  protected readonly markets = MARKETS;
  protected readonly locales = LOCALES;

  protected marketNameKey(market: Market): MessageKey {
    return MARKET_NAME_KEYS[market];
  }

  protected localeNameKey(locale: Locale): MessageKey {
    return LOCALE_NAME_KEYS[locale];
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
