import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { ResponseDto } from '../../../../core/models/response-dto';
import { FeaturesService } from '../../../../core/services/features.service';
import { PermissionService } from '../../../../core/services/permission.service';
import { LocalizationService } from '../../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

interface FeatureDto {
  name: string;
  displayName: string;
  description: string | null;
  isEnabled: boolean;
  defaultEnabled: boolean;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-features-page',
  imports: [TranslatePipe, MatCardModule, MatSlideToggleModule],
  styleUrl: './features-page.scss',
  templateUrl: './features-page.html'
})
export class FeaturesPage {
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);
  private readonly featuresService = inject(FeaturesService);
  readonly canEdit = inject(PermissionService).has('Features.Edit');

  readonly features = signal<FeatureDto[]>([]);

  constructor() {
    void this.load();
  }

  async toggle(feature: FeatureDto, enabled: boolean): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${environment.apiBaseUrl}/features/set`, { name: feature.name, enabled }));
      await this.featuresService.loadAsync(); // menu and routes react immediately
      this.snackBar.open(this.l10n.translate(enabled ? '{0}: enabled.' : '{0}: disabled.', feature.displayName), 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Something went wrong.', 'Dismiss', { duration: 4000 });
    }
    await this.load();
  }

  private async load(): Promise<void> {
    const response = await firstValueFrom(this.http.get<ResponseDto<FeatureDto[]>>(`${environment.apiBaseUrl}/features/list`));
    this.features.set(response.data ?? []);
  }
}
