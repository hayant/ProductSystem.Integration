# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Issue / PR workflow

When asked to work on a GitHub issue, follow this flow:

1. Create a feature branch off `main` (e.g. `issue-<number>-short-description`). **Never commit
   directly to `main`.**
2. Implement the change and verify it (build, tests, and a real sync run against a locally running
   ProductSystem API when the change affects sync behaviour).
3. Show the changes to the user and **wait for their review before committing**.
4. After approval: commit, push the branch, and open a PR that references the issue
   (`Fixes #<number>` in the PR body so the merge auto-closes it).
5. Let the user merge the PR (or merge only when they explicitly ask), and confirm the issue
   closed afterwards.

## Commands

```bash
dotnet build                                        # build whole solution (from repo root)
dotnet run --project src/ProductSystem.Integration  # run one sync pass, then exit
```

`dotnet run` picks up the launch profile, which sets `DOTNET_ENVIRONMENT=Development` —
`appsettings.Development.json` points the worker at `http://localhost:5080/api/v1/` with the
committed dev API key. For a real end-to-end run, start the API from the sister repository first:
`dotnet run --project src/ProductSystem.Api` in `../ProductSystem`.

`ProductSystemApi:BaseUrl` and `ProductSystemApi:ApiKey` are **required** — startup throws if
either is missing (the base `appsettings.json` leaves them empty on purpose).

There are no tests yet.

## Architecture

### A run-once worker, not a service
The single project (`net8.0`, Worker SDK) is a console app that runs **one inbound sync pass and
exits** — `Program.cs` builds the host, resolves `SyncService`, awaits `RunInboundAsync`, done.
There is deliberately no scheduler; production would host this as a `BackgroundService` or a
cron-triggered job (the comment in `Program.cs` spells this out). The sync watermark is computed
from `Sync:WatermarkDays` (default 7) rather than persisted — a real implementation would load it
from a `sync_state` store.

### HTTP is the only coupling to ProductSystem
This repo intentionally does **not** reference `ProductSystem.Shared` from the sister repository
(`../ProductSystem`). `Api/CreateProductRequest.cs` re-declares the API's contract, and all writes
go through `ProductsApiClient` over HTTP — keep it that way; do not add a project reference to
share types. The API enforces SKU uniqueness and validation; this worker just reports outcomes.

### The two boundaries: IErpClient and IProductsApiClient
`SyncService.RunInboundAsync` only sees clean abstractions on both sides:
- **`Erp/IErpClient`** — everything ERP-specific (auth, serialization, quirks) lives behind this.
  `FakeErpClient` (fixed in-memory catalog) is registered today; production would swap in an
  `HttpErpClient`. `ErpProduct` is the ERP's view of a product, distinct from the API's contract —
  the mapping in `SyncService` is an explicit translation boundary.
- **`Api/IProductsApiClient`** — translates HTTP status codes into the `CreateProductOutcome` enum
  (`Created` = 2xx, `Duplicate` = 409, `Failed` = everything else) so HTTP never leaks into sync
  logic. A 409 means the SKU already exists and is an **idempotent skip**, not an error. One
  `Failed` record never aborts the run; the client logs the detail and the run counts it.

### Typed HttpClient conventions
`ProductsApiClient` is registered via `AddHttpClient` in `Program.cs`: `BaseAddress` and the
`X-Api-Key` header are configured once at registration, with a 10s timeout. Two gotchas preserved
in the code:
- `BaseAddress` ends in a **trailing slash** (`.../api/v1/`) so relative URIs like `"products"`
  resolve correctly — don't remove it.
- `TaskCanceledException` with an uncancelled token means **timeout**, and is caught separately
  from `HttpRequestException`; both map to `Failed`.

The dev API key (`dev-smoke-test-key`) committed in `appsettings.Development.json` matches the
one committed in the ProductSystem repo — it is public dev configuration, not a secret.
