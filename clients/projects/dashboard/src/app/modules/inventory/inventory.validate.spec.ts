/**
 * Inventory response validation tests (issue 084).
 * Requirement tags: CC-SEC-001, CC-CAT-002/003, CC-DSH-006
 * (REQUIREMENTS.md §17).
 *
 * SECURITY.md, Input validation rule 1: a malformed payload is REJECTED, not
 * sanitized into acceptance.
 */

import { ResponseValidationError } from '../../core/validation';
import { parseInventoryView } from './inventory.validate';

const VALID_STORE = {
  storeId: 'CS-US-TX1',
  storeName: 'Austin cold store',
  markets: ['US'],
};

const VALID_ROW = {
  storeId: 'CS-US-TX1',
  storeName: 'Austin cold store',
  market: 'US',
  sku: 'BRISKET-WHOLE-5KG',
  productName: 'Whole packer brisket',
  onHandUnits: 412,
  availability: 'in-stock',
  serviceLevelBasisPoints: 9_860,
};

const VALID_VIEW = { stores: [VALID_STORE], rows: [VALID_ROW] };

describe('parseInventoryView (CC-SEC-001)', () => {
  it('accepts a well-formed payload and returns typed rows', () => {
    const view = parseInventoryView(VALID_VIEW);
    expect(view.rows.length).toBe(1);
    expect(view.rows[0].sku).toBe('BRISKET-WHOLE-5KG');
    expect(view.rows[0].serviceLevelBasisPoints).toBe(9_860);
    expect(view.stores[0].markets).toEqual(['US']);
  });

  it('rejects a non-object payload or missing collections', () => {
    expect(() => parseInventoryView(null)).toThrow(ResponseValidationError);
    expect(() => parseInventoryView({ stores: [VALID_STORE] })).toThrow(ResponseValidationError);
    expect(() => parseInventoryView({ rows: [VALID_ROW] })).toThrow(ResponseValidationError);
  });

  it('rejects an unknown availability state rather than mapping it onto a near one', () => {
    // The three CC-CAT-003 states are the whole vocabulary; a fourth is a bug.
    expect(() =>
      parseInventoryView({ ...VALID_VIEW, rows: [{ ...VALID_ROW, availability: 'maybe' }] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects an unknown market', () => {
    expect(() =>
      parseInventoryView({ ...VALID_VIEW, rows: [{ ...VALID_ROW, market: 'FR' }] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects a FLOAT or out-of-range service level rather than clamping it (CC-DSH-006)', () => {
    for (const serviceLevelBasisPoints of [98.6, 10_001, -1, '9860', Number.NaN]) {
      expect(() =>
        parseInventoryView({ ...VALID_VIEW, rows: [{ ...VALID_ROW, serviceLevelBasisPoints }] }),
      ).toThrow(ResponseValidationError);
    }
  });

  it('accepts the service-level range ends', () => {
    for (const serviceLevelBasisPoints of [0, 10_000]) {
      const view = parseInventoryView({
        ...VALID_VIEW,
        rows: [{ ...VALID_ROW, serviceLevelBasisPoints }],
      });
      expect(view.rows[0].serviceLevelBasisPoints).toBe(serviceLevelBasisPoints);
    }
  });

  it('rejects a non-integer or negative on-hand count', () => {
    for (const onHandUnits of [4.5, -1, '412']) {
      expect(() =>
        parseInventoryView({ ...VALID_VIEW, rows: [{ ...VALID_ROW, onHandUnits }] }),
      ).toThrow(ResponseValidationError);
    }
  });

  it('rejects a row attributed to a store the response never described', () => {
    expect(() =>
      parseInventoryView({ ...VALID_VIEW, rows: [{ ...VALID_ROW, storeId: 'CS-XX-NOPE' }] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects a store serving no markets', () => {
    expect(() =>
      parseInventoryView({ stores: [{ ...VALID_STORE, markets: [] }], rows: [] }),
    ).toThrow(ResponseValidationError);
  });
});
