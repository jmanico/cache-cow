/**
 * Inventory data seam (issue 084; CC-DSH-003, CC-CAT-002).
 *
 * `InventoryApi` is the injectable HTTP-or-mock boundary, following the
 * storefront catalog.api.ts pattern without importing it (ARCHITECTURE.md,
 * Dependency rule 4). Until the Back Office inventory endpoints exist (built
 * concurrently server-side), the root provider resolves to
 * `MockInventoryApi`; swapping in an HTTP client later is a one-line
 * provider change with no page edits — at which point these contracts MUST
 * be reconciled with the published server schemas (Dependency rule 7).
 *
 * READ-ONLY (issue 084 AC-07): the seam exposes no mutation. Inventory write
 * operations are not in CC-DSH-003 and would need ratification first.
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime parsers as `unknown` (SECURITY.md, Input validation rule 1): a
 * malformed payload throws instead of rendering (fail closed).
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { mockInventoryResponse } from './inventory.mock-data';
import { InventoryQuery, InventoryView } from './inventory.types';
import { parseInventoryView } from './inventory.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockInventoryApi) })
export abstract class InventoryApi {
  /**
   * Per-SKU per-cold-store inventory for the requested filters. Filters
   * execute SERVER-side against allowlisted columns (SECURITY.md, Input
   * validation rule 4); this client never filters ungated data itself
   * (ARCHITECTURE.md, Dependency rule 1).
   */
  abstract getInventory(query: InventoryQuery): Observable<InventoryView>;
}

@Injectable({ providedIn: 'root' })
export class MockInventoryApi extends InventoryApi {
  override getInventory(query: InventoryQuery): Observable<InventoryView> {
    const response: unknown = mockInventoryResponse(query);
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseInventoryView(response));
  }
}
