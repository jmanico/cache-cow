/**
 * Typed i18n message service (issue 064, client half; CC-I18N-002).
 *
 * Formats ICU MessageFormat strings from the validated locale bundles for
 * the current transacting locale. Escape-by-default (SECURITY.md, Input
 * validation rules 5 and 7): `t()` returns a plain string that callers bind
 * only through Angular text interpolation `{{ }}` or attribute bindings —
 * NEVER through [innerHTML] or bypassSecurityTrust* (banned, CI-grep-gated).
 * There is no opt-out to raw HTML anywhere in this pipeline; an interpolated
 * value like `<script>` renders inert as literal text.
 *
 * Reactive: t() reads the locale signal, so any template binding using it
 * re-renders when the user changes the language (and only the language —
 * CC-I18N-001).
 *
 * Failure behavior: a format error (missing value, malformed message that
 * escaped CI) fails closed to the message KEY — never raw/unescaped input,
 * never a throw that blanks the page. Runtime fallback policy for missing
 * keys is an open question in issue 064; the key-as-fallback here is a safe
 * placeholder, not a decision on that question.
 */

import { Injectable, inject } from '@angular/core';
import { TransactingContext } from '../core/transacting-context';
import { formatIcu, IcuValues } from './icu';
import { MESSAGES, MessageKey } from './messages';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly context = inject(TransactingContext);

  /** Format the message for `key` in the current locale. */
  t(key: MessageKey, values?: IcuValues): string {
    const locale = this.context.locale();
    const message = MESSAGES[locale][key];
    if (typeof message !== 'string') {
      return key;
    }
    try {
      return formatIcu(message, locale, values);
    } catch {
      // Fail closed to the key; never fall through to raw input.
      return key;
    }
  }
}
