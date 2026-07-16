/**
 * Order response validation tests (issue 082).
 * Requirement tags: CC-SEC-001, CC-PRC-003, CC-ORD-006 (REQUIREMENTS.md §17).
 *
 * SECURITY.md, Input validation rule 1: a malformed payload is REJECTED, not
 * sanitized into acceptance. These tests assert the parser throws rather than
 * returning a repaired object.
 */

import { ResponseValidationError } from '../../core/validation';
import { parseOrderDetail, parseOrderSearchResult } from './orders.validate';

const VALID_SUMMARY = {
  orderRef: 'ORD-2026-000141',
  market: 'US',
  state: 'received',
  placedAt: '2026-07-12T14:05:00Z',
  itemCount: 3,
  totalMinor: 14_900,
  currency: 'USD',
};

const VALID_DETAIL = {
  ...VALID_SUMMARY,
  history: [{ state: 'received', at: '2026-07-12T14:05:00Z', actor: 'system' }],
  allowedTransitions: ['confirmed', 'cancelled'],
  refundEligible: false,
};

describe('parseOrderSearchResult (CC-SEC-001)', () => {
  it('accepts a well-formed payload and returns typed rows', () => {
    const result = parseOrderSearchResult({ orders: [VALID_SUMMARY] });
    expect(result.orders.length).toBe(1);
    expect(result.orders[0].orderRef).toBe('ORD-2026-000141');
    expect(result.orders[0].totalMinor).toBe(14_900);
  });

  it('rejects a non-object payload', () => {
    expect(() => parseOrderSearchResult(null)).toThrow(ResponseValidationError);
    expect(() => parseOrderSearchResult('orders')).toThrow(ResponseValidationError);
  });

  it('rejects a missing or non-array orders field', () => {
    expect(() => parseOrderSearchResult({})).toThrow(ResponseValidationError);
    expect(() => parseOrderSearchResult({ orders: 'none' })).toThrow(ResponseValidationError);
  });

  it('rejects an unknown market or state rather than mapping it onto a near one', () => {
    expect(() => parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, market: 'FR' }] })).toThrow(
      ResponseValidationError,
    );
    expect(() =>
      parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, state: 'incinerated' }] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects FLOAT money — minor units are integers (CC-PRC-003)', () => {
    expect(() =>
      parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, totalMinor: 149.0 + 0.005 }] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects negative, NaN, and string money', () => {
    for (const totalMinor of [-1, Number.NaN, '14900']) {
      expect(() => parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, totalMinor }] })).toThrow(
        ResponseValidationError,
      );
    }
  });

  it('rejects a malformed currency code', () => {
    expect(() => parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, currency: 'usd' }] })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects an unparseable timestamp', () => {
    expect(() =>
      parseOrderSearchResult({ orders: [{ ...VALID_SUMMARY, placedAt: 'last Tuesday' }] }),
    ).toThrow(ResponseValidationError);
  });
});

describe('parseOrderDetail (CC-ORD-006)', () => {
  it('accepts a well-formed payload with history and server transitions', () => {
    const detail = parseOrderDetail(VALID_DETAIL);
    expect(detail.allowedTransitions).toEqual(['confirmed', 'cancelled']);
    expect(detail.history.length).toBe(1);
    expect(detail.refundEligible).toBe(false);
  });

  it('rejects an unknown state in allowedTransitions (fail closed)', () => {
    expect(() =>
      parseOrderDetail({ ...VALID_DETAIL, allowedTransitions: ['confirmed', 'teleported'] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects a payload offering the CURRENT state as a transition', () => {
    // A self-transition is a malformed policy payload, not a rendered button.
    expect(() =>
      parseOrderDetail({ ...VALID_DETAIL, allowedTransitions: ['received', 'confirmed'] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects a non-boolean refundEligible rather than coercing it', () => {
    // 'false', 0, and null are all truthiness traps — reject, never coerce.
    for (const refundEligible of ['false', 0, null]) {
      expect(() => parseOrderDetail({ ...VALID_DETAIL, refundEligible })).toThrow(
        ResponseValidationError,
      );
    }
  });

  it('rejects malformed history entries', () => {
    expect(() => parseOrderDetail({ ...VALID_DETAIL, history: [{ state: 'received' }] })).toThrow(
      ResponseValidationError,
    );
  });
});
