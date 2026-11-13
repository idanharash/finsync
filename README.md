# FinSync – Financial Transaction Collector

## Overview
A .NET 10 console application. It collects the previous calendar month's current-account transactions from Mizrahi-Tefahot online banking, masks the descriptions and upserts the results into JSON.
Technologies: C# / .NET 10, PuppeteerSharp 20.2.4, Microsoft.Extensions DI / Options / Configuration 10.0, System.Text.Json and xUnit.
Implemented today: one provider (Mizrahi-Tefahot, current account) and one writer (JSON).

## Architecture
| Project | Responsibility | References |
|---|---|---|
| `FinSync.Domain` | `Transaction`, `TransactionId`, `TransactionResource`, `MaskedDescription` and their invariants | – |
| `FinSync.Abstractions` | Interfaces only: `ICollector<TRaw>`, `IAdapter<TRaw>`, `ITransactionMasker<TRaw>`, `ISensitiveDataMasker`, `ITransactionWriter`, `IProviderRegistration` | Domain |
| `FinSync.Registration` | `AddProvider<TRaw, TCollector, TAdapter, TMasker>()` | Abstractions |
| `FinSync.Application` | Generic `TransactionJob<TRaw>`: Collect → Adapt → Save | Abstractions |
| `FinSync.Shared` | Reusable, provider-independent implementations; today the shared `SensitiveDataMasker`, the only production code that can create a `MaskedDescription` | Abstractions |
| `FinSync.Mizrahi` | Mizrahi collector, adapter, masker, options and `AddMizrahiBank()` | Abstractions, Registration |
| `FinSync.IO.Json` | `JsonTransactionWriter` and `AddJsonPersistence()` | Abstractions |
| `FinSync.Console` | Composition root: configuration, registration, `AddApplication()`, job execution | all modules |

**Dependency rule:** `Application`, `Mizrahi`, `IO.Json` and `Shared` never reference one another, directly or transitively.
Only `FinSync.Console` knows every module. The rule is enforced by `tests/FinSync.Tests/Architecture/DependencyRuleTests.cs`, which checks the project and assembly references.

## Processing Flow
1. **Collector** (`MizrahiCollector`) launches Chromium, types in the configured credentials and waits for a manual MFA code if the bank asks for one. It then applies the configured `CollectionPeriod` (by default the previous calendar month) and reads the raw rows, including their expanded details. The browser is disposed afterwards.
2. **Adapter** (`MizrahiAdapter`) parses and validates the date, amount and reference fields, builds the `TransactionId` and calls the provider masker.
3. **Masker** – each provider owns one. `MizrahiTransactionMasker` composes the description and classifies detail fields by label; unknown labels are sensitive. It then delegates to the shared `SensitiveDataMasker`, which redacts the flagged values, e-mail addresses and identifier-like numbers (by default keeping the last 2 digits).
4. **Job** (`TransactionJob<TRaw>`) checks that no transaction was lost and that ids are unique. It wraps each failure with the provider and stage, and honours cancellation.
5. **Writer** (`JsonTransactionWriter`) upserts by id into `output/<Resource>.json` and replaces the file atomically.

Every stage fails fast. Error messages name row positions and fields, never values. A failing provider is reported, and the remaining providers still run.

## Key Design Decisions
- **Generic providers:** `ICollector<TRaw>` and `IAdapter<TRaw>` share the provider's raw type, so a mismatched pair does not compile. Each provider owns its collector, adapter and masker.
- **One DI extension method per provider:** for example `AddMizrahiBank(configuration)` binds and validates the provider's options, then calls `AddProvider<MizrahiTransaction, MizrahiCollector, MizrahiAdapter, MizrahiTransactionMasker>("MizrahiBank")`.
- **Registration validation:** `AddApplication()` creates one `TransactionJob<TRaw>` per registered provider. It fails at startup when there is no provider, when a component is missing or conflicting, or when registration happens out of order. The container is built with `ValidateOnBuild`/`ValidateScopes`, and options use `ValidateOnStart`.
- **`MaskedDescription`:** has an `internal` constructor, visible only to `FinSync.Shared` and the tests. `Transaction` accepts only a `MaskedDescription`, so no code path can persist an unmasked string.
- **Transaction identity:** SHA-256 of the resource plus length-prefixed parts. For Mizrahi the parts are date, normalized amount, original reference and the bank's `TransactionNumber`. The number tells identical same-day transactions apart, and the running balance was rejected because it is unstable. The id is deterministic and contains no readable reference.
- **Central configuration with fail-fast validation:** `CollectionPeriod:Month` (`yyyy-MM`, empty = previous month) is resolved once in the composition root, and the provider only types the dates into its website. `Masking` (in `appsettings.json`) holds the shared numeric parameters, range-checked so they can never turn masking off, and the `SensitiveLabels` common to every provider. `MizrahiBank:Masking` (in `bankMizrahisettings.json`) adds Mizrahi's own lists (`SensitiveLabels`, `NonSensitiveLabels`, `TrustedNumericLabels`), all defined in JSON with no hidden code defaults; a missing or empty rule set fails at startup. Unknown labels stay sensitive, a sensitive label always wins, and conflicting lists fail at startup.
- **JSON upsert:** existing records are replaced by id or kept, and new ones are inserted. A corrupt or invalid existing file aborts the save before anything is written. Each file is written to a temporary file and then swapped in with `File.Replace`.

