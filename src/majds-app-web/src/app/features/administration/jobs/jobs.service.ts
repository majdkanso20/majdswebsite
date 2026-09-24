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

  /** Resolves to false when the job was already running and the request was skipped. */
  run(jobName: string): Observable<boolean> {
    return this.http.post<ResponseDto<boolean>>(`${this.baseUrl}/run`, { jobName }).pipe(map((r) => r.data ?? false));
  }
}
