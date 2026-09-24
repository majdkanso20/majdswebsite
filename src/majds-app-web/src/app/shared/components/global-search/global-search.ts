import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatIconModule } from '@angular/material/icon';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ResponseDto } from '../../../core/models/response-dto';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

interface SearchResult {
  category: string;
  title: string;
  subtitle: string | null;
  route: string;
}

/** Header search box (F-Search): debounced call to /api/search, results grouped by category. The
 *  server only returns what the signed-in user may see, so the UI does no filtering of its own. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-global-search',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatIconModule, TranslatePipe],
  styleUrl: './global-search.scss',
  templateUrl: './global-search.html'
})
export class GlobalSearch {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly control = new FormControl('', { nonNullable: true });
  readonly searched = signal(false);

  readonly results = toSignal(
    this.control.valueChanges.pipe(
      debounceTime(250),
      map((v) => (typeof v === 'string' ? v.trim() : '')),
      distinctUntilChanged(),
      switchMap((term) => {
        if (term.length < 2) {
          this.searched.set(false);
          return of([] as SearchResult[]);
        }
        return this.http
          .get<ResponseDto<SearchResult[]>>(`${environment.apiBaseUrl}/search`, { params: { q: term } })
          .pipe(
            map((r) => {
              this.searched.set(true);
              return r.data ?? [];
            }),
            catchError(() => of([] as SearchResult[]))
          );
      })
    ),
    { initialValue: [] as SearchResult[] }
  );

  readonly groups = computed(() => {
    const byCategory = new Map<string, SearchResult[]>();
    for (const r of this.results()) byCategory.set(r.category, [...(byCategory.get(r.category) ?? []), r]);
    return [...byCategory].map(([category, items]) => ({ category, items }));
  });

  open(event: MatAutocompleteSelectedEvent): void {
    const result = event.option.value as SearchResult;
    this.control.setValue('', { emitEvent: false });
    void this.router.navigateByUrl(result.route);
  }

  display(): string {
    return ''; // keep the box empty after choosing; the selection navigates away
  }
}
