import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/**
 * Standard loading state (U3 FR-SHELL-005). Presentational only; override its rendering entirely
 * via content projection (U1 FR-UI-005) if a consumer needs a skeleton instead of a spinner.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-loading-state',
  imports: [MatProgressSpinnerModule],
  styleUrl: './loading-state.scss',
  templateUrl: './loading-state.html'
})
export class LoadingState {
  readonly message = input<string>('Loading…');
}
