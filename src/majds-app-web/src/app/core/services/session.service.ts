import { Injectable, inject } from '@angular/core';
import { LocalizationService } from '../i18n/localization.service';
import { AppSettingsService } from './app-settings.service';
import { FeaturesService } from './features.service';
import { PermissionService } from './permission.service';

/** Everything that must be loaded right after a successful sign-in, however the user signed in
 *  (password, password + 2FA, Google, ...), so each path can't drift from the others. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly permissions = inject(PermissionService);
  private readonly appSettings = inject(AppSettingsService);
  private readonly features = inject(FeaturesService);
  private readonly localization = inject(LocalizationService);

  async bootstrapAsync(): Promise<void> {
    await Promise.all([this.permissions.loadAsync(), this.appSettings.loadAsync(), this.features.loadAsync()]);
    await this.localization.applyDefault(this.appSettings.get('General.DefaultLanguage', 'en'));
  }
}
