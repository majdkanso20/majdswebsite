import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../../core/models/response-dto';
import { saveBlob } from '../../../core/utils/download';
import { CreateUserRequest, UpdateUserRequest, UserDto } from './user.models';

export interface ListUsersParams {
  page: number;
  pageSize: number;
  sort?: string;
  filter?: string;
  isActive?: boolean;
  role?: string;
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

  resetTwoFactor(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/reset-two-factor`, { userId }).pipe(map(() => undefined));
  }

  exportCsv(filter?: string): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/export`, { params: filter ? { filter } : {}, responseType: 'blob' })
      .pipe(map((blob) => saveBlob(blob, 'users.csv')));
  }

  unlock(userId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/unlock`, { userId }).pipe(map(() => undefined));
  }
}
