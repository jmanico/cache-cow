/**
 * Response-validation seam tests (issues 066/067; SECURITY.md, Input
 * validation rule 1): malformed catalog payloads are REJECTED at the client
 * HTTP boundary — never sanitized into acceptance, never partially rendered.
 * Requirement tags: CC-SEC-001, CC-PRC-003, CC-CAT-003, CC-CAT-004,
 * CC-CNT-006 (REQUIREMENTS.md §17).
 */

import {
  ResponseValidationError,
  parseCatalogListing,
  parseProductDetail,
} from './catalog.validate';

function validProduct(): Record<string, unknown> {
  return {
    sku: 'paneer-smoked-block',
    name: 'Smoked Paneer Block',
    cut: 'paneer',
    netWeightDisplay: '0.7 kg',
    classification: 'veg',
    vegMarking: 'leafDot',
    stockState: 'cacheHit',
    price: { currency: 'EUR', amountMinor: 2700, taxDisplay: 'inclusive' },
  };
}

function validDetail(): Record<string, unknown> {
  return {
    ...validProduct(),
    serves: 3,
    ingredients: ['Paneer', 'Salt'],
    allergens: ['Milk'],
    nutrition: [{ label: 'Energy', value: '251 kcal' }],
    storage: 'Keep frozen.',
    reheat: [{ format: 'oven', text: 'Reheat at 120 °C.' }],
  };
}

describe('parseCatalogListing (typed response boundary)', () => {
  it('accepts a valid listing', () => {
    const listing = parseCatalogListing({
      market: 'DE',
      cuts: ['brisket', 'paneer'],
      products: [validProduct()],
    });
    expect(listing.market).toBe('DE');
    expect(listing.products[0]?.sku).toBe('paneer-smoked-block');
  });

  it('rejects an unknown market', () => {
    expect(() => parseCatalogListing({ market: 'XX', cuts: [], products: [] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a non-integer (binary float) money amount (CC-PRC-003)', () => {
    const product = validProduct();
    product['price'] = { currency: 'EUR', amountMinor: 27.5, taxDisplay: 'inclusive' };
    expect(() => parseCatalogListing({ market: 'DE', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a negative money amount', () => {
    const product = validProduct();
    product['price'] = { currency: 'EUR', amountMinor: -1, taxDisplay: 'inclusive' };
    expect(() => parseCatalogListing({ market: 'DE', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a float DE unit price', () => {
    const product = validProduct();
    product['price'] = {
      currency: 'EUR',
      amountMinor: 2700,
      taxDisplay: 'inclusive',
      unitPerKgMinor: 3857.14,
    };
    expect(() => parseCatalogListing({ market: 'DE', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a stock state outside the three CC-CAT-003 states', () => {
    const product = validProduct();
    product['stockState'] = 'backordered';
    expect(() => parseCatalogListing({ market: 'US', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a non-veg SKU carrying a veg marking (CC-CNT-006 cross-check)', () => {
    const product = validProduct();
    product['classification'] = 'nonVeg';
    product['vegMarking'] = 'fssaiVeg';
    expect(() => parseCatalogListing({ market: 'US', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a veg SKU without a veg marking (policy payload must be complete)', () => {
    const product = validProduct();
    product['vegMarking'] = 'none';
    expect(() => parseCatalogListing({ market: 'US', cuts: [], products: [product] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects non-object payloads', () => {
    for (const bad of [null, undefined, 'html error page', 42, []]) {
      expect(() => parseCatalogListing(bad)).toThrow(ResponseValidationError);
    }
  });
});

describe('parseProductDetail (structured food data, CC-CAT-004)', () => {
  it('accepts a valid detail payload', () => {
    const detail = parseProductDetail(validDetail());
    expect(detail.allergens).toEqual(['Milk']);
    expect(detail.reheat[0]?.format).toBe('oven');
  });

  it('rejects a payload with a missing allergens list (no free-text fallback)', () => {
    const detail = validDetail();
    delete detail['allergens'];
    expect(() => parseProductDetail(detail)).toThrow(ResponseValidationError);
  });

  it('rejects free-text (non-structured) nutrition', () => {
    const detail = validDetail();
    detail['nutrition'] = 'Energy 251 kcal, Fat 18 g';
    expect(() => parseProductDetail(detail)).toThrow(ResponseValidationError);
  });

  it('rejects an unknown reheat format', () => {
    const detail = validDetail();
    detail['reheat'] = [{ format: 'microwave', text: 'Zap it.' }];
    expect(() => parseProductDetail(detail)).toThrow(ResponseValidationError);
  });

  it('rejects a non-integer serving estimate', () => {
    const detail = validDetail();
    detail['serves'] = 2.5;
    expect(() => parseProductDetail(detail)).toThrow(ResponseValidationError);
  });
});
