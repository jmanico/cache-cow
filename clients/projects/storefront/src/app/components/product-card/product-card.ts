/**
 * Product card (issue 066; DESIGN.md §7 "Product card").
 *
 * Composition per §7: photo 4:5 (placeholder pending photography, §8.6),
 * name in Archivo 700, cut/weight in Plex Mono, locale-formatted price in
 * Plex Mono with the market's tax-inclusion note (CC-PRC-002/004),
 * cache-status badge, veg indicator where applicable. The ENTIRE card is one
 * link (stretched-link pattern keeps the DOM valid); add-to-cart is a
 * separate action inside it.
 *
 * Everything shown comes from one typed, validated `ProductSummary` — the
 * card computes nothing and gates nothing (ARCHITECTURE.md, Dependency
 * rules 1–2). Action per state (CC-CAT-003, issue 066 AC-07): in stock →
 * add to cart; restocking → preorder; unavailable in region → no purchase
 * action ("offer nearest substitute" has no specified mechanism — open
 * question, issue 066).
 *
 * The cart itself is issue 068: the action button is a STUB seam that only
 * emits; nothing here stores cart state.
 */

import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatMinorUnits } from '../../catalog/format-price';
import { Cut, ProductSummary } from '../../catalog/catalog.types';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import { StockBadge } from '../stock-badge/stock-badge';
import { VegIndicator } from '../veg-indicator/veg-indicator';

/** Typed key lookup for the cut vocabulary (compile-checked, no string building). */
export const CUT_NAME_KEYS: Readonly<Record<Cut, MessageKey>> = {
  brisket: 'cut.brisket',
  ribs: 'cut.ribs',
  shoulder: 'cut.shoulder',
  sausage: 'cut.sausage',
  paneer: 'cut.paneer',
  jackfruit: 'cut.jackfruit',
  mushroom: 'cut.mushroom',
};

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, StockBadge, VegIndicator],
  templateUrl: './product-card.html',
  styleUrl: './product-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductCard {
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  readonly product = input.required<ProductSummary>();

  /** Cart seam (issue 068): emits the acted-on SKU; no cart exists yet. */
  readonly addToCart = output<string>();

  protected readonly priceText = computed(() =>
    formatMinorUnits(this.product().price.amountMinor, this.product().price.currency, this.context.locale()),
  );

  protected cutKey(): MessageKey {
    return CUT_NAME_KEYS[this.product().cut];
  }

  protected taxNoteKey(): MessageKey {
    return this.product().price.taxDisplay === 'inclusive'
      ? 'price.taxNote.inclusive'
      : 'price.taxNote.exclusive';
  }

  /** DE per-kg unit price (CC-PRC-002): display-formats the server value. */
  protected unitPerKgText(): string {
    const unitPerKgMinor = this.product().price.unitPerKgMinor;
    return unitPerKgMinor === undefined
      ? ''
      : formatMinorUnits(unitPerKgMinor, this.product().price.currency, this.context.locale());
  }

  protected onAction(): void {
    // Stub: issue 068 (cart) consumes this event; deliberately no-op here.
    this.addToCart.emit(this.product().sku);
  }
}
