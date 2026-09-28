# MajdsApp.Modules.Localization

**Localization (F-Localization)** — SRS FR-I18N-001..006

English is the source text and the key everywhere, so a missing translation shows the English (never a blank, AC-I18N-2) and an English request costs nothing. This module holds the **server's** translations and decides which language each request is answered in. The Angular app translates its own text from `public/i18n/<language>.json`.

## Which language a request is answered in

1. the languages the client sends in `Accept-Language`, by preference (`ar-JO`, `ar;q=0.8` and so on; the Angular app always sends the language on screen, so a switch shows in error messages on the very next request);
2. the signed-in user's saved language (`General.DefaultLanguage`, which each user may set for themselves);
3. the application default language, and finally English.

The result sets the request's culture, which also selects FluentValidation's own built-in translations for validation messages.

## What is translated on the server

- error messages and validation errors (`ExceptionHandlingMiddleware`, the model-state response, the plugin gate, the rate limiter);
- notification titles and messages: written **per recipient**, in that person's saved language, when the notification is published;
- the account emails (confirm your email, reset your password, reset code), in the recipient's language and right to left for Arabic, and the test email in the administrator's language.

Text nobody translated, and text a person wrote by hand (for example an announcement an administrator sends), is left exactly as written.

## Adding a translation or a language

Server texts live in `Resources/<code>.json` as `"English text": "translation"`. A text with a changing part is a template with `{0}`, `{1}`... and matches any value: `"Missing permission: {0}"` translates `Missing permission: Users.View` and puts `Users.View` in the result. Keep every placeholder in the translation; a test checks that. Always write a message with a value as one parameterized text, never by joining translated pieces, because word order differs between languages.

To add a language: add `Resources/<code>.json`, add it to `MessageCatalog.Supported`, add `public/i18n/<code>.json` in the web app, and add it to `LANGUAGES` in `localization.service.ts`.

A test (`MessageCatalogTests`) scans the source for every exception message, validator message, notification text and email text and **fails when one has no Arabic entry**, listing what to add; the web app has the same kind of test for templates.

## API

All responses use the `ResponseDto<T>` envelope.

- `GET /api/localization/languages` — signed in; the languages that can be chosen (`code`, `label`, `rtl`, `isDefault`), the default first.
- `GET /api/localization/resources?culture=ar` — anonymous, so it works before sign-in; the server's translations for that culture keyed by English text. English and unknown cultures return an empty set.

## Settings

Defined in the Settings module and used here and by the web app: `General.DefaultLanguage`, `Appearance.Timezone` and `Appearance.Currency` (an ISO 4217 code). Each can be set for the whole application and overridden by a user. Values are validated: a language must be one the platform speaks, a time zone must exist, a currency must be three letters.

## Formatting and right-to-left (web app)

`FormattingService` and the `localDate`, `localNumber` and `localCurrency` pipes show dates in the user's time zone (the server's UTC timestamps carry no zone marker and are read as UTC), and numbers and money by the active language. Setting `<html dir>` flips the layout for a right-to-left language, and Stylelint rejects physical `left`/`right` properties so new CSS stays right-to-left safe.

## Notes

- ASP.NET Identity's own texts (password-policy errors, taken names) and model-binding errors (a bad query value, a missing body) go through the catalog too, field by field, the same as everything else the server says. Not covered: text inside a plugin's frontend screens, and Swagger.
- The catalog is embedded in the assembly, so a change needs a rebuild; there is no editing screen.

## Tests

`MessageCatalogTests` (matching, placeholders, and the source scan) and `LocalizationTests` (language selection, notifications per recipient, the endpoints, setting validation) in `src/MajdsApp.Tests`; `localization.service.spec.ts`, `formatting.service.spec.ts`, `language.interceptor.spec.ts` and `i18n-coverage.spec.ts` in the web app.

## Plugin translations

The catalog also loads each plugin's `localization/<language>.json` and adds entries the platform does not already have (see the Plugins module README).
