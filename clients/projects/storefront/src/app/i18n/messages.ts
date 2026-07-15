/**
 * Locale message-bundle registry (issue 064, client half; CC-I18N-002).
 *
 * The JSON bundles under src/assets/i18n/ are the client-consumed artifacts
 * of the ICU MessageFormat resource pipeline; they are validated as untrusted
 * input by `npm run i18n:check` (schema, no-HTML, key parity, placeholder
 * consistency — SECURITY.md, Input validation rule 7) before any build.
 *
 * Static imports keep SSR and browser rendering identical (no runtime fetch)
 * and give compile-time typed keys: `MessageKey` is derived from the en-US
 * bundle, and assigning every bundle to `Record<MessageKey, string>` makes a
 * missing key a build error on top of the CI parity gate.
 */

import { Locale } from '../core/transacting-context';

import deDE from '../../assets/i18n/de-DE.json';
import enIN from '../../assets/i18n/en-IN.json';
import enUS from '../../assets/i18n/en-US.json';
import esES from '../../assets/i18n/es-ES.json';
import esMX from '../../assets/i18n/es-MX.json';
import hiIN from '../../assets/i18n/hi-IN.json';
import jaJP from '../../assets/i18n/ja-JP.json';

/** Every UI string key available to the storefront shell. */
export type MessageKey = keyof typeof enUS;

export type MessageBundle = Readonly<Record<MessageKey, string>>;

export const MESSAGES: Readonly<Record<Locale, MessageBundle>> = {
  'en-US': enUS,
  'es-ES': esES,
  'es-MX': esMX,
  'de-DE': deDE,
  'ja-JP': jaJP,
  'en-IN': enIN,
  'hi-IN': hiIN,
};
