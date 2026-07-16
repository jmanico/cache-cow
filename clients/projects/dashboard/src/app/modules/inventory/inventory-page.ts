/**
 * Inventory-by-cold-store module page (issue 084; CC-DSH-003, CC-CAT-002,
 * CC-DSH-006; DESIGN.md §12).
 *
 * READ-ONLY (issue 084 AC-07): filters + a compact table (40px rows, sticky
 * header, Plex Mono right-aligned numerals, units in column headers). There
 * are no actions, so this page needs no ActionVisibility gating — module
 * access itself is the issue-079 guard plus server-side RBAC.
 *
 * The client renders exactly what the seam returns: availability states
 * (CC-CAT-003) and service levels (CC-DSH-006) arrive server-computed and
 * are never derived here (ARCHITECTURE.md, Dependency rule 1). Every badge
 * is paired with a plain-language line in the template — status is never
 * conveyed by color alone (DESIGN.md §§5.2, 13).
 *
 * Errors render generic copy only — no raw bodies (SECURITY.md, Logging
 * rules 1 and 7).
 *
 * BADGE VOCABULARY COMES FROM THE GENERATED TOKENS (ARCHITECTURE.md
 * Dependency rule 8; tokens/dist/tokens.json `status.*.badge`), identically
 * to the storefront's stock badge: the §5.2 words are brand vocabulary, not
 * locale copy, so no client hardcodes them. The plain-language lines stay
 * locale strings.
 */

import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { formatBasisPoints } from '../../core/format-percent';
import { RowNav } from '../../core/row-nav';
import { DashboardI18n, DashboardMessageKey } from '../../i18n/i18n.service';
import { InventoryApi } from './inventory.api';
import tokens from '../../../../../../tokens/dist/tokens.json';
import {
  AvailabilityState,
  ColdStore,
  INVENTORY_MARKETS,
  InventoryMarket,
  InventoryQuery,
  InventoryRow,
} from './inventory.types';

/**
 * CC-CAT-003 state -> DESIGN.md 5.2 presentation. The cache vocabulary is
 * the badge; the plain line always renders beside it (DESIGN.md §13 — status
 * is never color-only), and the accent is one family per meaning
 * (DESIGN.md §12).
 */
const AVAILABILITY_PRESENTATION: Readonly<
  Record<
    AvailabilityState,
    { readonly badge: string; readonly plainKey: DashboardMessageKey; readonly accent: string }
  >
> = {
  'in-stock': {
    badge: tokens.status.cacheHit.badge,
    plainKey: 'inventory.plain.inStock',
    accent: 'cc-status-good',
  },
  restocking: {
    badge: tokens.status.warming.badge,
    plainKey: 'inventory.plain.restocking',
    accent: 'cc-status-alert',
  },
  'unavailable-in-region': {
    badge: tokens.status.cacheMiss.badge,
    plainKey: 'inventory.plain.unavailableInRegion',
    accent: 'cc-status-neutral',
  },
};

@Component({
  selector: 'app-inventory-page',
  imports: [RowNav],
  templateUrl: './inventory-page.html',
  styleUrl: './inventory-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryPage {
  protected readonly i18n = inject(DashboardI18n);
  private readonly api = inject(InventoryApi);

  protected readonly markets = INVENTORY_MARKETS;

  /** Filter state as plain strings; narrowed to the typed query on change. */
  protected readonly market = signal('');
  protected readonly storeId = signal('');
  protected readonly sku = signal('');

  protected readonly stores = signal<readonly ColdStore[]>([]);
  protected readonly rows = signal<readonly InventoryRow[]>([]);
  protected readonly loadError = signal(false);

  /** Store options honor the market filter without narrowing the seam's list. */
  protected readonly storeOptions = computed<readonly ColdStore[]>(() => {
    const market = this.market();
    const stores = this.stores();
    return (INVENTORY_MARKETS as readonly string[]).includes(market)
      ? stores.filter((s) => (s.markets as readonly string[]).includes(market))
      : stores;
  });

  constructor() {
    this.load();
  }

  protected setFrom(target: EventTarget | null, field: 'market' | 'storeId' | 'sku'): void {
    const value = (target as HTMLInputElement | HTMLSelectElement).value;
    this[field].set(value);
    if (field === 'market') {
      // A store outside the chosen market would filter to nothing; clear it.
      this.storeId.set('');
    }
    this.load();
  }

  private load(): void {
    this.loadError.set(false);
    this.api.getInventory(this.buildQuery()).subscribe({
      next: (view) => {
        this.stores.set(view.stores);
        this.rows.set(view.rows);
      },
      error: () => {
        // Fail closed: nothing renders from a rejected payload — and never a
        // stale or fabricated inventory value (issue 084 Failure Behavior).
        this.stores.set([]);
        this.rows.set([]);
        this.loadError.set(true);
      },
    });
  }

  private buildQuery(): InventoryQuery {
    const market = this.market();
    const storeId = this.storeId();
    const sku = this.sku().trim();
    return {
      ...((INVENTORY_MARKETS as readonly string[]).includes(market)
        ? { market: market as InventoryMarket }
        : {}),
      ...(storeId.length > 0 ? { storeId } : {}),
      ...(sku.length > 0 ? { sku } : {}),
    };
  }

  /** Brand vocabulary from the generated tokens, not locale copy (Dependency rule 8). */
  protected badge(state: AvailabilityState): string {
    return AVAILABILITY_PRESENTATION[state].badge;
  }

  /** The plain-language line that ALWAYS accompanies the badge (§13). */
  protected plain(state: AvailabilityState): string {
    return this.i18n.t(AVAILABILITY_PRESENTATION[state].plainKey);
  }

  protected accent(state: AvailabilityState): string {
    return AVAILABILITY_PRESENTATION[state].accent;
  }

  /** Server-computed service level (CC-DSH-006); no arithmetic here. */
  protected hitRate(basisPoints: number): string {
    return formatBasisPoints(basisPoints);
  }
}
