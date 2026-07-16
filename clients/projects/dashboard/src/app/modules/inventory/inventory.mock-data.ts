/**
 * Mock inventory fixture (issue 084) — a stand-in for the SERVER's Back
 * Office inventory read endpoints until they land (the inventory model
 * itself is issue 030; this module is only its dashboard view).
 *
 * This module SIMULATES THE SERVER: filtering happens here the way the
 * server's parameterized, allowlisted query would (SECURITY.md, Input
 * validation rule 4), and both the CC-CAT-003 availability state and the
 * CC-DSH-006 service level arrive PRE-COMPUTED, exactly as the server will
 * send them. None of it is client logic — the page consumes these payloads
 * through the InventoryApi seam as untrusted `unknown` responses.
 *
 * OPEN QUESTION (flagged, not resolved — CLAUDE.md working rules): whether
 * this internal operational view is market-gated the way consumer surfaces
 * are is not stated anywhere. CC-MKT-003 excludes non-veg SKUs from IN
 * *responses* to consumers; CC-DSH-003/CC-CAT-002 describe the ops view as
 * the ground truth behind those states and say nothing about staff scope.
 * The IN cold store below therefore carries veg SKUs only — the conservative
 * shape — but that is a FIXTURE CHOICE, not a resolved policy: what an ops
 * agent may see for the IN region is a human decision, and the server (never
 * this client) will enforce whatever is decided (ARCHITECTURE.md, Dependency
 * rule 1).
 *
 * Service levels are integer basis points (core/format-percent.ts). No money
 * appears in this module at all.
 */

import { AvailabilityState, ColdStore, InventoryMarket } from './inventory.types';

/** Regional cold stores (CC-CAT-002 fulfillment nodes). */
export const MOCK_STORES: readonly ColdStore[] = [
  { storeId: 'CS-US-TX1', storeName: 'Austin cold store', markets: ['US'] },
  { storeId: 'CS-EU-DE1', storeName: 'Hamburg cold store', markets: ['DE', 'ES'] },
  { storeId: 'CS-MX-MX1', storeName: 'Monterrey cold store', markets: ['MX'] },
  { storeId: 'CS-JP-OS1', storeName: 'Osaka cold store', markets: ['JP'] },
  { storeId: 'CS-IN-PN1', storeName: 'Pune cold store', markets: ['IN'] },
];

interface MockRow {
  readonly storeId: string;
  readonly market: InventoryMarket;
  readonly sku: string;
  readonly productName: string;
  readonly onHandUnits: number;
  readonly availability: AvailabilityState;
  readonly serviceLevelBasisPoints: number;
}

/** SERVER-computed rows: availability and service level are given, not derived. */
const MOCK_ROWS: readonly MockRow[] = [
  {
    storeId: 'CS-US-TX1',
    market: 'US',
    sku: 'BRISKET-WHOLE-5KG',
    productName: 'Whole packer brisket',
    onHandUnits: 412,
    availability: 'in-stock',
    serviceLevelBasisPoints: 9860,
  },
  {
    storeId: 'CS-US-TX1',
    market: 'US',
    sku: 'RIBS-STLOUIS-2KG',
    productName: 'St. Louis cut ribs',
    onHandUnits: 0,
    availability: 'restocking',
    serviceLevelBasisPoints: 7425,
  },
  {
    storeId: 'CS-US-TX1',
    market: 'US',
    sku: 'JACKFRUIT-PULLED-1KG',
    productName: 'Smoked pulled jackfruit',
    onHandUnits: 88,
    availability: 'in-stock',
    serviceLevelBasisPoints: 9100,
  },
  {
    storeId: 'CS-EU-DE1',
    market: 'DE',
    sku: 'BRISKET-WHOLE-5KG',
    productName: 'Whole packer brisket',
    onHandUnits: 96,
    availability: 'in-stock',
    serviceLevelBasisPoints: 9535,
  },
  {
    storeId: 'CS-EU-DE1',
    market: 'ES',
    sku: 'RIBS-STLOUIS-2KG',
    productName: 'St. Louis cut ribs',
    onHandUnits: 24,
    availability: 'in-stock',
    serviceLevelBasisPoints: 8890,
  },
  {
    storeId: 'CS-MX-MX1',
    market: 'MX',
    sku: 'PANEER-SMOKED-500G',
    productName: 'Smoked paneer',
    onHandUnits: 0,
    availability: 'unavailable-in-region',
    serviceLevelBasisPoints: 0,
  },
  {
    storeId: 'CS-JP-OS1',
    market: 'JP',
    sku: 'BRISKET-WHOLE-5KG',
    productName: 'Whole packer brisket',
    onHandUnits: 57,
    availability: 'in-stock',
    serviceLevelBasisPoints: 10_000,
  },
  // IN: vegetarian SKUs only in this fixture — see the file header's flagged
  // open question. The server, not this client, decides the staff-scope rule.
  {
    storeId: 'CS-IN-PN1',
    market: 'IN',
    sku: 'PANEER-SMOKED-500G',
    productName: 'Smoked paneer',
    onHandUnits: 264,
    availability: 'in-stock',
    serviceLevelBasisPoints: 9725,
  },
  {
    storeId: 'CS-IN-PN1',
    market: 'IN',
    sku: 'JACKFRUIT-PULLED-1KG',
    productName: 'Smoked pulled jackfruit',
    onHandUnits: 12,
    availability: 'restocking',
    serviceLevelBasisPoints: 6640,
  },
];

/** SIMULATES the server's allowlisted filter query (issue 084 AC-02). */
export function mockInventoryResponse(query: {
  readonly market?: string;
  readonly storeId?: string;
  readonly sku?: string;
}): unknown {
  const storeName = (storeId: string): string =>
    MOCK_STORES.find((s) => s.storeId === storeId)?.storeName ?? storeId;

  const rows = MOCK_ROWS.filter(
    (r) =>
      (query.market === undefined || r.market === query.market) &&
      (query.storeId === undefined || r.storeId === query.storeId) &&
      (query.sku === undefined || r.sku.includes(query.sku.toUpperCase())),
  ).map((r) => ({
    storeId: r.storeId,
    storeName: storeName(r.storeId),
    market: r.market,
    sku: r.sku,
    productName: r.productName,
    onHandUnits: r.onHandUnits,
    availability: r.availability,
    serviceLevelBasisPoints: r.serviceLevelBasisPoints,
  }));

  // The store list is the filter's domain and is NOT narrowed by the row
  // filter — filtering to one store must not empty the store picker.
  return { stores: MOCK_STORES.map((s) => ({ ...s, markets: [...s.markets] })), rows };
}
