/**
 * Meet our Cows page (issue 074; CC-CNT-002, CC-MKT-005; DESIGN.md §7, §8.1,
 * §10).
 *
 * The herd, framed sincerely as mascots and brand family (DESIGN.md §8.1 —
 * "The India inversion"). The herd itself is identical in every market
 * (DESIGN.md §2.3: the cow is loved everywhere; only the menu changes), so
 * this page queries the seam with no market parameter and holds no market
 * conditional (ARCHITECTURE.md, Dependency rule 1).
 *
 * NAVIGATION PLACEMENT (CC-MKT-005: primary nav in IN, under Our Story
 * elsewhere) is NOT decided here. It is market gating policy encoded as data
 * and consumed by the shell header through the NavPolicy seam
 * (nav/nav-policy.api.ts) — the client renders the placement the policy
 * hands it. The REAL policy owner is the server-side Market & Gating Policy
 * context (issues 023/025); the mock seam stands in until it lands.
 *
 * CC-CNT-002 — NO NON-VEG PDP LINKS IN ANY MARKET: this page renders no
 * product link at all, in any market. That satisfies the requirement
 * structurally (nothing to regress, no runtime link check to get wrong) and
 * leaves the open question of whether veg PDP links would be permitted
 * (issue 074 Open Questions) for a human. A spec asserts zero '/product/'
 * hrefs in the rendered DOM.
 *
 * SEPARATION RULE (DESIGN.md §8.1): herd-mascot content and butchery content
 * never appear in the same view — this page contains no butchery content and
 * no link to the Cuts experience. NOTE (flagged, not resolved): the shell's
 * global navigation legitimately carries a Cuts link under Our Story in
 * non-IN markets (CC-MKT-005), so a strict reading of "same view" that
 * includes global chrome would conflict with the required nav placement.
 * This implementation reads "view" as the page's own content (issue 074
 * AC-04's "when its content is inspected"); the tension between AC-04 and
 * CC-MKT-005's nav requirement is surfaced for a human decision, not
 * silently resolved by hiding a nav link client-side (which would itself be
 * non-compliant client gating).
 *
 * Failure behavior (fail closed): a response failing schema validation or a
 * seam error renders the generic error state — never partial or guessed
 * content (SECURITY.md, Input validation rule 1; Logging rules 2/7).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, switchMap } from 'rxjs';
import { CowCard } from '../../components/cow-card/cow-card';
import { ContentApi } from '../../content/content.api';
import { CowHerd } from '../../content/content.types';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';

type HerdState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'ready'; readonly herd: CowHerd };

@Component({
  selector: 'app-cows',
  imports: [CowCard],
  templateUrl: './cows.html',
  styleUrl: './cows.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Cows {
  private readonly api = inject(ContentApi);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  /** Re-query when the locale changes: roles/bios are localized SERVER-side. */
  protected readonly state = toSignal<HerdState, HerdState>(
    toObservable(computed(() => this.context.locale())).pipe(
      switchMap(() =>
        this.api.getCowHerd().pipe(
          map((herd): HerdState => ({ kind: 'ready', herd })),
          // Fail closed: no fallback content, generic message only.
          catchError(() => of<HerdState>({ kind: 'error' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly herd = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.herd : null;
  });
}
