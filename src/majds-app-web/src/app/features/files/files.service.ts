import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResponse, ResponseDto } from '../../core/models/response-dto';

export interface FileDto {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  ownerName: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class FilesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/files`;

  list(page: number, pageSize: number, sort?: string, filter?: string): Observable<PagedResponse<FileDto>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (sort) params['sort'] = sort;
    if (filter) params['filter'] = filter;
    return this.http
      .get<ResponseDto<PagedResponse<FileDto>>>(`${this.baseUrl}/list`, { params })
      .pipe(map((r) => r.data ?? { items: [], totalCount: 0, page, pageSize }));
  }

  upload(file: File): Observable<FileDto> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ResponseDto<FileDto>>(`${this.baseUrl}/upload`, body).pipe(map((r) => r.data!));
  }

  /** Downloads through HttpClient (so the bearer token is attached) and saves via a temporary link. */
  download(file: FileDto): Observable<void> {
    return this.http
      .get(`${this.baseUrl}/download`, { params: { fileId: file.id }, responseType: 'blob' })
      .pipe(
        map((blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = file.fileName;
          link.click();
          URL.revokeObjectURL(url);
        })
      );
  }

  delete(fileId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/delete`, { fileId }).pipe(map(() => undefined));
  }
}
