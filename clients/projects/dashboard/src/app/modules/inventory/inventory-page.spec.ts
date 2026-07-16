/**
 * Inventory-by-cold-store page tests (issue 084).
 * Requirement tags: CC-DSH-003, CC-CAT-002, CC-CAT-003, CC-DSH-006,
 * CC-SEC-001, CC-NFR-004 (REQUIREMENTS.md §17).
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { InventoryPage } from './inventory-page';
import { InventoryApi } from './inventory.api';
import { InventoryView } from './inventory.types';
import tokens from '../../../../../../tokens/dist/tokens.json';

async function render(providers: Provider[] = []): Promise<ComponentFixture<InventoryPage>> {
  await TestBed.configureTestingModule({ imports: [InventoryPage], providers }).compileComponents();
  const fixture = TestBed.createComponent(InventoryPage);
  await fixture.whenStable();
  return fixture;
}

function host(fixture: ComponentFixture<InventoryPage>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

async function setFilter(
  fixture: ComponentFixture<InventoryPage>,
  testid: string,
  value: string,
): Promise<void> {
  const control = host(fixture).querySelector<HTMLSelectElement>(`[data-testid="${testid}"]`)!;
  control.value = value;
  control.dispatchEvent(new Event('change'));
  await fixture.whenStable();
}

describe('InventoryPage — per-store per-SKU rows (CC-CAT-002, AC-01)', () => {
  it('renders typed inventory rows from the seam on load', async () => {
    const fixture = await render();
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid="inventory-table"] tbody tr').length).toBeGreaterThan(0);
    expect(el.textContent).toContain('Austin cold store');
    expect(el.textContent).toContain('BRISKET-WHOLE-5KG');
  });

  it('is read-only: no action buttons or mutation controls anywhere (AC-07)', async () => {
    const fixture = await render();
    const el = host(fixture);
    // The only controls are the filters; no button element exists at all.
    expect(el.querySelector('button')).toBeNull();
  });

  it('fails closed on a rejected payload: generic error, no rows (CC-SEC-001)', async () => {
    const failing: Pick<InventoryApi, 'getInventory'> = {
      getInventory: (): Observable<InventoryView> =>
        throwError(() => new Error('Response failed schema validation: rows[0].availability')),
    };
    const fixture = await render([{ provide: InventoryApi, useValue: failing }]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="load-error"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="inventory-table"]')).toBeNull();
    // Never a stale or fabricated inventory value; never a raw error body.
    expect(el.textContent).not.toContain('schema validation');
  });
});

describe('InventoryPage — availability badges (CC-CAT-003; DESIGN.md §§5.2, 13)', () => {
  it('renders the cache vocabulary ALWAYS paired with its plain line', async () => {
    const fixture = await render();
    const el = host(fixture);
    // Badge + plain text, never the badge alone — status is never color-only.
    // Badge words assert against the generated tokens, never a local copy
    // (ARCHITECTURE.md Dependency rule 8) — the same discipline the
    // storefront's stock badge follows.
    expect(el.textContent).toContain(tokens.status.cacheHit.badge);
    expect(el.textContent).toContain('In stock');
    expect(el.textContent).toContain(tokens.status.warming.badge);
    expect(el.textContent).toContain('Restocking');
  });

  it('pairs every rendered badge with a plain-language sibling (§13)', async () => {
    const fixture = await render();
    const badges = host(fixture).querySelectorAll('.cc-badge');
    expect(badges.length).toBeGreaterThan(0);
    for (const badge of badges) {
      const plain = badge.nextElementSibling;
      expect(plain?.classList.contains('availability-plain')).toBe(true);
      expect((plain?.textContent ?? '').trim().length).toBeGreaterThan(0);
    }
  });

  it('maps each state to one accent family per meaning (DESIGN.md §12)', async () => {
    const fixture = await render();
    const el = host(fixture);
    const cell = (testid: string): Element =>
      el.querySelector(`[data-testid="${testid}"]`)!.querySelectorAll('td')[5];

    // in-stock => cache green; restocking => ember; unavailable => smoke.
    expect(cell('inventory-row-CS-US-TX1-BRISKET-WHOLE-5KG').querySelector('.cc-badge')!.classList)
      .toContain('cc-status-good');
    expect(cell('inventory-row-CS-US-TX1-RIBS-STLOUIS-2KG').querySelector('.cc-badge')!.classList)
      .toContain('cc-status-alert');
    expect(cell('inventory-row-CS-MX-MX1-PANEER-SMOKED-500G').querySelector('.cc-badge')!.classList)
      .toContain('cc-status-neutral');
  });

  it('renders the cache-miss badge only where the server said unavailable-in-region', async () => {
    const fixture = await render();
    const el = host(fixture);
    const misses = [...el.querySelectorAll('.cc-badge')].filter(
      (b) => b.textContent?.trim() === tokens.status.cacheMiss.badge,
    );
    expect(misses.length).toBe(1);
    // The client never derives this from onHandUnits: the restocking row is
    // also at 0 on hand and must NOT read as a miss (DESIGN.md 5.2).
    const restocking = el.querySelector('[data-testid="inventory-row-CS-US-TX1-RIBS-STLOUIS-2KG"]')!;
    expect(restocking.querySelectorAll('td')[4].textContent?.trim()).toBe('0');
    expect(restocking.textContent).toContain(tokens.status.warming.badge);
  });
});

describe('InventoryPage — service level / "cache hit rate" column (CC-DSH-006)', () => {
  it('renders server basis points as a mono right-aligned percentage', async () => {
    const fixture = await render();
    const el = host(fixture);
    const cell = el.querySelector('[data-testid="hit-rate-CS-US-TX1-BRISKET-WHOLE-5KG"]')!;
    expect(cell.textContent?.trim()).toBe('98.6%');
    // .cc-num carries Plex Mono + right alignment (DESIGN.md §12).
    expect(cell.classList.contains('cc-num')).toBe(true);
  });

  it('renders the range ends exactly (100% and 0%)', async () => {
    const fixture = await render();
    const el = host(fixture);
    expect(
      el.querySelector('[data-testid="hit-rate-CS-JP-OS1-BRISKET-WHOLE-5KG"]')?.textContent?.trim(),
    ).toBe('100%');
    expect(
      el.querySelector('[data-testid="hit-rate-CS-MX-MX1-PANEER-SMOKED-500G"]')?.textContent?.trim(),
    ).toBe('0%');
  });

  it('puts the unit in the column header, not the cells (DESIGN.md §12)', async () => {
    const fixture = await render();
    const headers = [...host(fixture).querySelectorAll('thead th')].map(
      (h) => h.textContent?.trim() ?? '',
    );
    expect(headers).toContain('Hit rate (%)');
    expect(headers).toContain('On hand (units)');
  });

  it('sets numeric columns to .cc-num and identifier columns to .cc-mono', async () => {
    const fixture = await render();
    const row = host(fixture).querySelector('[data-testid="inventory-row-CS-US-TX1-BRISKET-WHOLE-5KG"]')!;
    const cells = row.querySelectorAll('td');
    expect(cells[2].classList.contains('cc-mono')).toBe(true); // SKU
    expect(cells[4].classList.contains('cc-num')).toBe(true); // on hand
    expect(cells[6].classList.contains('cc-num')).toBe(true); // hit rate
  });
});

describe('InventoryPage — filtering (AC-02)', () => {
  it('filters rows by market through the seam', async () => {
    const fixture = await render();
    await setFilter(fixture, 'filter-market', 'IN');
    const el = host(fixture);
    expect(el.textContent).toContain('Pune cold store');
    expect(el.textContent).not.toContain('Austin cold store');
  });

  it('filters rows by cold store through the seam', async () => {
    const fixture = await render();
    await setFilter(fixture, 'filter-store', 'CS-JP-OS1');
    const el = host(fixture);
    const rows = el.querySelectorAll('[data-testid="inventory-table"] tbody tr');
    expect(rows.length).toBe(1);
    expect(el.textContent).toContain('Osaka cold store');
  });

  it('narrows the store options to the chosen market without emptying the picker', async () => {
    const fixture = await render();
    await setFilter(fixture, 'filter-market', 'IN');
    const options = host(fixture).querySelectorAll('[data-testid="filter-store"] option');
    // "Any" + the single IN store.
    expect(options.length).toBe(2);
    expect(options[1].textContent).toContain('Pune cold store');
  });

  it('filters by SKU substring', async () => {
    const fixture = await render();
    const input = host(fixture).querySelector<HTMLInputElement>('[data-testid="filter-sku"]')!;
    input.value = 'PANEER';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    const el = host(fixture);
    expect(el.textContent).toContain('Smoked paneer');
    expect(el.textContent).not.toContain('BRISKET-WHOLE-5KG');
  });

  it('shows a plain empty state when nothing matches — no fabricated rows', async () => {
    const fixture = await render();
    const input = host(fixture).querySelector<HTMLInputElement>('[data-testid="filter-sku"]')!;
    input.value = 'NO-SUCH-SKU';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    const el = host(fixture);
    expect(el.querySelector('[data-testid="inventory-empty"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="inventory-table"]')).toBeNull();
  });
});

describe('InventoryPage — keyboard-first navigation (DESIGN.md §§12, 13; CC-NFR-004)', () => {
  function rows(fixture: ComponentFixture<InventoryPage>): HTMLTableRowElement[] {
    return [
      ...host(fixture).querySelectorAll<HTMLTableRowElement>(
        '[data-testid="inventory-table"] tbody tr',
      ),
    ];
  }

  it('puts exactly one row in the tab order (roving tabindex)', async () => {
    const fixture = await render();
    expect(rows(fixture).filter((r) => r.tabIndex === 0).length).toBe(1);
  });

  it('ArrowDown moves focus between rows in this read-only table', async () => {
    const fixture = await render();
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(all[1]);
    expect(all[1].tabIndex).toBe(0);
  });

  it('filters are labeled native controls, operable without a mouse', async () => {
    const fixture = await render();
    const el = host(fixture);
    for (const testid of ['filter-market', 'filter-store', 'filter-sku']) {
      const control = el.querySelector(`[data-testid="${testid}"]`)!;
      // Each control sits inside its own <label>, so it is named and clickable
      // by label as well as reachable by Tab.
      expect(control.closest('label')).not.toBeNull();
    }
  });
});
