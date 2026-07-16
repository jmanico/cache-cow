/**
 * Meet our Chefs page (issue 073; CC-CNT-001; DESIGN.md §7, §10).
 *
 * Renders the SHARED chef roster (identical in every market — the roster
 * query deliberately carries no market parameter) with bios localized for
 * the transacting locale. Localization happens server-side: a locale change
 * simply re-queries the seam and displays whatever localized roster comes
 * back (CC-CNT-001). This page holds no market conditionals and no gating
 * (ARCHITECTURE.md, Dependency rule 1).
 *
 * CMS-authored bios reach this page as typed PLAIN TEXT, already sanitized
 * server-side through the allowlist renderer (issue 072); they bind through
 * text interpolation only — no raw-HTML sink (CC-SEC-002).
 *
 * Failure behavior (fail closed): a response failing schema validation or a
 * seam error renders the generic error state — never partial or unvalidated
 * content (SECURITY.md, Input validation rule 1; Logging rules 2/7).
 *
 * Pun budget (DESIGN.md §5.4): this page carries zero cache/tech puns — the
 * one permitted per-viewport pun is spent elsewhere.
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, switchMap } from 'rxjs';
import { ChefCard } from '../../components/chef-card/chef-card';
import { ContentApi } from '../../content/content.api';
import { ChefRoster } from '../../content/content.types';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';

type RosterState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'ready'; readonly roster: ChefRoster };

@Component({
  selector: 'app-chefs',
  imports: [ChefCard],
  templateUrl: './chefs.html',
  styleUrl: './chefs.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Chefs {
  private readonly api = inject(ContentApi);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  /** Re-query when the locale changes: bios are localized SERVER-side. */
  protected readonly state = toSignal<RosterState, RosterState>(
    toObservable(computed(() => this.context.locale())).pipe(
      switchMap(() =>
        this.api.getChefRoster().pipe(
          map((roster): RosterState => ({ kind: 'ready', roster })),
          // Fail closed: no fallback content, generic message only.
          catchError(() => of<RosterState>({ kind: 'error' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly roster = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.roster : null;
  });
}
