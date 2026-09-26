import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../../core/models/response-dto';
import { ExportFormat, exportFileName, saveBlob } from '../../../core/utils/download';
import { ImportResult } from '../../../shared/components/import-dialog/import-dialog';
import { CreateUserRequest, UpdateUserRequest, UserDto } from './user.models';

export interface ListUsersParams {
  page: number;
  pageSize: number;
  sort?: string;
  filter?: string;
  isActive?: boolean;
  role?: string;
}

/** One user's permissions explained (FR-AUTHZ-003): what their roles give, what was granted or denied to them directly, and the result. */
export interface UserPermissionsDto {
  userId: string;
  isSuperAdmin: boolean;
  fromRoles: string[];
  granted: string[];
  denied: string[];
  effective: string[];
}

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  list(params: ListUsersParams): Observable<PagedResponse<UserDto>> {
    const query: Record<string, string | number | boolean> = { page: params.page, pageSize: params.pageSize };
    if (params.sort) query['sort'] = params.sort;
    if (params.filter) query['filter'] = params.filter;
    if (params.isActive !== undefined) query['isActive'] = params.isActive;
    if (params.role) query['role'] = params.role;

    return this.http
      .get<ResponseDto<PagedResponse<UserDto>>>(`${this.baseUrl}/list`, { params: query })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page: params.page, pageSize: params.pageSize }));
  }

  create(request: CreateUserRequest): Observable<string> {
    return this.http.post<ResponseDto<string>>(`${this.baseUrl}/create`, request).pipe(map((r) => r.data!));
  }

  update(request: UpdateUserRequest): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/update`, request).pipe(map(() => undefined));
  }

  setActivation(userId: string, isActive: boolean): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${this.baseUrl}/set-activation`, { userId, isActive })
      .pipe(map(() => undefined));
  }

  delete(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/delete`, { userId }).pipe(map(() => undefined));
  }

  resetPassword(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/reset-password`, { userId }).pipe(map(() => undefined));
  }

  getPermissions(userId: string): Observable<UserPermissionsDto> {
    return this.http
      .get<ResponseDto<UserPermissionsDto>>(`${this.baseUrl}/permissions/get`, { params: { userId } })
      .pipe(map((r) => r.data!));
  }

  updatePermissions(userId: string, granted: string[], denied: string[]): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${this.baseUrl}/permissions/update`, { userId, granted, denied })
      .pipe(map(() => undefined));
  }

  resetTwoFactor(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/reset-two-factor`, { userId }).pipe(map(() => undefined));
  }

  /** Downloads the users the list currently shows (same filters) as CSV, Excel or PDF. */
  export(format: ExportFormat, filters: { filter?: string; isActive?: boolean; role?: string } = {}): Observable<void> {
    const params: Record<string, string | boolean> = { format };
    if (filters.filter) params['filter'] = filters.filter;
    if (filters.isActive !== undefined) params['isActive'] = filters.isActive;
    if (filters.role) params['role'] = filters.role;
    return this.http
      .get(`${this.baseUrl}/export`, { params, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, exportFileName('users', format))));
  }

  /** Creates users from a .csv or .xlsx file; valid rows are imported, invalid ones come back with a reason. */
  importUsers(file: File): Observable<ImportResult> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ResponseDto<ImportResult>>(`${this.baseUrl}/import`, body).pipe(map((r) => r.data!));
  }

  importTemplate(format: 'csv' | 'xlsx'): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/import-template`, { params: { format }, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, `users-import-template.${format}`)));
  }

  unlock(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/unlock`, { userId }).pipe(map(() => undefined));
  }
}
