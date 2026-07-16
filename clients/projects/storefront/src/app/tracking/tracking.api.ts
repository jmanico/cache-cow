/**
 * Order-tracking data seam (issue 070).
 *
 * Same HTTP-or-mock discipline as CatalogApi: pages depend on the abstract
 * class; the mock stands in for the server read endpoint and every payload —
 * mock included — passes runtime schema validation as `unknown` before
 * anything renders (SECURITY.md, Input validation rule 1; fail closed).
 *
 * ⚠ DEFERRED — access control (flagged, not implemented here):
 * - Guest access via the per-order capability token in the URL (CC-ORD-010,
 *   CC-SEC-017) is issue 042; authenticated object-level authorization is
 *   issue 062. This mock seam takes NO token and returns a fixture; the real
 *   client will pass the capability token to the server and the server
 *   answers 404 on any invalid/missing token. Tokens are secrets: when that
 *   lands, they stay out of logs, Referer, and analytics query strings
 *   (SECURITY.md, Authentication rule 14).
 * - Tracking responses are personalized: never edge-cached, Cache-Control:
 *   no-store (SECURITY.md, HTTP boundary rule 10) — server wiring, issue 028.
 * - The internal-state→stage mapping behind this seam is UNRATIFIED (epic
 *   Q13); see tracking.types.ts.
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { ResponseValidationError } from '../catalog/catalog.validate';
import { OrderTrackingView, TRACK_STAGES, TrackedStage } from './tracking.types';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Validate a tracking response: exactly five stages, canonical order, ISO
 * timestamps, and reached stages forming a strict prefix (a hole in the
 * fill is a malformed response, not something to render around).
 */
export function parseOrderTrackingView(input: unknown): OrderTrackingView {
  if (!isRecord(input)) {
    throw new ResponseValidationError('tracking must be an object');
  }
  const orderNumber = input['orderNumber'];
  if (typeof orderNumber !== 'string' || orderNumber.length === 0) {
    throw new ResponseValidationError('tracking.orderNumber must be a non-empty string');
  }
  const stagesRaw = input['stages'];
  if (!Array.isArray(stagesRaw) || stagesRaw.length !== TRACK_STAGES.length) {
    throw new ResponseValidationError('tracking.stages must list exactly the five stages');
  }
  let previousReached = true;
  const stages: TrackedStage[] = stagesRaw.map((entry, index) => {
    if (!isRecord(entry) || entry['stage'] !== TRACK_STAGES[index]) {
      throw new ResponseValidationError('tracking.stages must be the five stages in canonical order');
    }
    const reachedAt = entry['reachedAt'];
    if (reachedAt !== null) {
      if (typeof reachedAt !== 'string' || Number.isNaN(Date.parse(reachedAt))) {
        throw new ResponseValidationError(`tracking.stages[${index}].reachedAt must be null or ISO 8601`);
      }
      if (!previousReached) {
        throw new ResponseValidationError('tracking.stages fill must be a prefix without holes');
      }
    }
    previousReached = reachedAt !== null;
    return { stage: TRACK_STAGES[index], reachedAt: reachedAt as string | null };
  });
  return { orderNumber, stages };
}

@Injectable({ providedIn: 'root', useFactory: () => inject(MockTrackingApi) })
export abstract class TrackingApi {
  /** The server-mapped tracking view for the caller's authorized order. */
  abstract getTracking(): Observable<OrderTrackingView>;
}

/** Fixture: an order currently in transit (three stages reached + one). */
const MOCK_TRACKING_RESPONSE: unknown = {
  orderNumber: 'CC-2026-004217',
  stages: [
    { stage: 'smoked', reachedAt: '2026-07-12T08:30:00Z' },
    { stage: 'frozen', reachedAt: '2026-07-12T19:15:00Z' },
    { stage: 'packed', reachedAt: '2026-07-13T10:05:00Z' },
    { stage: 'inTransit', reachedAt: '2026-07-14T06:40:00Z' },
    { stage: 'delivered', reachedAt: null },
  ],
};

@Injectable({ providedIn: 'root' })
export class MockTrackingApi extends TrackingApi {
  override getTracking(): Observable<OrderTrackingView> {
    return of(parseOrderTrackingView(MOCK_TRACKING_RESPONSE));
  }
}
