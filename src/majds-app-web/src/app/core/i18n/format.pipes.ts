import { Pipe, PipeTransform, inject } from '@angular/core';
import { DateStyle, FormattingService } from './formatting.service';

/** {{ item.createdAt | localDate }} or {{ x | localDate: 'short' }}: in the user's language and time zone. Impure so a language or zone change shows at once. */
@Pipe({ name: 'localDate', pure: false })
export class LocalDatePipe implements PipeTransform {
  private readonly formatting = inject(FormattingService);

  transform(value: string | number | Date | null | undefined, style: DateStyle = 'medium'): string {
    return this.formatting.date(value, style);
  }
}

/** {{ count | localNumber }}: digits and separators of the active language. */
@Pipe({ name: 'localNumber', pure: false })
export class LocalNumberPipe implements PipeTransform {
  private readonly formatting = inject(FormattingService);

  transform(value: number | string | null | undefined, maximumFractionDigits?: number): string {
    return this.formatting.number(value, maximumFractionDigits === undefined ? undefined : { maximumFractionDigits });
  }
}

/** {{ amount | localCurrency }} in the user's currency, or {{ amount | localCurrency: 'JOD' }}. */
@Pipe({ name: 'localCurrency', pure: false })
export class LocalCurrencyPipe implements PipeTransform {
  private readonly formatting = inject(FormattingService);

  transform(value: number | string | null | undefined, currency?: string): string {
    return this.formatting.money(value, currency);
  }
}
