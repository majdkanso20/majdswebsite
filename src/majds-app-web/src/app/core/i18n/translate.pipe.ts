import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocalizationService } from './localization.service';

/** {{ 'Some English text' | translate }} — impure so it re-evaluates when the language signal changes. */
@Pipe({ name: 'translate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);

  transform(key: string | null | undefined): string {
    return key ? this.localization.translate(key) : '';
  }
}
