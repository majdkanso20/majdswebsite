import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocalizationService } from './localization.service';

/** {{ 'Some English text' | translate }} or {{ 'Delete {0}?' | translate: name }} — impure so it re-evaluates when the language changes. */
@Pipe({ name: 'translate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);

  transform(key: string | null | undefined, ...args: (string | number)[]): string {
    return key ? this.localization.translate(key, ...args) : '';
  }
}
