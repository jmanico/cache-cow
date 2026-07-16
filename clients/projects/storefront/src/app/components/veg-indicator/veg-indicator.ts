/**
 * Vegetarian marking (issues 066/067; CC-CNT-006; DESIGN.md §3.3, §13).
 *
 * Which mark renders is a SERVER policy decision carried in the typed
 * response (`vegMarking`, Market & Gating Policy context) — this component
 * holds no market conditional (ARCHITECTURE.md, Dependency rule 1):
 *
 * - 'fssaiVeg': the FSSAI vegetarian regulation mark — green square outline
 *   with a filled green circle (CC-CNT-006; the regulation mark, not a
 *   stylized leaf) — as static inline SVG (no [innerHTML], SECURITY.md,
 *   Input validation rule 5), plus the localized "Vegetarian" word so the
 *   status is shape-plus-label, never color alone (DESIGN.md §13).
 * - 'leafDot': the cache.500 leaf-dot badge plus the word "Vegetarian"
 *   (DESIGN.md §3.3 — a UI affordance, not a regulatory mark).
 * - 'none': renders nothing. There is NO non-veg mark anywhere in this
 *   client, so CC-CNT-006's "non-veg mark must not appear in IN" holds
 *   structurally.
 *
 * Green is the cache.500 token (the system's single green). Whether the
 * FSSAI mark must use an exact regulation color value distinct from the
 * token is an OPEN question for legal/design review — flagged, not decided.
 */

import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { VegMarking } from '../../catalog/catalog.types';
import { I18nService } from '../../i18n/i18n.service';

@Component({
  selector: 'app-veg-indicator',
  templateUrl: './veg-indicator.html',
  styleUrl: './veg-indicator.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VegIndicator {
  protected readonly i18n = inject(I18nService);

  readonly marking = input.required<VegMarking>();
}
