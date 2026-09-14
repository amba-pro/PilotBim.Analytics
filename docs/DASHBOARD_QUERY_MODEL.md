# Dashboard Query Model

Runtime/internal contract introduced in DB-2. Not persisted. Not bound to UI.

## Purpose

Describe a widget as **Scope + EntityTypeId + Filters + optional Dimension + Measure + Sort + Limit**.

Current executors (same `DashboardWidgetQuery`):

- `SnapshotWidgetQueryEngine` over `ProjectAnalyticsSnapshot`
- `ObjectRowsWidgetQueryEngine` over a complete `DashboardTypeDataset` + field catalog

Session orchestrator (DB-6): `DashboardQueryCoordinator.ExecuteAsync(query)` routes and shares TypeId materialization. See **Coordinator Execution** and `docs/DASHBOARD_QUERY_COORDINATOR.md`.

The query **must not** mention `AnalyticsChartSource`.

## Query Definition

`DashboardWidgetQuery`

| Member | DB-2 / DB-4 / DB-5 |
|--------|------|
| Scope | `CurrentProject` only |
| EntityTypeId | `int?` — snapshot: null; ObjectRows: required TypeId |
| Filters | AND list; empty = none. ObjectRows only |
| DimensionFieldId | null/empty = scalar Count; else a stable field id |
| Measure | `Count` only |
| Sort | ValueDescending (default), ValueAscending, LabelAscending, LabelDescending |
| Limit | null = all; `<= 0` → `InvalidQuery` |

## Scope

`DashboardQueryScopeKind.CurrentProject`

Selection, descendants, BIM model/part: not executable. Other enum values → `UnsupportedQuery`.

## Dimension

Stable ids from DB-1 (`DashboardFieldIds`).

Snapshot-executable:

- `system:typeId`
- `system:creatorId`
- `system:createdMonth`
- `system:userState`
- `system:responsible`

Catalog exists but **not** snapshot-executable (returns `UnsupportedQuery`, no fake data):

- `system:objectId`, `system:parentId`, `system:created`, `system:objectState`
- any `attribute:{typeId}:{name}`

## Measure

`DashboardQueryMeasure.Count` only.

Unknown enum values → `UnsupportedQuery`.  
Percent / Sum / Average are not part of this contract.

`long` values: Count of objects (or sampled objects where the snapshot only has samples).

## Sort

Primary key as requested. Ties: `Key` Ordinal, then `Label` Ordinal.

Not CurrentCulture. Timeline order for months is **not** implied by the query; request `LabelAscending` if period strings should sort lexicographically.

## Limit

Applied **after** sort.

- null → unlimited  
- N > count → all rows  
- N ≤ 0 → `InvalidQuery` (unlike `ChartDataService` take=0 meaning “all”)

## Dataset

`WidgetDataset` / `WidgetDataRow`

| Field | Meaning |
|-------|---------|
| Key | machine category id (invariant) |
| Label | display text (may be empty) |
| Value | `long` count |

No colors, geometry, WPF, Pilot types.

Empty/null source labels → Key/Label `""` (renderer may localize later). Not `"?"`, not Russian sentinels.

## Query Result Status

`WidgetQueryResult.Status`

| Status | When |
|--------|------|
| Success | ≥1 row after mapping (ObjectRows scalar Count of 0 is Success with Value=0) |
| Empty | snapshot null/empty source, or mapped zero rows (ObjectRows GroupBy on an empty Complete dataset) |
| UnsupportedQuery | dimension/scope/measure not executable from this engine |
| InvalidQuery | null query, Limit ≤ 0, ObjectRows missing/mismatched EntityTypeId, malformed Complete metadata |
| IncompleteData | ObjectRows only: query is valid and supported, but `Coverage != Complete`. **No analytical numbers.** Not Empty. Snapshot never emits this. |

Ordinary unsupported combinations do not throw.

## Stable Field Identity

Never persist or query by localized titles (`"Ответственный"`).  
Use `system:responsible`, `system:typeId`, etc.

## Snapshot-backed Capabilities

One in-memory pass over an existing aggregate list. Complexity ~ O(n log n) for sort.

**Invariant:** one dashboard refresh = one project scan; N widgets = N cheap transformations.

Pilot SDK calls per query: **0**.

## Unsupported Queries

Do not fall back to another dimension or to ChartSource defaults.

## Filters

`DashboardFilterDefinition`: `FieldId`, `Operator`, `Value`.

Operators (V1):

