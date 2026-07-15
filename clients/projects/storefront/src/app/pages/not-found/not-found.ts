/**
 * 404 page — "Signal lost" (issue 063; DESIGN.md §5.1: the 404 page is one of
 * the four permitted homes of the broadcast-arc motif). Voice rules
 * (DESIGN.md §9): states what happened and what to do next; no apologies, no
 * mascots in error states; zero puns in error recovery (§5.4) — "Signal lost"
 * is the sanctioned 404 title, not an extra joke.
 *
 * The HTTP 404 status for unmatched routes is set server-side in
 * app.routes.server.ts (fail closed: unknown URL is a real 404, which is
 * also the hardening default of SECURITY.md, Authentication rule 9).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../i18n/i18n.service';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  templateUrl: './not-found.html',
  styleUrl: './not-found.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFound {
  protected readonly i18n = inject(I18nService);
}
