import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';

import { routes } from './app.routes';
import { HeadI18n } from './core/head-i18n';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    // Instantiate the lang/hreflang head manager on both server and client
    // (CC-I18N-004): the SSR response carries the declarations; the client
    // keeps them updated across navigations and locale changes.
    provideAppInitializer(() => {
      inject(HeadI18n);
    }),
  ],
};