| Operator | Semantics |
|----------|-----------|
| Equals | typed semantic equality; missing row is **false** |
| NotEquals | logical inverse of Equals; **missing row is true** (`Status != A` includes rows without Status) |
| IsEmpty | missing field, null, empty text, whitespace — same missing definition as DB-4 grouping |
| IsNotEmpty | complement of IsEmpty |

Multiple filters: **AND** only, in query order. No OR.

`DashboardFilterValue`: `Kind` + primitive `Value` (string / long / double / bool / Guid / stable-key string). **No DisplayText.** Persistence-ready; not serialized in DB-5.

Typed equality:

- Text: `StringComparison.Ordinal`, no case-fold, no trim of non-empty values. Whitespace-only **source** values are missing (IsEmpty), so they do not Equals a non-empty criterion.
- Integer: numeric equality; `int`/`long`/`short` unify.
- Number: prefer `decimal` unification when conversion is safe; otherwise IEEE `double` equality. Integer and Number kinds are compatible. No culture `ToString`.
- Boolean: bool equality.
- Guid: Guid equality (`D` parse allowed).
- Enum / User / Reference: **StableKey** (group identity). DisplayText ignored. Same display + different ids → not equal.

DateTime filters: **UnsupportedQuery** (no formatted-string comparison; same as ObjectRows grouping).

Validation: unknown field / wrong TypeId attribute / `CanFilter=false` / DateTime / createdMonth → `UnsupportedQuery`. Equals/NotEquals with null value or incompatible kind → `InvalidQuery`. Unknown operator → `UnsupportedQuery`.

Pipeline (ObjectRows):

complete rows → validate query/filters → reject skipped-field quality → **filter objects** → Count / GroupBy → sort → limit

Never filter visualization buckets.

Scalar + filters + zero matches: **Success, Value=0** (same as empty Complete scalar).

`sum(group.Value) == filtered row count`.

Snapshot: empty Filters → unchanged. Non-empty Filters → `UnsupportedQuery` (no fake aggregate filtering).

### Data quality

DB-3.1 now records `SkippedUnsupportedFieldIds` (tiny extension). If a query **touches** (filter or dimension) a field with skipped unsupported values → `IncompleteData`. Global skip count alone does not fail unrelated fields. IsEmpty still cannot distinguish true absent vs skipped-for-that-field when the field was skipped — those queries are IncompleteData instead of silent missing.

## ObjectRows-backed Execution

`ObjectRowsWidgetQueryEngine.Execute(dataset, catalog, query)` → `WidgetQueryResult` / `WidgetDataset`.

No Pilot SDK. No materializer call. Does not clone `DashboardObjectRows`. Filters allocate a list of matching **references**. Aggregation memory is proportional to distinct groups.

### EntityTypeId

Required. Must equal `DashboardTypeDataset.TypeId`. Mismatch or null → `InvalidQuery`. Identity is the integer TypeId, never the type title.

Snapshot executor: `EntityTypeId == null` remains project-wide; non-null → `UnsupportedQuery`.

### Complete dataset requirement

Checked **before** aggregation, using `dataset.Coverage` only (not by inspecting rows to guess).

| Coverage | Result |
|----------|--------|
| Complete | proceed |
| Partial | `IncompleteData`, empty dataset |
| Failed | `IncompleteData`, empty dataset |

Complete metadata must match (`ExpectedCount == LoadedUniqueCount == Rows.Count`). Row `TypeId` must match the dataset. Otherwise `InvalidQuery`.

### Field validation

Catalog is required (no hidden global catalog).

1. Descriptor must exist → else `UnsupportedQuery`
2. Attribute descriptors must belong to this TypeId → else `UnsupportedQuery`
3. `CanGroup == true` → else `UnsupportedQuery`
4. Executable from object rows: not `DateTime`, not `Unknown`, not `system:createdMonth` (no hidden month buckets)

Absence of a field on every row does **not** mean the dimension id is invalid.

### Group identity

`DashboardGroupValue` maps `DashboardFieldValue` → `(Key, Label)`. Display text never controls identity when `StableKey` / typed value exists.

| Kind | Key | Label |
|------|-----|-------|
| Text | exact string, `StringComparer.Ordinal`. Empty/whitespace → missing. No case-fold, no trim of non-empty values. | same as key |
| Integer | invariant numeric string | DisplayText if non-empty, else key |
| Number | invariant `"R"` format (not culture `ToString`) | DisplayText if non-empty, else key |
| Boolean | `"1"` / `"0"` | DisplayText if non-empty, else key |
| Guid | `"D"` format | DisplayText if non-empty, else key |
| Enum | `StableKey` (else Guid/text fallback) | first non-empty DisplayText wins |
| User / Reference | `StableKey` (person/org/element id) | first non-empty DisplayText wins |

