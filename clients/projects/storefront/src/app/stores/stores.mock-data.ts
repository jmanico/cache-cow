/**
 * Mock store-locator fixture (issue 078) — a stand-in for the SERVER's
 * consumer read endpoint for retail partner locations until that endpoint
 * exists (partner records: issue 049; dashboard partner management: issue
 * 085).
 *
 * This module SIMULATES THE SERVER: the per-market filtering below is the
 * mock analogue of the server-side market scoping of issue 078 AC-01 — the
 * client receives ONLY the transacting market's partners and never filters a
 * cross-market list itself (ARCHITECTURE.md, Dependency rule 1).
 *
 * The fixture carries only public listing fields (retailer, address lines,
 * locality) — the same allowlist the typed projection declares. No wholesale
 * price, term, or partner-tenancy field appears here even as mock data
 * (CC-WHS-003).
 *
 * All entries are PLACEHOLDER: the partner-location data source, and the
 * public field set per location (hours? phone?), are open questions in issue
 * 078. No coordinates: no map ships until the provider decision lands.
 */

import { Market } from '../core/transacting-context';

interface MockStore {
  readonly id: string;
  readonly retailer: string;
  readonly addressLines: readonly string[];
  readonly locality: string;
}

/**
 * Per-market retail partners (placeholder). Address lines follow each
 * market's conventional ordering — the real per-market address formatting is
 * server-side (CC-ORD-002); the client renders the lines it is handed.
 */
const STORES: Readonly<Record<Market, readonly MockStore[]>> = {
  US: [
    {
      id: 'us-hillside-market-austin',
      retailer: 'Hillside Market',
      addressLines: ['1200 South Congress Avenue'],
      locality: 'Austin, TX 78704',
    },
    {
      id: 'us-brandt-grocery-kansas-city',
      retailer: 'Brandt Grocery',
      addressLines: ['418 West 9th Street'],
      locality: 'Kansas City, MO 64105',
    },
  ],
  ES: [
    {
      id: 'es-mercado-del-norte-madrid',
      retailer: 'Mercado del Norte',
      addressLines: ['Calle de Alcalá, 84'],
      locality: '28009 Madrid',
    },
  ],
  MX: [
    {
      id: 'mx-super-la-loma-monterrey',
      retailer: 'Súper La Loma',
      addressLines: ['Avenida Constitución 1450'],
      locality: '64000 Monterrey, N.L.',
    },
  ],
  DE: [
    {
      id: 'de-kuehlhaus-markt-berlin',
      retailer: 'Kühlhaus Markt',
      addressLines: ['Kastanienallee 47'],
      locality: '10119 Berlin',
    },
    {
      id: 'de-brandt-feinkost-muenchen',
      retailer: 'Brandt Feinkost',
      addressLines: ['Leopoldstraße 112'],
      locality: '80802 München',
    },
  ],
  JP: [
    {
      id: 'jp-tanaka-foods-tokyo',
      retailer: '田中フーズ 代官山店',
      addressLines: ['東京都渋谷区代官山町 12-3'],
      locality: '150-0034',
    },
  ],
  IN: [
    {
      id: 'in-green-cellar-bengaluru',
      retailer: 'Green Cellar',
      addressLines: ['4th Block, 100 Feet Road, Koramangala'],
      locality: 'Bengaluru, Karnataka 560034',
    },
  ],
};

/**
 * Mock server response for the transacting market's retail partners.
 * Returned as `unknown`: the StoresApi seam validates it like any untrusted
 * response.
 */
export function mockStoreLocationsResponse(market: Market): unknown {
  return {
    market,
    locations: STORES[market].map((store) => ({
      id: store.id,
      retailer: store.retailer,
      addressLines: store.addressLines,
      locality: store.locality,
    })),
  };
}
