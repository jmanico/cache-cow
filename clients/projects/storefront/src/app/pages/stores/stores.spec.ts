/**
 * Store locator tests (issue 078; DESIGN.md §7/§10).
 * Requirement tags per REQUIREMENTS.md §17: CC-WHS-003 (no wholesale data on
 * a consumer surface), CC-MKT-002 (market-scoped rendering), CC-NFR-004.
 */

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TransactingContext } from '../../core/transacting-context';
import { StoresApi } from '../../stores/stores.api';
import { Stores } from './stores';

function storeIds(host: HTMLElement): string[] {
  return Array.from(host.querySelectorAll<HTMLElement>('[data-testid="store-item"]')).map(
    (item) => item.dataset['store'] ?? '',
  );
}

async function createStores() {
  const fixture = TestBed.createComponent(Stores);
  await fixture.whenStable();
  return fixture;
}

describe('Stores (issue 078)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Stores],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('lists the transacting market’s retail partners with retailer, address lines and locality (AC-01)', async () => {
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;

    expect(storeIds(host)).toEqual([
      'us-hillside-market-austin',
      'us-brandt-grocery-kansas-city',
    ]);
    const item = host.querySelector<HTMLElement>('[data-store="us-hillside-market-austin"]')!;
    expect(item.querySelector('[data-testid="store-retailer"]')?.textContent).toContain(
      'Hillside Market',
    );
    expect(item.querySelector('[data-testid="store-address"]')?.textContent).toContain(
      '1200 South Congress Avenue',
    );
    expect(item.querySelector('[data-testid="store-locality"]')?.textContent).toContain('Austin');
  });

  it('re-renders to the newly selected market and shows no other market’s partners (AC-02)', async () => {
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;
    const context = TestBed.inject(TransactingContext);

    context.setMarket('DE');
    await fixture.whenStable();
    expect(storeIds(host)).toEqual(['de-kuehlhaus-markt-berlin', 'de-brandt-feinkost-muenchen']);
    expect(host.textContent).not.toContain('Hillside Market');

    context.setMarket('IN');
    await fixture.whenStable();
    expect(storeIds(host)).toEqual(['in-green-cellar-bengaluru']);
    expect(host.textContent).not.toContain('Kühlhaus Markt');
  });

  it('loads NO map script, tile, iframe or other third-party asset (AC-07 negative)', async () => {
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;

    // The map is excluded pending the open provider decision (issue 078).
    expect(host.querySelector('script')).toBeNull();
    expect(host.querySelector('iframe')).toBeNull();
    expect(host.querySelector('img')).toBeNull();
    expect(host.querySelector('canvas')).toBeNull();
    expect(host.querySelector('[data-testid="stores-map"]')).toBeNull();
    // No remote origin referenced anywhere in the rendered markup.
    expect(host.innerHTML).not.toContain('http://');
    expect(host.innerHTML).not.toContain('https://');
  });

  it('renders only public listing fields — never wholesale data (AC-03, CC-WHS-003 negative)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Stores],
      providers: [
        provideRouter([]),
        {
          provide: StoresApi,
          useValue: {
            // Even if a server response over-shared, the typed projection
            // never constructs those fields, so they cannot reach the page.
            getStoreLocations: () =>
              of({
                market: 'US' as const,
                locations: [
                  {
                    id: 'us-test',
                    retailer: 'Test Grocer',
                    addressLines: ['1 Test Street'],
                    locality: 'Austin, TX 78704',
                  },
                ],
              }),
          },
        },
      ],
    }).compileComponents();
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;

    const markup = host.innerHTML;
    for (const leak of ['wholesale', 'net-60', 'casePrice', 'terms', 'priceList']) {
      expect(markup).not.toContain(leak);
    }
    expect(host.querySelector('[data-testid="store-retailer"]')?.textContent).toContain(
      'Test Grocer',
    );
  });

  it('renders a labelled list that is keyboard-navigable as plain content (AC-06, DESIGN.md §13)', async () => {
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;
    const list = host.querySelector('[data-testid="stores-list"]')!;
    // A real list with an accessible name; no custom widget to trap focus.
    expect(list.tagName).toBe('UL');
    expect(list.getAttribute('aria-label')).toBe('Retail partners');
    expect(host.querySelector('[tabindex]')).toBeNull();
  });

  it('shows the empty state when the market has no partners', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Stores],
      providers: [
        provideRouter([]),
        {
          provide: StoresApi,
          useValue: { getStoreLocations: () => of({ market: 'JP' as const, locations: [] }) },
        },
      ],
    }).compileComponents();
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="stores-empty"]')).toBeTruthy();
    expect(storeIds(host)).toEqual([]);
  });

  it('fails closed to a generic error rather than an unfiltered list (Failure Behavior)', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Stores],
      providers: [
        provideRouter([]),
        {
          provide: StoresApi,
          useValue: { getStoreLocations: () => throwError(() => new Error('schema violation')) },
        },
      ],
    }).compileComponents();
    const fixture = await createStores();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="stores-error"]')).toBeTruthy();
    expect(storeIds(host)).toEqual([]);
    expect(host.textContent).not.toContain('schema violation');
  });
});
