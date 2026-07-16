/**
 * Catalog data seam (issues 066/067).
 *
 * `CatalogApi` is the injectable HTTP-or-mock boundary: pages depend only on
 * this abstract class. Until the server catalog APIs exist (issues 025/029/
 * 030/031/034), the root provider resolves to `MockCatalogApi`; swapping in
 * an `HttpCatalogApi` later is a one-line provider change with no page edits.
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime schema parsers as `unknown` (SECURITY.md, Input validation rule 1):
 * the client renders prices and inventory only from typed, validated
 * responses, and a malformed payload throws instead of rendering (fail
 * closed; issue 066/067 Failure Behavior).
 *
 * The seam does NOT accept a market parameter: the transacting market is
 * server-side state (CC-SEC-012; SECURITY.md, Authentication rule 10) — the
 * real HTTP client will not send one. The mock injects TransactingContext
 * only to SIMULATE the server's knowledge of that state. Filters travel with
 * the query and execute server-side (issue 031); this client never filters
 * ungated data (CC-MKT-003, issue 066 AC-03).
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { TransactingContext } from '../core/transacting-context';
import { mockCatalogListingResponse, mockProductDetailResponse } from './catalog.mock-data';
import { CatalogListing, CatalogQuery, ProductDetail } from './catalog.types';
import { parseCatalogListing, parseProductDetail } from './catalog.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockCatalogApi) })
export abstract class CatalogApi {
  /** The server-gated, server-filtered listing for the transacting market. */
  abstract getListing(query: CatalogQuery): Observable<CatalogListing>;

  /**
   * One product's structured detail, or null when the SKU is absent from the
   * transacting market's gated catalog (the real server answers HTTP 404 —
   * CC-MKT-004; callers render the 404 page).
   */
  abstract getProductDetail(sku: string): Observable<ProductDetail | null>;
}

@Injectable({ providedIn: 'root' })
export class MockCatalogApi extends CatalogApi {
  private readonly context = inject(TransactingContext);

  override getListing(query: CatalogQuery): Observable<CatalogListing> {
    const response: unknown = mockCatalogListingResponse(this.context.market(), query);
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseCatalogListing(response));
  }

  override getProductDetail(sku: string): Observable<ProductDetail | null> {
    const response = mockProductDetailResponse(this.context.market(), sku);
    return of(response === null ? null : parseProductDetail(response));
  }
}
