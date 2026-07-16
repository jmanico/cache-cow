/**
 * Order management page tests (issue 082).
 * Requirement tags: CC-DSH-003, CC-ORD-006, CC-DSH-002, CC-DSH-001,
 * CC-PRC-003/004, CC-SEC-001, CC-NFR-004 (REQUIREMENTS.md §17).
 *
 * The action matrix used throughout is TEST_ACTION_PERMISSION_MATRIX
 * (core/testing.ts) — a clearly-marked FIXTURE, not policy. Which roles may
 * transition or refund is an issue 080 human decision; these tests verify
 * only that the page honors whatever matrix is authored.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { provideActionPermissionMatrix } from '../../core/action-permissions';
import { DashboardRole, provideStaffSessionSource } from '../../core/staff-session';
import { TEST_ACTION_PERMISSION_MATRIX, TestStaffSessionSource } from '../../core/testing';
import { OrdersPage } from './orders-page';
import { OrdersApi } from './orders.api';
import { OrderDetail, OrderSearchQuery, OrderSearchResult, OrderState } from './orders.types';

/**
 * A stub seam that returns EXACTLY what the test dictates, so the page's
 * "render only what the server offered" contract can be tested against a
 * server answer that deliberately disagrees with the real CC-ORD-006 machine.
 */
class StubOrdersApi extends OrdersApi {
  searchCalls: OrderSearchQuery[] = [];
  transitionCalls: { orderRef: string; to: OrderState }[] = [];
  refundCalls: string[] = [];

  constructor(private detail: OrderDetail) {
    super();
  }

  override search(query: OrderSearchQuery): Observable<OrderSearchResult> {
    this.searchCalls.push(query);
    const { history, allowedTransitions, refundEligible, ...summary } = this.detail;
    void history;
    void allowedTransitions;
    void refundEligible;
    return of({ orders: [summary] });
  }

  override getOrder(): Observable<OrderDetail | null> {
    return of(this.detail);
  }

  override transition(orderRef: string, to: OrderState): Observable<OrderDetail> {
    this.transitionCalls.push({ orderRef, to });
    return of(this.detail);
  }

  override refund(orderRef: string): Observable<OrderDetail> {
    this.refundCalls.push(orderRef);
    return of(this.detail);
  }
}

function detailFixture(overrides: Partial<OrderDetail> = {}): OrderDetail {
  return {
    orderRef: 'ORD-2026-000141',
    market: 'US',
    state: 'received',
    placedAt: '2026-07-12T14:05:00Z',
    itemCount: 3,
    totalMinor: 14_900,
    currency: 'USD',
    history: [{ state: 'received', at: '2026-07-12T14:05:00Z', actor: 'system' }],
    allowedTransitions: ['confirmed', 'cancelled'],
    refundEligible: true,
    ...overrides,
  };
}

async function render(providers: Provider[] = []): Promise<ComponentFixture<OrdersPage>> {
  await TestBed.configureTestingModule({
    imports: [OrdersPage],
    providers,
  }).compileComponents();
  const fixture = TestBed.createComponent(OrdersPage);
  await fixture.whenStable();
  return fixture;
}

/** Signed in with the given roles, under the TEST action matrix. */
function session(...roles: DashboardRole[]): Provider[] {
  const source = new TestStaffSessionSource();
  source.authenticateAs(roles);
  return [
    provideStaffSessionSource(source),
    provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
  ];
}

