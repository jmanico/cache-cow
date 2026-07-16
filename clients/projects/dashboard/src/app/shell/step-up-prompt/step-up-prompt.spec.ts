/**
 * StepUpPrompt seam tests (issue 079; CC-DSH-001 re-auth seam — ceremony
 * lands with issues 060/082–087).
 */

import { TestBed } from '@angular/core/testing';
import { StepUpPrompt } from './step-up-prompt';

describe('StepUpPrompt (re-auth seam only)', () => {
  async function render() {
    TestBed.configureTestingModule({ imports: [StepUpPrompt] });
    const fixture = TestBed.createComponent(StepUpPrompt);
    await fixture.whenStable();
    return fixture;
  }

  it('renders the re-authentication dialog copy', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const dialog = el.querySelector('[role="alertdialog"]');
    expect(dialog).not.toBeNull();
    expect(el.textContent).toContain('Re-authentication required');
    expect(el.textContent).toContain('passkey');
  });

  it('emits confirmed/dismissed and performs no authentication itself', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    let confirmed = 0;
    let dismissed = 0;
    fixture.componentInstance.confirmed.subscribe(() => confirmed++);
    fixture.componentInstance.dismissed.subscribe(() => dismissed++);

    el.querySelector<HTMLButtonElement>('[data-testid="step-up-confirm"]')!.click();
    el.querySelector<HTMLButtonElement>('[data-testid="step-up-cancel"]')!.click();

    expect(confirmed).toBe(1);
    expect(dismissed).toBe(1);
    // Seam discipline: no credential inputs anywhere.
    expect(el.querySelector('input')).toBeNull();
  });
});
