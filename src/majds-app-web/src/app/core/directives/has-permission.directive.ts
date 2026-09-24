import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';
import { PermissionService } from '../services/permission.service';

/**
 * Structural directive hiding its host element when the user lacks the given permission
 * (F-Authorization US-3, AC-AUTHZ-3). Usage: <button *hasPermission="'Users.Create'">...</button>
 */
@Directive({
  selector: '[hasPermission]'
})
export class HasPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly permissionService = inject(PermissionService);

  readonly hasPermission = input.required<string>();

  private hasView = false;

  constructor() {
    effect(() => {
      const allowed = this.permissionService.has(this.hasPermission());
      if (allowed && !this.hasView) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.hasView = true;
      } else if (!allowed && this.hasView) {
        this.viewContainer.clear();
        this.hasView = false;
      }
    });
  }
}
