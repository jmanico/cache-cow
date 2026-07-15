/**
 * Home placeholder (issue 063 route scaffolding). The real home page —
 * hero, regional bestsellers, how-it-works, store locator teaser — is
 * DESIGN.md §10 / downstream issues 066+. Copy here is placeholder; real
 * per-market copy by native speakers is an open editorial question
 * (DESIGN.md §9).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { I18nService } from '../../i18n/i18n.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  styleUrl: './home.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly i18n = inject(I18nService);
}
