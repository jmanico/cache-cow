/**
 * Inventory-by-cold-store contracts (issue 084; CC-DSH-003, CC-CAT-002).
 *
 * CLIENT-SIDE TypeScript contracts for the Back Office inventory read
 * endpoints, which expose the Catalog & Inventory context's model through
 * that context's module boundary (issue 084 Implementation Notes). The
 * server endpoints are being built concurrently — these shapes MUST be
 * reconciled with the published server schemas before the HTTP
 * implementation lands (ARCHITECTURE.md, Dependency rule 7: schemas are the
 * single source of truth at every trust boundary).
 *
 * READ-ONLY by construction (issue 084 AC-07): there is no mutation method
 * on the seam. CC-DSH-003 names the module but defines no inventory WRITE
 * operations (adjustments, stock corrections); per issue 084's open question
 * that would have to be ratified in REQUIREMENTS.md first (§17 — unreferenced
 * code paths are scope creep), so no adjustment contract is drafted here.
 *
 * Availability is SERVER-derived (issue 084 AC-07; ARCHITECTURE.md,
 * Dependency rule 1): the three CC-CAT-003 states arrive computed for the
 * store/market pair and the client renders exactly what it is given. It
 * never derives a state from `onHandUnits`, and never applies market
 * conditionals of its own (CC-MKT-006).
 */

/** Launch markets (CC-MKT-001). */
export const INVENTORY_MARKETS = ['US', 'ES', 'MX', 'DE', 'JP', 'IN'] as const;

export type InventoryMarket = (typeof INVENTORY_MARKETS)[number];

/**
 * The three user-facing stock states (CC-CAT-003), presented in the
 * dashboard with the DESIGN.md 5.2 cache vocabulary + plain line.
 */
export const AVAILABILITY_STATES = ['in-stock', 'restocking', 'unavailable-in-region'] as const;

export type AvailabilityState = (typeof AVAILABILITY_STATES)[number];

/** A regional cold store — a fulfillment node serving one or more markets. */
export interface ColdStore {
  readonly storeId: string;
  readonly storeName: string;
  readonly markets: readonly InventoryMarket[];
}

/**
 * Filters; executed server-side against allowlisted columns (SECURITY.md,
 * Input validation rule 4). Filter set per issue 084 AC-02 (cold store,
 * market, SKU).
 */
export interface InventoryQuery {
  readonly market?: InventoryMarket;
  readonly storeId?: string;
  /** Substring match on the SKU. */
  readonly sku?: string;
}

/** One SKU's position in one regional cold store (CC-CAT-002). */
export interface InventoryRow {
  readonly storeId: string;
  readonly storeName: string;
  readonly market: InventoryMarket;
  readonly sku: string;
  readonly productName: string;
  /** Units on hand — a count, never money. */
  readonly onHandUnits: number;
  /** Server-derived CC-CAT-003 state; never recomputed client-side. */
  readonly availability: AvailabilityState;
  /**
   * Per-region per-SKU stock service level — the regional "cache hit rate"
   * (CC-DSH-006) — as INTEGER BASIS POINTS (0..10000). Server-computed;
   * what it measures is the server's definition (core/format-percent.ts).
   *
   * FLAGGED CONFLICT (not resolved here — CLAUDE.md working rules): DESIGN.md
   * §12 places this metric in THIS module ("inventory by regional cold store
   * is the literal cache view — the per-SKU per-region hit rate (CC-DSH-006)
   * is the one dashboard moment where the brand metaphor and the operational
   * truth are the same thing"), while CC-DSH-006 assigns the metric to sales
   * analytics and issue 084 declares it "explicitly out of scope here",
   * downstream to issue 083. The column is rendered here per DESIGN.md §12
   * and the build instruction for this slice; whether it lives here, in 083,
   * or in both needs a human decision, and issues 083/084 must be reconciled.
   */
  readonly serviceLevelBasisPoints: number;
}

export interface InventoryView {
  /** The cold stores in scope — populates the store filter (AC-02). */
  readonly stores: readonly ColdStore[];
  readonly rows: readonly InventoryRow[];
}
