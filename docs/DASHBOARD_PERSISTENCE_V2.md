# Dashboard Persistence V2

Internal definition store introduced in DB-7. Wired into the live dashboard in **DB-9**. Not a MEF export.

## Purpose

Persist a versioned dashboard:

Dashboard → Widgets → Layout + (Legacy content **or** Query + Visualization)

Runtime source of truth after DB-9 is `DashboardDefinition`. `DashboardLayoutStore` / `dashboard-layout.json` remain the V1 migration/rollback source only.

## Production integration (DB-9)

`AnalyticsDashboardPresenter` loads V2 with `IObjectsRepository.GetDatabaseId()`.

- Missing V2: in-memory V1 migration, **no write on open**
- First explicit mutation: atomic V2 save
- Corrupt / UnsupportedVersion / ProjectMismatch: degraded read-only, **file not overwritten**

See `docs/DASHBOARD_DB9_INTEGRATION.md`.

## Project Scoping

Generic widgets persist `EntityTypeId` and `attribute:{typeId}:…` ids that are **database-specific**.

A single global layout file is therefore unsafe for query widgets.

**Identity (CONFIRMED_STABLE):**

| | |
|--|--|
| API | `IObjectsRepository.GetDatabaseId()` |
| CLR type | `System.Guid` |
| Lifetime | Pilot database / connection |
| Restart | same Guid for the same database |
| Unique | different databases have different ids |

Not used: project display name, folder title, `GetStoragePath()`, `SystemObjectIds.RootObjectId` (constant), localized strings.

`IDataObject.Id` on `GetRootObject()` is a related Guid but `GetDatabaseId()` is the explicit database identity.

Helper: `DashboardProjectKey` (canonical `"D"` format, lowercase hex). Empty Guid is rejected.

## Storage Path

Strategy: **one V2 file per project**.

```
%LOCALAPPDATA%\PilotBim.Analytics\Dashboards\<database-guid-D>\dashboard.json
```

