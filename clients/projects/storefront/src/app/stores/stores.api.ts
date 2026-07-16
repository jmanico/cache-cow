/**
 * Store-locator data seam (issue 078; DESIGN.md §7, §10).
 *
 * `StoresApi` is the injectable HTTP-or-mock boundary: the locator page
 * depends only on this abstract class. Until the consumer read endpoint for
 * retail partner locations exists, the root provider resolves to
 * `MockStoresApi`; swapping in an `HttpStoresApi` later is a one-line
 * provider change with no page edits.
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime schema parser as `unknown` (SECURITY.md, Input validation rule 1)
 * and throws on violation; the page fails closed to a generic error state
 * rather than rendering an unfiltered or partial list (issue 078 Failure
 * Behavior: never a cross-market list).
 *
 * The seam does NOT accept a market parameter: the transacting market is
 * server-side state (CC-SEC-012; SECURITY.md, Authentication rule 10) — the
 * real HTTP client will not send one. The mock injects TransactingContext
 * only to SIMULATE the server's knowledge of that state.
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { TransactingContext } from '../core/transacting-context';
import { mockStoreLocationsResponse } from './stores.mock-data';
import { StoreLocationList } from './stores.types';
import { parseStoreLocationList } from './stores.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockStoresApi) })
export abstract class StoresApi {
  /** Retail partners for the transacting market, filtered server-side. */
  abstract getStoreLocations(): Observable<StoreLocationList>;
}

@Injectable({ providedIn: 'root' })
export class MockStoresApi extends StoresApi {
  private readonly context = inject(TransactingContext);

  override getStoreLocations(): Observable<StoreLocationList> {
    const response: unknown = mockStoreLocationsResponse(this.context.market());
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseStoreLocationList(response));
  }
}
