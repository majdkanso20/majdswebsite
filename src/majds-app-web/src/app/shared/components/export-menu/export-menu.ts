import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ExportFormat } from '../../../core/utils/download';

/** One "Export" button offering CSV, Excel and PDF (F-Export FR-EXP-001/002). The list decides what to download. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-export-menu',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, TranslatePipe],
  templateUrl: './export-menu.html'
})
export class ExportMenu {
  readonly chosen = output<ExportFormat>();

  readonly formats: { format: ExportFormat; label: string; icon: string }[] = [
    { format: 'csv', label: 'CSV', icon: 'description' },
    { format: 'xlsx', label: 'Excel', icon: 'table_chart' },
    { format: 'pdf', label: 'PDF', icon: 'picture_as_pdf' }
  ];
}
