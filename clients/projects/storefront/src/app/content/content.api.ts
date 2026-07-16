/**
 * Content data seam (issues 073/074/077; CC-CNT-001/002/005).
 *
 * `ContentApi` is the injectable HTTP-or-mock boundary: the content pages
 * depend only on this abstract class. Until the server Content & Localization
 * APIs exist (issue 072's Contentful integration and sanitizing allowlist
 * renderer; the per-market legal content set from issue 023), the root
 * provider resolves to `MockContentApi`; swapping in an `HttpContentApi`
 * later is a one-line provider change with no page edits.
 *
 * EVERY implementation — mock included — funnels its payload through the
 * runtime schema parsers as `unknown` (SECURITY.md, Input validation rule 1)
 * and throws on violation; pages fail closed to a generic error state rather
 * than rendering partial or unvalidated content.
 *
 * CMS rich text is sanitized SERVER-SIDE through the allowlist renderer
 * (issue 072) BEFORE it crosses this seam: what arrives here is typed
 * plain-text/structured fields, bound only through Angular text
 * interpolation. No [innerHTML], no bypassSecurityTrust* (CC-SEC-002).
 *
 * The seam does NOT accept market/locale parameters: both are server-side
 * transacting state (CC-SEC-012; SECURITY.md, Authentication rule 10) — the
 * real HTTP client will not send them. The mock injects TransactingContext
 * only to SIMULATE the server's knowledge of that state.
 */

import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { TransactingContext } from '../core/transacting-context';
import {
  mockChefRosterResponse,
  mockCowHerdResponse,
  mockLegalDocListResponse,
  mockLegalDocResponse,
} from './content.mock-data';
import { ChefRoster, CowHerd, LegalDoc, LegalDocList } from './content.types';
import { parseChefRoster, parseCowHerd, parseLegalDoc, parseLegalDocList } from './content.validate';

@Injectable({ providedIn: 'root', useFactory: () => inject(MockContentApi) })
export abstract class ContentApi {
  /** The SHARED chef roster, bios localized server-side (CC-CNT-001). */
  abstract getChefRoster(): Observable<ChefRoster>;

  /** The mascot herd, localized server-side (CC-CNT-002). */
  abstract getCowHerd(): Observable<CowHerd>;

  /** The transacting market's legal content set (CC-CNT-005; drives the footer). */
  abstract getLegalDocList(): Observable<LegalDocList>;

  /**
   * One versioned legal document, or null when the document is absent from
   * the transacting market's legal content set (the real server answers HTTP
   * 404 — CC-CNT-005 failure behavior; callers render the 404 page).
   */
  abstract getLegalDoc(docId: string): Observable<LegalDoc | null>;
}

@Injectable({ providedIn: 'root' })
export class MockContentApi extends ContentApi {
  private readonly context = inject(TransactingContext);

  override getChefRoster(): Observable<ChefRoster> {
    // The roster is market-independent (CC-CNT-001): locale only.
    const response: unknown = mockChefRosterResponse(this.context.locale());
    // Untrusted-response discipline even for the mock: parse or throw.
    return of(parseChefRoster(response));
  }

  override getCowHerd(): Observable<CowHerd> {
    const response: unknown = mockCowHerdResponse(this.context.locale());
    return of(parseCowHerd(response));
  }

  override getLegalDocList(): Observable<LegalDocList> {
    const response: unknown = mockLegalDocListResponse(this.context.market(), this.context.locale());
    return of(parseLegalDocList(response));
  }

  override getLegalDoc(docId: string): Observable<LegalDoc | null> {
    const response = mockLegalDocResponse(this.context.market(), this.context.locale(), docId);
    return of(response === null ? null : parseLegalDoc(response));
  }
}
