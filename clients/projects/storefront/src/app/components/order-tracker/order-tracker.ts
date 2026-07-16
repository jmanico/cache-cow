/**
 * Order tracker (issue 070; CC-ORD-008; DESIGN.md §7 "Order tracker", §5.1).
 *
 * Presents an ALREADY-MAPPED `OrderTrackingView` (see tracking.types.ts —
 * the internal-state→stage mapping is unratified and server-owned; this
 * component derives nothing but display state). Five stages as arc-fan
 * segments filling in Ember — the tracker is one of the four permitted homes
 * of the broadcast-arc motif (§5.1) — each stage with a plain-text label and
 * its timestamp in Plex Mono (§4.1).
 *
 * Accessibility (§13): the arc fan is decorative (aria-hidden) — stage
 * status is conveyed by the text list, never by color alone; the current
 * stage carries aria-current="step". The arc-fill animation runs only in the
 * browser when `prefers-reduced-motion` does NOT match 'reduce' (checked via
 * matchMedia, double-guarded in CSS); otherwise — SSR included — the arcs
 * render directly in their final state.
 *
 * Zero puns: order progress is adjacent to money movement (§5.4).
 */

import { ChangeDetectionStrategy, Component, PLATFORM_ID, computed, inject, input } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { TransactingContext } from '../../core/transacting-context';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';
import { OrderTrackingView, TrackStage, reachedCount } from '../../tracking/tracking.types';

const STAGE_LABEL_KEYS: Readonly<Record<TrackStage, MessageKey>> = {
  smoked: 'track.stage.smoked',
  frozen: 'track.stage.frozen',
  packed: 'track.stage.packed',
  inTransit: 'track.stage.inTransit',
  delivered: 'track.stage.delivered',
};

/** Arc-fan geometry: one concentric arc per stage, innermost first. */
const ARC_RADII = [8, 16, 24, 32, 40] as const;

@Component({
  selector: 'app-order-tracker',
  templateUrl: './order-tracker.html',
  styleUrl: './order-tracker.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderTracker {
  private readonly context = inject(TransactingContext);
  protected readonly i18n = inject(I18nService);

  readonly tracking = input.required<OrderTrackingView>();

  /**
   * Animation gate: browser only, and only without a reduced-motion
   * preference. On the server (no window) this is false, so the SSR HTML is
   * the final state (DESIGN.md §13).
   */
  protected readonly animate =
    isPlatformBrowser(inject(PLATFORM_ID)) &&
    typeof window.matchMedia === 'function' &&
    !window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  protected readonly reached = computed(() => reachedCount(this.tracking()));

  protected arcPath(index: number): string {
    const radius = ARC_RADII[index] ?? ARC_RADII[ARC_RADII.length - 1];
    return `M${50 - radius} 58 A${radius} ${radius} 0 0 1 ${50 + radius} 58`;
  }

  protected stageLabelKey(stage: TrackStage): MessageKey {
    return STAGE_LABEL_KEYS[stage];
  }

  /** Locale-formatted timestamp, Plex Mono in the template (CC-I18N-003). */
  protected timestampText(reachedAt: string): string {
    return new Intl.DateTimeFormat(this.context.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(reachedAt));
  }
}
