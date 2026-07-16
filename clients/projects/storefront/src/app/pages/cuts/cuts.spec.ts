/**
 * Meet our Cuts page tests (issue 075; CC-CNT-003, CC-MKT-005).
 * Requirement tags per REQUIREMENTS.md §17: CC-CNT-003, CC-MKT-005,
 * CC-NFR-004.
 *
 * Scope note: these assert the CLIENT MIRROR of the gating decision and the
 * interactive's accessibility. The real enforcement — HTTP 404 status in IN,
 * sitemap exclusion, cache-variant keys — is server-side (issues
 * 025/026/028) and is asserted there, not here.
 */

import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { throwError } from 'rxjs';
import { CUT_REGIONS } from '../../components/cut-diagram/cut-diagram';
import { TransactingContext } from '../../core/transacting-context';
import { NavPolicyApi } from '../../nav/nav-policy.api';
import { routes } from '../../app.routes';
import { Cuts } from './cuts';

function regionButtons(host: HTMLElement): HTMLButtonElement[] {
  return Array.from(host.querySelectorAll<HTMLButtonElement>('[data-testid="cut-region"]'));
}

function listButtons(host: HTMLElement): HTMLButtonElement[] {
  return Array.from(host.querySelectorAll<HTMLButtonElement>('[data-testid="cuts-list-button"]'));
}

/** Routes without the real Menu component: navigation resolves, but the
 * menu page (and its catalog seam) is not constructed by these tests. */
const testRoutes = routes.map((route) =>
  route.path === 'menu' ? { path: 'menu', children: [] } : route,
);

describe('Cuts (issue 075)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(testRoutes)],
    });
  });

  it('exposes every cut region as a NAMED BUTTON inside a named group (AC-02, DESIGN.md §13)', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    const group = host.querySelector('[role="group"]')!;
    expect(group.getAttribute('aria-label')).toBe('Interactive steer diagram');

    const buttons = regionButtons(host);
    expect(buttons.length).toBe(CUT_REGIONS.length);
    for (const button of buttons) {
      // A REAL button: keyboard-operable and focusable by construction.
      expect(button.tagName).toBe('BUTTON');
      expect(button.type).toBe('button');
      expect(button.getAttribute('aria-label')).toMatch(/^Filter the menu by /);
    }
    // The artwork itself is decorative; the buttons carry the semantics.
    expect(host.querySelector('[data-testid="cut-art"]')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('activating a diagram region navigates to the menu with that cut filter (AC-01)', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    const brisket = regionButtons(host).find((b) => b.dataset['cut'] === 'brisket')!;
    brisket.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/menu?cut=brisket');
  });

  it('offers a list equivalent that mirrors the diagram regions and filters identically (AC-01, CC-CNT-003)', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    // Exposed to ALL users, not a hidden screen-reader-only fallback.
    expect(host.querySelector('[data-testid="cuts-list"]')).toBeTruthy();

    // Same regions, same order — both render from one array.
    const diagramCuts = regionButtons(host).map((b) => b.dataset['cut']);
    const listCuts = listButtons(host).map((b) => b.dataset['cut']);
    expect(listCuts).toEqual(diagramCuts);
    expect(listCuts).toEqual(CUT_REGIONS.map((r) => r.cut));

    // And the same filtering action.
    listButtons(host).find((b) => b.dataset['cut'] === 'ribs')!.click();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/menu?cut=ribs');
  });

  it('renders the 404 body in the IN market and no butchery content (AC-03/AC-04, CC-MKT-005 negative)', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(TransactingContext).setMarket('IN');
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    expect(host.querySelector('[data-testid="cuts-gated"]')).toBeTruthy();
    expect(host.textContent).toContain('Signal lost');
    // The interactive is never constructed — not hidden, absent.
    expect(regionButtons(host).length).toBe(0);
    expect(listButtons(host).length).toBe(0);
    expect(host.querySelector('[data-testid="cut-art"]')).toBeNull();
    expect(host.textContent).not.toContain('Brisket');
  });

  it('carries no cow-mascot illustration or herd content in any market (AC-07, DESIGN.md §8.1)', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    expect(host.querySelector('[data-testid="cow-illustration"]')).toBeNull();
    expect(host.querySelector('[data-testid="cow-card"]')).toBeNull();
    expect(host.innerHTML).not.toContain('/cows');
    // The diagram is line art: strokes, no filled mascot shapes.
    expect(host.querySelectorAll('.cut-line').length).toBeGreaterThan(0);
  });

  it('fails closed to the 404 body when the gating policy cannot be resolved (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(testRoutes),
        {
          provide: NavPolicyApi,
          useValue: { getNavPolicy: () => throwError(() => new Error('policy unavailable')) },
        },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cuts', Cuts);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    // A gating-path exception is a denial, never a bypass.
    expect(host.querySelector('[data-testid="cuts-gated"]')).toBeTruthy();
    expect(regionButtons(host).length).toBe(0);
    expect(host.textContent).not.toContain('policy unavailable');
  });
});
