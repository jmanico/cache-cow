/**
 * Order-tracker tests (issue 070; CC-ORD-008; DESIGN.md §7/§5.1/§13).
 * Requirement tags per REQUIREMENTS.md §17: CC-ORD-008 (CC-ORD-010/
 * CC-SEC-017 token gating is issue 042, deferred and server-owned).
 */

import { TestBed } from '@angular/core/testing';
import { throwError } from 'rxjs';
import { ResponseValidationError } from '../../catalog/catalog.validate';
import { TrackingApi, parseOrderTrackingView } from '../../tracking/tracking.api';
import { TRACK_STAGES } from '../../tracking/tracking.types';
import { Track } from './track';

type MatchMediaLike = (query: string) => Pick<MediaQueryList, 'matches' | 'media'>;

function stubMatchMedia(reducedMotion: boolean): void {
  (window as unknown as { matchMedia: MatchMediaLike }).matchMedia = (query) => ({
    matches: query.includes('prefers-reduced-motion') ? reducedMotion : false,
    media: query,
  });
}

async function createTrack() {
  const fixture = TestBed.createComponent(Track);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('Track page + OrderTracker (issue 070)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Track] }).compileComponents();
  });

  it('renders exactly the five stages in order with plain-text labels (AC-01/AC-02)', async () => {
    const host = await createTrack();
    const items = host.querySelectorAll('[data-testid="tracker-stages"] li');
    expect(items.length).toBe(5);
    const text = Array.from(items).map((item) => item.textContent ?? '');
    expect(text[0]).toContain('Smoked');
    expect(text[1]).toContain('Frozen');
    expect(text[2]).toContain('Packed');
    expect(text[3]).toContain('In transit');
    expect(text[4]).toContain('Delivered');
  });

  it('fills one Ember arc segment per reached stage; pending arcs stay Smoke-classed (AC-02)', async () => {
    const host = await createTrack();
    const arcs = host.querySelectorAll('[data-testid="tracker-arcs"] path');
    expect(arcs.length).toBe(5);
    // Mock fixture: four stages reached, delivery pending.
    expect(host.querySelectorAll('.arc-reached').length).toBe(4);
    expect(host.querySelectorAll('.arc-pending').length).toBe(1);
    // Decorative: status is carried by the text list, never color alone (§13).
    expect(host.querySelector('[data-testid="tracker-arcs"]')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('shows a mono timestamp per reached stage and marks the current stage (AC-02, a11y)', async () => {
    const host = await createTrack();
    const times = host.querySelectorAll('[data-testid="tracker-stages"] time');
    expect(times.length).toBe(4);
    expect(times[0]?.getAttribute('datetime')).toBe('2026-07-12T08:30:00Z');
    const current = host.querySelectorAll('[aria-current="step"]');
    expect(current.length).toBe(1);
    expect(current[0]?.textContent).toContain('In transit');
    // Unreached stages carry explicit text, not just a missing timestamp.
    const pending = host.querySelector('.tracker-stage:last-child');
    expect(pending?.textContent).toContain('Not yet reached');
    // Order number renders in the mono slot.
    expect(host.querySelector('[data-testid="order-number"]')?.textContent).toContain('CC-2026-004217');
  });

  it('disables the arc-fill animation under prefers-reduced-motion: final state renders (AC-03, §13)', async () => {
    stubMatchMedia(true);
    const host = await createTrack();
    const svg = host.querySelector('[data-testid="tracker-arcs"]');
    expect(svg?.classList.contains('tracker-animate')).toBe(false);
    // Content is already in its final state: all reached arcs present.
    expect(host.querySelectorAll('.arc-reached').length).toBe(4);
  });

  it('enables the animation class only when motion is not reduced (AC-03)', async () => {
    stubMatchMedia(false);
    const host = await createTrack();
    expect(host.querySelector('[data-testid="tracker-arcs"]')?.classList.contains('tracker-animate')).toBe(
      true,
    );
  });

  it('fails closed to a generic error state when the seam rejects the response', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Track],
      providers: [
        {
          provide: TrackingApi,
          useValue: { getTracking: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const host = await createTrack();
    expect(host.querySelector('[data-testid="track-error"]')).toBeTruthy();
    expect(host.querySelectorAll('[data-testid="tracker-stages"] li').length).toBe(0);
    expect(host.textContent).not.toContain('schema violation');
  });
});

describe('parseOrderTrackingView (typed seam over the UNRATIFIED server mapping)', () => {
  const validStages = [
    { stage: 'smoked', reachedAt: '2026-07-12T08:30:00Z' },
    { stage: 'frozen', reachedAt: '2026-07-12T19:15:00Z' },
    { stage: 'packed', reachedAt: null },
    { stage: 'inTransit', reachedAt: null },
    { stage: 'delivered', reachedAt: null },
  ];

  it('accepts a canonical five-stage prefix fill', () => {
    const view = parseOrderTrackingView({ orderNumber: 'CC-1', stages: validStages });
    expect(view.stages.map((entry) => entry.stage)).toEqual([...TRACK_STAGES]);
  });

  it('rejects a wrong stage count', () => {
    expect(() =>
      parseOrderTrackingView({ orderNumber: 'CC-1', stages: validStages.slice(0, 4) }),
    ).toThrow(ResponseValidationError);
  });

  it('rejects stages out of canonical order', () => {
    const shuffled = [validStages[1], validStages[0], ...validStages.slice(2)];
    expect(() => parseOrderTrackingView({ orderNumber: 'CC-1', stages: shuffled })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a fill with a hole (reached stage after an unreached one)', () => {
    const holed = validStages.map((entry, index) =>
      index === 4 ? { stage: 'delivered', reachedAt: '2026-07-15T00:00:00Z' } : entry,
    );
    expect(() => parseOrderTrackingView({ orderNumber: 'CC-1', stages: holed })).toThrow(
      ResponseValidationError,
    );
  });

  it('rejects a non-ISO timestamp', () => {
    const bad = validStages.map((entry, index) =>
      index === 0 ? { stage: 'smoked', reachedAt: 'yesterday-ish' } : entry,
    );
    expect(() => parseOrderTrackingView({ orderNumber: 'CC-1', stages: bad })).toThrow(
      ResponseValidationError,
    );
  });
});
