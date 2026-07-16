/**
 * Order tracking page (issue 070; CC-ORD-008), route /track.
 *
 * Hosts the OrderTracker over the TrackingApi seam, which today serves a
 * MOCK already-mapped tracking view.
 *
 * ⚠ DEFERRED (flagged, not implemented):
 * - Guest capability-token-in-URL handling (CC-ORD-010/CC-SEC-017, issue
 *   042) and authenticated object-level authorization (issue 062): this
 *   route currently takes NO token and shows the fixture order. When the
 *   real read endpoint lands, an invalid/missing token is a server-side 404.
 * - Cache-Control: no-store for this personalized response (SECURITY.md,
 *   HTTP boundary rule 10) is server wiring, issue 028.
 * - The internal-state→stage mapping is UNRATIFIED (epic Q13); the seam is
 *   documented accordingly in tracking.types.ts.
 *
 * Voice (DESIGN.md §9): errors state what happened and what to do next; no
 * mascots, no puns near order/money state (§5.4).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { OrderTracker } from '../../components/order-tracker/order-tracker';
import { I18nService } from '../../i18n/i18n.service';
import { TrackingApi } from '../../tracking/tracking.api';
import { OrderTrackingView } from '../../tracking/tracking.types';

type TrackState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'error' }
  | { readonly kind: 'ready'; readonly tracking: OrderTrackingView };

@Component({
  selector: 'app-track',
  imports: [OrderTracker],
  templateUrl: './track.html',
  styleUrl: './track.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Track {
  private readonly api = inject(TrackingApi);
  protected readonly i18n = inject(I18nService);

  protected readonly state = toSignal<TrackState, TrackState>(
    this.api.getTracking().pipe(
      map((tracking): TrackState => ({ kind: 'ready', tracking })),
      // Fail closed: schema-invalid tracking renders the generic error state.
      catchError(() => of<TrackState>({ kind: 'error' })),
    ),
    { initialValue: { kind: 'loading' } },
  );

  protected readonly tracking = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.tracking : null;
  });
}
