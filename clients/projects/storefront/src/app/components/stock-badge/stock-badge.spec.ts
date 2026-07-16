/**
 * Cache-status badge tests (issue 066 AC-02; CC-CAT-003; DESIGN.md §5.2/§13).
 * The badge vocabulary is asserted against the generated design tokens —
 * never against hardcoded copies (ARCHITECTURE.md, Dependency rule 8).
 * Requirement tags: CC-CAT-003.
 */

import { TestBed } from '@angular/core/testing';
import { STOCK_STATES, StockState } from '../../catalog/catalog.types';
import { BADGE_TEXT, StockBadge } from './stock-badge';
import tokens from '../../../../../../tokens/dist/tokens.json';

const EN_US_PLAIN_LINES: Readonly<Record<StockState, string>> = {
  cacheHit: 'Ships today from your regional cold store',
  warming: 'Restocking, preorder available',
  cacheMiss: 'Not available in your region yet',
};

async function render(state: StockState) {
  const fixture = TestBed.createComponent(StockBadge);
  fixture.componentRef.setInput('state', state);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('StockBadge (DESIGN.md §5.2 badge/plain-line pairing)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StockBadge] }).compileComponents();
  });

  it('renders badge AND plain line together for all three states — never color alone (AC-02, §13)', async () => {
    for (const state of STOCK_STATES) {
      const host = await render(state);
      const badge = host.querySelector('[data-testid="stock-badge"]');
      const line = host.querySelector('[data-testid="stock-line"]');
      expect(badge?.textContent?.trim()).toBe(BADGE_TEXT[state]);
      expect(line?.textContent?.trim()).toBe(EN_US_PLAIN_LINES[state]);
    }
  });

  it('takes its badge vocabulary from the generated tokens (Dependency rule 8)', () => {
    expect(BADGE_TEXT.cacheHit).toBe(tokens.status.cacheHit.badge);
    expect(BADGE_TEXT.warming).toBe(tokens.status.warming.badge);
    expect(BADGE_TEXT.cacheMiss).toBe(tokens.status.cacheMiss.badge);
  });

  it('carries a per-state class for the token status color, on top of the text', async () => {
    for (const state of STOCK_STATES) {
      const host = await render(state);
      const status = host.querySelector(`[data-testid="stock-status"]`);
      expect(status?.classList.contains(`stock-status-${state}`)).toBe(true);
    }
  });
});
