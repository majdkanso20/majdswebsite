import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../../core/models/response-dto';
import { ExportFormat, exportFileName, saveBlob } from '../../../core/utils/download';
import { AuditLogEntryDetailDto, AuditLogEntryDto, AuditLogFilterParams } from './audit-log.models';

export interface ListAuditLogParams extends AuditLogFilterParams {
  page: number;
  pageSize: number;
  sort?: string;
  filter?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditLogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/audit`;

  /** Downloads the entries matching the current filters as CSV, Excel or PDF. */
  export(format: ExportFormat, filters: AuditLogFilterParams): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/export`, { params: { ...this.filterQuery(filters), format }, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, exportFileName('audit-log', format))));
  }

  list(params: ListAuditLogParams): Observable<PagedResponse<AuditLogEntryDto>> {
    const query: Record<string, string | number> = { page: params.page, pageSize: params.pageSize, ...this.filterQuery(params) };
    if (params.sort) query['sort'] = params.sort;
    if (params.filter) query['filter'] = params.filter;

    return this.http
      .get<ResponseDto<PagedResponse<AuditLogEntryDto>>>(`${this.baseUrl}/list`, { params: query })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page: params.page, pageSize: params.pageSize }));
  }

  get(id: number): Observable<AuditLogEntryDetailDto> {
    return this.http
      .get<ResponseDto<AuditLogEntryDetailDto>>(`${this.baseUrl}/get`, { params: { id } })
      .pipe(map((r) => r.data!));
  }

  private filterQuery(filters: AuditLogFilterParams): Record<string, string> {
    const query: Record<string, string> = {};
    if (filters.from) query['from'] = filters.from;
    if (filters.to) query['to'] = filters.to;
    if (filters.outcome) query['outcome'] = filters.outcome;
    return query;
  }
}
