import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { LocalizationService } from '../i18n/localization.service';

/**
 * Tells the API which language the user is using (F-Localization FR-I18N-002): every API request carries the UI language as
 * <c>Accept-Language</c>, so error messages come back in the language on screen right after a switch, with no reload. Only calls to our own
 * API get it; static files and other origins are left alone.
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiBaseUrl) || req.headers.has('Accept-Language')) return next(req);

  return next(req.clone({ setHeaders: { 'Accept-Language': inject(LocalizationService).language() } }));
};
