/**
 * Nav-policy data seam (issues 074/075; CC-MKT-005, CC-MKT-006).
 *
 * `NavPolicyApi` is the injectable HTTP-or-mock boundary: the shell and the
 * gated pages depend only on this abstract class. Until the server gating
 * API exists (issues 023/025), the root provider resolves to
 * `MockNavPolicyApi`; swapping in an HTTP implementation later is a
 * one-line provider change.
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime schema parser as `unknown` (SECURITY.md, Input validation rule 1)
 * and throws on violation; consumers fail closed (no navigation, or a 404
 * page) rather than guessing a placement.
 *
 * The seam does NOT accept a market parameter: the transacting market is
 * server-side state (CC-SEC-012; SECURITY.md, Authentication rule 10). The
 * mock injects TransactingContext only to SIMULATE the server's knowledge
 * of that state.
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { TransactingContext } from '../core/transacting-context';
import { mockNavPolicyResponse } from './nav-policy.mock-data';
import { NavPolicy } from './nav-policy.types';
import { parseNavPolicy } from './nav-policy.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockNavPolicyApi) })
export abstract class NavPolicyApi {
  /** The server-resolved navigation policy for the transacting market. */
  abstract getNavPolicy(): Observable<NavPolicy>;
}

@Injectable({ providedIn: 'root' })
export class MockNavPolicyApi extends NavPolicyApi {
  private readonly context = inject(TransactingContext);

  override getNavPolicy(): Observable<NavPolicy> {
    const response: unknown = mockNavPolicyResponse(this.context.market());
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseNavPolicy(response));
  }
}
