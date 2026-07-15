/**
 * 404 route tests (issue 063; DESIGN.md §5.1 "Signal lost").
 * Requirement tags: CC-I18N-002 (strings from bundles); the HTTP 404 status
 * itself is asserted by the SSR smoke check (app.routes.server.ts).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { NotFound } from './not-found';

describe('NotFound (Signal lost)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes)],
    });
  });

  it('renders for any unknown URL via the catch-all route', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/definitely/not/a/page', NotFound);
    expect(component).toBeTruthy();
    const host = harness.routeNativeElement as HTMLElement;
    expect(host.textContent).toContain('Signal lost');
    // What happened and what to do next; no mascot in error states (DESIGN.md §9).
    expect(host.querySelector('a[href="/"]')).toBeTruthy();
    expect(host.querySelector('img')).toBeNull();
  });

  it('shows the arc motif as decorative only (aria-hidden, DESIGN.md §5.1/§13)', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/nope', NotFound);
    const svg = (harness.routeNativeElement as HTMLElement).querySelector('svg');
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
  });
});
