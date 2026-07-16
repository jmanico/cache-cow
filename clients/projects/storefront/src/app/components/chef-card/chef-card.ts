/**
 * Chef card (issue 073; CC-CNT-001; DESIGN.md §7 "Chef card").
 *
 * Composition per §7: portrait, name, pit specialty, market flag(s). The
 * roster is shared across markets; the bios arrive already localized for the
 * transacting locale (server-side — the client never translates content).
 *
 * Everything shown comes from one typed, validated `ChefProfile`. The bio and
 * specialty are PLAIN TEXT that the CMS pipeline already sanitized through
 * the allowlist renderer server-side (issue 072); they bind through Angular
 * text interpolation only — no [innerHTML], no bypassSecurityTrust*
 * (CC-SEC-002; SECURITY.md, Input validation rule 5).
 *
 * The market flags are DATA ABOUT THE CHEF (which pits they cook for), not a
 * gating decision: the card renders the markets the server sent and holds no
 * market conditional of its own (ARCHITECTURE.md, Dependency rule 1).
 *
 * Portrait: placeholder frame pending real photography (DESIGN.md §8.6 —
 * an open asset decision, deliberately not invented here).
 */

import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ChefProfile } from '../../content/content.types';
import { Market } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';

/** Typed key lookup for market names (compile-checked, no string building). */
const MARKET_NAME_KEYS: Readonly<Record<Market, MessageKey>> = {
  US: 'shell.market.us',
  ES: 'shell.market.es',
  MX: 'shell.market.mx',
  DE: 'shell.market.de',
  JP: 'shell.market.jp',
  IN: 'shell.market.in',
};

@Component({
  selector: 'app-chef-card',
  templateUrl: './chef-card.html',
  styleUrl: './chef-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChefCard {
  protected readonly i18n = inject(I18nService);

  readonly chef = input.required<ChefProfile>();

  protected marketNameKey(market: Market): MessageKey {
    return MARKET_NAME_KEYS[market];
  }
}
