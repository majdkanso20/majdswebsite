import { ActiveFilter } from '../../../shared/components/active-filter/active-filter';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { FileDto, FilesService } from '../files.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-files-list',
  imports: [ActiveFilter, DataGrid, HasPermissionDirective, TranslatePipe, MatButtonModule, MatIconModule],
  styleUrl: './files-list.scss',
  templateUrl: './files-list.html'
})
export class FilesList {
  private readonly filesApi = inject(FilesService);
  private readonly snackBar = inject(MatSnackBar);

  readonly columns: GridColumn<FileDto>[] = [
    { key: 'fileName', header: 'Name', sortable: true },
    { key: 'size', header: 'Size', sortable: true, value: (f) => FilesList.formatSize(f.size) },
    { key: 'ownerName', header: 'Owner', value: (f) => f.ownerName ?? '—' },
    { key: 'createdAt', header: 'Uploaded', sortable: true, value: (f) => new Date(f.createdAt + 'Z').toLocaleString() }
  ];

  readonly rows = signal<FileDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly uploading = signal(false);

  readonly filter = signal('');
  private page = 0;
  private pageSize = 20;
  private sort = 'createdAt:desc';

  constructor() {
    inject(ActivatedRoute).queryParamMap.subscribe((params) => {
      this.filter.set(params.get('q') ?? '');
      this.page = 0;
      this.load();
    });
  }

  onPage(event: GridPage): void {
    this.page = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  onSort(event: GridSort): void {
    this.sort = event.direction ? `${event.active}:${event.direction}` : '';
    this.load();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.uploading.set(true);
    this.filesApi.upload(file).subscribe({
      next: () => {
        this.uploading.set(false);
        this.snackBar.open('File uploaded.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => {
        this.uploading.set(false);
        this.showError(err);
      }
    });
  }

  download(file: FileDto): void {
    this.filesApi.download(file).subscribe({ error: (err) => this.showError(err) });
  }

  remove(file: FileDto): void {
    if (!confirm(`Delete "${file.fileName}"?`)) return;
    this.filesApi.delete(file.id).subscribe({
      next: () => {
        this.snackBar.open('File deleted.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  private static formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private load(): void {
    this.loading.set(true);
    this.filesApi.list(this.page + 1, this.pageSize, this.sort || undefined, this.filter() || undefined).subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private showError(err: unknown): void {
    const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }
}
