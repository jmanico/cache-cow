/**
 * Nav-policy validation seam tests (issues 074/075; SECURITY.md, Input
 * validation rule 1): malformed or non-compliant policy payloads are
 * REJECTED — the client renders no navigation rather than a wrong placement.
 * Requirement tags per REQUIREMENTS.md §17: CC-SEC-001, CC-MKT-005,
 * CC-MKT-006.
 */

import { NavPolicyValidationError, parseNavPolicy } from './nav-policy.validate';
import { mockNavPolicyResponse } from './nav-policy.mock-data';

describe('parseNavPolicy (typed policy boundary)', () => {
  it('accepts the IN policy: cows primary, cuts absent (CC-MKT-005)', () => {
    const policy = parseNavPolicy(mockNavPolicyResponse('IN'));
    expect(policy.primary).toContain('cows');
    expect(policy.ourStory).not.toContain('cows');
    expect(policy.reachable).not.toContain('cuts');
  });

  it('accepts a non-IN policy: cows under Our Story, cuts reachable (CC-MKT-005)', () => {
    for (const market of ['US', 'ES', 'MX', 'DE', 'JP'] as const) {
      const policy = parseNavPolicy(mockNavPolicyResponse(market));
      expect(policy.ourStory).toContain('cows');
      expect(policy.primary).not.toContain('cows');
      expect(policy.reachable).toContain('cuts');
    }
  });

  it('rejects a policy that reaches cuts in IN — fail closed (CC-MKT-005 negative)', () => {
    expect(() =>
      parseNavPolicy({
        market: 'IN',
        primary: ['menu', 'cows'],
        ourStory: ['chefs'],
        reachable: ['menu', 'chefs', 'cows', 'cuts'],
      }),
    ).toThrow(NavPolicyValidationError);
  });

  it('rejects a policy that places cuts in IN navigation (CC-MKT-005 negative)', () => {
    expect(() =>
      parseNavPolicy({
        market: 'IN',
        primary: ['menu', 'cows', 'cuts'],
        ourStory: ['chefs'],
        reachable: ['menu', 'chefs', 'cows', 'cuts'],
      }),
    ).toThrow(NavPolicyValidationError);
  });

  it('rejects placement of a page that is not reachable (inconsistent policy)', () => {
    expect(() =>
      parseNavPolicy({
        market: 'US',
        primary: ['menu', 'stores'],
        ourStory: ['chefs'],
        reachable: ['menu', 'stores'],
      }),
    ).toThrow(NavPolicyValidationError);
  });

  it('rejects an unknown market', () => {
    expect(() =>
      parseNavPolicy({ market: 'XX', primary: [], ourStory: [], reachable: [] }),
    ).toThrow(NavPolicyValidationError);
  });

  it('rejects an undeclared nav page', () => {
    expect(() =>
      parseNavPolicy({
        market: 'US',
        primary: ['menu', 'wholesale'],
        ourStory: [],
        reachable: ['menu', 'wholesale'],
      }),
    ).toThrow(NavPolicyValidationError);
  });

  it('rejects duplicate pages in a placement list', () => {
    expect(() =>
      parseNavPolicy({
        market: 'US',
        primary: ['menu', 'menu'],
        ourStory: [],
        reachable: ['menu'],
      }),
    ).toThrow(NavPolicyValidationError);
  });
});
