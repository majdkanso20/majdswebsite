import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable } from 'rxjs';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ImportJob } from '../../../core/services/imports.service';

export interface ImportDialogData {
  title: string;
  /** Uploads the chosen file and queues it as a background job (F-Export FR-EXP-003/004); returns straight away. */
  upload: (file: File) => Observable<ImportJob>;
  /** Downloads the blank template (FR-EXP-005). */
  template: (format: 'csv' | 'xlsx') => Observable<void>;
}

/**
 * Reusable "import from a file" dialog: template download, file choice, and queuing. The import itself runs in the
 * background — this only confirms it was queued; **Import history** shows progress and the per-row results.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-import-dialog',
  imports: [TranslatePipe, MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  styleUrl: './import-dialog.scss',
  templateUrl: './import-dialog.html'
})
export class ImportDialog {
  readonly data = inject<ImportDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ImportDialog, boolean>);
  private readonly destroyRef = inject(DestroyRef);

  readonly file = signal<File | null>(null);
  readonly uploading = signal(false);
  readonly queued = signal<ImportJob | null>(null);
  readonly error = signal<string | null>(null);

  onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.queued.set(null);
    this.error.set(null);
  }

  downloadTemplate(format: 'csv' | 'xlsx'): void {
    this.data.template(format).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  import(): void {
    const file = this.file();
    if (!file) return;

    this.uploading.set(true);
    this.error.set(null);
    this.data
      .upload(file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (job) => {
          this.queued.set(job);
          this.uploading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          // A problem caught before queuing (wrong type, too large, too many already in progress) comes back as a readable message.
          this.error.set(err.error?.errors?.[0] ?? err.error?.message ?? 'The import could not be queued.');
          this.uploading.set(false);
        }
      });
  }

  /** Closes and tells the list something was queued, so it can point the user at Import history. */
  close(): void {
    this.dialogRef.close(this.queued() !== null);
  }
}
