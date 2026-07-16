/**
 * Runtime validation for partner-management responses (issue 085;
 * SECURITY.md, Input validation rule 1 — parse `unknown`, throw on any
 * violation, never sanitize into acceptance).
 *
 * Beyond shape checking, this parser enforces the MASKED-BY-CONTRACT posture
 * (partners.types.ts): a business identity must arrive masked, and a payload
 * carrying a full identifier is REJECTED rather than rendered. The client is
 * the last place a full USt-IdNr./GSTIN should ever appear, so a server
 * regression that starts sending one fails closed here instead of quietly
 * leaking it onto a screen (SECURITY.md, Logging rule 2).
 */

import {
  ResponseValidationError,
  requireArray,
  requireEnum,
  requireIsoDateTime,
  requireNonNegativeInt,
  requireRecord,
  requireString,
} from '../../core/validation';
import {
  BusinessIdentity,
  PARTNER_ACTIONS,
  PARTNER_MARKETS,
  PARTNER_STATES,
  PartnerAction,
  PartnerDetail,
  PartnerListResult,
  PartnerSummary,
} from './partners.types';

/**
 * A masked identifier: mask characters followed by at most 4 trailing
 * characters (the last-4 pattern). Enforcing the shape here means an
 * unmasked value cannot pass by being placed in the masked field.
 */
const MASKED_VALUE_RE = /^[•*]{4,}[A-Za-z0-9]{1,4}$/;

/**
 * Field names that would carry a FULL identifier. Their presence means the
 * server is sending more than this boundary is allowed to receive — treated
 * as a malformed response, never trimmed into acceptance.
 */
const FORBIDDEN_IDENTITY_FIELDS = ['value', 'fullValue', 'identifier', 'raw'] as const;

function parseBusinessIdentity(value: unknown, field: string): BusinessIdentity {
  const record = requireRecord(value, field);
  for (const forbidden of FORBIDDEN_IDENTITY_FIELDS) {
    if (forbidden in record) {
      throw new ResponseValidationError(
        `${field} must not carry a full identifier — masked values only`,
      );
    }
  }
  const maskedValue = requireString(record['maskedValue'], `${field}.maskedValue`);
  if (!MASKED_VALUE_RE.test(maskedValue)) {
    throw new ResponseValidationError(`${field}.maskedValue must be masked to its last characters`);
  }
  return { kind: requireString(record['kind'], `${field}.kind`), maskedValue };
}

function parseSummaryFields(value: unknown, field: string): PartnerSummary {
  const record = requireRecord(value, field);
  return {
    partnerRef: requireString(record['partnerRef'], `${field}.partnerRef`),
    name: requireString(record['name'], `${field}.name`),
    market: requireEnum(record['market'], PARTNER_MARKETS, `${field}.market`),
    state: requireEnum(record['state'], PARTNER_STATES, `${field}.state`),
    appliedAt: requireIsoDateTime(record['appliedAt'], `${field}.appliedAt`),
  };
}

export function parsePartnerListResult(value: unknown): PartnerListResult {
  const record = requireRecord(value, 'partnerList');
  const partners = requireArray(record['partners'], 'partnerList.partners').map((item, i) =>
    parseSummaryFields(item, `partnerList.partners[${i}]`),
  );
  return { partners };
}

export function parsePartnerDetail(value: unknown): PartnerDetail {
  const summary = parseSummaryFields(value, 'partner');
  const record = requireRecord(value, 'partner');
  const businessIdentities = requireArray(
    record['businessIdentities'],
    'partner.businessIdentities',
  ).map((item, i) => parseBusinessIdentity(item, `partner.businessIdentities[${i}]`));
  const allowedActions: PartnerAction[] = requireArray(
    record['allowedActions'],
    'partner.allowedActions',
  ).map((item, i) => requireEnum(item, PARTNER_ACTIONS, `partner.allowedActions[${i}]`));
  return {
    ...summary,
    businessIdentities,
    // Terms are days, an integer count — not money, so no minor-unit handling
    // applies. The server owns the net-60 default (CC-WHS-004).
    paymentTermsDays: requireNonNegativeInt(record['paymentTermsDays'], 'partner.paymentTermsDays'),
    allowedActions,
  };
}
