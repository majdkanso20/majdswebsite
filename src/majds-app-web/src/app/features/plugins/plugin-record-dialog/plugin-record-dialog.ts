import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { PluginUiField } from '../plugin.models';

export interface PluginRecordDialogData {
  title: string;
  fields: PluginUiField[];
  record: Record<string, unknown> | null;
}

/**
 * Schema-driven create/edit form: builds its FormGroup at runtime from the plugin's declared
 * `fields` (P5's metadata-renderer path, FR-PLUG-015) instead of a hand-written form per plugin —
 * the same generic-component idea as the shared DataGrid, just for the write side.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugin-record-dialog',
  imports: [TranslatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  styleUrl: './plugin-record-dialog.scss',
  templateUrl: './plugin-record-dialog.html'
})
export class PluginRecordDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<PluginRecordDialog, Record<string, unknown>>);
  readonly data = inject<PluginRecordDialogData>(MAT_DIALOG_DATA);

  readonly isEdit = this.data.record !== null;

  readonly form = this.fb.group(
    Object.fromEntries(
      this.data.fields.map((field) => [
        field.key,
        [this.data.record?.[field.key] ?? null, field.required ? [Validators.required] : []]
      ])
    )
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close(this.form.getRawValue());
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
