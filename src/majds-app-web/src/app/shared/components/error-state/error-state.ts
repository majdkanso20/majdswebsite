import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Standard error state (U3 FR-SHELL-005): what a page shows when it could not load. Presentational
 *  only; a consumer can replace the whole thing by projecting its own content instead. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-error-state',
  imports: [TranslatePipe, MatButtonModule, MatIconModule],
  styleUrl: './error-state.scss',
  templateUrl: './error-state.html'
})
export class ErrorState {
  readonly message = input('Something went wrong.');
  readonly retryable = input(true);
  readonly retry = output<void>();
}
