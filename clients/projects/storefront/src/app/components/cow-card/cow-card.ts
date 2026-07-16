/**
 * Cow card — mascot system (issue 074; CC-CNT-002; DESIGN.md §7 "Cow card",
 * §8.1).
 *
 * Composition per §7: illustration in the logo's geometric style (flat Char
 * shapes on Butcher), each cow differentiated by BLAZE SHAPE (database
 * cylinder, lightning bolt, heart), plus name, "role", and a one-line bio.
 * These illustrations are the ONLY place the mascot style is permitted
 * (DESIGN.md §7) — nothing else in this client renders them, and the Cuts
 * diagram deliberately shares zero DNA with them (§8.1 separation rule).
 *
 * NO PRODUCT LINKS — of any kind, in any market. CC-CNT-002 prohibits links
 * to non-veg PDPs in every market; this card carries no PDP link at all,
 * which satisfies the rule STRUCTURALLY rather than by a runtime check that
 * could regress (and sidesteps the open question of whether veg PDP links
 * would be permitted — issue 074 Open Questions). A spec asserts zero
 * '/product/' hrefs on the rendered page.
 *
 * The illustrations here are PLACEHOLDER geometry: the real seven-cow mascot
 * illustration set is an open design-asset decision (DESIGN.md §15). The
 * blaze shape is the differentiator and is carried in the ALT TEXT so the
 * distinction survives for assistive technology (DESIGN.md §13).
 */

import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Blaze, CowProfile } from '../../content/content.types';
import { I18nService } from '../../i18n/i18n.service';
import { MessageKey } from '../../i18n/messages';

/** Typed key lookup for blaze alt text (compile-checked, no string building). */
const BLAZE_ALT_KEYS: Readonly<Record<Blaze, MessageKey>> = {
  database: 'cows.illustrationAlt.database',
  lightning: 'cows.illustrationAlt.lightning',
  heart: 'cows.illustrationAlt.heart',
};

@Component({
  selector: 'app-cow-card',
  templateUrl: './cow-card.html',
  styleUrl: './cow-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CowCard {
  protected readonly i18n = inject(I18nService);

  readonly cow = input.required<CowProfile>();

  /** Alt text naming the cow AND her blaze differentiator (DESIGN.md §7/§13). */
  protected illustrationAlt(): string {
    return this.i18n.t(BLAZE_ALT_KEYS[this.cow().blaze], { name: this.cow().name });
  }
}
