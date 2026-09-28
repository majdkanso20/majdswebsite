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
  /** Omitted (or null) when sendSetPasswordEmail is true: the account gets a password nobody knows instead. */
  password: string | null;
  /** The new user gets an emailed set-password link (the same mechanism as Forgot password) instead of a password set here now. */
  sendSetPasswordEmail: boolean;
  fullName: string | null;
  roles: string[];
}

export interface UpdateUserRequest {
  userId: string;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
}
