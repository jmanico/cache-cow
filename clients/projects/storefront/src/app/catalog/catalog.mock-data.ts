/**
 * Mock catalog fixture (issues 066/067) — a stand-in for the SERVER's gated
 * catalog responses until the real APIs land (server gating is issue 025,
 * inventory three-state derivation issue 030, search/filtering issue 031,
 * price formatting/tax data issue 034, SKU domain model issue 029).
 *
 * This module SIMULATES THE SERVER, market-gating included: the IN dataset
 * simply contains no non-veg SKU — mirroring the server-side exclusion of
 * CC-MKT-003 — and per-market filtering happens here the way issue 031's
 * server query would. None of this is client gating logic: the client
 * consumes these payloads through the CatalogApi seam as untrusted `unknown`
 * responses, exactly as it will consume HTTP responses (ARCHITECTURE.md,
 * Dependency rule 1).
 *
 * All copy is English placeholder; real localized product names/instructions
 * are server-side per CC-CAT-001 (open editorial question, DESIGN.md §9).
 * Nutrition rows are placeholders pending the per-market format decision
 * (CC-CMP-004, issues 029/034). Prices are integer minor units (CC-PRC-003).
 */

import { Market } from '../core/transacting-context';
import { CatalogQuery } from './catalog.types';

interface MockPrice {
  readonly currency: string;
  readonly amountMinor: number;
  readonly taxDisplay: 'inclusive' | 'exclusive';
  readonly unitPerKgMinor?: number;
}

interface MockSku {
  readonly sku: string;
  readonly name: string;
  readonly cut: string;
  readonly classification: 'veg' | 'nonVeg';
  readonly netWeightKg: number;
  readonly serves: number;
  readonly ingredients: readonly string[];
  readonly allergens: readonly string[];
  /** Per-market integer minor-unit price (server-priced, CC-PRC-001). */
  readonly priceMinor: Readonly<Partial<Record<Market, number>>>;
  readonly stockState: Readonly<Partial<Record<Market, string>>>;
}

const CURRENCY: Readonly<Record<Market, string>> = {
  US: 'USD',
  ES: 'EUR',
  MX: 'MXN',
  DE: 'EUR',
  JP: 'JPY',
  IN: 'INR',
};

/** Tax-display convention per CC-PRC-002: US exclusive, all others inclusive. */
const TAX_DISPLAY: Readonly<Record<Market, 'inclusive' | 'exclusive'>> = {
  US: 'exclusive',
  ES: 'inclusive',
  MX: 'inclusive',
  DE: 'inclusive',
  JP: 'inclusive',
  IN: 'inclusive',
};

const SKUS: readonly MockSku[] = [
  {
    sku: 'brisket-whole-packer',
    name: 'Whole Packer Brisket',
    cut: 'brisket',
    classification: 'nonVeg',
    netWeightKg: 2.8,
    serves: 10,
    ingredients: ['Beef brisket', 'Salt', 'Black pepper'],
    allergens: [],
    priceMinor: { US: 14900, ES: 13900, MX: 259900, DE: 13900, JP: 14900 },
    stockState: { US: 'cacheHit', ES: 'cacheHit', MX: 'warming', DE: 'cacheHit', JP: 'cacheHit' },
  },
  {
    sku: 'ribs-st-louis',
    name: 'St. Louis Ribs',
    cut: 'ribs',
    classification: 'nonVeg',
    netWeightKg: 1.4,
    serves: 4,
    ingredients: ['Pork ribs', 'Salt', 'Paprika', 'Brown sugar'],
    allergens: [],
    priceMinor: { US: 6900, ES: 6400, MX: 119900, DE: 6400, JP: 6900 },
    stockState: { US: 'warming', ES: 'cacheHit', MX: 'cacheHit', DE: 'warming', JP: 'cacheMiss' },
  },
  {
    sku: 'sausage-smoked-links',
    name: 'Smoked Sausage Links',
    cut: 'sausage',
    classification: 'nonVeg',
    netWeightKg: 0.9,
    serves: 4,
    ingredients: ['Pork', 'Beef', 'Salt', 'Garlic', 'Mustard seed'],
    allergens: ['Mustard'],
    priceMinor: { US: 3900, ES: 3600, MX: 69900, DE: 3600, JP: 3900 },
    stockState: { US: 'cacheHit', ES: 'cacheMiss', MX: 'cacheHit', DE: 'cacheHit', JP: 'cacheHit' },
  },
  {
    sku: 'shoulder-pork-pulled',
    name: 'Pulled Pork Shoulder',
    cut: 'shoulder',
    classification: 'nonVeg',
    netWeightKg: 1.2,
    serves: 5,
    ingredients: ['Pork shoulder', 'Salt', 'Black pepper', 'Cider vinegar'],
    allergens: [],
    priceMinor: { US: 8900, ES: 8200, MX: 154900, DE: 8200, JP: 8900 },
    stockState: { US: 'cacheHit', ES: 'cacheHit', MX: 'cacheHit', DE: 'cacheHit', JP: 'warming' },
  },
  {
    sku: 'paneer-smoked-block',
    name: 'Smoked Paneer Block',
    cut: 'paneer',
    classification: 'veg',
    netWeightKg: 0.7,
    serves: 3,
    ingredients: ['Paneer', 'Salt', 'Smoked chilli', 'Cold-pressed oil'],
    allergens: ['Milk'],
    priceMinor: { US: 2900, ES: 2700, MX: 49900, DE: 2700, JP: 2900, IN: 64900 },
    stockState: { US: 'cacheHit', ES: 'warming', MX: 'cacheHit', DE: 'cacheHit', JP: 'cacheHit', IN: 'cacheHit' },
  },
  {
    sku: 'jackfruit-pulled-smoked',
    name: 'Pulled Smoked Jackfruit',
    cut: 'jackfruit',
    classification: 'veg',
    netWeightKg: 0.8,
    serves: 4,
    ingredients: ['Young jackfruit', 'Salt', 'Smoked paprika', 'Tomato'],
    allergens: [],
    priceMinor: { US: 2450, ES: 2250, MX: 42900, DE: 2250, JP: 2450, IN: 54900 },
    stockState: { US: 'warming', ES: 'cacheHit', MX: 'warming', DE: 'cacheHit', JP: 'cacheHit', IN: 'warming' },
  },
  {
    sku: 'mushroom-king-oyster',
    name: 'Smoked King Oyster Mushrooms',
    cut: 'mushroom',
    classification: 'veg',
    netWeightKg: 0.5,
    serves: 2,
    ingredients: ['King oyster mushrooms', 'Salt', 'Black pepper', 'Cold-pressed oil'],
    allergens: [],
    priceMinor: { US: 2700, ES: 2500, MX: 46900, DE: 2500, JP: 2700, IN: 59900 },
    stockState: { US: 'cacheMiss', ES: 'cacheHit', MX: 'cacheHit', DE: 'cacheMiss', JP: 'cacheHit', IN: 'cacheMiss' },
  },
];

