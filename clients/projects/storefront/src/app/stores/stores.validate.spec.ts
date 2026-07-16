/**
 * Store-locator validation seam tests (issue 078; SECURITY.md, Input
 * validation rule 1). Requirement tags per REQUIREMENTS.md §17: CC-SEC-001,
 * CC-WHS-003.
 */

import { StoreLocationValidationError, parseStoreLocationList } from './stores.validate';

function validLocation(): Record<string, unknown> {
  return {
    id: 'us-hillside-market-austin',
    retailer: 'Hillside Market',
    addressLines: ['1200 South Congress Avenue'],
    locality: 'Austin, TX 78704',
  };
}

describe('parseStoreLocationList (typed public projection)', () => {
  it('accepts a valid market-scoped list', () => {
    const list = parseStoreLocationList({ market: 'US', locations: [validLocation()] });
    expect(list.market).toBe('US');
    expect(list.locations[0]?.retailer).toBe('Hillside Market');
  });

  it('accepts an empty list', () => {
    expect(parseStoreLocationList({ market: 'JP', locations: [] }).locations).toEqual([]);
  });

  it('DROPS any wholesale field an over-sharing response carries (CC-WHS-003 negative)', () => {
    const location = {
      ...validLocation(),
      wholesalePriceListId: 'pl-77',
      paymentTerms: 'net-60',
      casePriceMinor: 12000,
    };
    const parsed = parseStoreLocationList({ market: 'US', locations: [location] });

    // The projection constructs only declared public fields; the rest cannot
    // reach the page because it is never carried across the boundary.
    expect(Object.keys(parsed.locations[0]!).sort()).toEqual([
      'addressLines',
      'id',
      'locality',
      'retailer',
    ]);
    expect(JSON.stringify(parsed)).not.toContain('net-60');
    expect(JSON.stringify(parsed)).not.toContain('12000');
  });

  it('rejects an unknown market', () => {
    expect(() => parseStoreLocationList({ market: 'XX', locations: [] })).toThrow(
      StoreLocationValidationError,
    );
  });

  it('rejects a location with markup in a field (defense in depth)', () => {
    const location = validLocation();
    location['retailer'] = 'Hillside <script>alert(1)</script>';
    expect(() => parseStoreLocationList({ market: 'US', locations: [location] })).toThrow(
      StoreLocationValidationError,
    );
  });

  it('rejects a location with no address lines', () => {
    const location = validLocation();
    location['addressLines'] = [];
    expect(() => parseStoreLocationList({ market: 'US', locations: [location] })).toThrow(
      StoreLocationValidationError,
    );
  });

  it('rejects duplicate location ids', () => {
    expect(() =>
      parseStoreLocationList({ market: 'US', locations: [validLocation(), validLocation()] }),
    ).toThrow(StoreLocationValidationError);
  });

  it('rejects an over-long list (client-side page-size defense in depth)', () => {
    const locations = Array.from({ length: 501 }, (_, i) => ({ ...validLocation(), id: `s-${i}` }));
    expect(() => parseStoreLocationList({ market: 'US', locations })).toThrow(
      StoreLocationValidationError,
    );
  });

  it('never echoes payload content in the error message (SECURITY.md, Logging rule 5)', () => {
    const location = validLocation();
    location['locality'] = '<img src=x onerror=alert(1)>';
    try {
      parseStoreLocationList({ market: 'US', locations: [location] });
      expect.unreachable('should have thrown');
    } catch (error) {
      expect((error as Error).message).not.toContain('onerror');
    }
  });
});
