/**
 * Partner management page tests (issue 085).
 * Requirement tags: CC-DSH-003, CC-WHS-002, CC-WHS-004, CC-DSH-002,
 * CC-SEC-001, CC-NFR-004 (REQUIREMENTS.md §17).
 *
 * The action matrix used throughout is TEST_ACTION_PERMISSION_MATRIX
 * (core/testing.ts) — a clearly-marked FIXTURE, not policy. Which roles may
 * approve partners is an issue 080 human decision; these tests verify only
 * that the page honors whatever matrix is authored.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Provider } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { provideActionPermissionMatrix } from '../../core/action-permissions';
import { DashboardRole, provideStaffSessionSource } from '../../core/staff-session';
import { TEST_ACTION_PERMISSION_MATRIX, TestStaffSessionSource } from '../../core/testing';
import { PartnersPage } from './partners-page';
import { PartnersApi } from './partners.api';
import { PartnerAction, PartnerDetail, PartnerListResult } from './partners.types';

/** A stub seam returning exactly what the test dictates. */
class StubPartnersApi extends PartnersApi {
  actCalls: { partnerRef: string; action: PartnerAction }[] = [];

  constructor(private detail: PartnerDetail) {
    super();
  }

  override list(): Observable<PartnerListResult> {
    const { businessIdentities, paymentTermsDays, allowedActions, ...summary } = this.detail;
    void businessIdentities;
    void paymentTermsDays;
    void allowedActions;
    return of({ partners: [summary] });
  }

  override getPartner(): Observable<PartnerDetail | null> {
    return of(this.detail);
  }

  override act(partnerRef: string, action: PartnerAction): Observable<PartnerDetail> {
    this.actCalls.push({ partnerRef, action });
    return of(this.detail);
  }
}

function detailFixture(overrides: Partial<PartnerDetail> = {}): PartnerDetail {
  return {
    partnerRef: 'WHP-000117',
    name: 'Nordwind Feinkost GmbH',
    market: 'DE',
    state: 'pending',
    appliedAt: '2026-07-09T08:15:00Z',
    businessIdentities: [{ kind: 'USt-IdNr.', maskedValue: '•••••••••4821' }],
    paymentTermsDays: 60,
    allowedActions: ['approve', 'reject'],
    ...overrides,
  };
}

async function render(providers: Provider[] = []): Promise<ComponentFixture<PartnersPage>> {
  await TestBed.configureTestingModule({ imports: [PartnersPage], providers }).compileComponents();
  const fixture = TestBed.createComponent(PartnersPage);
  await fixture.whenStable();
  return fixture;
}

function session(...roles: DashboardRole[]): Provider[] {
  const source = new TestStaffSessionSource();
  source.authenticateAs(roles);
  return [
    provideStaffSessionSource(source),
    provideActionPermissionMatrix(TEST_ACTION_PERMISSION_MATRIX),
  ];
}

function host(fixture: ComponentFixture<PartnersPage>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

async function openDetail(fixture: ComponentFixture<PartnersPage>, ref: string): Promise<void> {
  host(fixture).querySelector<HTMLElement>(`[data-testid="partner-row-${ref}"]`)!.click();
  await fixture.whenStable();
}

describe('PartnersPage — list with onboarding-state badges (CC-WHS-002, AC-01)', () => {
  it('renders typed partner rows from the seam on load', async () => {
    const fixture = await render();
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid="partners-table"] tbody tr').length).toBeGreaterThan(0);
    expect(el.textContent).toContain('Nordwind Feinkost GmbH');
    expect(el.textContent).toContain('WHP-000117');
  });

  it('renders each onboarding state as a badge that carries its own text (§13)', async () => {
    const fixture = await render();
    const el = host(fixture);
    for (const label of ['Pending approval', 'Approved', 'Rejected', 'Suspended']) {
      expect(el.textContent).toContain(label);
    }
    // Status is never color-only: every badge has non-empty text.
    for (const badge of el.querySelectorAll('.cc-badge')) {
      expect((badge.textContent ?? '').trim().length).toBeGreaterThan(0);
    }
  });

  it('maps states to one accent family per meaning (DESIGN.md §12)', async () => {
    const fixture = await render();
    const el = host(fixture);
    const badge = (ref: string): Element =>
      el.querySelector(`[data-testid="partner-row-${ref}"]`)!.querySelector('.cc-badge')!;
    expect(badge('WHP-000098').classList).toContain('cc-status-good'); // approved
    expect(badge('WHP-000091').classList).toContain('cc-status-alert'); // suspended
    expect(badge('WHP-000117').classList).toContain('cc-status-neutral'); // pending
  });

  it('sets partner refs and dates in Plex Mono (DESIGN.md §12)', async () => {
    const fixture = await render();
    const cells = host(fixture)
      .querySelector('[data-testid="partner-row-WHP-000117"]')!
      .querySelectorAll('td');
    expect(cells[0].classList.contains('cc-mono')).toBe(true); // partner ref
    expect(cells[4].classList.contains('cc-mono')).toBe(true); // applied date
  });

  it('fails closed on a rejected payload: generic error, no rows (CC-SEC-001)', async () => {
    class FailingApi extends StubPartnersApi {
      override list(): Observable<PartnerListResult> {
        return throwError(() => new Error('Response failed schema validation: maskedValue'));
      }
    }
    const fixture = await render([
      { provide: PartnersApi, useValue: new FailingApi(detailFixture()) },
    ]);
    const el = host(fixture);
    expect(el.querySelector('[data-testid="load-error"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="partners-table"]')).toBeNull();
    expect(el.textContent).not.toContain('schema validation');
  });
});

