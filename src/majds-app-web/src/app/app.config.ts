import { ApplicationConfig, inject, isDevMode, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatedPaginatorIntl, TranslatingSnackBar } from './core/i18n/translating-material';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { languageInterceptor } from './core/interceptors/language.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';
import { PluginsService } from './core/services/plugins.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // A soft crossfade between routes on browsers that support the View Transitions API (U2 FR-MOB-005); a
    // no-op fallback everywhere else. prefers-reduced-motion turns it off globally, in styles.scss.
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(withInterceptors([languageInterceptor, authInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
    // Installable PWA with an offline app shell (FR-MOB-007). Production builds only: a service worker
    // in `ng serve` would serve stale bundles during development.
    provideServiceWorker('ngsw-worker.js', { enabled: !isDevMode(), registrationStrategy: 'registerWhenStable:30000' }),
    // Registers enabled plugins' routes/menu (P5) before the router's initial navigation, so a hard
    // refresh or deep link straight into a plugin's own route (e.g. /tasks) resolves correctly instead
    // of failing to match a route that authGuard would otherwise only add mid-navigation.
    provideAppInitializer(async () => {
      if (inject(AuthService).isAuthenticated()) {
        await inject(PluginsService).loadAsync();
      }
    }),
    { provide: MatSnackBar, useExisting: TranslatingSnackBar },
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl }
  ]
};
