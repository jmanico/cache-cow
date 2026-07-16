/**
 * Runtime validation for inventory responses (issue 084; SECURITY.md, Input
 * validation rule 1 — parse `unknown`, throw on any violation, never
 * sanitize into acceptance; issue 084 Zero Trust Consideration: the client
 * renders inventory values only from typed, validated responses).
 */

import {
  ResponseValidationError,
  requireArray,
  requireEnum,
  requireIntInRange,
  requireNonNegativeInt,
  requireRecord,
  requireString,
} from '../../core/validation';
import {
  AVAILABILITY_STATES,
  ColdStore,
  INVENTORY_MARKETS,
  InventoryRow,
  InventoryView,
} from './inventory.types';

function parseColdStore(value: unknown, field: string): ColdStore {
  const record = requireRecord(value, field);
  const markets = requireArray(record['markets'], `${field}.markets`).map((item, i) =>
    requireEnum(item, INVENTORY_MARKETS, `${field}.markets[${i}]`),
  );
  if (markets.length === 0) {
    throw new ResponseValidationError(`${field}.markets must not be empty`);
  }
  return {
    storeId: requireString(record['storeId'], `${field}.storeId`),
    storeName: requireString(record['storeName'], `${field}.storeName`),
    markets,
  };
}

function parseInventoryRow(value: unknown, field: string): InventoryRow {
  const record = requireRecord(value, field);
  return {
    storeId: requireString(record['storeId'], `${field}.storeId`),
    storeName: requireString(record['storeName'], `${field}.storeName`),
    market: requireEnum(record['market'], INVENTORY_MARKETS, `${field}.market`),
    sku: requireString(record['sku'], `${field}.sku`),
    productName: requireString(record['productName'], `${field}.productName`),
    onHandUnits: requireNonNegativeInt(record['onHandUnits'], `${field}.onHandUnits`),
    // Server-derived CC-CAT-003 state: an unknown state is a malformed
    // response, never something to map onto a nearby state.
    availability: requireEnum(record['availability'], AVAILABILITY_STATES, `${field}.availability`),
    // Integer basis points only: a float ratio or an out-of-range rate is a
    // malformed response, not something to clamp (CC-DSH-006).
    serviceLevelBasisPoints: requireIntInRange(
      record['serviceLevelBasisPoints'],
      0,
      10_000,
      `${field}.serviceLevelBasisPoints`,
    ),
  };
}

export function parseInventoryView(value: unknown): InventoryView {
  const record = requireRecord(value, 'inventory');
  const stores = requireArray(record['stores'], 'inventory.stores').map((item, i) =>
    parseColdStore(item, `inventory.stores[${i}]`),
  );
  const rows = requireArray(record['rows'], 'inventory.rows').map((item, i) =>
    parseInventoryRow(item, `inventory.rows[${i}]`),
  );
  // Cross-field sanity: a row attributed to a cold store the response does
  // not describe is a malformed payload — rejected, not rendered with a
  // blank filter entry (fail closed).
  const known = new Set(stores.map((s) => s.storeId));
  for (const row of rows) {
    if (!known.has(row.storeId)) {
      throw new ResponseValidationError('inventory.rows[].storeId must name a described store');
    }
  }
  return { stores, rows };
}
