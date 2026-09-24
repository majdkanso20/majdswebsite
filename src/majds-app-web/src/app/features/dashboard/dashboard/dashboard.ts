import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { DiagnosticsApiService, PingListItem } from '../../../core/services/diagnostics-api.service';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { catchError, of } from 'rxjs';

/**
 * Baseline dashboard widget (F-Dashboard) that also serves as the frontend's connectivity proof
 * for Phase 0/1: it calls the real Web API (Diagnostics module) through HttpClient and renders
 * loading / empty / data states via the shared components (U3 FR-SHELL-005).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-dashboard',
  imports: [DatePipe, MatCardModule, MatListModule, LoadingState, EmptyState],
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html'
})
export class Dashboard {
  private readonly diagnosticsApi = inject(DiagnosticsApiService);

  private readonly http = inject(HttpClient);

  readonly loadFailed = signal(false);

  /** Readiness probe (F-Health) — lives outside /api, so strip that suffix from the API base URL. */
  readonly health = toSignal(
    this.http
      .get<{ status: string; checks: { name: string; status: string }[] }>(
        `${environment.apiBaseUrl.replace(/\/api$/, '')}/health/ready`
      )
      .pipe(catchError(() => of(null))),
    { initialValue: undefined }
  );

  readonly ping = toSignal(
    this.diagnosticsApi.ping().pipe(
      catchError(() => {
        this.loadFailed.set(true);
        return of(null);
      })
    ),
    { initialValue: undefined }
  );

  readonly pings = toSignal(
    this.diagnosticsApi.listPings().pipe(
      catchError(() => {
        this.loadFailed.set(true);
        return of({ items: [] as PingListItem[], totalCount: 0, page: 1, pageSize: 10 });
      })
    ),
    { initialValue: undefined }
  );
}
