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

## Export

`Export/` renders one dataset to CSV, Excel (ClosedXML) or PDF (QuestPDF) from a single description of rows and columns (`TabularExport.Render`), so the formats can never disagree. Text is written literally in Excel and guarded in CSV, so user-supplied values cannot run as formulas. PDFs are capped at 2,000 rows and say so on the page. QuestPDF is used under its Community license (free below 1M USD annual revenue; larger organizations need a paid license). The bundled font covers Latin text; Arabic content in a PDF needs a font with Arabic glyphs.

## Import

`Import/` reads an uploaded CSV or Excel file (`TabularReader`: quoted fields, BOM, `,` or `;`, header matching that ignores case and order, at most 5,000 rows), runs a per-row action and collects what failed (`ImportRunner`, result `{ total, succeeded, failed, errors: [{ row, reason }] }`), and builds blank templates (`ImportTemplate`). The per-row action should send the same command as the normal create endpoint, so an import obeys exactly the same validation and audit rules; a validation or conflict error fails only that row, while a permission refusal fails the whole import.

`Export/IExportSource.cs` lets a module register a dataset (key, title, view permission, a build method that takes the list's filters). The same source serves the immediate download and background exports (`MajdsApp.Modules.Exports`), so a new resource gets both by registering one class.

## Plugin installer

`Plugins/PluginInstaller.cs` verifies an uploaded package and stages it, applies staged installs, upgrades, rollbacks and uninstalls at startup (before any plugin loads), and keeps the previous version for rollback. See `MajdsApp.Modules.Plugins/README.md` for the rules and the trust policy (`PluginHostOptions`).

## Jobs

`Jobs/IRecurringJob.cs` (interval or cron) and `Jobs/IBackgroundJob.cs` (a one-off job plus `IBackgroundJobQueue` to queue it). The scheduler, the persisted queue and the retry rules live in `MajdsApp.Modules.Jobs`.

## Localization

`Localization/IMessageCatalog.cs` translates the server's own text (English is the key; templates use `{0}`). The catalog and the request-language selection are in `MajdsApp.Modules.Localization`. `HttpContext.Localize(text)` translates into the current request's language; the exception middleware, the model-state response, the plugin gate and the rate limiter use it.

## Caching

`Caching/ICacheService.cs` is the platform's cache: get, set with a time to live, remove, and get-or-add. `Cache:Provider` chooses the backend by configuration: `Memory` (one server), `Redis` (`Cache:Redis:ConnectionString`; shared by every server) or `Distributed` (a distributed cache held in this process). Values must be serializable to JSON, since a distributed backend stores them outside the process. A cache that cannot be reached is a miss and is logged, never an error. Permissions, settings, feature flags and cacheable MediatR queries use it, so with Redis a write on one server invalidates the cache on all of them.

## Module options (P2 FR-MOD-004)

`Configuration/ModuleOptions.cs`: `services.AddModuleOptions<MyOptions>(configuration, "MySection")` binds the section through the options pattern and validates it (data annotations, or `IValidatableObject` for rules across several values) when the application starts. A bad value stops the start with a message naming the setting. Used for `Email:Smtp`, `RateLimiting`, `Docs` and `Resilience`.

## Resilience (P4 FR-XC-006)

`Resilience/OutboundResilience.cs` defines, once, how an outbound call retries (exponential backoff with jitter, only for the failures the caller says are transient), times out per attempt and trips a circuit breaker. Apply it by decorating the service that makes the call (Scrutor `Decorate`, see `MajdsApp.Core/Services/ResilientEmailMessageSender.cs`) instead of writing try/catch in callers. The `Resilience` section tunes it: `MaxRetries`, `RetryDelayMilliseconds`, `TimeoutSeconds`, `BreakMinimumCalls`, `BreakSamplingSeconds`, `BreakSeconds`. The service being called must pass the cancellation token on to its network calls, or the timeout cannot stop it.

## Mapping (P4 FR-XC-007)

`Mapping/ObjectMapper.cs`: handlers ask for `IObjectMapper` to turn an entity into a DTO (`Map<TDto>(entity)`) or to project a query (`Select(mapper.Projection<TEntity, TDto>())`). Members and constructor parameters are matched by name; a mapping that needs more is written once in a class implementing Mapster's `IRegister` inside the module, and is found by the scan of the module assemblies.

## Repositories, soft delete and domain events (P3)

Any entity that implements `IEntity<TKey>` can be reached through `IRepository<TEntity, TKey>` (tracked; add, update, remove) or `IReadRepository<TEntity, TKey>` (never tracked), both injectable and scoped, sharing the request's `DbContext` with `IUnitOfWork`. A list endpoint calls `repository.PagedAsync(request, sortableColumns, selector, spec)`: an unknown sort column is a 400. `Paging:MaxPageSize` (default 100, 1 to 1000) caps a page.

`SaveChanges` stamps audit fields and turns the removal of any `ISoftDelete` entity into a soft delete; a query filter for such entities is added automatically. An entity that implements `IHasDomainEvents` (or derives from `DomainEventSource`) collects `IDomainEvent`s; they are published through MediatR after the save that stored the change succeeds, once each, and not at all if it fails. Handle one with an ordinary `INotificationHandler<T>`.
