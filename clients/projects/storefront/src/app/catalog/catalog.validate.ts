/**
 * Runtime response validation for the catalog seam (issues 066/067).
 *
 * SECURITY.md, Input validation rule 1: every input crossing a trust
 * boundary is attacker-controlled; the client renders prices and inventory
 * values ONLY from typed, validated responses. These parsers are the client
 * HTTP-boundary schema check: they take `unknown`, verify every field, and
 * THROW on any violation — invalid input is rejected, never sanitized into
 * acceptance. Callers fail closed to an error state (issue 066/067 Failure
 * Behavior); no partial unvalidated data ever renders.
 *
 * Money is checked as a non-negative SAFE integer of minor units
 * (CC-PRC-003): a float, NaN, negative, or unsafe-magnitude amount is a
 * malformed response, not something to round. The client still performs no
 * monetary arithmetic — this is shape validation only (ARCHITECTURE.md,
 * Dependency rule 2).
 */

import { MARKETS, Market } from '../core/transacting-context';
import {
  CLASSIFICATIONS,
  CUTS,
  CatalogListing,
  Classification,
  Cut,
  NutritionRow,
  PriceDisplay,
  ProductDetail,
  ProductSummary,
  REHEAT_FORMATS,
  ReheatInstruction,
  STOCK_STATES,
  StockState,
  VEG_MARKINGS,
  VegMarking,
} from './catalog.types';

/** Raised when a response fails the typed schema. Message is generic —
 * it names the field/rule, never echoes raw payload content
 * (SECURITY.md, Logging rules 1 and 5). */
export class ResponseValidationError extends Error {
  constructor(rule: string) {
    super(`Response failed schema validation: ${rule}`);
    this.name = 'ResponseValidationError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function requireString(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new ResponseValidationError(`${field} must be a non-empty string`);
  }
  return value;
}

function requireEnum<T extends string>(
  value: unknown,
  allowed: readonly T[],
  field: string,
): T {
  if (typeof value !== 'string' || !(allowed as readonly string[]).includes(value)) {
    throw new ResponseValidationError(`${field} must be one of the declared values`);
  }
  return value as T;
}

/** Integer minor units (CC-PRC-003): safe integer, never a binary float. */
function requireMinorUnits(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) {
    throw new ResponseValidationError(`${field} must be a non-negative integer of minor units`);
  }
  return value;
}

function requireStringArray(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value)) {
    throw new ResponseValidationError(`${field} must be an array`);
  }
  return value.map((item, i) => requireString(item, `${field}[${i}]`));
}

function parsePriceDisplay(value: unknown, field: string): PriceDisplay {
  if (!isRecord(value)) {
    throw new ResponseValidationError(`${field} must be an object`);
  }
  const currency = requireString(value['currency'], `${field}.currency`);
  if (!/^[A-Z]{3}$/.test(currency)) {
    throw new ResponseValidationError(`${field}.currency must be an ISO 4217 code`);
  }
  const price: PriceDisplay = {
    currency,
    amountMinor: requireMinorUnits(value['amountMinor'], `${field}.amountMinor`),
    taxDisplay: requireEnum(value['taxDisplay'], ['inclusive', 'exclusive'] as const, `${field}.taxDisplay`),
    ...(value['unitPerKgMinor'] !== undefined
      ? { unitPerKgMinor: requireMinorUnits(value['unitPerKgMinor'], `${field}.unitPerKgMinor`) }
      : {}),
  };
  return price;
}

