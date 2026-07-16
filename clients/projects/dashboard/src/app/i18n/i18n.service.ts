/**
 * Dashboard i18n (issue 079 shell scope).
 *
 * All shell strings are externalized to a typed en-US bundle, mirroring the
 * storefront's bundle-plus-typed-keys pattern WITHOUT importing any storefront
 * module (ARCHITECTURE.md, Dependency rule 4; SECURITY.md, HTTP boundary
 * rule 8 — the dashboard shares no modules with storefront or portal).
 *
 * OPEN QUESTION (flagged, not resolved — CLAUDE.md working rules): the
 * dashboard's locale scope is not specified anywhere (CC-I18N-001 lists the
 * consumer launch locales; CC-DSH-* is silent on staff-tooling locales).
 * This service ships en-US only as the minimal staff-tooling baseline and
 * keeps the same t(key) seam so additional locales can be added if a human
 * decides the dashboard localizes. Placeholder interpolation is a plain
 * `{name}` substitution — deliberately NOT a parallel ICU grammar, which
 * would violate the single-source rule (ARCHITECTURE.md, Dependency rule 7)
 * without importing the storefront's (banned).
 *
 * Security posture (SECURITY.md, Input validation rules 5 and 7): messages
 * and interpolated values are always plain text; callers bind results only
 * through Angular text interpolation — never [innerHTML] or
 * bypassSecurityTrust* (banned, CI-grep-gated). Unknown keys fail closed to
 * the key itself.
 */

import { Injectable } from '@angular/core';
import enUS from './en-US.json';

/** Every UI string key available to the dashboard shell. */
export type DashboardMessageKey = keyof typeof enUS;

export type DashboardMessageValues = Readonly<Record<string, string | number>>;

const MESSAGES: Readonly<Record<DashboardMessageKey, string>> = enUS;

const PLACEHOLDER_RE = /\{([a-zA-Z][a-zA-Z0-9_]*)\}/g;

@Injectable({ providedIn: 'root' })
export class DashboardI18n {
  /** Format the message for `key`; plain text out, fail closed to the key. */
  t(key: DashboardMessageKey, values?: DashboardMessageValues): string {
    const message = MESSAGES[key];
    if (typeof message !== 'string') {
      return key;
    }
    return message.replace(PLACEHOLDER_RE, (match, name: string) =>
      values !== undefined && name in values ? String(values[name]) : match,
    );
  }
}
