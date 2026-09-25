import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../../core/models/response-dto';

export interface JobDto {
  name: string;
  intervalMinutes: number;
  lastRunAt: string | null;
  lastSuccess: boolean | null;
  nextRunAt: string | null;
  /** Cron expression (UTC) when the job runs on one instead of a fixed interval. */
  cron: string | null;
  consecutiveFailures: number;
}

export type BackgroundJobStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed';

export interface BackgroundJobDto {
  id: string;
  type: string;
  status: BackgroundJobStatus;
  attempts: number;
  maxAttempts: number;
  userName: string | null;
  lastError: string | null;
  createdAt: string;
  nextAttemptAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface JobRunDto {
  id: number;
  jobName: string;
  startedAt: string;
  durationMs: number;
  success: boolean;
  error: string | null;
  trigger: string;
}

@Injectable({ providedIn: 'root' })
export class JobsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/jobs`;

  list(): Observable<JobDto[]> {
    return this.http.get<ResponseDto<JobDto[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  runs(page: number, pageSize: number, sort?: string): Observable<PagedResponse<JobRunDto>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (sort) params['sort'] = sort;
    return this.http
      .get<ResponseDto<PagedResponse<JobRunDto>>>(`${this.baseUrl}/runs`, { params })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page, pageSize }));
  }

  /** The queued one-off jobs (FR-JOB-003), newest first, optionally only one status. */
  queue(page: number, pageSize: number, status?: string, sort?: string): Observable<PagedResponse<BackgroundJobDto>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (status) params['status'] = status;
    if (sort) params['sort'] = sort;
    return this.http
      .get<ResponseDto<PagedResponse<BackgroundJobDto>>>(`${this.baseUrl}/queue`, { params })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page, pageSize }));
  }

  /** Puts a failed job back in the queue with a fresh set of attempts. */
  retry(jobId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/retry`, { jobId }).pipe(map(() => undefined));
  }

  delete(jobId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/delete`, { jobId }).pipe(map(() => undefined));
  }

  /** Resolves to false when the job was already running and the request was skipped. */
  run(jobName: string): Observable<boolean> {
    return this.http.post<ResponseDto<boolean>>(`${this.baseUrl}/run`, { jobName }).pipe(map((r) => r.data ?? false));
  }
}
