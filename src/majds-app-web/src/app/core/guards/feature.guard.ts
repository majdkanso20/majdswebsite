import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { FeaturesService } from '../services/features.service';

/** Sends the user to the dashboard when a route's feature is switched off (F-Features). */
export const featureGuard =
  (feature: string): CanActivateFn =>
  () =>
    inject(FeaturesService).isEnabled(feature) ? true : inject(Router).createUrlTree(['/dashboard']);