describe('PartnersPage — business identity is MASKED (CC-WHS-002)', () => {
  it('renders only the masked value; the full identifier appears nowhere', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    expect(el.querySelector('[data-testid="identity-value-USt-IdNr."]')?.textContent?.trim()).toBe(
      '•••••••••4821',
    );
    // The seam never delivered a full value, so none can be rendered.
    expect(el.textContent).not.toContain('DE123454821');
  });

  it('renders every masked identity a partner carries, labeled by kind', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000104'); // IN partner: GSTIN + FSSAI licence
    const el = host(fixture);
    expect(el.textContent).toContain('GSTIN');
    expect(el.textContent).toContain('FSSAI licence');
    const values = el.querySelectorAll('[data-testid^="identity-value-"]');
    expect(values.length).toBe(2);
    for (const value of values) {
      // Every rendered identifier is masked — mask chars plus last-4 only.
      expect(value.textContent?.trim()).toMatch(/^[•]{4,}[A-Za-z0-9]{1,4}$/);
    }
  });

  it('states that values are masked and that no reveal flow exists yet', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117');
    const note = host(fixture).querySelector('[data-testid="identity-note"]');
    expect(note?.textContent).toContain('masked');
    // The reveal/audit-export flow is unauthored: no reveal control exists.
    expect(host(fixture).querySelector('[data-testid="identity-reveal"]')).toBeNull();
  });

  it('renders the per-partner payment terms (CC-WHS-004 net-60 default)', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117');
    expect(host(fixture).querySelector('[data-testid="partner-terms"]')?.textContent).toContain(
      'net 60 days',
    );
  });
});

describe('PartnersPage — actions render only what the SERVER offers (AC-02)', () => {
  it('renders exactly one button per allowedActions entry', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117'); // pending => approve/reject
    const el = host(fixture);
    expect(el.querySelector('[data-testid="partner-action-approve"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="partner-action-reject"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid^="partner-action-"]').length).toBe(2);
  });

  it('follows the SERVER even when it offers a narrower action set', async () => {
    const api = new StubPartnersApi(detailFixture({ allowedActions: ['reject'] }));
    const fixture = await render([...session('admin'), { provide: PartnersApi, useValue: api }]);
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="partner-action-"]').length).toBe(1);
    expect(el.querySelector('[data-testid="partner-action-approve"]')).toBeNull();
  });

  it('renders no actions for a terminal record (empty allowedActions)', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000085'); // rejected => no actions offered
    expect(host(fixture).querySelectorAll('[data-testid^="partner-action-"]').length).toBe(0);
  });
});