function parseSummaryFields(value: unknown, field: string): ProductSummary {
  if (!isRecord(value)) {
    throw new ResponseValidationError(`${field} must be an object`);
  }
  const classification: Classification = requireEnum(
    value['classification'],
    CLASSIFICATIONS,
    `${field}.classification`,
  );
  const vegMarking: VegMarking = requireEnum(value['vegMarking'], VEG_MARKINGS, `${field}.vegMarking`);
  // Cross-field policy check: a non-veg SKU can never carry a veg mark, and
  // there is no non-veg mark at all in this client (CC-CNT-006) — a veg SKU
  // arriving unmarked is a malformed policy payload, rejected, not guessed.
  if (classification === 'nonVeg' && vegMarking !== 'none') {
    throw new ResponseValidationError(`${field}: non-veg SKU must not carry a veg marking`);
  }
  if (classification === 'veg' && vegMarking === 'none') {
    throw new ResponseValidationError(`${field}: veg SKU must carry a veg marking`);
  }
  const stockState: StockState = requireEnum(value['stockState'], STOCK_STATES, `${field}.stockState`);
  const cut: Cut = requireEnum(value['cut'], CUTS, `${field}.cut`);
  return {
    sku: requireString(value['sku'], `${field}.sku`),
    name: requireString(value['name'], `${field}.name`),
    cut,
    netWeightDisplay: requireString(value['netWeightDisplay'], `${field}.netWeightDisplay`),
    classification,
    vegMarking,
    stockState,
    price: parsePriceDisplay(value['price'], `${field}.price`),
  };
}

/** Validate a catalog listing response. Throws ResponseValidationError. */
export function parseCatalogListing(input: unknown): CatalogListing {
  if (!isRecord(input)) {
    throw new ResponseValidationError('listing must be an object');
  }
  const market: Market = requireEnum(input['market'], MARKETS, 'listing.market');
  const cutsRaw = input['cuts'];
  if (!Array.isArray(cutsRaw)) {
    throw new ResponseValidationError('listing.cuts must be an array');
  }
  const cuts = cutsRaw.map((c, i) => requireEnum(c, CUTS, `listing.cuts[${i}]`));
  const productsRaw = input['products'];
  if (!Array.isArray(productsRaw)) {
    throw new ResponseValidationError('listing.products must be an array');
  }
  const products = productsRaw.map((p, i) => parseSummaryFields(p, `listing.products[${i}]`));
  return { market, cuts, products };
}

function parseReheat(value: unknown, field: string): ReheatInstruction {
  if (!isRecord(value)) {
    throw new ResponseValidationError(`${field} must be an object`);
  }
  return {
    format: requireEnum(value['format'], REHEAT_FORMATS, `${field}.format`),
    text: requireString(value['text'], `${field}.text`),
  };
}

function parseNutritionRow(value: unknown, field: string): NutritionRow {
  if (!isRecord(value)) {
    throw new ResponseValidationError(`${field} must be an object`);
  }
  return {
    label: requireString(value['label'], `${field}.label`),
    value: requireString(value['value'], `${field}.value`),
  };
}

/** Validate a product-detail response. Throws ResponseValidationError. */
export function parseProductDetail(input: unknown): ProductDetail {
  const summary = parseSummaryFields(input, 'product');
  const record = input as Record<string, unknown>;
  const serves = record['serves'];
  if (typeof serves !== 'number' || !Number.isSafeInteger(serves) || serves < 1) {
    throw new ResponseValidationError('product.serves must be a positive integer');
  }
  const allergensRaw = record['allergens'];
  if (!Array.isArray(allergensRaw)) {
    // Typed list is mandatory (CC-CAT-004): its absence is a malformed
    // response, never "no allergens". An empty array means none declared.
    throw new ResponseValidationError('product.allergens must be a typed list');
  }
  const nutritionRaw = record['nutrition'];
  if (!Array.isArray(nutritionRaw)) {
    throw new ResponseValidationError('product.nutrition must be a typed list');
  }
  const reheatRaw = record['reheat'];
  if (!Array.isArray(reheatRaw)) {
    throw new ResponseValidationError('product.reheat must be a typed list');
  }
  return {
    ...summary,
    serves,
    ingredients: requireStringArray(record['ingredients'], 'product.ingredients'),
    allergens: allergensRaw.map((a, i) => requireString(a, `product.allergens[${i}]`)),
    nutrition: nutritionRaw.map((n, i) => parseNutritionRow(n, `product.nutrition[${i}]`)),
    storage: requireString(record['storage'], 'product.storage'),
    reheat: reheatRaw.map((r, i) => parseReheat(r, `product.reheat[${i}]`)),
  };
}
