import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';
import { saveBlob } from '../utils/download';

export type ImportJobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

export interface ImportRowError {
  row: number;
  reason: string;
}

export interface ImportJob {
  id: string;
  source: string;
  title: string;
  fileName: string;
  status: ImportJobStatus;
  total: number;
  succeeded: number;
  errors: ImportRowError[] | null;
  error: string | null;
  createdAt: string;
  completedAt: string | null;
}

/** Background imports (F-Export FR-EXP-003/004): start one, watch its status, see the per-row results. */
@Injectable({ providedIn: 'root' })
export class ImportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/imports`;

  /** Queues an import of the given file against one source; returns immediately. */
  start(source: string, file: File): Observable<ImportJob> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ResponseDto<ImportJob>>(`${this.baseUrl}/start`, body, { params: { source } }).pipe(map((r) => r.data!));
  }

  list(): Observable<ImportJob[]> {
    return this.http.get<ResponseDto<ImportJob[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  template(source: string, format: 'csv' | 'xlsx'): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/template`, { params: { source, format }, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, `${source}-import-template.${format}`)));
  }
}
