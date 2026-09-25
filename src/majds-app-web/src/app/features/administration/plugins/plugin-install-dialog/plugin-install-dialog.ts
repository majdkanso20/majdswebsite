import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { PluginChangeDto, PluginsApiService } from '../plugins-api.service';

/**
 * Upload a plugin package (P5 FR-PLUG-040). The server verifies it (structure, checksum, platform version, trust policy,
 * that its module loads) and stages it; the result says what will happen at the next start and shows the package's SHA-256.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugin-install-dialog',
  imports: [TranslatePipe, FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatProgressBarModule],
  styleUrl: './plugin-install-dialog.scss',
  templateUrl: './plugin-install-dialog.html'
})
export class PluginInstallDialog {
  private readonly api = inject(PluginsApiService);
  private readonly dialogRef = inject(MatDialogRef<PluginInstallDialog, boolean>);
  private readonly destroyRef = inject(DestroyRef);

  readonly file = signal<File | null>(null);
  readonly uploading = signal(false);
  readonly result = signal<PluginChangeDto | null>(null);
  readonly error = signal<string | null>(null);

  /** The publisher's SHA-256, if the administrator has one to check the package against. */
  checksum = '';

  onFileChosen(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.result.set(null);
    this.error.set(null);
  }

  install(): void {
    const file = this.file();
    if (!file) return;

    this.uploading.set(true);
    this.error.set(null);
    this.api
      .install(file, this.checksum)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.uploading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.error.set(err.error?.errors?.[0] ?? err.error?.message ?? 'The package could not be installed.');
          this.uploading.set(false);
        }
      });
  }

  /** Closes and tells the list whether something was staged, so it can show the pending change. */
  close(): void {
    this.dialogRef.close(this.result() !== null);
  }
}
