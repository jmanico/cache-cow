/**
 * Meet our Cuts page (issue 075; CC-CNT-003, CC-MKT-005; DESIGN.md §7, §8.1,
 * §10, §13).
 *
 * The interactive butcher diagram plus its accessible list equivalent. Both
 * surfaces render from the SAME region array (CUT_REGIONS in the diagram
 * component), so the list cannot drift from the diagram, and both route to
 * the menu with the cut filter — where filtering executes SERVER-side (issue
 * 031; ARCHITECTURE.md, Dependency rules 1–2). The list equivalent is
 * exposed to ALL users, not hidden behind an assistive-technology-only
 * class: it is an equal path to the same filtering (DESIGN.md §13,
 * CC-CNT-003).
 *
 * IN EXCLUSION (CC-MKT-005) — THE CLIENT ONLY MIRRORS:
 * The REAL enforcement is server-side and is not implemented here. The
 * server-side Market & Gating Policy service (issues 023/025/026) owns:
 *   - the HTTP 404 status for /cuts in the IN market (404, never 403, never
 *     a cross-market redirect — CC-MKT-004 semantics; issue 075 AC-04),
 *   - excluding the route from IN navigation, sitemaps, and feeds,
 *   - keeping an IN response variant out of any shared cache (CC-MKT-009).
 * This page reads the policy through the NavPolicy seam and, when 'cuts' is
 * not reachable, renders the 404 page BODY — mirroring the server's decision
 * so a client-side route activation cannot show butchery content in IN. It
 * is defense in depth, NOT the enforcement point, and it is not client-side
 * "hiding" (which AC-05 rightly calls non-compliant): the content is never
 * constructed, and the decision comes from policy data, not a market
 * conditional in this file (there is no `if (market === 'IN')` here).
 *
 * Failure behavior (fail closed): if the policy cannot be resolved, the page
 * renders the 404 body rather than the diagram — a gating-path exception is
 * a denial, never a bypass (SECURITY.md, Logging rule 2; issue 075 Failure
 * Behavior: the experience MUST NOT render in IN under any failure mode).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { Cut } from '../../catalog/catalog.types';
import { CUT_REGIONS, CutDiagram } from '../../components/cut-diagram/cut-diagram';
import { CUT_NAME_KEYS } from '../../components/product-card/product-card';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import { NavPolicyApi } from '../../nav/nav-policy.api';
import { NotFound } from '../not-found/not-found';

/** 'gated' covers both "policy says unreachable" and "policy unresolved". */
type CutsState = { readonly kind: 'loading' | 'gated' | 'ready' };

@Component({
  selector: 'app-cuts',
  imports: [CutDiagram, NotFound],
  templateUrl: './cuts.html',
  styleUrl: './cuts.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Cuts {
  private readonly navPolicy = inject(NavPolicyApi);
  private readonly context = inject(TransactingContext);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  /** Diagram and list render from one array — they cannot drift (AC-01/02). */
  protected readonly regions = CUT_REGIONS;

  /** Re-ask the policy when the transacting market changes. */
  protected readonly state = toSignal<CutsState, CutsState>(
    toObservable(computed(() => this.context.market())).pipe(
      switchMap(() =>
        this.navPolicy.getNavPolicy().pipe(
          map((policy): CutsState => ({
            kind: policy.reachable.includes('cuts') ? 'ready' : 'gated',
          })),
          // Fail closed: an unresolved policy is a denial, never a render.
          catchError(() => of<CutsState>({ kind: 'gated' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected cutNameKey(cut: Cut): MessageKey {
    return CUT_NAME_KEYS[cut];
  }

  /** Diagram and list share one handler: identical behavior by construction. */
  protected onSelectCut(cut: Cut): void {
    // The menu owns filtering; the filter travels as a query param and
    // executes server-side (issue 031).
    void this.router.navigate(['/menu'], { queryParams: { cut } });
  }
}
