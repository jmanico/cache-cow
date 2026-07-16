/**
 * Dashboard shell tests (issue 079).
 * Requirement tags: CC-SEC-011, CC-DSH-001, CC-DSH-002, CC-DSH-003,
 * CC-NFR-004 (REQUIREMENTS.md §17).
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { provideRolePermissionMatrix } from './core/role-visibility';
import { provideSessionLifetime } from './core/session-expiry';
import { provideStaffSessionSource } from './core/staff-session';
import { TEST_ROLE_PERMISSION_MATRIX, TestStaffSessionSource } from './core/testing';

/** Every module name that must never leak past the gate / a closed matrix. */
const ALL_MODULE_NAMES = [
  'Sales analytics',
  'Order management',
  'Invoice management',
  'Inventory by cold store',
  'Partner management',
  'Employee management',
];

async function render(providers: Provider[]): Promise<ComponentFixture<App>> {
  await TestBed.configureTestingModule({
    imports: [App],
    providers: [provideRouter(routes), ...providers],
  }).compileComponents();
  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  return fixture;
}

function host(fixture: ComponentFixture<App>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

describe('App shell — unauthenticated gate (CC-DSH-001, fail closed)', () => {
  it('renders only the sign-in gate: no nav, no module names leaked', async () => {
    const fixture = await render([]); // shipped defaults: unauthenticated, no matrix
    const el = host(fixture);
    expect(el.querySelector('[data-testid="sign-in-action"]')).not.toBeNull();
    expect(el.querySelector('nav')).toBeNull();
    expect(el.querySelector('[data-testid="module-nav"]')).toBeNull();
    expect(el.querySelector('router-outlet')).toBeNull();
    for (const name of ALL_MODULE_NAMES) {
      expect(el.textContent).not.toContain(name);
    }
  });

  it('has no login form — the single action hands off to the SSO seam (issue 060)', async () => {
    const source = new TestStaffSessionSource();
    const fixture = await render([provideStaffSessionSource(source)]);
    const el = host(fixture);
    expect(el.querySelector('form')).toBeNull();
    expect(el.querySelector('input')).toBeNull();
    const action = el.querySelector<HTMLButtonElement>('[data-testid="sign-in-action"]');
    expect(action).not.toBeNull();
    action!.click();
    expect(source.signInRequests).toBe(1);
  });
});

describe('App shell — authenticated, NO matrix configured (fail-closed RBAC posture)', () => {
  it('hides all module nav and shows the awaiting-authorization-configuration state', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([provideStaffSessionSource(source)]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="module-nav"]')).toBeNull();
    expect(el.querySelector('[data-testid="awaiting-config"]')).not.toBeNull();
    expect(el.textContent).toContain('Awaiting authorization configuration');
    for (const name of ALL_MODULE_NAMES) {
      expect(el.textContent).not.toContain(name);
    }
  });
});

describe('App shell — authenticated with TEST matrix (CC-DSH-002 role filtering)', () => {
  it('hr-admin sees employee management and nothing else', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['hr-admin']);
    const fixture = await render([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="awaiting-config"]')).toBeNull();
    expect(el.querySelector('[data-testid="nav-employees"]')).not.toBeNull();
    expect(el.textContent).toContain('Employee management');
    expect(el.querySelector('[data-testid="nav-partners"]')).toBeNull();
    expect(el.textContent).not.toContain('Partner management');
    expect(el.querySelectorAll('[data-testid="module-nav"] a').length).toBe(1);
  });

  it('sales-viewer sees sales analytics but never partner management', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['sales-viewer']);
    const fixture = await render([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="nav-sales"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="nav-partners"]')).toBeNull();
    expect(el.textContent).not.toContain('Partner management');
    expect(el.textContent).not.toContain('Employee management');
  });

  it('shows who is signed in', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    expect(host(fixture).querySelector('[data-testid="signed-in-as"]')?.textContent).toContain(
      'Test Staff',
    );
  });
});

describe('App shell — keyboard access (DESIGN.md §13, CC-NFR-004)', () => {
  it('renders the skip link first, targeting the main landmark', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    const el = host(fixture);
    const skip = el.querySelector<HTMLAnchorElement>('[data-testid="skip-link"]');
    expect(skip).not.toBeNull();
    expect(skip!.getAttribute('href')).toBe('#cc-main');
    // First focusable element in the shell.
    const firstInteractive = el.querySelector('a, button, [tabindex]');
    expect(firstInteractive).toBe(skip);
    const main = el.querySelector('main#cc-main');
    expect(main).not.toBeNull();
    expect(main!.getAttribute('tabindex')).toBe('-1');
  });

  it('module navigation is a labeled nav of real links (natively keyboard-operable)', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([
      provideStaffSessionSource(source),
      provideRolePermissionMatrix(TEST_ROLE_PERMISSION_MATRIX),
    ]);
    const nav = host(fixture).querySelector('[data-testid="module-nav"]');
    expect(nav).not.toBeNull();
    expect(nav!.getAttribute('aria-label')).toBe('Dashboard modules');
    const links = nav!.querySelectorAll('a[href]');
    expect(links.length).toBe(6); // admin: all six CC-DSH-003 modules
  });
});

describe('App shell — session-expiry banner (SECURITY.md, Authentication rule 2)', () => {
  it('warns when the (short test) session lifetime is nearly used up', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin'], Date.now()); // fresh session...
    const fixture = await render([
      provideStaffSessionSource(source),
      // ...but a 1-minute test lifetime with a 2-minute warning window.
      provideSessionLifetime(60_000, 120_000),
    ]);
    const banner = host(fixture).querySelector('[data-testid="session-expiry-banner"]');
    expect(banner).not.toBeNull();
    expect(banner!.textContent).toContain('Session expires in 1 min');
  });

  it('reports an expired session once the lifetime has fully elapsed', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin'], Date.now() - 120_000); // established 2 min ago
    const fixture = await render([
      provideStaffSessionSource(source),
      provideSessionLifetime(60_000, 30_000), // 1-minute lifetime → already over
    ]);
    const banner = host(fixture).querySelector('[data-testid="session-expiry-banner"]');
    expect(banner).not.toBeNull();
    expect(banner!.textContent).toContain('Session expired');
  });

  it('shows no banner while a default 12-hour session is fresh', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([provideStaffSessionSource(source)]);
    expect(host(fixture).querySelector('[data-testid="session-expiry-banner"]')).toBeNull();
  });
});
