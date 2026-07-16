/**
 * Store locator page (issue 078; DESIGN.md §7 "Store locator", §10, §13).
 *
 * LIST ONLY — NO MAP. DESIGN.md §7 specifies "map plus list", but no map
 * provider is named anywhere in the specs, and any hosted map (scripts,
 * tiles, geocoding) collides with the ban on third-party runtime CDNs
 * (SECURITY.md, Deployment rule 10) and the strict exact-origin CSP (HTTP
 * boundary rule 2); self-hosted tiles are a separate cost/licensing call.
 * That is an OPEN HUMAN DECISION (issue 078 Open Questions; epic open
 * question 11) and issue 078 excludes the map from its acceptance criteria
 * rather than letting an implementer pick a vendor: "AI MUST NOT select a
 * map provider or add a mapping dependency."
 *
 * So this page ships the list firmly and loads NO map script, NO tile
 * server, and NO other runtime-CDN asset (issue 078 AC-07) — a spec asserts
 * the absence. The list is also the accessible foundation any future map
 * must ENHANCE, never replace (DESIGN.md §7, §13), so building it first is
 * the right order regardless of how the provider question lands.
 *
 * The partner list is filtered SERVER-side off transacting-market state
 * (CC-SEC-012; issue 078 AC-01/AC-02): this page holds no market conditional
 * and never receives another market's partners (ARCHITECTURE.md, Dependency
 * rule 1). The response is a public-field projection — no wholesale prices,
 * terms, or partner-tenancy data can reach a consumer session (CC-WHS-003;
 * Dependency rule 3).
 *
 * Failure behavior (fail closed): a response failing schema validation or a
 * seam error renders the generic error state — never an unfiltered or
 * cross-market list (issue 078 Failure Behavior).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, switchMap } from 'rxjs';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { StoresApi } from '../../stores/stores.api';
import { StoreLocationList } from '../../stores/stores.types';

type StoresState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'ready'; readonly locations: StoreLocationList };

@Component({
  selector: 'app-stores',
  templateUrl: './stores.html',
  styleUrl: './stores.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Stores {
  private readonly api = inject(StoresApi);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  /** Re-query when the transacting market changes (AC-02). */
  protected readonly state = toSignal<StoresState, StoresState>(
    toObservable(computed(() => this.context.market())).pipe(
      switchMap(() =>
        this.api.getStoreLocations().pipe(
          map((locations): StoresState => ({ kind: 'ready', locations })),
          // Fail closed: no unfiltered or cross-market fallback list.
          catchError(() => of<StoresState>({ kind: 'error' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly locations = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.locations : null;
  });
}
