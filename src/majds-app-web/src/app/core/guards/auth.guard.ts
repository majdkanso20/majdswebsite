import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { PermissionService } from '../services/permission.service';
import { FeaturesService } from '../services/features.service';
import { LocalizationService } from '../i18n/localization.service';
import { AppSettingsService } from '../services/app-settings.service';
import { PluginsService } from '../services/plugins.service';

/**
 * Redirects unauthenticated users to /login (U3 FR-SHELL-006). Also lazily loads permissions on the
 * first protected navigation of a session (e.g. after a page reload where the token already exists
 * in storage but PermissionService hasn't fetched anything yet), and — same choke point, so it can't
 * be missed on any entry path — the enabled plugins' menu/route contributions (P5 FR-SHELL-008).
 */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const permissions = inject(PermissionService);
  const appSettings = inject(AppSettingsService);
  const localization = inject(LocalizationService);
  const features = inject(FeaturesService);
  const plugins = inject(PluginsService);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  if (!permissions.isLoaded()) {
    await permissions.loadAsync();
  }

  if (!features.isLoaded()) {
    await features.loadAsync();
  }

  if (!appSettings.isLoaded()) {
    await appSettings.loadAsync();
    await localization.applyDefault(appSettings.get('General.DefaultLanguage', 'en'));
  }

  if (!plugins.isLoaded()) {
    await plugins.loadAsync();
  }

  return true;
};