function host(fixture: ComponentFixture<OrdersPage>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

async function openDetail(fixture: ComponentFixture<OrdersPage>, ref: string): Promise<void> {
  host(fixture).querySelector<HTMLElement>(`[data-testid="order-row-${ref}"]`)!.click();
  await fixture.whenStable();
}

describe('OrdersPage — search results (CC-DSH-003, AC-01)', () => {
  it('renders typed rows from the seam on load', async () => {
    const fixture = await render();
    const el = host(fixture);
    const rows = el.querySelectorAll('[data-testid="order-results"] tbody tr');
    expect(rows.length).toBeGreaterThan(0);
    expect(el.textContent).toContain('ORD-2026-000141');
  });

  it('renders money from integer minor units, per currency (CC-PRC-003/004)', async () => {
    const fixture = await render();
    const el = host(fixture);
    // USD two-decimal, JPY zero-decimal — from the currency data, not code.
    expect(el.textContent).toContain('$149.00');
    expect(el.textContent).toContain('¥14,900');
  });

  it('sets every numeral in the mono right-aligned column class (DESIGN.md §12)', async () => {
    const fixture = await render();
    const el = host(fixture);
    const row = el.querySelector('[data-testid="order-row-ORD-2026-000141"]')!;
    const cells = row.querySelectorAll('td');
    // Items and total are the numeric columns.
    expect(cells[4].classList.contains('cc-num')).toBe(true);
    expect(cells[5].classList.contains('cc-num')).toBe(true);
    // Order refs are Plex Mono identifiers, not right-aligned quantities.
    expect(cells[0].classList.contains('cc-mono')).toBe(true);
  });

  it('puts units in the column headers, not the cells (DESIGN.md §12)', async () => {
    const fixture = await render();
    const el = host(fixture);
    const headers = [...el.querySelectorAll('[data-testid="order-results"] thead th')].map(
      (h) => h.textContent?.trim() ?? '',
    );
    expect(headers).toContain('Items (count)');
    const row = el.querySelector('[data-testid="order-row-ORD-2026-000141"]')!;
    expect(row.querySelectorAll('td')[4].textContent?.trim()).toBe('3');
  });

  it('filters through the seam: market narrows the result set server-side', async () => {
    const fixture = await render();
    const el = host(fixture);
    const select = el.querySelector<HTMLSelectElement>('[data-testid="search-market"]')!;
    select.value = 'JP';
    select.dispatchEvent(new Event('change'));
    el.querySelector<HTMLFormElement>('[data-testid="order-search"]')!.dispatchEvent(
      new Event('submit'),
    );
    await fixture.whenStable();
    expect(el.textContent).toContain('ORD-2026-000132');
    expect(el.textContent).not.toContain('ORD-2026-000141');
  });

  it('fails closed on a rejected payload: generic error, no rows (CC-SEC-001)', async () => {
    class FailingApi extends StubOrdersApi {
      override search(): Observable<OrderSearchResult> {
        return throwError(() => new Error('Response failed schema validation'));
      }
    }
    const fixture = await render([{ provide: OrdersApi, useValue: new FailingApi(detailFixture()) }]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="load-error"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="order-results"]')).toBeNull();
    // Generic copy only — no raw error body (SECURITY.md, Logging rule 1).
    expect(el.textContent).not.toContain('schema validation');
  });
});

describe('OrdersPage — state history (CC-ORD-006, CC-DSH-004)', () => {
  it('renders the server-recorded history with actor and timestamp', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'ORD-2026-000132');
    const el = host(fixture);
    const history = el.querySelector('[data-testid="state-history"]')!;
    expect(history.querySelectorAll('tbody tr').length).toBe(3); // received/confirmed/packed
    expect(history.textContent).toContain('Received');
    expect(history.textContent).toContain('Packed');
  });
});

describe('OrdersPage — transitions render ONLY server-provided states (AC-02/AC-03)', () => {
  it('renders exactly one button per allowedTransitions entry', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'ORD-2026-000141'); // received
    const el = host(fixture);
    expect(el.querySelector('[data-testid="transition-confirmed"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="transition-cancelled"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(2);
  });

  it('follows the SERVER even when it offers less than the CC-ORD-006 machine would', async () => {
    // The real machine allows received -> confirmed|cancelled. This server
    // answer offers ONLY cancelled: the client must not add `confirmed` back
    // from any legality of its own (issue 082 Zero Trust Consideration).
    const api = new StubOrdersApi(detailFixture({ allowedTransitions: ['cancelled'] }));
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(1);
    expect(el.querySelector('[data-testid="transition-cancelled"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="transition-confirmed"]')).toBeNull();
  });

  it('renders NO transition buttons for a terminal order (empty allowedTransitions)', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'ORD-2026-000104'); // cancelled — terminal
    expect(host(fixture).querySelectorAll('[data-testid^="transition-"]').length).toBe(0);
  });

  it('sends the server-offered target verbatim to the seam', async () => {
    const api = new StubOrdersApi(detailFixture({ allowedTransitions: ['confirmed'] }));
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    host(fixture).querySelector<HTMLButtonElement>('[data-testid="transition-confirmed"]')!.click();
    await fixture.whenStable();
    expect(api.transitionCalls).toEqual([{ orderRef: 'ORD-2026-000141', to: 'confirmed' }]);
  });

  it('shows generic copy and no state change when the server rejects a transition', async () => {
    class RejectingApi extends StubOrdersApi {
      override transition(): Observable<OrderDetail> {
        return throwError(() => new Error('Request rejected'));
      }
    }
    const api = new RejectingApi(detailFixture({ allowedTransitions: ['confirmed'] }));
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    host(fixture).querySelector<HTMLButtonElement>('[data-testid="transition-confirmed"]')!.click();
    await fixture.whenStable();
    const el = host(fixture);
    expect(el.querySelector('[data-testid="action-error"]')).not.toBeNull();
    expect(el.textContent).toContain('No changes were made');
  });
});

