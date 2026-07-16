/**
 * Runtime validation for order-management responses (issue 082;
 * SECURITY.md, Input validation rule 1 — parse `unknown`, throw on any
 * violation, never sanitize into acceptance). Money fields are checked as
 * integer minor units (CC-PRC-003); the client performs no monetary
 * arithmetic (ARCHITECTURE.md, Dependency rule 2).
 */

import {
  ResponseValidationError,
  requireArray,
  requireBoolean,
  requireCurrency,
  requireEnum,
  requireIsoDateTime,
  requireNonNegativeInt,
  requireRecord,
  requireString,
} from '../../core/validation';
import {
  ORDER_MARKETS,
  ORDER_STATES,
  OrderDetail,
  OrderSearchResult,
  OrderState,
  OrderStateEvent,
  OrderSummary,
} from './orders.types';

function parseSummaryFields(value: unknown, field: string): OrderSummary {
  const record = requireRecord(value, field);
  return {
    orderRef: requireString(record['orderRef'], `${field}.orderRef`),
    market: requireEnum(record['market'], ORDER_MARKETS, `${field}.market`),
    state: requireEnum(record['state'], ORDER_STATES, `${field}.state`),
    placedAt: requireIsoDateTime(record['placedAt'], `${field}.placedAt`),
    itemCount: requireNonNegativeInt(record['itemCount'], `${field}.itemCount`),
    totalMinor: requireNonNegativeInt(record['totalMinor'], `${field}.totalMinor`),
    currency: requireCurrency(record['currency'], `${field}.currency`),
  };
}

export function parseOrderSearchResult(value: unknown): OrderSearchResult {
  const record = requireRecord(value, 'searchResult');
  const orders = requireArray(record['orders'], 'searchResult.orders').map((item, i) =>
    parseSummaryFields(item, `searchResult.orders[${i}]`),
  );
  return { orders };
}

function parseStateEvent(value: unknown, field: string): OrderStateEvent {
  const record = requireRecord(value, field);
  return {
    state: requireEnum(record['state'], ORDER_STATES, `${field}.state`),
    at: requireIsoDateTime(record['at'], `${field}.at`),
    actor: requireString(record['actor'], `${field}.actor`),
  };
}

export function parseOrderDetail(value: unknown): OrderDetail {
  const summary = parseSummaryFields(value, 'order');
  const record = requireRecord(value, 'order');
  const history = requireArray(record['history'], 'order.history').map((item, i) =>
    parseStateEvent(item, `order.history[${i}]`),
  );
  const allowedTransitions: OrderState[] = requireArray(
    record['allowedTransitions'],
    'order.allowedTransitions',
  ).map((item, i) => requireEnum(item, ORDER_STATES, `order.allowedTransitions[${i}]`));
  // Cross-field sanity: a server offering the CURRENT state as a transition
  // is a malformed policy payload — rejected, not rendered (fail closed).
  if (allowedTransitions.includes(summary.state)) {
    throw new ResponseValidationError('order.allowedTransitions must not contain the current state');
  }
  return {
    ...summary,
    history,
    allowedTransitions,
    refundEligible: requireBoolean(record['refundEligible'], 'order.refundEligible'),
  };
}
