/**
 * Root shell tests (issue 063 AC-03/AC-06 structure).
 * Requirement tags: CC-MKT-002, CC-I18N-001, CC-I18N-004 (REQUIREMENTS.md §17).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App (storefront shell)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders header (logo + both switchers), main outlet, and Char footer wordmark', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelector('app-header img.logo')?.getAttribute('src')).toBe('cache-cow-logo.svg');
    expect(host.querySelector('[data-testid="market-switcher"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="locale-switcher"]')).toBeTruthy();
    expect(host.querySelector('main.page-main router-outlet')).toBeTruthy();
    // Footer: wordmark-only treatment (DESIGN.md §2.2) + always-English tag line (§2.3).
    expect(host.querySelector('app-footer .wordmark')?.textContent).toContain('CACHE COW');
    expect(host.querySelector('app-footer .tagline')?.textContent).toContain('SMOKED · CACHED · DELIVERED');
  });

  it('contains no raw-HTML sinks (SECURITY.md, Input validation rule 5)', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    // Belt-and-braces runtime assertion alongside the CI grep gate.
    expect((fixture.nativeElement as HTMLElement).outerHTML).not.toContain('bypassSecurityTrust');
  });
});
