import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Standard empty state (U3 FR-SHELL-005), overridable via content projection (U1 FR-UI-005). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-empty-state',
  imports: [TranslatePipe, MatIconModule],
  styleUrl: './empty-state.scss',
  templateUrl: './empty-state.html'
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly message = input('Nothing here yet.');
}
