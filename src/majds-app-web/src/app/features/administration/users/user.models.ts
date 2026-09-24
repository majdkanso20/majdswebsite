export interface UserDto {
  id: string;
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
  lockedOut: boolean;
  twoFactorEnabled: boolean;
  roles: string[];
  createdAt: string;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  fullName: string | null;
  roles: string[];
}

export interface UpdateUserRequest {
  userId: string;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
}
