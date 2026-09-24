import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

export type WidgetKind = 'kpi' | 'chart' | 'feed';

/** A widget the current user may see. Widgets the user hid still arrive (visible=false) so Customize can bring them back. */
export interface DashboardWidget {
  key: string;
  title: string;
  kind: WidgetKind;
  columns: number;
  visible: boolean;
}

export interface KpiData {
  value: string;
  caption: string | null;
}

export interface ChartData {
  points: { label: string; value: number }[];
}

export interface FeedData {
  items: { title: string; subtitle: string | null; at: string | null }[];
}

export type WidgetData = KpiData | ChartData | FeedData;

/** Dashboard widgets (F-Dashboard): the list is permission-filtered by the server, and every widget loads its own data. */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/dashboard`;

  widgets(): Observable<DashboardWidget[]> {
    return this.http.get<ResponseDto<DashboardWidget[]>>(`${this.baseUrl}/widgets`).pipe(map((r) => r.data ?? []));
  }

  data(key: string): Observable<WidgetData | null> {
    return this.http
      .get<ResponseDto<WidgetData>>(`${this.baseUrl}/data`, { params: { key } })
      .pipe(map((r) => r.data));
  }

  /**
   * Saves the caller's layout as their own User-scope setting (FR-DASH-004). A layout with nothing hidden
   * and the default order is stored as an empty value, which removes the personal override.
   */
  saveLayout(widgets: DashboardWidget[], isDefaultOrder: boolean): Observable<unknown> {
    const hidden = widgets.filter((w) => !w.visible).map((w) => w.key);
    const value =
      isDefaultOrder && hidden.length === 0 ? '' : JSON.stringify({ order: widgets.map((w) => w.key), hidden });
    return this.http.post(`${environment.apiBaseUrl}/settings/update-mine`, {
      items: [{ name: 'Dashboard.Layout', value }]
    });
  }
}
