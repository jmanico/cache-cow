/**
 * Interactive butcher diagram (issue 075; CC-CNT-003; DESIGN.md §7 "Cut
 * card", §13).
 *
 * Visual language per §7: line-art side-profile steer diagram, numbered cut
 * regions, Char linework on Paper, Plex Mono numbering — "deliberately
 * technical (a diagram, not a character)". It shares ZERO DNA with the
 * cow-mascot illustrations (DESIGN.md §8.1 separation rule): no face, no
 * blaze, no flat Char mascot shapes — outline strokes and numbers only.
 *
 * ACCESSIBILITY (DESIGN.md §13; issue 075 AC-02): the artwork itself is
 * decorative (`aria-hidden`), and every cut region is a REAL HTML
 * `<button>` overlaid on the art, each with an ARIA name and a visible Plex
 * Mono number. That gives full keyboard operability, a logical tab order,
 * and visible focus for free — no mouse-only SVG hit areas, no
 * `role="button"` imitations. Region positions live in static CSS keyed on
 * the `data-cut` attribute (no inline styles — SECURITY.md, HTTP boundary
 * rule 2 CSP discipline).
 *
 * The component GATES NOTHING and FILTERS NOTHING: it emits the chosen cut
 * and the page routes to the menu, where filtering executes server-side
 * (ARCHITECTURE.md, Dependency rules 1–2).
 */

import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { Cut } from '../../catalog/catalog.types';
import { CUT_NAME_KEYS } from '../product-card/product-card';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';

/** One numbered region of the diagram. */
export interface CutRegion {
  /** Catalog cut/category (CC-CAT-001) — reuses issue 066's menu filter
   * vocabulary rather than a parallel taxonomy (issue 075 Constraints). */
  readonly cut: Cut;
  /** Plex Mono region number shown on the diagram (DESIGN.md §7). */
  readonly number: string;
}

/**
 * PLACEHOLDER region set. The cut-region taxonomy — which numbered regions
 * the diagram exposes and how each maps to the catalog's cut/category field
 * — is an OPEN content/catalog decision (issue 075 Open Questions) and is
 * NOT resolved here: this is the anatomical subset of the placeholder CUTS
 * vocabulary in catalog.types.ts. ('sausage' is a preparation, not a region
 * of a steer, so it has no place on the diagram; veg cuts likewise have no
 * anatomy — how the veg filter interacts with cut filtering is a second open
 * question.) The list equivalent below renders from this SAME array, so the
 * two surfaces cannot drift apart.
 */
export const CUT_REGIONS: readonly CutRegion[] = [
  { cut: 'shoulder', number: '1' },
  { cut: 'ribs', number: '2' },
  { cut: 'brisket', number: '3' },
];

@Component({
  selector: 'app-cut-diagram',
  templateUrl: './cut-diagram.html',
  styleUrl: './cut-diagram.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CutDiagram {
  protected readonly i18n = inject(I18nService);
  protected readonly regions = CUT_REGIONS;

  /** Emits the chosen cut; the page owns routing to the filtered menu. */
  readonly selectCut = output<Cut>();

  protected cutNameKey(cut: Cut): MessageKey {
    return CUT_NAME_KEYS[cut];
  }

  /** Accessible name: "Filter the menu by {cut}" (DESIGN.md §13). */
  protected regionLabel(cut: Cut): string {
    return this.i18n.t('cuts.regionAction', { cut: this.i18n.t(this.cutNameKey(cut)) });
  }

  protected onSelect(cut: Cut): void {
    this.selectCut.emit(cut);
  }
}
