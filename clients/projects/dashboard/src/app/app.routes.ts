/**
 * Dashboard shell routes (issue 079; CC-DSH-003).
 *
 * One route per launch module, every one behind moduleAccessGuard so direct
 * navigation is denied exactly like nav visibility (fail closed with no
 * matrix; SECURITY.md, Logging rule 2). Unknown paths return to the shell
 * root rather than confirming what exists (hardening default per
 * SECURITY.md, Authentication rule 9).
 */

import { Routes } from '@angular/router';
import { moduleAccessGuard } from './core/module-access-guard';
import { DASHBOARD_MODULES } from './core/modules';
import { Home } from './pages/home/home';
import { ModulePlaceholder } from './pages/module-placeholder/module-placeholder';

export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  ...DASHBOARD_MODULES.map((module) => ({
    path: module.path,
    component: ModulePlaceholder,
    canActivate: [moduleAccessGuard],
    data: { moduleId: module.id },
  })),
  { path: '**', redirectTo: '' },
];
