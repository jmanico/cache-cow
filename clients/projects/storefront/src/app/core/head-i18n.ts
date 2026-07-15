/**
 * Document-head locale declaration (issue 063; CC-I18N-004).
 *
 * Sets the `lang` attribute on <html> to the rendered locale and emits
 * `hreflang` alternate <link> tags for every launch-locale variant of the
 * current route, plus x-default. Runs during SSR (the server-rendered HTML
 * carries the correct declarations — AC-02) and keeps them updated on
 * client-side navigation and locale changes.
 *
 * The rendered locale comes exclusively from the server-seeded
 * TransactingContext — never from Accept-Language or navigator (CC-SEC-012).
 *
 * PLACEHOLDER URL SCHEME: alternates are emitted as origin-relative URLs with
 * a `locale` query parameter. The canonical per-locale URL scheme and
 * absolute URLs (needed for SEO surfaces/sitemaps) land with issues 024/071;
 * whether alternates should list only the transacting market's valid locales
 * or all seven is an OPEN question recorded in issue 063. All DOM writes use
 * createElement/setAttribute — no HTML string sinks (SECURITY.md, Input
 * validation rule 5).
 */

import { DOCUMENT, Injectable, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { LOCALES, Locale, TransactingContext } from './transacting-context';

const HREFLANG_MARKER = 'data-cc-hreflang';

@Injectable({ providedIn: 'root' })
export class HeadI18n {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly context = inject(TransactingContext);
  private readonly url = signal(this.router.url);

  constructor() {
    // App-lifetime root service; the subscription lives as long as the app.
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.url.set(event.urlAfterRedirects);
      }
    });
    effect(() => this.apply(this.context.locale(), this.url()));
  }

  private apply(locale: Locale, url: string): void {
    this.document.documentElement.setAttribute('lang', locale);

    const head = this.document.head;
    for (const stale of Array.from(head.querySelectorAll(`link[${HREFLANG_MARKER}]`))) {
      stale.parentNode?.removeChild(stale);
    }
    for (const alternate of LOCALES) {
      head.appendChild(this.alternateLink(alternate, this.alternateHref(url, alternate)));
    }
    head.appendChild(this.alternateLink('x-default', this.alternateHref(url, null)));
  }

  private alternateLink(hreflang: string, href: string): HTMLLinkElement {
    const link = this.document.createElement('link');
    link.setAttribute('rel', 'alternate');
    link.setAttribute('hreflang', hreflang);
    link.setAttribute('href', href);
    link.setAttribute(HREFLANG_MARKER, '');
    return link;
  }

  /** Placeholder alternate-URL builder (see header comment). */
  private alternateHref(url: string, locale: Locale | null): string {
    // 'http://internal' is a parsing base only; emitted hrefs stay relative.
    const parsed = new URL(url, 'http://internal');
    if (locale === null) {
      parsed.searchParams.delete('locale');
    } else {
      parsed.searchParams.set('locale', locale);
    }
    const query = parsed.searchParams.toString();
    return `${parsed.pathname}${query ? `?${query}` : ''}`;
  }
}
