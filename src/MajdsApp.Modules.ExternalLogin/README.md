# MajdsApp.Modules.ExternalLogin

**External login (F-Account)** — SRS FR-ACCT-004 (sign-in with an identity provider)

'Continue with Google / Microsoft' for the Angular app. It bridges the provider's cookie-based OAuth handshake to the SPA's bearer tokens without re-implementing sign-in.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/external/providers` (anonymous)
- `GET /api/external/{provider}/start?returnUrl=` (anonymous)
- `GET /api/external/callback` (anonymous)

## Permissions

None; all endpoints are anonymous (they are the sign-in).

## Notes

- A provider is offered only when its client id and secret are configured. Register `https://<api>/signin-google` (or `-microsoft`) as an authorized redirect URI with the provider.
- The callback validates `returnUrl` against `Spa:AllowedOrigins` (prevents token theft through an open redirect) and returns the token in the URL *fragment* so it never reaches server logs.
- By design there is no extra two-factor prompt after a provider sign-in: the provider has already authenticated the person, including any 2-step verification on that account. An existing account signs in; an unknown email creates a profile with the default role and notifies administrators.

## Configuration keys

- `Authentication:Google:ClientId` / `ClientSecret`
- `Authentication:Microsoft:ClientId` / `ClientSecret`
- `Spa:AllowedOrigins`

## Tests

Not yet covered by automated tests (see the traceability document).