/** US displays imperial-primary weights (CC-I18N-003); others metric. */
function weightDisplay(kg: number, market: Market): string {
  if (market === 'US') {
    const pounds = Math.round(kg * 2.20462 * 10) / 10;
    return `${pounds} lb (${kg} kg)`;
  }
  return `${kg} kg`;
}

function price(entry: MockSku, market: Market): MockPrice | null {
  const amountMinor = entry.priceMinor[market];
  if (amountMinor === undefined) {
    return null;
  }
  const base: MockPrice = {
    currency: CURRENCY[market],
    amountMinor,
    taxDisplay: TAX_DISPLAY[market],
  };
  if (market === 'DE') {
    // DE unit price per kg (Preisangabenverordnung, CC-PRC-002) — computed
    // here because this module IS the mock server; the client never computes
    // it (integer division only, no float money).
    return { ...base, unitPerKgMinor: Math.round(amountMinor / entry.netWeightKg) };
  }
  return base;
}

/** The market's gated SKU set — the mock analogue of issue 025's exclusion. */
function gatedSkus(market: Market): readonly MockSku[] {
  return SKUS.filter((entry) => entry.priceMinor[market] !== undefined);
}

function summary(entry: MockSku, market: Market): Record<string, unknown> {
  return {
    sku: entry.sku,
    name: entry.name,
    cut: entry.cut,
    netWeightDisplay: weightDisplay(entry.netWeightKg, market),
    classification: entry.classification,
    // Veg-marking selection is SERVER market policy (DESIGN.md §3.3,
    // CC-CNT-006): FSSAI regulation mark in IN, leaf-dot elsewhere.
    vegMarking: entry.classification === 'veg' ? (market === 'IN' ? 'fssaiVeg' : 'leafDot') : 'none',
    stockState: entry.stockState[market],
    price: price(entry, market),
  };
}

/**
 * Mock server response for a gated, filtered catalog listing. Returned as
 * `unknown`: the CatalogApi seam validates it like any untrusted response.
 */
export function mockCatalogListingResponse(market: Market, query: CatalogQuery): unknown {
  const gated = gatedSkus(market);
  const filtered = gated.filter(
    (entry) =>
      (query.cut === null || entry.cut === query.cut) &&
      (!query.vegOnly || entry.classification === 'veg'),
  );
  return {
    market,
    // Filter vocabulary of THIS market's gated catalog only (no non-veg cut
    // is ever offered in IN — data, not a client conditional).
    cuts: [...new Set(gated.map((entry) => entry.cut))],
    products: filtered.map((entry) => summary(entry, market)),
  };
}

/**
 * Mock server response for one product detail, or null when the SKU does not
 * exist in the market's gated catalog (the real server answers HTTP 404 —
 * CC-MKT-004/issue 026; the client shows the 404 page).
 */
export function mockProductDetailResponse(market: Market, sku: string): unknown | null {
  const entry = gatedSkus(market).find((candidate) => candidate.sku === sku);
  if (!entry) {
    return null;
  }
  return {
    ...summary(entry, market),
    serves: entry.serves,
    ingredients: entry.ingredients,
    allergens: entry.allergens,
    // Placeholder rows pending the per-market nutrition format (CC-CMP-004).
    nutrition: [
      { label: 'Energy', value: '1,050 kJ / 251 kcal' },
      { label: 'Fat', value: '18 g' },
      { label: 'Protein', value: '21 g' },
      { label: 'Salt', value: '1.9 g' },
    ],
    storage: 'Keep frozen at -18 °C. Once thawed, refrigerate and eat within 48 hours. Do not refreeze.',
    reheat: [
      { format: 'oven', text: 'Thaw fully, then reheat at 120 °C for 40 minutes to an internal 74 °C.' },
      { format: 'sousVide', text: 'From frozen: 65 °C water bath for 90 minutes in the sealed pouch.' },
      { format: 'steam', text: 'Thaw fully, then steam covered for 15 minutes until heated through.' },
    ],
  };
}
