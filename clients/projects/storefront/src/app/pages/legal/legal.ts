/**
 * Legal document page — /legal/:doc (issue 077; CC-CNT-005, CC-FUL-003;
 * DESIGN.md §8.4, §10).
 *
 * Serves one VERSIONED legal document from the transacting market's legal
 * content set, in the active locale. The set itself is per-market policy
 * data owned server-side (Market & Gating Policy, issue 023; the mock seam
 * stands in) — this page holds no market conditional: it asks the seam for a
 * document and renders whatever the server's set allows. A document outside
 * the market's set (e.g. Impressum in the US) resolves to null and renders
 * the 404 body; the HTTP 404 STATUS is server-owned (SECURITY.md,
 * Authentication rule 9 hardening default; issue 026).
 *
 * VERSIONING (CC-CNT-005): every rendered document shows its version and
 * effective date. Issued versions are immutable — a correction is a NEW
 * version, never a mutation of the served text (ARCHITECTURE.md, Dependency
 * rule 6's append-only posture).
 *
 * LEGAL TEXT IS NOT AUTHORED HERE. The drafted texts were accepted
 * 2026-07-15 with legal review to run against implemented behavior
 * (ARCHITECTURE.md decision record), and issue 077's AI guidance is explicit:
 * AI MUST NOT draft or alter legal text. The mock seam therefore carries
 * PLACEHOLDER bodies that state only that the accepted text will be rendered
 * unchanged; the DE Widerrufsbelehrung's perishable-frozen-food exemption
 * (CC-FUL-003) is likewise referenced, not drafted. Where legal texts are
 * authored (Contentful vs. repo-versioned resources) is an OPEN question
 * (issue 077).
 *
 * Body text arrives as typed, structured PLAIN-TEXT sections already
 * sanitized server-side through the allowlist renderer (issue 072); it binds
 * through text interpolation only — no raw-HTML sink (CC-SEC-002).
 *
 * Pun budget (DESIGN.md §5.4): ZERO puns on legal surfaces — comedy never
 * touches money movement, safety, or legal content.
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { ContentApi } from '../../content/content.api';
import { LegalDoc } from '../../content/content.types';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { NotFound } from '../not-found/not-found';

type LegalState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'notFound' }
  | { readonly kind: 'ready'; readonly doc: LegalDoc };

@Component({
  selector: 'app-legal',
  imports: [NotFound],
  templateUrl: './legal.html',
  styleUrl: './legal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Legal {
  private readonly api = inject(ContentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  private readonly docId = toSignal(this.route.paramMap.pipe(map((p) => p.get('doc') ?? '')), {
    initialValue: '',
  });

  /** Re-query on document, market, or locale change (all server-resolved). */
  private readonly request = computed(() => ({
    docId: this.docId(),
    market: this.context.market(),
    locale: this.context.locale(),
  }));

  protected readonly state = toSignal<LegalState, LegalState>(
    toObservable(this.request).pipe(
      switchMap(({ docId }) =>
        this.api.getLegalDoc(docId).pipe(
          map((doc): LegalState => (doc === null ? { kind: 'notFound' } : { kind: 'ready', doc })),
          // Fail closed: never fall back to another market's text or an
          // unversioned draft (issue 077 Failure Behavior).
          catchError(() => of<LegalState>({ kind: 'error' })),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly doc = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.doc : null;
  });

  /** Locale-formatted effective date (CC-I18N-003) — never hand-formatted. */
  protected readonly effectiveDateText = computed(() => {
    const doc = this.doc();
    if (doc === null) {
      return '';
    }
    // The seam validated this as an ISO yyyy-mm-dd date.
    const [year, month, day] = doc.effectiveDate.split('-').map(Number);
    const date = new Date(Date.UTC(year, month - 1, day));
    return new Intl.DateTimeFormat(this.context.locale(), {
      dateStyle: 'long',
      timeZone: 'UTC',
    }).format(date);
  });
}
