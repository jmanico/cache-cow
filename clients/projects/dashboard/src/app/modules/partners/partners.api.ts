/**
 * Partner-management data seam (issue 085; CC-DSH-003, CC-WHS-002).
 *
 * `PartnersApi` is the injectable HTTP-or-mock boundary, following the
 * storefront catalog.api.ts pattern without importing it (ARCHITECTURE.md,
 * Dependency rule 4). Until the Back Office partner endpoints exist (built
 * concurrently server-side), the root provider resolves to
 * `MockPartnersApi`; swapping in an HTTP client later is a one-line provider
 * change with no page edits — at which point these contracts MUST be
 * reconciled with the published server schemas (Dependency rule 7).
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime parsers as `unknown` (SECURITY.md, Input validation rule 1): a
 * malformed payload, or one carrying an unmasked business identity, throws
 * instead of rendering (fail closed).
 *
 * Server authority (issue 085 Zero Trust Consideration): approval is an
 * explicit, audited human decision executed SERVER-side (CC-WHS-002 — no
 * self-service activation anywhere); the workflow decides which actions are
 * legal (issue 049) and arrives as `allowedActions`; approval state, actor,
 * and timestamps are server-controlled fields the client never supplies
 * (SECURITY.md, Input validation rule 3). Every action is audited before it
 * commits — if the audit write fails, the action fails (CC-DSH-004).
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import {
  MOCK_ALLOWED_ACTIONS,
  MockPartner,
  createMockPartners,
  toDetailPayload,
  toSummaryPayload,
} from './partners.mock-data';
import { PartnerAction, PartnerDetail, PartnerListResult, PartnerState } from './partners.types';
import { parsePartnerDetail, parsePartnerListResult } from './partners.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockPartnersApi) })
export abstract class PartnersApi {
  /** The partner list (CC-DSH-003). */
  abstract list(): Observable<PartnerListResult>;

  /** One partner's detail, or null when inaccessible (the real server 404s —
   * SECURITY.md, Authentication rule 9). Identities arrive masked. */
  abstract getPartner(partnerRef: string): Observable<PartnerDetail | null>;

  /**
   * Execute a workflow action. The server validates that the action is legal
   * from the current state (issue 049), authorizes the actor (issue 080), and
   * audits it (CC-DSH-004). Errors mean: rejected, no state change — a
   * partner is never activated by a failed or ambiguous step.
   */
  abstract act(partnerRef: string, action: PartnerAction): Observable<PartnerDetail>;
}

/** SIMULATES the server's post-action state (issue 049 owns the truth). */
const MOCK_RESULT_STATE: Readonly<Record<PartnerAction, PartnerState>> = {
  approve: 'approved',
  reject: 'rejected',
  suspend: 'suspended',
};

@Injectable({ providedIn: 'root' })
export class MockPartnersApi extends PartnersApi {
  private readonly partners = createMockPartners();

  override list(): Observable<PartnerListResult> {
    const response: unknown = { partners: this.partners.map(toSummaryPayload) };
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parsePartnerListResult(response));
  }

  override getPartner(partnerRef: string): Observable<PartnerDetail | null> {
    const partner = this.find(partnerRef);
    return of(partner === undefined ? null : parsePartnerDetail(toDetailPayload(partner)));
  }

  override act(partnerRef: string, action: PartnerAction): Observable<PartnerDetail> {
    const partner = this.find(partnerRef);
    // SIMULATES the server rejections: generic errors only, no state change
    // (SECURITY.md, Logging rule 1 — no internal detail reaches the client).
    if (partner === undefined || !MOCK_ALLOWED_ACTIONS[partner.state].includes(action)) {
      return throwError(() => new Error('Request rejected'));
    }
    // The real server re-checks authorization, runs the issue-049 workflow,
    // and commits the audit event with the action (CC-DSH-004).
    partner.state = MOCK_RESULT_STATE[action];
    return of(parsePartnerDetail(toDetailPayload(partner)));
  }

  private find(partnerRef: string): MockPartner | undefined {
    return this.partners.find((p) => p.partnerRef === partnerRef);
  }
}
