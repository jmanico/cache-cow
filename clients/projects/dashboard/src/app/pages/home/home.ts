/**
 * Dashboard landing page (issue 079). Renders no operational data — the
 * modules (CC-DSH-003) land in issues 082–087.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  styleUrl: './home.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly i18n = inject(DashboardI18n);
}