describe('PartnersPage — confirmation before every action (DESIGN.md §§5.4, 9)', () => {
  it('confirms first and does NOT call the seam yet', async () => {
    const api = new StubPartnersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: PartnersApi, useValue: api }]);
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="partner-action-approve"]')!.click();
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="confirm-dialog"]')).not.toBeNull();
    expect(api.actCalls).toEqual([]);
  });

  it('states the consequence in plain voice — no puns near the decision', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="partner-action-approve"]')!.click();
    await fixture.whenStable();
    const message = el.querySelector('[data-testid="confirm-message"]')?.textContent ?? '';
    expect(message).toContain('Nordwind Feinkost GmbH');
    expect(message).toContain('Approval activates wholesale ordering');
    // Pun budget is zero on an irreversible workflow decision (DESIGN.md 5.4).
    expect(message.toLowerCase()).not.toContain('cache');
  });

  it('calls the seam only after confirmation', async () => {
    const api = new StubPartnersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: PartnersApi, useValue: api }]);
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="partner-action-approve"]')!.click();
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-accept"]')!.click();
    await fixture.whenStable();
    expect(api.actCalls).toEqual([{ partnerRef: 'WHP-000117', action: 'approve' }]);
  });

  it('cancelling abandons the action entirely (negative case)', async () => {
    const api = new StubPartnersApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: PartnersApi, useValue: api }]);
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="partner-action-approve"]')!.click();
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-cancel"]')!.click();
    await fixture.whenStable();
    expect(api.actCalls).toEqual([]);
    expect(el.querySelector('[data-testid="confirm-dialog"]')).toBeNull();
  });

  it('shows generic copy and no state change when the server rejects the action', async () => {
    class RejectingApi extends StubPartnersApi {
      override act(): Observable<PartnerDetail> {
        return throwError(() => new Error('Request rejected'));
      }
    }
    const api = new RejectingApi(detailFixture());
    const fixture = await render([...session('admin'), { provide: PartnersApi, useValue: api }]);
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    el.querySelector<HTMLButtonElement>('[data-testid="partner-action-approve"]')!.click();
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-accept"]')!.click();
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="action-error"]')).not.toBeNull();
    expect(el.textContent).toContain('No changes were made');
  });
});

describe('PartnersPage — permission gating (CC-DSH-002, AC-06)', () => {
  it('with NO authored action matrix, shows no actions — even to admin (fail closed)', async () => {
    const source = new TestStaffSessionSource();
    source.authenticateAs(['admin']);
    const fixture = await render([provideStaffSessionSource(source)]);
    await openDetail(fixture, 'WHP-000117');
    expect(host(fixture).querySelectorAll('[data-testid^="partner-action-"]').length).toBe(0);
  });

  it('hides every action without the approval permission — record stays viewable', async () => {
    // ops-agent holds orders.transition but NOT partners.approve in the TEST
    // matrix: the module renders read-only for them.
    const fixture = await render(session('ops-agent'));
    await openDetail(fixture, 'WHP-000117');
    const el = host(fixture);
    expect(el.querySelectorAll('[data-testid^="partner-action-"]').length).toBe(0);
    expect(el.querySelector('[data-testid="partner-detail"]')).not.toBeNull();
  });

  it('shows actions with the approval permission', async () => {
    const fixture = await render(session('admin'));
    await openDetail(fixture, 'WHP-000117');
    expect(
      host(fixture).querySelectorAll('[data-testid^="partner-action-"]').length,
    ).toBeGreaterThan(0);
  });

  it('never exposes an approval action from the list view itself (no bulk bypass)', async () => {
    const fixture = await render(session('admin'));
    const table = host(fixture).querySelector('[data-testid="partners-table"]')!;
    expect(table.querySelector('button')).toBeNull();
  });
});

describe('PartnersPage — keyboard-first navigation (DESIGN.md §§12, 13; CC-NFR-004)', () => {
  function rows(fixture: ComponentFixture<PartnersPage>): HTMLTableRowElement[] {
    return [
      ...host(fixture).querySelectorAll<HTMLTableRowElement>(
        '[data-testid="partners-table"] tbody tr',
      ),
    ];
  }

  it('puts exactly one row in the tab order (roving tabindex)', async () => {
    const fixture = await render();
    expect(rows(fixture).filter((r) => r.tabIndex === 0).length).toBe(1);
  });

  it('ArrowDown moves focus between rows', async () => {
    const fixture = await render();
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(all[1]);
  });

  it('Enter on a focused row opens its detail — no mouse required', async () => {
    const fixture = await render(session('admin'));
    const all = rows(fixture);
    all[0].focus();
    all[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await fixture.whenStable();
    expect(host(fixture).querySelector('[data-testid="partner-detail"]')).not.toBeNull();
  });
});
