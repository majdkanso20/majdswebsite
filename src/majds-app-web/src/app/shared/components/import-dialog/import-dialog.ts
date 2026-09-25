import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable } from 'rxjs';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** What an import returns (F-Export FR-EXP-003): how many rows went in and, for the rest, the row and why. */
export interface ImportResult {
  total: number;
  succeeded: number;
  failed: number;
  errors: { row: number; reason: string }[];
}

export interface ImportDialogData {
  title: string;
  /** Uploads the chosen file; the resource's own API decides what a row means. */
  upload: (file: File) => Observable<ImportResult>;
  /** Downloads the blank template (FR-EXP-005). */
  template: (format: 'csv' | 'xlsx') => Observable<void>;
}

/** Reusable "import from a file" dialog: template download, file choice, and a per-row result summary. */
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
  readonly result = signal<ImportResult | null>(null);
  readonly error = signal<string | null>(null);

  onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.result.set(null);
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
        next: (result) => {
          this.result.set(result);
          this.uploading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          // A whole-file problem (wrong type, missing column) comes back as a validation error with a readable message.
          this.error.set(err.error?.errors?.[0] ?? err.error?.message ?? 'The import failed.');
          this.uploading.set(false);
        }
      });
  }

  /** Closes and tells the list whether it should reload (something was imported). */
  close(): void {
    this.dialogRef.close((this.result()?.succeeded ?? 0) > 0);
  }
}
