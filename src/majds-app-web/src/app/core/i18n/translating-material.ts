import { Injectable, effect, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { MatSnackBar, MatSnackBarConfig, MatSnackBarRef, TextOnlySnackBar } from '@angular/material/snack-bar';
import { LocalizationService } from './localization.service';

/** Snackbars everywhere pass plain English text, so translating here covers every message (and any
 *  server message that matches a known string) without touching each call site. Unknown text passes through. */
@Injectable({ providedIn: 'root' })
export class TranslatingSnackBar extends MatSnackBar {
  private readonly l10n = inject(LocalizationService);

  override open(message: string, action = '', config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> {
    return super.open(this.l10n.translate(message), action ? this.l10n.translate(action) : action, config);
  }
}

/** Material's paginator ships English labels; this re-labels it whenever the language changes. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly l10n = inject(LocalizationService);

  constructor() {
    super();
    effect(() => {
      this.l10n.language();
      const t = (key: string) => this.l10n.translate(key);
      this.itemsPerPageLabel = t('Items per page:');
      this.nextPageLabel = t('Next page');
      this.previousPageLabel = t('Previous page');
      this.firstPageLabel = t('First page');
      this.lastPageLabel = t('Last page');
      this.getRangeLabel = (page, pageSize, length) => {
        if (length === 0) return `0 ${t('of')} 0`;
        const start = page * pageSize;
        return `${start + 1} – ${Math.min(start + pageSize, length)} ${t('of')} ${length}`;
      };
      this.changes.next();
    });
  }
}
