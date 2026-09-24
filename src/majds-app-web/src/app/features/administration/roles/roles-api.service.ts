import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../../core/models/response-dto';
import { PermissionGroup, RoleDto } from './role.models';

export interface ListRolesParams {
  page: number;
  pageSize: number;
  sort?: string;
  filter?: string;
}

@Injectable({ providedIn: 'root' })
export class RolesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/roles`;

  list(params: ListRolesParams): Observable<PagedResponse<RoleDto>> {
    const query: Record<string, string | number> = { page: params.page, pageSize: params.pageSize };
    if (params.sort) query['sort'] = params.sort;
    if (params.filter) query['filter'] = params.filter;

    return this.http
      .get<ResponseDto<PagedResponse<RoleDto>>>(`${this.baseUrl}/list`, { params: query })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page: params.page, pageSize: params.pageSize }));
  }

  create(name: string, displayName: string | null, isDefault: boolean): Observable<string> {
    return this.http
      .post<ResponseDto<string>>(`${this.baseUrl}/create`, { name, displayName, isDefault })
      .pipe(map((r) => r.data!));
  }

  update(roleId: string, displayName: string | null, isDefault: boolean): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${this.baseUrl}/update`, { roleId, displayName, isDefault })
      .pipe(map(() => undefined));
  }

  delete(roleId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/delete`, { roleId }).pipe(map(() => undefined));
  }

  getPermissionTree(): Observable<PermissionGroup[]> {
    return this.http
      .get<ResponseDto<PermissionGroup[]>>(`${environment.apiBaseUrl}/permissions/tree`)
      .pipe(map((r) => r.data ?? []));
  }

  getRolePermissions(roleId: string): Observable<string[]> {
    return this.http
      .get<ResponseDto<string[]>>(`${environment.apiBaseUrl}/roles/permissions/get`, { params: { roleId } })
      .pipe(map((r) => r.data ?? []));
  }

  updateRolePermissions(roleId: string, permissionNames: string[]): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${environment.apiBaseUrl}/roles/permissions/update`, { roleId, permissionNames })
      .pipe(map(() => undefined));
  }
}
