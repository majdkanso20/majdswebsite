# MajdsApp (original Razor Pages site)

The original ASP.NET Core Identity site: cookie sign-in, email confirmation, two-factor authentication (authenticator app and recovery codes), and Google and Microsoft sign-in. It is kept as it was and **runs independently of the platform**: it references only `MajdsApp.Core` (the shared `DbContext` and entities) and `MajdsApp.SharedKernel`, never the API or the Angular app, so either side can be built and run while the other is running.

```bash
dotnet run --project src/MajdsApp --launch-profile http     # http://localhost:5132
```

It uses the same SQLite database and user-secrets store as the API, so an account works in both. The API's admin-initiated password-reset emails link to this site's reset page (`WebApp:BaseUrl`, default `http://localhost:5132`).

Provider credentials (`Authentication:Google:*`, `Authentication:Microsoft:*`) and SMTP (`Email:Smtp:*`) are read from configuration and user-secrets; a provider or the email sender is only enabled when its credentials exist. See the [root README](../../README.md#configuration).
