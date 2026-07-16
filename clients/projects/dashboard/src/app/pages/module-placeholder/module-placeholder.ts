/**
 * Module placeholder page (issue 079; CC-DSH-003).
 *
 * One placeholder per launch module, stating the module name and the epic
 * sub-issue that delivers its content (082–087). Deliberately renders NO
 * data, fake or otherwise. Reached only through moduleAccessGuard, which
 * fails closed exactly like nav visibility (core/module-access-guard.ts).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DASHBOARD_MODULE_BY_ID, DashboardModuleDef, DashboardModuleId } from '../../core/modules';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-module-placeholder',
  templateUrl: './module-placeholder.html',
  styleUrl: './module-placeholder.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModulePlaceholder {
  protected readonly i18n = inject(DashboardI18n);

  /** The guard has already verified the id is known and permitted. */
  protected readonly module: DashboardModuleDef =
    DASHBOARD_MODULE_BY_ID[inject(ActivatedRoute).snapshot.data['moduleId'] as DashboardModuleId];
}
