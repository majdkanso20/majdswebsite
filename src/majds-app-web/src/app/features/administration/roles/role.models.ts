export interface RoleDto {
  id: string;
  name: string;
  displayName: string | null;
  isStatic: boolean;
  isDefault: boolean;
  userCount: number;
}

export interface PermissionGroup {
  name: string;
  permissions: string[];
}
