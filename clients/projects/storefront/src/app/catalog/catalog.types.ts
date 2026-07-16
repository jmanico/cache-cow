/**
 * Typed catalog response model (issues 066/067; CC-CAT-001/003, CC-PRC-002/003).
 *
 * These interfaces mirror the server's typed catalog responses (the server
 * APIs land with issues 025/029/030/031/034 — this is the client half of that
 * contract, consumed only through the CatalogApi seam in catalog.api.ts).
 *
 * The client renders prices and inventory ONLY from these typed, validated
 * shapes (SECURITY.md, Input validation rule 1) and performs no gating and no
 * price computation of its own (ARCHITECTURE.md, Dependency rules 1–2):
 *
 * - `stockState` is the server-derived three-state availability of
 *   CC-CAT-003; the client never infers availability.
 * - `vegMarking` is a server policy decision (Market & Gating Policy context):
 *   which veg mark a presentation carries — the FSSAI regulation mark in the
 *   IN market (CC-CNT-006), the leaf-dot badge elsewhere (DESIGN.md §3.3) —
 *   is market policy data, NOT a client-side market conditional. No non-veg
 *   mark exists anywhere in this client (CC-CNT-006's IN prohibition is
 *   satisfied structurally, and the server never sends one).
 * - money is integer minor units end to end (CC-PRC-003); display formatting
 *   is locale-aware Intl only (CC-PRC-004, format-price.ts).
 */

import { Market } from '../core/transacting-context';

/** Server-derived three-state availability (CC-CAT-003). */
export const STOCK_STATES = ['cacheHit', 'warming', 'cacheMiss'] as const;
export type StockState = (typeof STOCK_STATES)[number];

/**
 * Server-selected veg marking (DESIGN.md §3.3; CC-CNT-006). 'none' is the
 * only value a non-veg SKU may carry; there is deliberately no non-veg mark.
 */
export const VEG_MARKINGS = ['fssaiVeg', 'leafDot', 'none'] as const;
export type VegMarking = (typeof VEG_MARKINGS)[number];

export const CLASSIFICATIONS = ['veg', 'nonVeg'] as const;
export type Classification = (typeof CLASSIFICATIONS)[number];

/**
 * Cut/category vocabulary (CC-CAT-001). Placeholder launch set; the real
 * taxonomy is owned by the SKU domain model (issue 029).
 */
export const CUTS = ['brisket', 'ribs', 'shoulder', 'sausage', 'paneer', 'jackfruit', 'mushroom'] as const;
export type Cut = (typeof CUTS)[number];

export const REHEAT_FORMATS = ['oven', 'sousVide', 'steam'] as const;
export type ReheatFormat = (typeof REHEAT_FORMATS)[number];

/**
 * Price display data (CC-PRC-002/003). `amountMinor` is integer minor units;
 * `taxDisplay` is the market convention the server selected; `unitPerKgMinor`
 * is present when the market requires unit pricing (DE Preisangabenverordnung)
 * — the client renders it if and only if the server sent it.
 */
export interface PriceDisplay {
  readonly currency: string;
  readonly amountMinor: number;
  readonly taxDisplay: 'inclusive' | 'exclusive';
  readonly unitPerKgMinor?: number;
}

/** One product card's worth of server-gated catalog data (DESIGN.md §7). */
export interface ProductSummary {
  readonly sku: string;
  /** Localized server-side per CC-CAT-001; never client-translated. */
  readonly name: string;
  readonly cut: Cut;
  /** Formatted per market convention server-side (issue 034 seam; CC-I18N-003). */
  readonly netWeightDisplay: string;
  readonly classification: Classification;
  readonly vegMarking: VegMarking;
  readonly stockState: StockState;
  readonly price: PriceDisplay;
}

export interface ReheatInstruction {
  readonly format: ReheatFormat;
  readonly text: string;
}

/**
 * Structured nutrition row (CC-CAT-004: structured fields, never free text).
 * The per-market nutrition FORMAT (EU FIC vs FDA vs JP vs FSSAI tables,
 * CC-CMP-004) is owned server-side by issues 029/034 — this row shape is a
 * placeholder pending that work.
 */
export interface NutritionRow {
  readonly label: string;
  readonly value: string;
}

/** Full PDP payload (CC-CAT-001 structured food data; CC-CAT-004). */
export interface ProductDetail extends ProductSummary {
  readonly serves: number;
  readonly ingredients: readonly string[];
  /** Typed list — never free text (CC-CAT-004). Empty means none declared. */
  readonly allergens: readonly string[];
  readonly nutrition: readonly NutritionRow[];
  readonly storage: string;
  readonly reheat: readonly ReheatInstruction[];
}

/**
 * A server-gated catalog listing for the transacting market. `cuts` is the
 * filter vocabulary present in THIS market's gated catalog (so the IN cut
 * filter never even offers a non-veg cut — server data, not client logic).
 */
export interface CatalogListing {
  readonly market: Market;
  readonly cuts: readonly Cut[];
  readonly products: readonly ProductSummary[];
}

/**
 * Filter parameters the client sends WITH the query; filtering executes
 * server-side (issue 031) — never as client-side list filtering of ungated
 * data (issue 066 AC-03).
 */
export interface CatalogQuery {
  readonly cut: Cut | null;
  readonly vegOnly: boolean;
}
