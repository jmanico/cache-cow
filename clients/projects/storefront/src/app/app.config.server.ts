import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideServerTransactingContext } from './core/transacting-context';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // Placeholder seed for the server-resolved transacting market/locale.
    // Issue 024 replaces this provider with the real per-request resolution
    // (persisted explicit user choice; geolocation only as a proposal —
    // CC-MKT-002). Never derived from client hints (CC-SEC-012).
    provideServerTransactingContext(),
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
