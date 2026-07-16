/**
 * Storefront footer (issue 063; legal links: issue 077, CC-CNT-005;
 * DESIGN.md §8.4).
 *
 * Char footer per DESIGN.md §6 page anatomy, wordmark-only treatment per
 * §2.2 (variant table: "Wordmark only — Legal, footer"). The wordmark
 * renders as text in the display face token until the dedicated wordmark-only
 * asset lands (DESIGN.md §15 lists it in the remaining asset set). The tag
 * line stays English in all locales as lockup text (DESIGN.md §2.3);
 * translated taglines are separate copy owned by future content issues.
 *
 * LEGAL LINKS ARE POLICY DATA (CC-CNT-005). The footer renders the
 * transacting market's legal content set exactly as the content seam hands
 * it — so the DE market's Impressum and Widerrufsbelehrung appear as
 * FIRST-CLASS footer items (DESIGN.md §8.4: first-class, not buried), and no
 * other market shows them, without a single market conditional in this
 * component (ARCHITECTURE.md, Dependency rule 1; the set is authored
 * server-side, issue 023). Every item sits in one flat list at the same
 * level — "first-class" is structural here, not a styling opinion.
 *
 * NOTE (open, not resolved): DESIGN.md §8.4 also makes "detailed allergen/
 * nutrition links" first-class DE footer items. No allergen/nutrition
 * destination page exists yet (per-SKU allergen/nutrition data lives on the
 * PDP, issue 067), so that link has no target. Flagged for a content/IA
 * decision rather than invented here.
 *
 * Fail closed: if the legal content set cannot be resolved, no legal links
 * render — never a guessed or another market's set (SECURITY.md, Logging
 * rule 2).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { ContentApi } from '../../content/content.api';
import { LegalDocList } from '../../content/content.types';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';

@Component({
  selector: 'app-footer',
  imports: [RouterLink],
  templateUrl: './footer.html',
  styleUrl: './footer.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Footer {
  private readonly api = inject(ContentApi);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  // A string, not a number: numeric values are locale-grouped by the
  // formatter (CC-I18N-003) and a year must never render as "2,026".
  protected readonly year = String(new Date().getFullYear());

  /** Re-ask on market or locale change: the SET is per market, the TITLES
   * are per locale — both server-resolved. null = unresolved → no links. */
  protected readonly docs = toSignal<LegalDocList | null, LegalDocList | null>(
    toObservable(
      computed(() => ({ market: this.context.market(), locale: this.context.locale() })),
    ).pipe(
      switchMap(() =>
        this.api.getLegalDocList().pipe(
          map((list): LegalDocList | null => list),
          catchError(() => of<LegalDocList | null>(null)),
        ),
      ),
    ),
    { initialValue: null },
  );
}
