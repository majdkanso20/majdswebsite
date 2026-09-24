/** Mirrors the backend's ResponseDto<T> envelope (P1) — every API call unwraps this shape. */
export interface ResponseDto<T> {
  code: number;
  message: string;
  data: T | null;
}

/** Mirrors the backend's PagedResponse<T> (F-Data). */
export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
