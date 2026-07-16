/**
 * Typed content response model (issues 073/074/077; CC-CNT-001/002/005).
 *
 * These interfaces mirror the server's Content & Localization responses
 * (ARCHITECTURE.md, bounded context 10) as consumed through the ContentApi
 * seam in content.api.ts. CMS-sourced rich text is sanitized SERVER-SIDE
 * through the allowlist renderer (issue 072; SECURITY.md, Input validation
 * rule 5) — what crosses this boundary is typed PLAIN-TEXT/structured fields
 * only, and the client binds them exclusively through Angular text
 * interpolation. There is no HTML field in this model and no raw-HTML sink
 * anywhere in the client (CC-SEC-002).
 */

import { Locale, Market } from '../core/transacting-context';

// --- Chefs (issue 073; CC-CNT-001; DESIGN.md §7 chef card) -----------------

/** One chef card: portrait, name, pit specialty, market flag(s). The roster
 * is SHARED across markets; `specialty` and `bio` arrive localized for the
 * requesting locale (server-side localization — never client translation). */
export interface ChefProfile {
  readonly id: string;
  readonly name: string;
  /** Localized pit specialty (plain text). */
  readonly specialty: string;
  /** Localized one-paragraph bio (plain text, already through issue 072). */
  readonly bio: string;
  /** Market flag(s) shown on the card (DESIGN.md §7). */
  readonly markets: readonly Market[];
}

export interface ChefRoster {
  readonly locale: Locale;
  readonly roster: readonly ChefProfile[];
}

// --- Cows (issue 074; CC-CNT-002; DESIGN.md §7 cow card) --------------------

/** Blaze differentiators of the mascot illustration set (DESIGN.md §7):
 * one cow's blaze is the database cylinder, one a lightning bolt, one a
 * heart. The value selects the placeholder illustration and its alt text. */
export const BLAZES = ['database', 'lightning', 'heart'] as const;
export type Blaze = (typeof BLAZES)[number];

/** One cow card: illustration (by blaze), name, "role", one-line bio. */
export interface CowProfile {
  readonly id: string;
  readonly name: string;
  /** Localized role line (plain text; untranslatable puns are cut per locale — DESIGN.md §9). */
  readonly role: string;
  /** Localized one-line bio (plain text). */
  readonly bio: string;
  readonly blaze: Blaze;
}

export interface CowHerd {
  readonly locale: Locale;
  readonly herd: readonly CowProfile[];
}

// --- Legal (issue 077; CC-CNT-005, CC-FUL-003) ------------------------------

/** Every legal document id the platform can serve. Which subset a market
 * carries is per-market policy data (DE additionally impressum + widerruf —
 * CC-CNT-005); the server resolves the set from the transacting market. */
export const LEGAL_DOC_IDS = ['privacy', 'terms', 'shipping-returns', 'impressum', 'widerruf'] as const;
export type LegalDocId = (typeof LEGAL_DOC_IDS)[number];

export interface LegalDocSummary {
  readonly id: LegalDocId;
  /** Localized document title (plain text). */
  readonly title: string;
}

/** The transacting market's legal content set (drives footer links). */
export interface LegalDocList {
  readonly market: Market;
  readonly docs: readonly LegalDocSummary[];
}

/** Structured plain-text body section — never free HTML (CC-SEC-002). */
export interface LegalSection {
  readonly heading: string;
  readonly paragraphs: readonly string[];
}

/** One versioned legal document (CC-CNT-005: "served per locale and
 * versioned"). Issued versions are immutable; corrections are new versions. */
export interface LegalDoc extends LegalDocSummary {
  readonly version: string;
  /** ISO date (yyyy-mm-dd); displayed locale-formatted (CC-I18N-003). */
  readonly effectiveDate: string;
  readonly locale: Locale;
  readonly sections: readonly LegalSection[];
}
