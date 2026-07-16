/**
 * Menu page (issue 066; CC-CAT-003, CC-CAT-006, CC-MKT-007; DESIGN.md §10).
 *
 * Renders the server-gated, market-priced catalog listing as product cards.
 * Filters (cut + single-toggle vegetarian, CC-CAT-006) travel WITH the query
 * through the CatalogApi seam and execute server-side (issue 031) — this
 * page never filters ungated data client-side (CC-MKT-003; issue 066 AC-03),
 * and holds no market conditionals (AC-06): a market change simply re-queries
 * the seam and displays whatever gated listing comes back. In the IN market
 * the response itself contains no non-veg SKU in any state (AC-05 — server
 * exclusion, issue 025).
 *
 * The `cut` QUERY PARAM seeds the cut filter, so the Meet our Cuts diagram
 * (issue 075, CC-CNT-003) can link here with a region selected. It is
 * untrusted client input like any other: an unrecognized value is rejected
 * (no filter), never passed through — and it seeds the SERVER-executed query
 * exactly like the select control does, so it opens no client-side filtering
 * path around the gated catalog (CC-MKT-003).
 *
 * Failure behavior (fail closed): a response that fails schema validation or
 * a seam error renders the generic error state — never partial or guessed
 * catalog data (SECURITY.md, Input validation rule 1; Logging rules 2/7).
 */

import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { CatalogApi } from '../../catalog/catalog.api';
import { CatalogListing, CatalogQuery, Cut, CUTS } from '../../catalog/catalog.types';
import { CUT_NAME_KEYS, ProductCard } from '../../components/product-card/product-card';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';

type ListingState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'ready'; readonly listing: CatalogListing };

@Component({
  selector: 'app-menu',
  imports: [ProductCard],
  templateUrl: './menu.html',
  styleUrl: './menu.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Menu {
  private readonly api = inject(CatalogApi);
  private readonly context = inject(TransactingContext);
  private readonly route = inject(ActivatedRoute);
  protected readonly i18n = inject(I18nService);

  /** Server-executed filter parameters (issue 066 AC-03). */
  private readonly query = signal<CatalogQuery>({ cut: null, vegOnly: false });
  protected readonly vegOnly = computed(() => this.query().vegOnly);
  protected readonly activeCut = computed(() => this.query().cut);

  /** ?cut= from the Cuts diagram (issue 075). Validated against the declared
   * vocabulary; anything else is ignored (no filter), never forwarded. */
  private readonly cutParam = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => {
        const value = params.get('cut');
        return value !== null && (CUTS as readonly string[]).includes(value)
          ? (value as Cut)
          : null;
      }),
    ),
    { initialValue: null },
  );

  constructor() {
    // Seed the filter from the URL, then let the controls take over.
    effect(() => {
      const cut = this.cutParam();
      this.query.update((q) => (q.cut === cut ? q : { ...q, cut }));
    });
  }

  /** Re-query when the filter or the transacting market changes. */
  private readonly request = computed(() => ({
    query: this.query(),
    market: this.context.market(),
  }));

  protected readonly state = toSignal<ListingState, ListingState>(
    toObservable(this.request).pipe(
      switchMap(({ query }) =>
        this.api.getListing(query).pipe(
          map((listing): ListingState => ({ kind: 'ready', listing })),
          // Fail closed: no fallback ungated content, generic message only.
          catchError(() => of<ListingState>({ kind: 'error' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  /** Narrowed view of the ready listing for the template. */
  protected readonly listing = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.listing : null;
  });

  protected cutNameKey(cut: Cut): MessageKey {
    return CUT_NAME_KEYS[cut];
  }

  protected onCutChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    const cut = (CUTS as readonly string[]).includes(value) ? (value as Cut) : null;
    this.query.update((q) => ({ ...q, cut }));
  }

  /** Single-toggle vegetarian filter (CC-CAT-006). */
  protected onVegToggle(event: Event): void {
    this.query.update((q) => ({ ...q, vegOnly: (event.target as HTMLInputElement).checked }));
  }
}
