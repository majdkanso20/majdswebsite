# MajdsApp.Core

The data foundation shared by the API and every module: the single EF Core `DbContext`, the identity entities, and every migration.

## Contents

| Item | Purpose |
|---|---|
| `Data/ApplicationDbContext` | The one `DbContext`. It applies `IEntityTypeConfiguration<T>` classes found in every loaded `MajdsApp.Modules.*` and `MajdsApp.Plugins.*` assembly, so a module adds an entity without editing this class. |
| `Data/ApplicationUser`, `ApplicationRole` | Extend (never fork) ASP.NET Identity with full name, active flag, soft delete, audit fields, static/default role flags. |
| `Data/Migrations` | All migrations, including those for module and sample-plugin tables. |
| `Services/SmtpEmailSender` | Sends Identity emails and platform notification email over SMTP (MailKit). Reads Email settings from the Settings module first, then falls back to `Email:Smtp:*` configuration. |
| `Configuration` | `SmtpOptions`, `FeatureOptions`. |

## Migrations

```bash
dotnet ef migrations add <Name> --project src/MajdsApp.Core --startup-project src/MajdsApp.Api
dotnet ef database update      --project src/MajdsApp.Core --startup-project src/MajdsApp.Api
```

The API discovers module and plugin assemblies when it starts, so a plugin's entity is included in a new migration only if the plugin is already deployed in the `plugins/` folder when you run the command.

## Notes

- Text search must use `EF.Functions.Like` (see `LikePattern` in the shared kernel).
- The sample plugin's table lives in this project's migrations (a documented simplification of per-plugin schema isolation). The test harness suppresses EF's "pending model changes" warning for that reason.
- SQLite is the default provider. The data layer avoids provider-specific SQL so another relational provider is a configuration and migration change.
