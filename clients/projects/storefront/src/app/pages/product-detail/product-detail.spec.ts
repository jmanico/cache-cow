/**
 * Product detail page tests (issue 067; CC-CAT-004, CC-CNT-006, CC-PRC-002).
 * Requirement tags per REQUIREMENTS.md §17: CC-CAT-004, CC-CNT-006,
 * CC-PRC-002 (CC-MKT-004's HTTP 404 status is server-owned, issues 025/026).
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { throwError } from 'rxjs';
import { CatalogApi } from '../../catalog/catalog.api';
import { TransactingContext } from '../../core/transacting-context';
import { ProductDetail } from './product-detail';

const ROUTES = [{ path: 'product/:sku', component: ProductDetail }];

async function open(url: string): Promise<HTMLElement> {
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  await harness.fixture.whenStable();
  return harness.routeNativeElement as HTMLElement;
}

describe('ProductDetail (issue 067)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter(ROUTES)],
    }).compileComponents();
  });

  it('renders every structured food section from typed fields only (AC-01/AC-02, CC-CAT-004)', async () => {
    const host = await open('/product/paneer-smoked-block');
    expect(host.querySelector('h1')?.textContent).toContain('Smoked Paneer Block');
    expect(host.querySelector('[data-testid="pdp-gallery"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="pdp-meta"]')?.textContent).toContain('0.7 kg');
    expect(host.querySelector('[data-testid="pdp-serves"]')?.textContent).toContain('3');
    expect(host.querySelector('[data-testid="pdp-ingredients"]')?.textContent).toContain('Paneer');
    // Typed allergen list (never free text).
    expect(host.querySelector('[data-testid="pdp-allergens-list"]')?.textContent).toContain('Milk');
    // Structured nutrition rows in a table.
    expect(host.querySelectorAll('[data-testid="pdp-nutrition"] tr').length).toBeGreaterThan(0);
    expect(host.querySelector('[data-testid="pdp-storage"]')?.textContent).toContain('frozen');
    // Per-format reheat instructions: oven / sous-vide / steam (DESIGN.md 10).
    const reheat = host.querySelector('[data-testid="pdp-reheat"]');
    expect(reheat?.querySelectorAll('h3').length).toBe(3);
  });

  it('states "no declared allergens" from the typed empty list, not free text (AC-02)', async () => {
    const host = await open('/product/jackfruit-pulled-smoked');
    expect(host.querySelector('[data-testid="pdp-allergens-none"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="pdp-allergens-list"]')).toBeNull();
  });

  it('shows the US price tax-exclusive with a tax note, and NO per-kg line (AC-06, CC-PRC-002)', async () => {
    const host = await open('/product/paneer-smoked-block');
    expect(host.querySelector('[data-testid="pdp-price"]')?.textContent).toBe('$29.00');
    expect(host.querySelector('[data-testid="pdp-tax-note"]')?.textContent).toContain('tax');
    expect(host.querySelector('[data-testid="pdp-unit-price"]')).toBeNull();
  });

  it('renders the DE unit price per kg if and only if the typed response carries it (AC-06, CC-PRC-002)', async () => {
    TestBed.inject(TransactingContext).setMarket('DE');
    const host = await open('/product/paneer-smoked-block');
    const unit = host.querySelector('[data-testid="pdp-unit-price"]');
    expect(unit).toBeTruthy();
    // round(2700 / 0.7 kg) = 3857 minor units, locale-formatted (en-US locale).
    expect(unit?.textContent).toContain('38.57');
    expect(unit?.textContent).toContain('kg');
  });

  it('shows the FSSAI green-in-square regulation mark for IN presentations (AC-03, CC-CNT-006)', async () => {
    TestBed.inject(TransactingContext).setMarket('IN');
    const host = await open('/product/paneer-smoked-block');
    const mark = host.querySelector('[data-testid="fssai-mark"]');
    expect(mark).toBeTruthy();
    // The regulation geometry: square outline plus filled circle, not a leaf.
    expect(mark?.querySelector('rect.fssai-square')).toBeTruthy();
    expect(mark?.querySelector('circle.fssai-dot')).toBeTruthy();
    expect(mark?.getAttribute('role')).toBe('img');
    expect(mark?.getAttribute('aria-label')?.length).toBeGreaterThan(0);
    expect(host.querySelector('[data-testid="leaf-dot"]')).toBeNull();
    // Negative (AC-04): no non-veg mark exists anywhere on the IN surface —
    // structurally, this client has no non-veg mark at all.
    expect(host.querySelector('[data-testid="nonveg-mark"]')).toBeNull();
  });

  it('shows the leaf-dot badge plus the word "Vegetarian" outside IN (AC-05, DESIGN.md 3.3)', async () => {
    const host = await open('/product/paneer-smoked-block');
    expect(host.querySelector('[data-testid="leaf-dot"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="fssai-mark"]')).toBeNull();
    expect(host.querySelector('[data-testid="veg-indicator"]')?.textContent).toContain('Vegetarian');
  });

  it('renders the 404 page for a SKU the gated catalog does not contain', async () => {
    const host = await open('/product/definitely-not-a-sku');
    expect(host.textContent).toContain('Signal lost');
    expect(host.querySelector('[data-testid="pdp-price"]')).toBeNull();
  });

  it('fails closed to a generic error state on a rejected response (AC-08, Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      providers: [
        provideRouter(ROUTES),
        {
          provide: CatalogApi,
          useValue: { getProductDetail: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const host = await open('/product/paneer-smoked-block');
    expect(host.querySelector('[data-testid="pdp-error"]')).toBeTruthy();
    expect(host.querySelector('[data-testid="pdp-ingredients"]')).toBeNull(); // no partial render
    expect(host.textContent).not.toContain('schema violation'); // generic message only
  });
});
