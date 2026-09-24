import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResponse, ResponseDto } from '../models/response-dto';

export interface PingListItem {
  id: string;
  message: string;
  createdAt: string;
}

/**
 * Thin typed client for the smoke-test Diagnostics module (Phase 0) — proves the Angular app can
 * reach the Web API through the full P1–P4 pipeline. Each feature gets its own service like this.
 */
@Injectable({ providedIn: 'root' })
export class DiagnosticsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/diagnostics`;

  ping(): Observable<string> {
    return this.http
      .get<ResponseDto<string>>(`${this.baseUrl}/ping`)
      .pipe(map((response) => response.data ?? ''));
  }

  listPings(page = 1, pageSize = 10): Observable<PagedResponse<PingListItem>> {
    return this.http
      .get<ResponseDto<PagedResponse<PingListItem>>>(`${this.baseUrl}/ping/list`, {
        params: { page, pageSize, sort: 'createdAt:desc' }
      })
      .pipe(map((response) => response.data ?? { items: [], totalCount: 0, page, pageSize }));
  }
}
