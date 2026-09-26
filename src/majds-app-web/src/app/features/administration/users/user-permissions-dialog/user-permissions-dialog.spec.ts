import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { RolesApiService } from '../../roles/roles-api.service';
import { UserDto } from '../user.models';
import { UsersApiService } from '../users-api.service';
import { UserPermissionsDialog } from './user-permissions-dialog';

const user: UserDto = {
  id: 'u1', email: 'a@example.com', fullName: 'Ada', phoneNumber: null, isActive: true, emailConfirmed: true, lockedOut: false,
  twoFactorEnabled: false, roles: ['User'], createdAt: '2026-01-01T00:00:00'
};

describe('UserPermissionsDialog', () => {
  const usersApi = { getPermissions: vi.fn(), updatePermissions: vi.fn() };
  const rolesApi = { getPermissionTree: vi.fn() };
  const dialogRef = { close: vi.fn() };

  async function create(explained = { userId: 'u1', isSuperAdmin: false, fromRoles: ['Users.View'], granted: ['Roles.View'], denied: [], effective: [] }) {
    usersApi.getPermissions.mockReturnValue(of(explained));
    usersApi.updatePermissions.mockReturnValue(of(undefined));
    rolesApi.getPermissionTree.mockReturnValue(of([{ name: 'Users', permissions: ['Users.View', 'Users.Edit'] }, { name: 'Roles', permissions: ['Roles.View'] }]));
    TestBed.configureTestingModule({
      providers: [
        { provide: UsersApiService, useValue: usersApi },
        { provide: RolesApiService, useValue: rolesApi },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { user } }
      ]
    });
    const fixture = TestBed.createComponent(UserPermissionsDialog);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, dialog: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    usersApi.getPermissions.mockReset();
    usersApi.updatePermissions.mockReset();
    dialogRef.close.mockReset();
  });

  it('shows each permission with what the roles give and the direct choice already made', async () => {
    const { dialog, element } = await create();

    expect(element.textContent).toContain('Users.View');
    expect(dialog.givenByRoles('Users.View')).toBe(true);
    expect(dialog.givenByRoles('Users.Edit')).toBe(false);
    expect(dialog.choiceOf('Roles.View')).toBe('grant');
    expect(dialog.choiceOf('Users.Edit')).toBe('inherit');
  });

  it('saves the direct grants and denies, leaving "by role" permissions out', async () => {
    const { dialog } = await create();

    dialog.choose('Users.View', 'deny');
    dialog.choose('Users.Edit', 'grant');
    dialog.choose('Roles.View', 'inherit');
    dialog.save();

    expect(usersApi.updatePermissions).toHaveBeenCalledWith('u1', ['Users.Edit'], ['Users.View']);
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('tells the administrator when the user holds everything anyway', async () => {
    const { element } = await create({ userId: 'u1', isSuperAdmin: true, fromRoles: [], granted: [], denied: [], effective: [] });

    expect(element.textContent).toContain('holds every permission');
  });
});
