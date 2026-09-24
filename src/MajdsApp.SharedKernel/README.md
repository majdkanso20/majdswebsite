# MajdsApp.SharedKernel

The platform backbone (SRS P1–P5). Every module references this project and nothing else in the platform; modules never reference each other's internals.

## What is in it

| Folder | Purpose |
|---|---|
| `Api` | `ApiControllerBase`, the `ResponseDto<T>` envelope and `ResponseStatusCode`, and the filter that maps the envelope code to the HTTP status. |
| `Behaviors` | The MediatR pipeline: `LoggingBehavior`, `PerformanceBehavior`, `FailureAuditBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `FeatureBehavior`, `CachingBehavior`, `TransactionBehavior`, `AuditBehavior`, plus the marker attributes and interfaces. |
| `Middleware` | Correlation id, exception-to-envelope mapping, security headers. |
| `Modules` | `IFeatureModule` and the discovery/registration helpers (`ModuleRegistrar`). |
| `Plugins` | Runtime plugin loading: manifest, isolated `PluginLoadContext`, `PluginManager`, the enabled-state gate. |
| `Data` | `IEntity`, `AuditableEntity`, `ISoftDelete`, `IUnitOfWork`, the generic repository and specifications, and the save interceptor. |
| `Audit` | `IAuditLogWriter`, the change buffer, redaction, the audit record factory. |
| `Security` | `ICurrentUser`, `IPermissionChecker`, rate-limit policies. |
| `Settings`, `Features`, `Notifications`, `Jobs`, `Search`, `Export`, `Paging` | Contracts and registries that feature modules implement or contribute to. |

## The request pipeline

MediatR runs behaviors in registration order (outermost first). A request opts in by carrying a marker:

| Marker | Effect |
|---|---|
| `[RequiresPermission("X.Y")]` | `AuthorizationBehavior` throws `Forbidden` (403) or `Unauthorized` (401). |
| `[RequiresFeature("Name")]` | `FeatureBehavior` refuses the request when the feature flag is off. |
| `IAuditableCommand` | The action is written to the audit log with its metadata, redacted parameters and entity changes; failures are recorded too. |
| `ITransactionalCommand` | The handler and its audit row commit or roll back together. |
| a validator (`AbstractValidator<T>`) | Runs before the handler; failures become a 400 with the messages in `errors`. |

Refusals (403) and failures of auditable commands are recorded by `FailureAuditBehavior`, on a separate context so a rolled-back transaction cannot erase them.

## "Default, overridden by a module" contracts

`ISettingsProvider`, `IPermissionChecker`, `IFeatureChecker`, `IUserNotificationPublisher`, `IAuditLogWriter` and `IPluginStateCache` each ship a harmless default here (`TryAdd*` in `AddPlatformCore`) so other projects can depend on them. The owning module registers the real implementation afterwards, and the last registration wins. To swap one in your own app, register yours after `AddPlatformCore`.

## Adding a module

1. Create `MajdsApp.Modules.<Name>` referencing this project (and `MajdsApp.Core` if it needs the `DbContext`).
2. Add a class implementing `IFeatureModule` (usually with an empty `ConfigureServices`).
3. Put commands/queries, validators and a controller deriving from `ApiControllerBase` in the project. Handlers, validators and controllers are found automatically.
4. Declare permissions as nested static classes of `const string` (`Permissions.Group.Action`) and settings as `SettingDefinition` fields; both are discovered by assembly scan.
5. Add an `IEntityTypeConfiguration<T>` for any entity, then a migration in `MajdsApp.Core`.
6. Reference the project from `MajdsApp.Api.csproj` and add its module assembly to the list in `Program.cs`.

To ship the feature as a runtime plugin instead, see `MajdsApp.Plugins.Tasks`.

## Conventions worth knowing

- Routes are `/api/{controller}/{action}`; reads are `GET`, everything else `POST`.
- Handlers throw `NotFoundException`, `ConflictException`, `ForbiddenException`, `UnauthorizedAppException` or FluentValidation's `ValidationException`; the middleware maps each to the right envelope code. `ValidationException(string)` messages reach the client in `errors`.
- Name assemblies `MajdsApp.Modules.*` (compiled in) or `MajdsApp.Plugins.*` (runtime): the permission, setting and entity-configuration scans key off those prefixes.
- Use `LikePattern.Contains` with `EF.Functions.Like` for text search, not `string.Contains` (case-sensitive on SQLite).

## Configuration keys

| Key | Purpose |
|---|---|
| `RateLimiting:*` | See the root README. Policies are `auth`, `expensive` and the global limit. |
| `MediatR:LicenseKey` | MediatR license key. |
