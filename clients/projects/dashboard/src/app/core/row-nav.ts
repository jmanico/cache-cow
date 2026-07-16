/**
 * Roving-tabindex row navigation for the module data tables (issues
 * 082/084/085; DESIGN.md §12 "keyboard-first", §13 keyboard operability).
 *
 * Applied to a `<tbody>`: exactly one row is in the tab order at a time
 * (roving tabindex); ArrowUp/ArrowDown/Home/End move focus between rows.
 * Row ACTIVATION (Enter/click opening a detail view) stays in the page
 * template so read-only tables (inventory, issue 084) can use the same
 * navigation without any activation semantics.
 *
 * Focus visibility is the global :focus-visible outline in styles.css
 * (DESIGN.md §13).
 */

import { AfterViewChecked, Directive, ElementRef, inject } from '@angular/core';

@Directive({
  selector: 'tbody[appRowNav]',
  host: {
    '(keydown)': 'onKeydown($event)',
    '(focusin)': 'onFocusIn($event)',
  },
})
export class RowNav implements AfterViewChecked {
  private readonly host: HTMLElement = inject(ElementRef).nativeElement;

  /** Keep exactly one row reachable by Tab as rows render/change. */
  ngAfterViewChecked(): void {
    const rows = this.rows();
    if (rows.length === 0) {
      return;
    }
    if (!rows.some((row) => row.tabIndex === 0)) {
      this.setActive(rows, rows[0]);
    } else {
      for (const row of rows) {
        if (!row.hasAttribute('tabindex')) {
          row.tabIndex = -1;
        }
      }
    }
  }

  protected onFocusIn(event: FocusEvent): void {
    const row = this.rowOf(event.target);
    if (row !== null) {
      this.setActive(this.rows(), row);
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    const rows = this.rows();
    const current = this.rowOf(event.target);
    const index = current === null ? -1 : rows.indexOf(current);
    if (index < 0) {
      return;
    }
    let next: number;
    switch (event.key) {
      case 'ArrowDown':
        next = Math.min(index + 1, rows.length - 1);
        break;
      case 'ArrowUp':
        next = Math.max(index - 1, 0);
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = rows.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.setActive(rows, rows[next]);
    rows[next].focus();
  }

  private rows(): HTMLTableRowElement[] {
    return Array.from(this.host.querySelectorAll('tr'));
  }

  private rowOf(target: EventTarget | null): HTMLTableRowElement | null {
    if (!(target instanceof Element)) {
      return null;
    }
    const row = target.closest('tr');
    return row !== null && this.host.contains(row) ? row : null;
  }

  private setActive(rows: readonly HTMLTableRowElement[], active: HTMLTableRowElement): void {
    for (const row of rows) {
      row.tabIndex = row === active ? 0 : -1;
    }
  }
}
