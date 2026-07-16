/**
 * Product detail page (issue 067; CC-CAT-004, CC-CNT-006, CC-PRC-002;
 * DESIGN.md §10 "Product detail").
 *
 * Renders ONE typed, validated `ProductDetail` from the CatalogApi seam:
 * gallery placeholder, weight/serving, ingredients, typed allergen list,
 * nutrition table, storage, per-format reheat instructions, regional price
 * per the market's tax convention. Allergen/nutrition render exclusively
 * from structured fields — there is no CMS/free-text input path into this
 * component at all (CC-CAT-004 negative case, AC-02), and zero puns appear
 * in the safety sections (DESIGN.md §5.4).
 *
 * NO client-side gating (ARCHITECTURE.md, Dependency rule 1): veg marking is
 * the server's `vegMarking` policy value (FSSAI regulation mark in IN —
 * CC-CNT-006 — leaf-dot elsewhere); the DE per-kg unit price renders if and
 * only if the typed response carries it. A SKU the seam reports absent
 * renders the 404 page ("Signal lost"); the HTTP 404 status for gated/unknown
 * product URLs is server-owned (CC-MKT-004, issues 025/026 — the real server
 * 404s before this page ever sees a non-veg SKU in IN).
 *
 * Fail closed: a response failing schema validation renders the generic
 * error state, never partial data (SECURITY.md, Input validation rule 1).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { CatalogApi } from '../../catalog/catalog.api';
import { ProductDetail as ProductDetailData, ReheatFormat } from '../../catalog/catalog.types';
import { formatMinorUnits } from '../../catalog/format-price';
import { CUT_NAME_KEYS } from '../../components/product-card/product-card';
import { StockBadge } from '../../components/stock-badge/stock-badge';
import { VegIndicator } from '../../components/veg-indicator/veg-indicator';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import { NotFound } from '../not-found/not-found';

type DetailState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'notFound' }
  | { readonly kind: 'ready'; readonly product: ProductDetailData };

const REHEAT_KEYS: Readonly<Record<ReheatFormat, MessageKey>> = {
  oven: 'reheat.oven',
  sousVide: 'reheat.sousVide',
  steam: 'reheat.steam',
};

@Component({
  selector: 'app-product-detail',
  imports: [StockBadge, VegIndicator, NotFound],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductDetail {
  private readonly api = inject(CatalogApi);
  private readonly route = inject(ActivatedRoute);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  protected readonly state = toSignal<DetailState, DetailState>(
    this.route.paramMap.pipe(
      switchMap((params) => {
        const sku = params.get('sku');
        if (sku === null || sku.length === 0) {
          return of<DetailState>({ kind: 'notFound' });
        }
        return this.api.getProductDetail(sku).pipe(
          map((product): DetailState =>
            product === null ? { kind: 'notFound' } : { kind: 'ready', product },
          ),
          // Fail closed on compliance-bearing content: schema failure is the
          // generic error state, never partial/free-text substitutes.
          catchError(() => of<DetailState>({ kind: 'error' })),
        );
      }),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly product = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.product : null;
  });

  protected readonly priceText = computed(() => {
    const product = this.product();
    return product === null
      ? ''
      : formatMinorUnits(product.price.amountMinor, product.price.currency, this.context.locale());
  });

  protected cutKey(product: ProductDetailData): MessageKey {
    return CUT_NAME_KEYS[product.cut];
  }

  protected taxNoteKey(product: ProductDetailData): MessageKey {
    return product.price.taxDisplay === 'inclusive'
      ? 'price.taxNote.inclusive'
      : 'price.taxNote.exclusive';
  }

  protected reheatKey(format: ReheatFormat): MessageKey {
    return REHEAT_KEYS[format];
  }

  /** DE per-kg unit price (CC-PRC-002): renders iff the server sent it. */
  protected unitPerKgText(product: ProductDetailData): string {
    const unitPerKgMinor = product.price.unitPerKgMinor;
    return unitPerKgMinor === undefined
      ? ''
      : formatMinorUnits(unitPerKgMinor, product.price.currency, this.context.locale());
  }
}
