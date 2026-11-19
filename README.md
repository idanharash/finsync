# FinSync – Financial Transaction Collector

[![CI](https://github.com/idanharash/finsync/actions/workflows/ci.yml/badge.svg)](https://github.com/idanharash/finsync/actions/workflows/ci.yml)

## Overview
A .NET 10 console application. It collects the previous calendar month's current-account transactions from Mizrahi-Tefahot online banking, masks the descriptions and upserts the results into JSON.
Technologies: C# / .NET 10, PuppeteerSharp 20.2.4, Microsoft.Extensions DI / Options / Configuration 10.0, System.Text.Json and xUnit.

## Architecture
| Project | Responsibility | References |
|---|---|---|
| `FinSync.Core` | Domain (`Transaction`, `TransactionId`, `TransactionResource`, `CollectionPeriod`, `MaskedDescription`) and masking (`SensitiveDataMasker`). No I/O. | – |
| `FinSync.Mizrahi` | Bank-specific collection and adaptation: `IMizrahiCollector` and its browser implementation, `MizrahiAdapter`, `MizrahiTransactionMasker`, options and `AddMizrahiBank()` | Core |
| `FinSync.IO.Json` | Persistence: `ITransactionWriter`, `JsonTransactionWriter` and `AddJsonPersistence()` | Core |
| `FinSync.Console` | Composition root (standard DI), configuration, `TransactionJob` orchestration, exit codes | Core, Mizrahi, IO.Json |

Each project is one existing responsibility with its own dependency: Mizrahi isolates PuppeteerSharp and the bank's website, IO.Json isolates the file system and the JSON format, and Core has no I/O at all.

**Dependency rules**, enforced by `tests/FinSync.Tests/Architecture/DependencyRuleTests.cs`: every project references exactly the projects listed above; Mizrahi and IO.Json never depend on each other; Core does not reference PuppeteerSharp.

## Processing Flow
1. **Collect** (`MizrahiCollector`) launches Chromium, types in the configured credentials and waits for a manual MFA code if the bank asks for one. It then applies the configured `CollectionPeriod` (by default the previous calendar month) and reads the raw rows, including their expanded details. The browser is disposed afterwards.
2. **Adapt** (`MizrahiAdapter`) parses and validates the date, amount and reference fields, builds the `TransactionId` and calls `MizrahiTransactionMasker`.
3. **Mask** (`MizrahiTransactionMasker`) composes the description and classifies detail fields by label; unknown labels are sensitive. It then delegates to `SensitiveDataMasker`, which redacts the flagged values, e-mail addresses and identifier-like numbers (by default keeping the last 2 digits).
4. **Job** (`TransactionJob`) runs collect → adapt → save, checks that transaction ids are unique before saving, wraps each failure with the stage that failed, and honours cancellation.
5. **Save** (`JsonTransactionWriter`) upserts by id into `output/<Resource>.json` and replaces the file atomically.

Every stage fails fast. Error messages name row positions and fields, never values, and the console prints the message of an inner exception only when it comes from FinSync's own code.

## Key Design Decisions
- **`MaskedDescription` can only be created inside Core:** its constructor is `internal`, and `InternalsVisibleTo` names only the test project. `Transaction` accepts only a `MaskedDescription`, so neither the bank code nor the persistence code can produce an unmasked description that gets saved.
- **Interfaces only at I/O boundaries:** `IMizrahiCollector` (the browser and the bank) and `ITransactionWriter` (the file system). They let the job be tested without a browser or files. The adapter and maskers are pure and deterministic, so they are used directly and the job's tests run them for real.
- **Standard DI with plain registrations:** each project exposes one `Add…()` method that binds and validates its options and registers concrete types. The container is built with `ValidateOnBuild`/`ValidateScopes`, and options use `ValidateOnStart`.
- **Transaction identity:** SHA-256 of the resource plus length-prefixed parts. For Mizrahi the parts are date, normalized amount, original reference and the bank's `TransactionNumber`. The number tells identical same-day transactions apart, and the running balance was rejected because it is unstable. The id is deterministic and contains no readable reference.
- **Central configuration with fail-fast validation:** `CollectionPeriod:Month` (`yyyy-MM`, empty = previous month) is resolved once in the composition root. `Masking` (in `appsettings.json`) holds the numeric masking parameters, range-checked so they can never turn masking off, and the common `SensitiveLabels`. `MizrahiBank:Masking` (in `bankMizrahisettings.json`) adds Mizrahi's own lists (`SensitiveLabels`, `NonSensitiveLabels`, `TrustedNumericLabels`), all defined in JSON with no hidden code defaults; a missing or empty rule set fails at startup. Unknown labels stay sensitive, a sensitive label always wins, and conflicting lists fail at startup.
- **JSON upsert:** existing records are replaced by id or kept, and new ones are inserted. A corrupt or invalid existing file aborts the save before anything is written. Each file is written to a temporary file and then swapped in with `File.Replace`.

## Setup & Execution
Requires the .NET 10 SDK. Put `MizrahiBank:Username` and `MizrahiBank:Password` in an optional `bankMizrahisettings.local.json` next to `bankMizrahisettings.json` in `src/FinSync.Console` (`*.local.json` is in `.gitignore`), so credentials never land in git.
Settings load from `src/FinSync.Console` in this order, later files overriding earlier ones: `appsettings.json` (`CollectionPeriod`, `Masking`, `JsonPersistence`), `bankMizrahisettings.json` (login URL, timeouts, `Headless`, masking labels) and `bankMizrahisettings.local.json`.
```powershell
dotnet restore FinSync.sln
dotnet build FinSync.sln
dotnet run --project src\FinSync.Console
```
The first run downloads Chromium. Enter any one-time code the bank asks for in the opened browser window. The console exits with 0 on success, 1 if the job fails, 2 for a configuration error and 130 when cancelled.

## Tests
xUnit, with hand-written test doubles and no mocking library. No test touches the real bank.
```powershell
dotnet test tests\FinSync.Tests\FinSync.Tests.csproj --filter "Category!=Browser"
dotnet test tests\FinSync.Tests\FinSync.Tests.csproj --filter "Category=Browser"
```
- **Unit tests (85):** domain invariants, masking and its privacy boundary, the adapter, the job (with the real adapter and masker; only the collector and the writer are stubbed), startup validation of every settings section, configuration loading, JSON upsert and atomic replace, and the dependency rules.
- **Browser tests (11):** run the transaction reader and the rejected-login check in headless Chromium against synthetic HTML, with no network.

## Adding Another Provider
Not built in advance on purpose. A second bank or card company would get its own project next to `FinSync.Mizrahi` (collector, adapter, masker built on `SensitiveDataMasker`, options), a value in `TransactionResource`, and its own job in the console. Whether the providers then share an abstraction is decided from what they actually have in common, not guessed ahead of time.

## Known Limitations
- **Pagination:** a result spanning more than one grid page fails fast instead of being paged.
- **Identity stability:** what `TransactionNumber` means was inferred from observation. If the bank later added an identical transaction to an already-closed day, ids could shift.
- **Masking:** pattern- and label-based, so personal names inside unlabelled free text are not detected.
- **Upsert:** never deletes records, even when the bank later removes them.
- **Live site:** login, navigation and the date filter are verified only against the live site, and selectors break if the site changes.
- **Credentials:** read from a plain-text JSON file (git-ignored), not from an OS credential store.
