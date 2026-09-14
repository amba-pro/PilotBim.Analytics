# Dashboard Type Dataset Runtime Canary (DB-R1)

Diagnostic only. Not a product UI. Not DB-5.

## Purpose

Prove DB-3.1 `DashboardTypeDatasetMaterializer` against a real Pilot-BIM server for **one explicit TypeId**:

- full `SearchByType` is accepted
- `LoadedUniqueCount == Total` (`ExpectedCount`)
- no silent result cap (would appear as PARTIAL)
- `SubscribeObjects` returns the searched IDs
- in-memory `Attributes` are readable (field values counted, contents not logged)
- elapsed time and approximate memory
- cancellation does not report PASS

DB-4 query engine is **not** invoked by this canary.

## How to enable

User environment variables (loaded when Pilot **starts**):

| Variable | Required | Meaning |
|----------|----------|---------|
| `PILOTBIM_ANALYTICS_DASHBOARD_CANARY_TYPE_ID` | yes | Positive integer Pilot type id |
| `PILOTBIM_ANALYTICS_DASHBOARD_CANARY_CANCEL_AFTER_MS` | no | Positive delay; cancels the canary token |

No variable → canary never runs. Plugin behavior is identical to a build without the harness.

Invalid TypeId → one log line, no materialization.

## Diagnostic trigger

There was no existing self-test hook with Pilot services.

The harness is **env-gated**. On first **Catalog** or **Overview/Analytics** open, `AnalyticsCommandService` resolves `IObjectsRepository` + `ISearchService` and calls `DashboardTypeMaterializerCanary.TryStart`.

Work runs on `Task.Run` (not the WPF UI thread). At most **once per Pilot process**.

No toolbar/menu item, no popup, no persistence.

## How to choose TypeId

Do **not** put project-specific ids in source.

From a normal Inventory scan (Catalog window):

- Types grid column **TypeId**
- Text report section `## TYPE INVENTORY`: `ID: {TypeId}` and `OBJECTS: {count}`
- Analytics Overview types grid: **TypeId** / Count
- Optional CSV export `types` columns `TypeId`, `Count`

Pick:

1. **Canary A** — moderate `OBJECTS` (not empty, not the largest type)
2. **Canary B** — larger than A, still not the project maximum
3. **Cancellation** — large enough that 500 ms is likely to interrupt

`OBJECTS` in inventory is search `Total` (or a walk estimate). The canary re-searches that TypeId completely; it does **not** use inventory samples.

## Normal canary

1. Deploy `scripts\deploy-dev.ps1` with Pilot **closed**.
2. Set TypeId (User scope), **fully restart** Pilot-BIM.
3. Open **Catalog** or **Overview** once (starts the background canary).
4. Wait; do not spam-open windows (one-shot anyway).
5. Read `%LOCALAPPDATA%\PilotBim.Analytics\Logs\analytics.log`

## Cancellation canary

Set both TypeId and `CANCEL_AFTER_MS` (for example `500`). Restart Pilot. Open Catalog/Overview.

Expected terminal status: `CANCELLED` or other non-`PASS` (`TIMEOUT` / `FAILED` / `PARTIAL`). Never `PASS` after cancel.

Restart Pilot between configurations.

## Expected log records

Prefix: `[DashboardCanary]` (logger area `DashboardCanary`).

```
[DashboardCanary] START TypeId=...
[DashboardCanary] RESULT TypeId=... ExpectedCount=... LoadedUniqueCount=... Coverage=... Rows=... FieldValueCount=... SkippedUnsupportedValues=... ElapsedMs=...
[DashboardCanary] MEMORY approximate beforeManaged=... afterManaged=... deltaManaged=... beforeWorkingSet=... afterWorkingSet=... deltaWorkingSet=...
[DashboardCanary] PASS
```

Optional:

```
[DashboardCanary] DATA_QUALITY_WARNING SkippedUnsupportedValues=...
```

`INVALID_TYPE_ID` if the env value is not a positive integer.

No object values, names, or attribute payloads are logged.

## PASS criteria

All of:

- `Coverage == Complete`
- `LoadedUniqueCount == ExpectedCount`
- `Rows == LoadedUniqueCount`

Then the terminal line is `[DashboardCanary] PASS`.

Non-zero `SkippedUnsupportedValues` does **not** fail the canary; it only warns. Field-level skip accounting is later work.

## FAIL / PARTIAL / other

| Terminal | Meaning |
|----------|---------|
| `PARTIAL` | Coverage Partial (often search/subscribe short of Total, or possible server cap) |
| `FAILED` | Coverage Failed, exception, or Complete metadata mismatch |
| `CANCELLED` | cancel token fired (`Reason` cancelled) |
| `TIMEOUT` | 30s search or subscribe wait abandoned |

Do not treat PARTIAL as PASS.

Also search the log for `Exception`, `ERROR`, `timeout`, `ObjectDisposed`, `Callback`.

## How to disable

Set the TypeId variable to `$null` (User scope) and restart Pilot.

## Privacy

Logs contain TypeId and counts only. Do not commit project TypeIds, object names, or live counts to git.
