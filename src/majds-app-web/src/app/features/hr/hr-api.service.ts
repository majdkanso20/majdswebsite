import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../core/models/response-dto';
import { DepartmentDto, EmployeeDto, EmployeeInput } from './hr.models';

export interface ListParams {
  page: number;
  pageSize: number;
  sort?: string;
  filter?: string;
}

@Injectable({ providedIn: 'root' })
export class HrApiService {
  private readonly http = inject(HttpClient);

  private query(params: ListParams): Record<string, string | number> {
    const query: Record<string, string | number> = { page: params.page, pageSize: params.pageSize };
    if (params.sort) query['sort'] = params.sort;
    if (params.filter) query['filter'] = params.filter;
    return query;
  }

  listDepartments(params: ListParams): Observable<PagedResponse<DepartmentDto>> {
    return this.http
      .get<ResponseDto<PagedResponse<DepartmentDto>>>(`${environment.apiBaseUrl}/departments/list`, { params: this.query(params) })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page: params.page, pageSize: params.pageSize }));
  }

  listAllDepartments(): Observable<DepartmentDto[]> {
    return this.http
      .get<ResponseDto<DepartmentDto[]>>(`${environment.apiBaseUrl}/departments/list-all`)
      .pipe(map((r) => r.data ?? []));
  }

  createDepartment(name: string, description: string | null): Observable<number> {
    return this.http
      .post<ResponseDto<number>>(`${environment.apiBaseUrl}/departments/create`, { name, description })
      .pipe(map((r) => r.data!));
  }

  updateDepartment(id: number, name: string, description: string | null): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${environment.apiBaseUrl}/departments/update`, { id, name, description }).pipe(map(() => undefined));
  }

  deleteDepartment(id: number): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${environment.apiBaseUrl}/departments/delete`, { id }).pipe(map(() => undefined));
  }

  listEmployees(params: ListParams): Observable<PagedResponse<EmployeeDto>> {
    return this.http
      .get<ResponseDto<PagedResponse<EmployeeDto>>>(`${environment.apiBaseUrl}/employees/list`, { params: this.query(params) })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page: params.page, pageSize: params.pageSize }));
  }

  createEmployee(employee: EmployeeInput): Observable<number> {
    return this.http.post<ResponseDto<number>>(`${environment.apiBaseUrl}/employees/create`, employee).pipe(map((r) => r.data!));
  }

  updateEmployee(id: number, employee: EmployeeInput): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${environment.apiBaseUrl}/employees/update`, { id, employee }).pipe(map(() => undefined));
  }

  deleteEmployee(id: number): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${environment.apiBaseUrl}/employees/delete`, { id }).pipe(map(() => undefined));
  }
}
