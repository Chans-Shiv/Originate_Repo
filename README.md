# OrigenateFunction

Azure Function (C# .NET 8 isolated worker) that processes Origenate Excel uploads from `originate-landing` in storage account `sadveaddoc0001`, syncs Microsoft Dataverse staging tables, archives the file to `originate-processed`, and routes row-level failures to `originate-failed`.

See `/Users/shivamchandra/.claude/plans/rustling-kindling-penguin.md` for the design.

## Prerequisites

- **.NET 8 SDK** (project targets `net8.0`). You have .NET 10; if .NET 8 isn't installed, run `brew install --cask dotnet-sdk` or download from microsoft.com/dotnet/8.0.
- **Azure Functions Core Tools v4** — not installed. Install with `npm i -g azure-functions-core-tools@4 --unsafe-perm true` or `brew tap azure/functions && brew install azure-functions-core-tools@4`.
- **RBAC on `sadveaddoc0001`** for the signed-in user:
  - Storage Blob Data Contributor
  - Storage Queue Data Contributor
  - Storage Account Contributor (one-time, host bookkeeping containers)
- **Dataverse app user** for the signed-in user on `https://org1e37fc51.crm.dynamics.com` with appropriate security roles to read/write STG tables.

## Before first run — fill in placeholders

Open `src/OrigenateFunction/Models/ColumnMap.cs` and replace:

1. `PublisherPrefix` — currently `"new_"`. Replace with the real prefix (e.g., `"cr1a3_"`).
2. The `ExcelHeaderToDataverse` dictionary — add every column from the Origenate Excel that should land in `STG_ORIGENATE`, mapping Excel header text → Dataverse logical column name.
3. If primary key field names don't follow the `{prefix}_{logical}id` pattern, update `StgOrigenatePrimaryId`, `HoldingPrimaryId`, `ExceptionsPrimaryId`.

## Build

```bash
dotnet build src/OrigenateFunction/OrigenateFunction.csproj
```

## Run locally

```bash
cd src/OrigenateFunction
func start
```

First storage call → browser sign-in (cached for session). First Dataverse call → second browser sign-in (different audience).

Drop a `.xlsx` into `originate-landing` via Azure Storage Explorer to fire the trigger.

## Architecture (SOLID + design patterns)

```
src/OrigenateFunction/
├── Abstractions/        — interfaces only (DIP)
├── Options/             — OrigenateOptions, BlobConnectionOptions (Options pattern)
├── Dataverse/           — Gateway + Builder + Factory + RetryPolicy
│   ├── DataverseGateway        (HTTP + auth + retry orchestration)
│   ├── DataverseTokenProvider  (OAuth token caching)
│   ├── HttpRequestFactory      (authorized HttpRequestMessage — Factory)
│   ├── ExponentialBackoffRetryPolicy  (Strategy)
│   ├── MultipartBatchBuilder   ($batch payloads — Builder)
│   ├── BulkWriter / BulkDeleter / PagedReader / InFilterBuilder
│   └── DataverseConnectivityCheck (WhoAmI)
├── Repositories/        — one per table (Repository pattern)
│   ├── TableRepositoryBase
│   └── StgOrigenateRepository / HoldingRepository / ExceptionsRepository
├── Storage/             — split by responsibility (SRP/ISP)
│   ├── ContainerClientFactory  (Factory)
│   ├── BlobArchiver            (landing → archive)
│   ├── FailedBlobMover         (landing → failed)
│   └── BlobFailureFileWriter   (per-row CSV → failed)
├── Excel/OpenXmlExcelReader.cs  (streaming reader)
├── Mappers/JsonRowMapper.cs     (JsonElement → record dict)
├── Pipeline/            — orchestration via Strategy/Chain
│   ├── PipelineExecutor         (iterates IPipelineStep — OCP)
│   └── Steps/                   (Download, Connect, ClearHolding,
│                                  BackupOldRows, TruncateStg, LoadExcel,
│                                  InsertExceptions, Reconcile,
│                                  UploadFailures, ArchiveBlob)
├── Models/              — DTOs + ColumnMap (★ edit me)
├── Functions/OrigenateBlobTrigger.cs  (thin entry point)
└── Program.cs           (DI — binds interfaces to implementations)
```

**SOLID application:**
- **S**RP — every class has one reason to change (a single step, one HTTP concern, one table).
- **O**CP — pipeline is open for extension (add an `IPipelineStep` and register it), closed for modification (executor is unchanged).
- **L**SP — repositories all honor `ITableRepository`; substitutable.
- **I**SP — small focused interfaces (`IBulkWriter`, `IBulkDeleter`, `IPagedReader`, etc.) instead of one fat client.
- **D**IP — every consumer depends on an interface; concrete bindings live only in `Program.cs`.
