/**
 * Partner response validation tests (issue 085).
 * Requirement tags: CC-SEC-001, CC-WHS-002, CC-WHS-004 (REQUIREMENTS.md §17).
 *
 * The masked-by-contract tests are the important ones: the client is the last
 * place a full USt-IdNr./GSTIN should ever appear, so a payload carrying one
 * must fail closed rather than render.
 */

import { ResponseValidationError } from '../../core/validation';
import { parsePartnerDetail, parsePartnerListResult } from './partners.validate';

const VALID_SUMMARY = {
  partnerRef: 'WHP-000117',
  name: 'Nordwind Feinkost GmbH',
  market: 'DE',
  state: 'pending',
  appliedAt: '2026-07-09T08:15:00Z',
};

const VALID_DETAIL = {
  ...VALID_SUMMARY,
  businessIdentities: [{ kind: 'USt-IdNr.', maskedValue: '•••••••••4821' }],
  paymentTermsDays: 60,
  allowedActions: ['approve', 'reject'],
};

describe('parsePartnerListResult (CC-SEC-001)', () => {
  it('accepts a well-formed payload and returns typed rows', () => {
    const result = parsePartnerListResult({ partners: [VALID_SUMMARY] });
    expect(result.partners.length).toBe(1);
    expect(result.partners[0].state).toBe('pending');
  });

  it('rejects a non-object payload or missing partners array', () => {
    expect(() => parsePartnerListResult(null)).toThrow(ResponseValidationError);
    expect(() => parsePartnerListResult({})).toThrow(ResponseValidationError);
  });

  it('rejects an unknown onboarding state or market', () => {
    expect(() =>
      parsePartnerListResult({ partners: [{ ...VALID_SUMMARY, state: 'probationary' }] }),
    ).toThrow(ResponseValidationError);
    expect(() =>
      parsePartnerListResult({ partners: [{ ...VALID_SUMMARY, market: 'FR' }] }),
    ).toThrow(ResponseValidationError);
  });
});

describe('parsePartnerDetail — masked-by-contract identity (CC-WHS-002)', () => {
  it('accepts a masked identity and returns it verbatim', () => {
    const detail = parsePartnerDetail(VALID_DETAIL);
    expect(detail.businessIdentities[0].maskedValue).toBe('•••••••••4821');
    expect(detail.businessIdentities[0].kind).toBe('USt-IdNr.');
  });

  it('REJECTS a payload carrying a full identifier alongside the mask', () => {
    // A server regression that starts sending the real value must fail closed
    // here, not quietly render it.
    for (const field of ['value', 'fullValue', 'identifier', 'raw']) {
      expect(() =>
        parsePartnerDetail({
          ...VALID_DETAIL,
          businessIdentities: [
            { kind: 'USt-IdNr.', maskedValue: '•••••••••4821', [field]: 'DE123454821' },
          ],
        }),
      ).toThrow(ResponseValidationError);
    }
  });

  it('REJECTS an unmasked value placed in the masked field', () => {
    expect(() =>
      parsePartnerDetail({
        ...VALID_DETAIL,
        businessIdentities: [{ kind: 'USt-IdNr.', maskedValue: 'DE123454821' }],
      }),
    ).toThrow(ResponseValidationError);
  });

  it('REJECTS a mask that reveals more than the last 4 characters', () => {
    expect(() =>
      parsePartnerDetail({
        ...VALID_DETAIL,
        businessIdentities: [{ kind: 'GSTIN', maskedValue: '••••23454821' }],
      }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects an identity missing its kind or masked value', () => {
    expect(() =>
      parsePartnerDetail({ ...VALID_DETAIL, businessIdentities: [{ kind: 'GSTIN' }] }),
    ).toThrow(ResponseValidationError);
    expect(() =>
      parsePartnerDetail({ ...VALID_DETAIL, businessIdentities: [{ maskedValue: '•••••4821' }] }),
    ).toThrow(ResponseValidationError);
  });
});

describe('parsePartnerDetail — workflow and terms (CC-WHS-004)', () => {
  it('returns the server-offered actions verbatim', () => {
    const detail = parsePartnerDetail(VALID_DETAIL);
    expect(detail.allowedActions).toEqual(['approve', 'reject']);
    expect(detail.paymentTermsDays).toBe(60);
  });

  it('rejects an unknown action (fail closed)', () => {
    expect(() =>
      parsePartnerDetail({ ...VALID_DETAIL, allowedActions: ['approve', 'deactivate'] }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects non-integer or negative payment terms', () => {
    for (const paymentTermsDays of [60.5, -30, '60']) {
      expect(() => parsePartnerDetail({ ...VALID_DETAIL, paymentTermsDays })).toThrow(
        ResponseValidationError,
      );
    }
  });
});
