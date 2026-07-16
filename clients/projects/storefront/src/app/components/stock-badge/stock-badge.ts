/**
 * Cache-status stock badge (issue 066; CC-CAT-003; DESIGN.md §5.2).
 *
 * Exactly three states, always the badge PLUS its plain-language line —
 * status is never conveyed by color alone (DESIGN.md §13):
 *   cache-hit   (cache.500)  "Ships today from your regional cold store"
 *   warming     (ember.500)  "Restocking, preorder available"
 *   cache-miss  (smoke.400)  "Not available in your region yet"
 *
 * The state is the SERVER-derived value from the typed catalog response
 * (CC-CAT-003, issue 030); this component maps state -> presentation only.
 *
 * BADGE VOCABULARY AND COLORS COME FROM THE GENERATED TOKENS (ARCHITECTURE.md
 * Dependency rule 8; tokens/dist/tokens.json `status.*.badge` + the
 * --cc-status-* CSS variables) — the badge words are DESIGN.md §5.2 brand
 * vocabulary, identical across locales like the tagline lockup, and the
 * tokens gate rejects any hardcoded copy of them. The plain-language lines
 * are locale strings (CC-I18N-002; per-market translation is native-speaker
 * editorial work, DESIGN.md §9 — open). The badge terms are within the §5.4
 * pun budget as the sanctioned §5.2 vocabulary.
 */

import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { StockState } from '../../catalog/catalog.types';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import tokens from '../../../../../../tokens/dist/tokens.json';

/** Badge vocabulary from the generated design tokens (Dependency rule 8). */
export const BADGE_TEXT: Readonly<Record<StockState, string>> = {
  cacheHit: tokens.status.cacheHit.badge,
  warming: tokens.status.warming.badge,
  cacheMiss: tokens.status.cacheMiss.badge,
};

/** Typed key lookup — keys stay compile-checked (no string building). */
const LINE_KEYS: Readonly<Record<StockState, MessageKey>> = {
  cacheHit: 'stock.cacheHit.line',
  warming: 'stock.warming.line',
  cacheMiss: 'stock.cacheMiss.line',
};

@Component({
  selector: 'app-stock-badge',
  templateUrl: './stock-badge.html',
  styleUrl: './stock-badge.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockBadge {
  protected readonly i18n = inject(I18nService);

  readonly state = input.required<StockState>();

  protected badgeText(): string {
    return BADGE_TEXT[this.state()];
  }

  protected lineKey(): MessageKey {
    return LINE_KEYS[this.state()];
  }
}
