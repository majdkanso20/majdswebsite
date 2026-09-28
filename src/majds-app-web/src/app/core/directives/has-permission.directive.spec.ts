import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HasPermissionDirective } from './has-permission.directive';
import { PermissionService } from '../services/permission.service';

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `<button *hasPermission="'Users.Delete'">Delete</button>`
})
class HostComponent {}

/**
 * F-Data FR-GRID-006: this is how a list screen's row and toolbar actions stay hidden from a user
 * who lacks the permission — used directly in the data-grid's content-projected action templates
 * (Users, Roles, Jobs, Files, Plugins), rather than the grid itself knowing about permission strings.
 */
describe('HasPermissionDirective', () => {
  function create(allowed: boolean) {
    TestBed.configureTestingModule({
      providers: [{ provide: PermissionService, useValue: { has: () => allowed } }]
    });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the element when the user has the permission', () => {
    expect(create(true).querySelector('button')).not.toBeNull();
  });

  it('removes the element from the DOM (not just hides it) when the user lacks the permission', () => {
    expect(create(false).querySelector('button')).toBeNull();
  });
});
