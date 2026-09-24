export interface LoginRequest {
  email: string;
  password: string;
  twoFactorCode?: string;
  twoFactorRecoveryCode?: string;
}

export interface LoginResponse {
  tokenType: string;
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
}

export interface CurrentUser {
  email: string;
}
