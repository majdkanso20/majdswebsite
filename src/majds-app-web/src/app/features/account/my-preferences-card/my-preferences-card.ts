import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { LANGUAGES, LocalizationService } from '../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MySetting, UserSettingsService } from '../../../core/services/user-settings.service';

/** The user's personal language and time zone (F-Account FR-ACCT-004), stored as User-scope settings
 *  that override the Application defaults just for them. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-my-preferences-card',
  imports: [FormsModule, TranslatePipe, MatButtonModule, MatCardModule, MatFormFieldModule, MatSelectModule],
  styleUrl: './my-preferences-card.scss',
  templateUrl: './my-preferences-card.html'
})
export class MyPreferencesCard {
  private readonly settings = inject(UserSettingsService);
  private readonly localization = inject(LocalizationService);
  private readonly snackBar = inject(MatSnackBar);

  readonly languages = LANGUAGES;
  readonly timezones = ['UTC', ...Intl.supportedValuesOf('timeZone').filter((z) => z !== 'UTC')];
  readonly currencies = Intl.supportedValuesOf('currency');

  readonly language = signal('en');
  readonly timezone = signal('UTC');
  readonly currency = signal('USD');
  readonly defaults = signal<Record<string, string>>({});
  readonly saving = signal(false);

  constructor() {
    void this.settings.getMineAsync().then((rows) => {
      const byName = (n: string): MySetting | undefined => rows.find((r) => r.name === n);
      this.language.set(byName('General.DefaultLanguage')?.value ?? 'en');
      this.timezone.set(byName('Appearance.Timezone')?.value ?? 'UTC');
      this.currency.set(byName('Appearance.Currency')?.value ?? 'USD');
      this.defaults.set(Object.fromEntries(rows.map((r) => [r.name, r.defaultValue])));
    });
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      await this.settings.saveAsync([
        { name: 'General.DefaultLanguage', value: this.language() },
        { name: 'Appearance.Timezone', value: this.timezone() },
        { name: 'Appearance.Currency', value: this.currency() }
      ]);
      await this.localization.set(this.language());
      this.snackBar.open('Preferences saved.', 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Something went wrong.', 'Dismiss', { duration: 4000 });
    } finally {
      this.saving.set(false);
    }
  }

  reset(): void {
    this.language.set(this.defaults()['General.DefaultLanguage'] ?? 'en');
    this.timezone.set(this.defaults()['Appearance.Timezone'] ?? 'UTC');
    this.currency.set(this.defaults()['Appearance.Currency'] ?? 'USD');
  }
}
