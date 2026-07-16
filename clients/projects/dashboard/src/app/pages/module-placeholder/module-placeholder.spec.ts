/**
 * Module placeholder tests (issue 079; CC-DSH-003): states the module name
 * and pending issue, renders no data.
 */

import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ModulePlaceholder } from './module-placeholder';

describe('ModulePlaceholder (CC-DSH-003 nav stubs)', () => {
  async function render(moduleId: string) {
    TestBed.configureTestingModule({
      imports: [ModulePlaceholder],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { data: { moduleId } } } },
      ],
    });
    const fixture = TestBed.createComponent(ModulePlaceholder);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('states the module name and its pending issue — no fake data', async () => {
    const el = await render('orders');
    expect(el.querySelector('[data-testid="module-title"]')?.textContent).toContain(
      'Order management',
    );
    expect(el.querySelector('[data-testid="module-pending"]')?.textContent).toContain(
      'pending implementation in issue 082',
    );
    // Placeholder discipline: exactly a heading and the pending line.
    expect(el.querySelectorAll('table, ul, ol, form, input').length).toBe(0);
  });

  it('maps every module to its own issue', async () => {
    const el = await render('employees');
    expect(el.textContent).toContain('Employee management');
    expect(el.textContent).toContain('issue 087');
  });
});
