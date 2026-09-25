import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';
import { saveBlob } from '../utils/download';

export type ExportJobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

export interface ExportJob {
  id: string;
  source: string;
  title: string;
  format: string;
  status: ExportJobStatus;
  fileId: string | null;
  fileName: string | null;
  size: number | null;
  error: string | null;
  createdAt: string;
  completedAt: string | null;
}

/** Background exports (F-Export FR-EXP-004): start one, watch its status, download the finished file. */
@Injectable({ providedIn: 'root' })
export class ExportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/exports`;

  /** Queues an export of a dataset with the list's current filters; returns immediately. */
  start(source: string, format: 'csv' | 'xlsx', filters: Record<string, string> = {}): Observable<ExportJob> {
    return this.http
      .post<ResponseDto<ExportJob>>(`${this.baseUrl}/start`, { source, format, filters })
      .pipe(map((r) => r.data!));
  }

  list(): Observable<ExportJob[]> {
    return this.http.get<ResponseDto<ExportJob[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  download(job: ExportJob): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/download`, { params: { jobId: job.id }, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, job.fileName ?? `${job.source}.${job.format}`)));
  }
}

/** Drops empty values so only real filters are sent. */
export function activeFilters(values: Record<string, string | boolean | undefined | null>): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && value !== '') result[key] = String(value);
  }
  return result;
}