Example folder: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` — filename-safe, not a display name.

Legacy global file is left in place:

```
%LOCALAPPDATA%\PilotBim.Analytics\dashboard-layout.json
```

Tests inject a temp dashboards root. Production default is `DashboardDefinitionStore.DefaultRoot`.

## Schema Version

`SchemaVersion = 2`

| Document | Meaning |
|----------|---------|
| No `SchemaVersion` (legacy layout JSON) | `LEGACY_V1` |
| `SchemaVersion = 2` | current |
| `SchemaVersion > 2` | `UNSUPPORTED_VERSION` — do not parse as V2, do not overwrite |
| Other / corrupt | invalid or corrupt — do not overwrite |

Do not deserialize a future schema as the current schema for use.

## Dashboard Definition

`DashboardDefinition`

| Field | Role |
|-------|------|
| SchemaVersion | 2 |
| Id | machine id; V1 uses `"default"` (one dashboard per project). Title is not identity. |
| Title | user-authored display |
| ProjectKey | canonical database Guid |
| Widgets[] | ordered widget definitions |

Multiple dashboards later can add more files/ids. DB-7 does not implement a dashboard library.

## Widget Definition

`DashboardWidgetDefinition`

| Field | Role |
|-------|------|
| Id | stable machine id (independent of title) |
| Title | display (persisted) |
| Layout | `DashboardWidgetLayoutDefinition` |
| ContentKind | `Legacy` xor `Query` |
| Legacy | specialized/current widget payload |
| Query | generic `DashboardWidgetQuery` document |
| Visualization | render type for query widgets |

Field catalog display names are **not** persisted as identity. Resolve titles from `DashboardFieldCatalog` at runtime.

## Legacy Widget Content

Lossless copy of current `DashboardWidgetState` semantics:

- `WidgetKind` (`Kpi` / `Bim` / `Responsible` / `Chart`)
- `ChartSource` (enum **name** string: `Types`, `StateSemantic`, `IfcTypes`, …)
- `ChartKind` (`HorizontalBar`, `VerticalBar`, `Pie`, `Line`)
- `TopN` (`0` = all, same as today)

Do **not** rewrite StateSemantic / BIM / Remarks charts as `DashboardWidgetQuery`. Those remain Legacy.

## Query Widget Content

Invariant (whole document rejected if broken):

```
LegacyContent != null   XOR   (Query != null AND Visualization != null)
```

Query widgets store the same semantics as DB-2/4/5: Scope, EntityTypeId, Dimension, Measure, Sort, Limit, Filters[] (AND, authored order).

Runtime type: `DashboardWidgetQuery`. Persisted type: `DashboardWidgetQueryDocument` (strings + encoded filter values).

## Query Persistence

Enums as **exact CLR names** (`CurrentProject`, `Count`, `ValueDescending`, `Equals`, …), not integers.

`DimensionFieldId` omitted/null = scalar Count.

Limit: null = unlimited; `<= 0` is structurally invalid (same as executor).

No coordinator, datasets, snapshot, catalog, or `WidgetDataset` in JSON.

## Filter Value Encoding

`kind` + invariant `value` **string**. Never boxed JSON `object` (avoids Int32/Int64 and Guid/string ambiguity).

| Kind | Encoding |
|------|----------|
| Text | exact string (JSON-escaped; Unicode / newlines / quotes OK) |
| Integer | invariant integer text (`-7`, `42`) |
| Number | invariant `"R"` (`1.5`) |
| Boolean | `true` / `false` |
| Guid | canonical `D` |
| User / Reference / Enum | exact stable key string (not DisplayText) |

IsEmpty / IsNotEmpty: no value payload.

Culture: encode/decode uses `CultureInfo.InvariantCulture` only.

## Visualization

Separate from the query. Same query may render as Bar, Pie, or Table later.

V1 `Type` names:

`Auto` | `Kpi` | `Bar` | `HorizontalBar` | `Pie` | `Line` | `Table`

No color/theme JSON. Legacy chart kind stays on Legacy content, not Visualization.

## Layout

`DashboardWidgetLayoutDefinition`:

- `Order` (int ≥ 0)
- `ColumnSpan` (1 or 2) — current WrapPanel half/full
- `IsVisible`

No X/Y/W/H in DB-7. DB-10 can extend this object without rewriting Query/Legacy.

## Legacy V1 Detection

Current serializer: `DataContractJsonSerializer`, PascalCase CLR names, no schema version, enums as **strings already** on the layout DTO (`ChartSource`, `ChartKind`). Unknown JSON properties ignored. Missing file / corrupt JSON: V1 `LoadOrDefault` currently writes defaults (unchanged in DB-7).

`DashboardDefinitionStore.Detect(stream)`:

- `SchemaVersion == 2` → V2
- `SchemaVersion > 2` → unsupported
- `SchemaVersion` missing/0 → Legacy V1

## V1 → V2 Migration

`DashboardDefinitionV2Migrator.FromLegacy(state, databaseId)` — **pure**, in memory.

- preserves widget order, titles, kinds, source, chart kind, TopN, ColumnSpan, visibility
- preserves existing ids (`kpi`, `bim`, `responsible`, `chart-{guid:N}`)
- empty id → deterministic `legacy-{index}` (never a new random Guid per load)
- duplicate ids: first wins
- unknown `Blocks` ids skipped (same as V1 Normalize)
- does **not** auto-write any file
- does **not** turn Chart widgets into Query widgets

Repeated migration of the same V1 state yields equivalent V2.

## File Safety

V2 Load **never** writes. Missing V2 file → `Missing` (no create). Corrupt / unsupported / invalid / mismatch → log, return status, **leave the file**.

Malformed V2: **whole document rejected** (no dropping widgets).

## Corrupt File Behavior

Status `Corrupt`. Plugin must not crash. File preserved for recovery. No automatic default overwrite (unlike V1 `LoadOrDefault`).

## Unknown Version Behavior

Status `UnsupportedVersion`. Do not migrate, do not overwrite, do not downgrade.

## Project Mismatch

If the document `ProjectKey` is not the requested database Guid: `ProjectMismatch`. Do not apply TypeIds/queries. Not a template.

## Atomic Save

Serialize to `dashboard.json.tmp` in the same directory → `Flush(true)` → `File.Replace` if the destination exists, else `File.Move`. Failed save deletes the temp file and must not destroy a previous valid file.

V2 lives under `Dashboards\<guid>\`; the legacy global file is already a backup. DB-7 does **not** copy or delete `dashboard-layout.json`. First UI save in DB-8/DB-9 can add `dashboard-layout.v1.backup.json` if still needed.

## Examples

Sanitized V2 (illustrative; DCJS emits compact JSON):

```json
{
  "SchemaVersion": 2,
  "Id": "default",
  "Title": "Dashboard",
  "ProjectKey": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "Widgets": [
    {
      "Id": "kpi",
      "Title": "KPI / сводка",
      "Layout": { "Order": 0, "ColumnSpan": 2, "IsVisible": true },
      "ContentKind": "Legacy",
      "Legacy": {
        "WidgetKind": "Kpi",
        "ChartSource": "Types",
        "ChartKind": "HorizontalBar",
        "TopN": 12
      }
    },
    {
      "Id": "remarks-by-type",
      "Title": "Remarks",
      "Layout": { "Order": 1, "ColumnSpan": 1, "IsVisible": true },
      "ContentKind": "Query",
      "Query": {
        "Scope": "CurrentProject",
        "EntityTypeId": 123,
        "DimensionFieldId": "attribute:123:RemarkType",
        "Measure": "Count",
        "Sort": "ValueDescending",
        "Limit": 10,
        "Filters": [
          {
            "FieldId": "attribute:123:Status",
            "Operator": "Equals",
            "ValueKind": "Text",
            "Value": "Open"
          }
        ]
      },
      "Visualization": { "Type": "Bar" }
    }
  ]
}
```

No real project ids. No analytical result rows.

## Future Schema Evolution

V1 → V2 is one explicit migrator. Later: V2 → V3 with a new `SchemaVersion`. Unknown newer files stay untouched.

DB-8: Widget Editor V2 against this model (definitions only; live preview may wait for DB-9).
