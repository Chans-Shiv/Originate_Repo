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

## Project layout

```
src/OrigenateFunction/
├── Functions/OrigenateBlobTrigger.cs   # entry point
├── Services/
│   ├── OrigenateProcessor.cs           # 8-step pipeline
│   ├── DataverseClient.cs              # Web API: CreateMultiple, $batch delete, paged retrieve
│   ├── DataverseTokenProvider.cs       # OAuth token caching
│   ├── BlobService.cs                  # archive / failed / failure-file upload
│   ├── ExcelReader.cs                  # OpenXml SAX streaming (150K-row safe)
│   └── FailureWriter.cs                # CSV serializer for row failures
├── Models/
│   ├── ColumnMap.cs                    # ★ edit me — single source of truth
│   ├── OrigenateRow.cs
│   └── ExceptionRow.cs
└── Program.cs                          # DI / credential setup
```
