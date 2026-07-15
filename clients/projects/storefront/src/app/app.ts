/**
 * Storefront root shell (issue 063): header (market + language switchers),
 * routed page content, Char footer. Page anatomy per DESIGN.md §6.
 * CSP-compatible by construction — no inline event handlers or inline styles
 * (SECURITY.md, HTTP boundary rule 2); no raw-HTML sinks (Input validation
 * rule 5). The shell performs no market gating (ARCHITECTURE.md, Dependency
 * rule 1): it renders only server-gated data.
 */

import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './shell/header/header';
import { Footer } from './shell/footer/footer';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, Footer],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
