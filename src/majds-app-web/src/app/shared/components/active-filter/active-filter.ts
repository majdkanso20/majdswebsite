import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Shows the "?q=" filter a list was opened with (e.g. from global search) and lets the user drop it. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-active-filter',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './active-filter.html',
  styleUrl: './active-filter.scss'
})
export class ActiveFilter {
  private readonly router = inject(Router);
  readonly value = input.required<string>();

  clear(): void {
    void this.router.navigate([], { queryParams: { q: null }, queryParamsHandling: 'merge' });
  }
}