describe('OrdersPage — refund requires step-up FIRST (AC-04; CC-DSH-001)', () => {
  it('opens the step-up prompt and does NOT call the seam yet', async () => {
    const api = new StubOrdersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    expect(el.querySelector('[data-testid="step-up-prompt"]')).toBeNull();

    el.querySelector<HTMLButtonElement>('[data-testid="refund-action"]')!.click();
    await fixture.whenStable();

    expect(el.querySelector('[data-testid="step-up-prompt"]')).not.toBeNull();
    // The money path has NOT been touched: step-up precedes the seam call.
    expect(api.refundCalls).toEqual([]);
  });

  it('calls the refund seam only after step-up is confirmed', async () => {
    const api = new StubOrdersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="refund-action"]')!.click();
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('[data-testid="step-up-confirm"]')!.click();
    await fixture.whenStable();
    expect(api.refundCalls).toEqual(['ORD-2026-000141']);
  });

  it('cancelling step-up abandons the refund entirely (negative case)', async () => {
    const api = new StubOrdersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="refund-action"]')!.click();
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('[data-testid="step-up-cancel"]')!.click();
    await fixture.whenStable();
    expect(api.refundCalls).toEqual([]);
    expect(el.querySelector('[data-testid="step-up-prompt"]')).toBeNull();
  });

  it('offers no refund when the server says the order is not refund-eligible', async () => {
    const api = new StubOrdersApi(detailFixture({ refundEligible: false }));
    const fixture = await render([...session('admin'), { provide: OrdersApi, useValue: api }]);
    await openDetail(fixture, 'ORD-2026-000141');
    expect(host(fixture).querySelector('[data-testid="refund-action"]')).toBeNull();
  });
});

describe('OrdersPage — per-action permission gating (CC-DSH-002, AC-06)', () => {
  it('with NO authored action matrix, shows no actions — even to admin (fail closed)', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([provideStaffSessionSource(source)]); // no matrix
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(0);
    expect(el.querySelector('[data-testid="refund-action"]')).toBeNull();
  });

  it('ops-agent (TEST matrix: transition, no refund) sees transitions but no refund', async () => {
    const fixture = await render(session('ops-agent'));
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(2);
    expect(el.querySelector('[data-testid="refund-action"]')).toBeNull();
  });

  it('finance (TEST matrix: refund, no transition) sees the refund but no transitions', async () => {
    const fixture = await render(session('finance'));
    await openDetail(fixture, 'ORD-2026-000138'); // confirmed => refund-eligible
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(0);
    expect(el.querySelector('[data-testid="refund-action"]')).not.toBeNull();
  });

  it('sales-viewer (absent from the TEST matrix) sees no actions at all', async () => {
    const fixture = await render(session('sales-viewer'));
    await openDetail(fixture, 'ORD-2026-000141');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="transition-"]').length).toBe(0);
    expect(el.querySelector('[data-testid="refund-action"]')).toBeNull();
  });
});

describe('OrdersPage — keyboard-first navigation (DESIGN.md §§12, 13; CC-NFR-004)', () => {
  function rows(fixture: ComponentFixture<OrdersPage>): HTMLTableRowElement[] {
    return [
      ...host(fixture).querySelectorAll<HTMLTableRowElement>(
        '[data-testid="order-results"] tbody tr',
      ),
    ];
  }

  it('puts exactly one row in the tab order (roving tabindex)', async () => {
    const fixture = await render();
    const tabbable = rows(fixture).filter((r) => r.tabIndex === 0);
    expect(tabbable.length).toBe(1);
    expect(rows(fixture)[0].tabIndex).toBe(0);
  });

  it('ArrowDown/ArrowUp move focus and the roving tab stop between rows', async () => {
    const fixture = await render();
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(all[1]);
    expect(all[1].tabIndex).toBe(0);
    expect(all[0].tabIndex).toBe(-1);

    all[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }));
    expect(document.activeElement).toBe(all[0]);
  });

  it('Home/End jump to the first and last row', async () => {
    const fixture = await render();
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    expect(document.activeElement).toBe(all[all.length - 1]);

    all[all.length - 1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    expect(document.activeElement).toBe(all[0]);
  });

  it('ArrowDown does not run past the last row', async () => {
    const fixture = await render();
    const all = rows(fixture);
    const last = all[all.length - 1];
    last.focus();
    last.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(last);
  });

  it('Enter on a focused row opens its detail — no mouse required', async () => {
    const fixture = await render(session('admin'));
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await fixture.whenStable();
    expect(host(fixture).querySelector('[data-testid="order-detail"]')).not.toBeNull();
  });
});
