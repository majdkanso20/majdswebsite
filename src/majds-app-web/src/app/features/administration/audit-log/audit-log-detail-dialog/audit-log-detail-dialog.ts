import { FormattingService } from '../../../../core/i18n/formatting.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { AuditLogApiService } from '../audit-log-api.service';
import { AuditLogEntryDetailDto } from '../audit-log.models';

/** One audit entry in full (FR-AUDIT-003 "get"): request metadata, redacted parameters, and the
 *  property-level before/after values of every entity the action changed (FR-AUDIT-002). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-audit-log-detail-dialog',
  imports: [TranslatePipe, MatDialogModule, MatButtonModule],
  styleUrl: './audit-log-detail-dialog.scss',
  templateUrl: './audit-log-detail-dialog.html'
})
export class AuditLogDetailDialog {
  private readonly api = inject(AuditLogApiService);
  private readonly data = inject<{ id: number }>(MAT_DIALOG_DATA);

  readonly entry = signal<AuditLogEntryDetailDto | null>(null);

  private readonly fmt = inject(FormattingService);

  constructor() {
    this.api.get(this.data.id).subscribe((entry) => this.entry.set(entry));
  }

  when(entry: AuditLogEntryDetailDto): string {
    return this.fmt.date(entry.createdAt);
  }
}