## Setup & Execution
Requires the .NET 10 SDK. Fill in `MizrahiBank:Username` and `MizrahiBank:Password` in `src/FinSync.Console/bankMizrahisettings.json`, or, to keep them out of git, put only those two values in an optional `bankMizrahisettings.local.json` next to it (`*.local.json` is in `.gitignore`).
All settings live in JSON under `src/FinSync.Console`: `appsettings.json` (shared: `CollectionPeriod`, `Masking`, `JsonPersistence`), `bankMizrahisettings.json` (Mizrahi settings: login URL, credentials, timeouts, `Headless`, masking labels) and an optional `bankMizrahisettings.local.json` that overrides it. They load in that order.
```powershell
dotnet restore FinSync.sln
dotnet build FinSync.sln
dotnet run --project src\FinSync.Console
```
The first run downloads Chromium. Enter any one-time code the bank asks for in the opened browser window. The console exits with 0 on success, 1 if any provider fails, 2 for a configuration error and 130 when cancelled.

## Tests
xUnit, with hand-written fakes and no mocking library. No test touches the real bank.
```powershell
dotnet test tests\FinSync.Tests\FinSync.Tests.csproj --filter "Category!=Browser"
dotnet test tests\FinSync.Tests\FinSync.Tests.csproj --filter "Category=Browser"
```
- **Unit tests:** domain invariants, masking, adapter, job, DI validation, configuration, JSON upsert and architecture rules.
- **Browser tests:** run the transaction reader and the rejected-login check in headless Chromium against synthetic HTML, with no network.

## Adding a New Provider (example: Discount)
1. **Resource:** `TransactionResource` already defines `DiscountBank` and `Cal`. Other providers, such as Isracard, need a new value.
2. **Project:** create `FinSync.Discount`, referencing only `FinSync.Abstractions` and `FinSync.Registration`. It contains `DiscountTransaction` (raw model), `DiscountCollector`, `DiscountAdapter`, `DiscountTransactionMasker` (built on `ISensitiveDataMasker`) and `DiscountOptions` with a validator.
3. **Registration:** one extension method, following `AddMizrahiBank`:
   ```csharp
   public static IServiceCollection AddDiscountBank(this IServiceCollection services, IConfiguration configuration)
   {
       services.AddOptions<DiscountOptions>()
           .Bind(configuration.GetSection(DiscountOptions.SectionName))
           .ValidateOnStart();
       return services.AddProvider<DiscountTransaction, DiscountCollector, DiscountAdapter, DiscountTransactionMasker>(
           nameof(TransactionResource.DiscountBank));
   }
   ```
4. **Configuration:** add `bankDiscountsettings.json` (settings, empty credentials and masking labels) to `src/FinSync.Console`; the console copies and loads it automatically.
5. **Console:** reference the project and call `services.AddDiscountBank(configuration);` before `AddApplication()`.
6. **Tests:** add adapter, masker and registration tests, and add the project to `DependencyRuleTests`.

The job, the writer, the shared masker and the Mizrahi provider stay unchanged. The writer creates `output/DiscountBank.json` on its own.

## Future Extensibility (not implemented)
- **New providers (Discount, Cal, Isracard):** follow the steps above. No such provider exists yet.
- **PostgreSQL or another database:** implement `ITransactionWriter` with its own `AddPostgresPersistence()`. `TransactionId` maps to a primary key. `AddJsonPersistence()` refuses to register when another writer is already present.
- **More account types or multiple accounts:** another account type is another collector/adapter pair with its own raw type and registration. Multiple accounts per provider are not modelled yet: this needs an account identifier on `Transaction` and in the identity parts, which is a domain change.
- **Observability (Elasticsearch, Kibana, OpenTelemetry):** today the only output is the console. Modules would log through `ILogger<T>` from `Microsoft.Extensions.Logging.Abstractions`. The composition root would add an `AddObservability()` with an OpenTelemetry exporter to Elasticsearch, giving Kibana dashboards, a trace span per job stage and metrics for collected and saved counts. Logs would carry only provider, stage, counts and hashed ids, never descriptions or credentials.
- **Messaging (RabbitMQ):** an `ITransactionWriter` implementation could publish each saved batch for other services to consume, with no change to the job or providers.
- **Parallel providers:** `TransactionJobRunner` could use `Parallel.ForEachAsync` with a configurable degree of parallelism and collect failures as it does now. Each collector already owns its browser, and each resource has its own JSON file. This needs non-interactive authentication first, since manual MFA prompts cannot run at the same time.
- **No changes to existing code:** a new provider or writer leaves `TransactionJob<TRaw>`, the shared masker and the existing providers untouched. Only the composition root changes.

## Known Limitations
- **Pagination:** a result spanning more than one grid page fails fast instead of being paged.
- **Identity stability:** what `TransactionNumber` means was inferred from observation. If the bank later added an identical transaction to an already-closed day, ids could shift.
- **Masking:** pattern- and label-based, so personal names inside unlabelled free text are not detected.
- **Upsert:** never deletes records, even when the bank later removes them.
- **Live site:** login, navigation and the date filter are verified only against the live site, and selectors break if the site changes.
- **Execution:** providers run one after another (each login may need manual MFA), not in parallel.
