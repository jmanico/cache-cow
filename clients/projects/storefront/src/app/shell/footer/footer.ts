/**
 * Storefront footer (issue 063): Char footer per DESIGN.md §6 page anatomy,
 * wordmark-only treatment per DESIGN.md §2.2 (variant table: "Wordmark only —
 * Legal, footer"). The wordmark renders as text in the display face token
 * until the dedicated wordmark-only asset lands (DESIGN.md §15 lists it in
 * the remaining asset set). The tag line stays English in all locales as
 * lockup text (DESIGN.md §2.3); translated taglines are separate copy owned
 * by future content issues.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { I18nService } from '../../i18n/i18n.service';

@Component({
  selector: 'app-footer',
  templateUrl: './footer.html',
  styleUrl: './footer.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Footer {
  protected readonly i18n = inject(I18nService);
  // A string, not a number: numeric values are locale-grouped by the
  // formatter (CC-I18N-003) and a year must never render as "2,026".
  protected readonly year = String(new Date().getFullYear());
}
