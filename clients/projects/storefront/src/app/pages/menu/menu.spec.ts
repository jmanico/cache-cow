/**
 * Menu page tests (issue 066; CC-CAT-003, CC-CAT-006, CC-MKT-007, and the
 * CC-MKT-003 IN negative case). Requirement tags per REQUIREMENTS.md §17:
 * CC-CAT-003, CC-CAT-006, CC-MKT-003, CC-MKT-007.
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { throwError } from 'rxjs';
import { routes } from '../../app.routes';
import { CatalogApi } from '../../catalog/catalog.api';
import { TransactingContext } from '../../core/transacting-context';
import { Menu } from './menu';

function skus(host: HTMLElement): string[] {
  return Array.from(host.querySelectorAll<HTMLElement>('[data-testid="product-card"]')).map(
    (card) => card.dataset['sku'] ?? '',
  );
}

async function createMenu() {
  const fixture = TestBed.createComponent(Menu);
  await fixture.whenStable();
  return fixture;
}

describe('Menu (issue 066)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Menu],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders the full US catalog including non-veg SKUs (AC-04, CC-MKT-007)', async () => {
    const fixture = await createMenu();
    const listed = skus(fixture.nativeElement as HTMLElement);
    expect(listed).toContain('brisket-whole-packer'); // non-veg present
    expect(listed).toContain('paneer-smoked-block'); // veg available
    expect(listed.length).toBe(7);
  });

  it('offers a single-toggle vegetarian filter that queries the seam (AC-03, CC-CAT-006)', async () => {
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    const toggle = host.querySelector<HTMLInputElement>('[data-testid="veg-filter"]');
    expect(toggle?.type).toBe('checkbox'); // single toggle

    toggle!.checked = true;
    toggle!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const listed = skus(host);
    expect(listed.sort()).toEqual([
      'jackfruit-pulled-smoked',
      'mushroom-king-oyster',
      'paneer-smoked-block',
    ]);
  });

  it('filters by cut through the seam (AC-03)', async () => {
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    const select = host.querySelector<HTMLSelectElement>('[data-testid="cut-filter"]');
    select!.value = 'ribs';
    select!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(skus(host)).toEqual(['ribs-st-louis']);
  });

  it('renders each card as ONE link with add-to-cart as a separate action inside (AC-01)', async () => {
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    const card = host.querySelector<HTMLElement>('[data-sku="brisket-whole-packer"]');
    const links = card!.querySelectorAll('a');
    expect(links.length).toBe(1);
    expect(links[0]?.getAttribute('href')).toBe('/product/brisket-whole-packer');
    const action = card!.querySelector<HTMLButtonElement>('[data-testid="card-action"]');
    expect(action?.tagName).toBe('BUTTON');
    expect(action?.closest('a')).toBeNull(); // separate action, not inside the link
  });

  it('pairs actions with states: add-to-cart when in stock, preorder when restocking, none when unavailable (AC-07)', async () => {
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    // US fixture: brisket=cacheHit, ribs=warming, mushroom=cacheMiss.
    const inStock = host.querySelector('[data-sku="brisket-whole-packer"] [data-testid="card-action"]');
    expect(inStock?.textContent).toContain('Add to cart');
    const restocking = host.querySelector('[data-sku="ribs-st-louis"] [data-testid="card-action"]');
    expect(restocking?.textContent).toContain('Preorder');
    const unavailable = host.querySelector('[data-sku="mushroom-king-oyster"]');
    expect(unavailable?.querySelector('[data-testid="card-action"]')).toBeNull();
  });

  it('renders card composition: name, mono cut/weight, locale price with tax note, badge, veg mark (AC-01/AC-02)', async () => {
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    const card = host.querySelector<HTMLElement>('[data-sku="paneer-smoked-block"]')!;
    expect(card.querySelector('.card-media')).toBeTruthy(); // 4:5 placeholder
    expect(card.querySelector('.card-name')?.textContent).toContain('Smoked Paneer Block');
    expect(card.querySelector('.card-meta')?.textContent).toContain('kg');
    expect(card.querySelector('[data-testid="card-price"]')?.textContent).toBe('$29.00');
    expect(card.querySelector('[data-testid="tax-note"]')?.textContent?.trim()).not.toBe(''); // CC-PRC-002 note
    expect(card.querySelector('[data-testid="stock-badge"]')?.textContent?.trim()).not.toBe('');
    expect(card.querySelector('[data-testid="stock-line"]')?.textContent?.trim()).not.toBe('');
    expect(card.querySelector('[data-testid="veg-indicator"]')).toBeTruthy();
    // Non-veg card carries NO veg indicator (and no non-veg mark exists at all).
    const nonVeg = host.querySelector<HTMLElement>('[data-sku="brisket-whole-packer"]')!;
    expect(nonVeg.querySelector('[data-testid="veg-indicator"]')).toBeNull();
  });

  it('shows only what the server-gated IN response contains — no non-veg SKU in any state (AC-05, CC-MKT-003 negative)', async () => {
    TestBed.inject(TransactingContext).setMarket('IN');
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    const listed = skus(host);
    expect(listed.sort()).toEqual([
      'jackfruit-pulled-smoked',
      'mushroom-king-oyster',
      'paneer-smoked-block',
    ]);
    // No cache-miss placeholder for beef either: the SKU is absent entirely
    // (DESIGN.md 5.2 — beef never renders as unavailable-in-region in IN).
    expect(host.querySelector('[data-sku="brisket-whole-packer"]')).toBeNull();
    // The cut filter vocabulary never offers a non-veg cut in IN (server data).
    const options = Array.from(
      host.querySelectorAll<HTMLOptionElement>('[data-testid="cut-filter"] option'),
    ).map((option) => option.value);
    expect(options).not.toContain('brisket');
    expect(options).toContain('paneer');
  });

  it('seeds the cut filter from the ?cut= query param the Cuts diagram links with (issue 075, CC-CNT-003)', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/menu?cut=ribs', Menu);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    // The seam ran the filter server-side; the control reflects the state.
    expect(skus(host)).toEqual(['ribs-st-louis']);
    expect(host.querySelector<HTMLSelectElement>('[data-testid="cut-filter"]')?.value).toBe('ribs');
  });

  it('ignores an unrecognized ?cut= value rather than forwarding it (untrusted input)', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/menu?cut=../../etc/passwd', Menu);
    await harness.fixture.whenStable();
    const host = harness.routeNativeElement as HTMLElement;

    // No filter applied; the full gated listing renders.
    expect(skus(host).length).toBe(7);
    expect(host.querySelector<HTMLSelectElement>('[data-testid="cut-filter"]')?.value).toBe('');
  });

  it('fails closed to a generic error state when the seam rejects the response (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Menu],
      providers: [
        provideRouter([]),
        {
          provide: CatalogApi,
          useValue: { getListing: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const fixture = await createMenu();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="menu-error"]')).toBeTruthy();
    expect(host.querySelectorAll('[data-testid="product-card"]').length).toBe(0);
    // Generic message only — never the raw error body (SECURITY.md, Logging rule 7).
    expect(host.textContent).not.toContain('schema violation');
  });
});
