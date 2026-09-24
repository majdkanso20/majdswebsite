/** Saves a blob to disk through a temporary link (used for exports and file downloads). */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/** Export formats the API can produce (F-Export). */
export type ExportFormat = 'csv' | 'xlsx' | 'pdf';

export function exportFileName(baseName: string, format: ExportFormat): string {
  return `${baseName}.${format}`;
}