Same stable ID + different display → **one** bucket. Different IDs + same display → **two** buckets.

Raw `DateTime` grouping is **UnsupportedQuery**. Use `system:createdMonth` on the snapshot engine, or a later bucket function. ObjectRows does not invent monthly buckets and does not store `createdMonth` on rows.

### Missing bucket

Missing field, null value, empty text, whitespace → one reserved bucket.

- Key = `DashboardGroupValue.MissingKey` (`"\u0001missing"`) — not a user-facing string
- Label = `""`

UI may later render `(не задано)`. DB-4 does not.

For single-dimension Count: `sum(group.Value) == Rows.Count` (including missing).

### Unsupported DB-3.1 values

Skipped values are **absent** on the row (global `SkippedUnsupportedValues` only). DB-4 cannot prove per-field skip vs true missing. Policy: they join the **missing** bucket. Mapped groups stay accurate. Dimension queries are **not** failed solely because the dataset skip counter is > 0. Completeness of “missing vs unreadable” is **not** distinguished.

### Scalar Count

Dimension null. One row: Key `""`, Label `""`, Value = `Rows.Count`. Empty Complete type → Success with `0` (defined count), not IncompleteData.

### Sort / limit

Shared `WidgetQueryPresentation` (same as snapshot): ValueDescending default; ties Key then Label, `StringComparer.Ordinal`. Limit after sort. null = all; ≤0 Invalid; oversize = all.

### Performance

Group O(N), sort O(K log K). Pilot SDK calls: 0. Additional materialization: 0. Catalog is not rebuilt per row.

## Coordinator Execution

DB-6. Internal. Unused by current UI.

`DashboardQueryCoordinator` owns one session (`ProjectAnalyticsSnapshot` + `DashboardFieldCatalog` + per-TypeId `Task<DashboardTypeDataset>` cache).

Routing (deliberately simple — not chart type / labels / `WidgetKind`):

```
if query.EntityTypeId == null
    → SnapshotWidgetQueryEngine (0 materializer calls)
if query.EntityTypeId != null
    → session TypeId cache (miss: one Task.Run around Materialize)
    → ObjectRowsWidgetQueryEngine
```

Invariant: one TypeId = at most one materialization attempt per coordinator instance, including concurrent widgets. Partial/Failed/faulted tasks stay cached until Dispose. Refresh = dispose + new coordinator.

Caller `CancellationToken` is ignored in V1; session Dispose cancels shared loads. `ExecuteAsync` after Dispose throws `ObjectDisposedException`.

Do not invent a second query language. Widget Editor should call only `ExecuteAsync`.

### Conceptual queries (IDs, not Russian titles)

Example 1 — remarks by type:

```
EntityTypeId=12
Dimension=attribute:12:RemarkType
Measure=Count
→ WidgetDataRow Key="Coordination" Label="Coordination" Value=42
  WidgetDataRow Key="Attributes"   Label="Attributes"   Value=17
  …
```

Example 2 — documents by creator:

```
EntityTypeId=34
Dimension=system:creatorId
Measure=Count
→ WidgetDataRow Key="7" Label="" (or first display if present) Value=…
```

Example 3 — custom enum attribute:

```
EntityTypeId=77
Dimension=attribute:77:Status
Measure=Count
→ WidgetDataRow Key="<stable enum id>" Label="<first display>" Value=…
```

### Runtime

DB-4 is pure in-memory: **PILOT_RUNTIME_VALIDATION_REQUIRED = NO**.  
DB-3.1 materializer canary remains **YES** before UI integration.

## Persistence Considerations

Do **not** write this model into `dashboard-layout.json` until ids and statuses stabilize. Attribute ids remain rename-sensitive.

## Examples

Scalar project object count (sum of `ObjectsByType`):

```
Scope=CurrentProject Dimension=null Measure=Count
```

Objects by type, top 12:

```
Scope=CurrentProject
Dimension=system:typeId
Measure=Count
Sort=ValueDescending
Limit=12
```

Unsupported on snapshot (needs ObjectRows + EntityTypeId):

```
EntityTypeId=12
Dimension=attribute:12:RemarkType
Measure=Count
```
