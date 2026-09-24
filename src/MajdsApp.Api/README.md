# MajdsApp.Api

The API host. It contains almost no logic: it loads modules and plugins, configures authentication, and orders the middleware.

## Run

```bash
dotnet run --project src/MajdsApp.Api --launch-profile http     # http://localhost:5156
```

Swagger UI is at `/swagger` in the Development environment only. Health probes: `/health/live` and `/health/ready` (public, not wrapped in the response envelope).

## Startup, in order

1. Loads the compiled-in module assemblies (the list at the top of `Program.cs` — the one place a new module is wired in).
2. Scans the plugins folder and loads each plugin into an isolated `AssemblyLoadContext`; a plugin that fails is recorded with its error and skipped.
3. Registers the `DbContext`, Identity (bearer tokens), optional Google and Microsoft sign-in, deny-by-default authorization, rate limiting, CORS.
4. Registers the platform pipeline and every module (`AddPlatformCore`, `AddPlatformControllers`).
5. After build: seeds the `Admin` and `User` roles and reconciles the plugin registry with the database.

Middleware order: Swagger (Development only), correlation id, security headers, exception handling, HSTS (not Development), HTTPS redirection, CORS, authentication, session-timeout check, rate limiter, authorization, endpoints.

## Sign-in

`POST /api/identity/login` (ASP.NET Identity API endpoints) returns a bearer token. Everything under `/api/identity` except `/manage/*` is public and rate limited per IP. External login (Google, Microsoft) is in the `ExternalLogin` module and only appears when its credentials are configured.

## Configuration

See the table in the [root README](../../README.md#configuration). Keys read directly by this project: `ConnectionStrings:DefaultConnection`, `Plugins:Directory`, `Spa:AllowedOrigins`, `RateLimiting:*`, `Email:Smtp:Username`/`Password` (the SMTP sender is only registered when both exist), `Authentication:Google:*`, `Authentication:Microsoft:*`.

A user-secrets store (id in the `.csproj`) is shared with the Razor site so provider credentials are set once:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/MajdsApp.Api
```

## Deploying

- Point `Spa:AllowedOrigins` at the real frontend address and set `MediatR:LicenseKey`.
- Configure a **persistent Data Protection key ring**; encrypted settings are unreadable without it.
- Rate-limit counters and caches are per process; behind a load balancer they are per instance.
- Serve the built Angular app from a host that sends its own security headers; this API's headers only cover API responses.
