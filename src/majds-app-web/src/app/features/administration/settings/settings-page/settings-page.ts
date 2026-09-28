import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppSettingsService } from '../../../../core/services/app-settings.service';
import { SettingsApiService } from '../settings-api.service';
import { SettingDataType, SettingDto } from '../settings.models';

/**
 * Admin settings page (F-Settings): one form control per setting, grouped by SettingDefinition.Group.
 * Saves every setting on submit — the backend deletes an override that matches the default rather than
 * storing it, so this stays simple instead of diffing changed fields client-side.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-settings-page',
  imports: [TranslatePipe, 
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatCardModule,
    MatProgressBarModule
  ],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss'
})
export class SettingsPage {
  private readonly api = inject(SettingsApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly appSettings = inject(AppSettingsService);

  readonly SettingDataType = SettingDataType;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly settings = signal<SettingDto[]>([]);
  readonly groups = computed(() => [...new Set(this.settings().map((s) => s.group))]);

  readonly form: FormGroup = this.fb.group({});
  /** Sensitive settings the admin marked for removal; a blank sensitive field otherwise means "keep". */
  readonly toClear = signal<ReadonlySet<string>>(new Set());
  readonly testing = signal(false);

  constructor() {
    this.load();
  }

  settingsForGroup(group: string): SettingDto[] {
    return this.settings().filter((s) => s.group === group);
  }

  // mat-slide-toggle bound via [formControlName] crashes on click here (an RxJS Subject.next()
  // iterator error from its FocusMonitor subscription) — bound directly instead, bypassing that path.
  setToggle(name: string, checked: boolean): void {
    this.form.controls[name].setValue(checked);
  }

  save(): void {
    this.saving.set(true);
    // FormGroup.get(path) splits string paths on '.' for nested-group lookup, and setting names are
    // dotted ("General.ApplicationName") — .controls is a plain dictionary keyed by the literal name.
    const items = this.settings().map((s) => ({
      name: s.name,
      value: String(this.form.controls[s.name].value ?? ''),
      clear: this.toClear().has(s.name)
    }));

    this.api.update(items).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open('Settings saved.', 'Dismiss', { duration: 3000 });
        this.load();
        void this.appSettings.loadAsync(); // so the application name and the delivery-mode banner follow the change without a reload
      },
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Failed to save settings.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  toggleClear(name: string): void {
    this.toClear.update((current) => {
      const next = new Set(current);
      if (!next.delete(name)) next.add(name);
      return next;
    });
  }

  testEmail(): void {
    this.testing.set(true);
    this.api.testEmail().subscribe({
      next: () => {
        this.testing.set(false);
        this.snackBar.open('Test email sent to your address.', 'Dismiss', { duration: 4000 });
      },
      error: (err) => {
        this.testing.set(false);
        const message = (err as { error?: { errors?: string[] } })?.error?.errors?.[0] ?? 'Something went wrong.';
        this.snackBar.open(message, 'Dismiss', { duration: 8000 });
      }
    });
  }

  private load(): void {
    this.toClear.set(new Set());
    this.loading.set(true);
    this.api.list().subscribe((settings) => {
      for (const name of Object.keys(this.form.controls)) this.form.removeControl(name);
      for (const setting of settings) this.form.addControl(setting.name, this.fb.control(this.parseValue(setting)));

      this.settings.set(settings);
      this.loading.set(false);
    });
  }

  private parseValue(setting: SettingDto): string | boolean | number {
    switch (setting.dataType) {
      case SettingDataType.Boolean:
        return setting.value.toLowerCase() === 'true';
      case SettingDataType.Integer:
        return Number(setting.value);
      default:
        return setting.value;
    }
  }
}
