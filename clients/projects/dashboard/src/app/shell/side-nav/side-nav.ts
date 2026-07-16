/**
 * RBAC-aware side navigation (issue 079; CC-DSH-002, CC-DSH-003).
 *
 * Renders the six launch modules as router links, filtered per role by
 * RoleVisibility. With NO role-permission matrix configured (the shipped
 * default — the matrix is unauthored, issue 080 / epic open question 17)
 * every module link is hidden and the awaiting-authorization-configuration
 * state renders instead, mirroring the server's fail-closed posture.
 *
 * Presentation only: hiding a link is never the access control — the
 * moduleAccessGuard denies direct navigation the same way, and the server
 * enforces RBAC on every endpoint regardless (SECURITY.md, Authentication
 * rules 1 and 8).
 */

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DASHBOARD_MODULES } from '../../core/modules';
import { RoleVisibility } from '../../core/role-visibility';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-side-nav',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './side-nav.html',
  styleUrl: './side-nav.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SideNav {
  protected readonly i18n = inject(DashboardI18n);
  protected readonly visibility = inject(RoleVisibility);

  protected readonly visibleModules = computed(() =>
    DASHBOARD_MODULES.filter((m) => this.visibility.visibleModuleIds().has(m.id)),
  );
}
