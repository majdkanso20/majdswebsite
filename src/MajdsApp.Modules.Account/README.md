# MajdsApp.Modules.Account

**Account (F-Account)** — SRS FR-ACCT-001..006

Self-service for the signed-in user (profile, password, picture) and the public entry points for a person who is not signed in yet: registration, forgot password and reset password.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/account/me`
- `POST /api/account/update-profile`
- `POST /api/account/change-password`
- `GET /api/account/picture`
- `POST /api/account/picture` (multipart; PNG, JPEG or WebP, up to 2 MB)
- `GET /api/account/registration-open` (anonymous)
- `POST /api/account/register` (anonymous, rate limited)
- `POST /api/account/forgot-password` (anonymous, rate limited)
- `POST /api/account/reset-password` (anonymous, rate limited)

## Permissions

None. Every signed-in endpoint derives the user from the token, never from the request, so a caller can only ever touch their own record.

## Notes

- Forgot password answers identically whether or not the email has an account, and reset failures give one generic message (no account enumeration). The emailed link points at the Angular `/reset-password` page (first entry of `Spa:AllowedOrigins`).
- Registration is refused when the *Allow users to self-register* setting is off; new accounts must confirm their email before signing in, and administrators are notified.
- Language, time zone and notification preferences are stored through the Settings and Notifications modules (see their READMEs); two-factor setup uses Identity's own `/api/identity/manage/2fa` endpoint.
- Email delivery needs working SMTP credentials (see the root README).

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
