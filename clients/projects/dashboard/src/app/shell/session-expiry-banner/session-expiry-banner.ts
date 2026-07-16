/**
 * Session-expiry warning banner (issue 079; CC-DSH-001).
 *
 * Surfaces the approaching 12-hour server-side session cap (SECURITY.md,
 * Authentication rule 2) from the typed lifetime seam in
 * core/session-expiry.ts. Ember is the alert accent (DESIGN.md §12); text
 * stays Pitpaper on Pit for AA contrast. The banner only informs — session
 * termination and re-authentication are server/issue-060 concerns.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SessionExpiry } from '../../core/session-expiry';
import { DashboardI18n } from '../../i18n/i18n.service';

@Component({
  selector: 'app-session-expiry-banner',
  templateUrl: './session-expiry-banner.html',
  styleUrl: './session-expiry-banner.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionExpiryBanner {
  protected readonly expiry = inject(SessionExpiry);
  protected readonly i18n = inject(DashboardI18n);
}
