# Dashboard Query Model

Runtime/internal contract introduced in DB-2. Not persisted. Not bound to UI.

## Purpose

Describe a widget as **Scope + optional Dimension + Measure + Sort + Limit**, then execute it against a data source that does **not** live inside the renderer.

Current executor: `SnapshotWidgetQueryEngine` over `ProjectAnalyticsSnapshot`.  
Future executor: ObjectRows over the same query type.

The query **must not** mention `AnalyticsChartSource`.

## Query Definition

`DashboardWidgetQuery`

| Member | DB-2 |
|--------|------|
| Scope | `CurrentProject` only |
| DimensionFieldId | null/empty = scalar Count; else a stable field id |
| Measure | `Count` only |
| Sort | ValueDescending (default), ValueAscending, LabelAscending, LabelDescending |
| Limit | null = all; `<= 0` → `InvalidQuery` |

No Filters[] in DB-2.

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
| Success | ≥1 row after mapping |
| Empty | snapshot null/empty source, or mapped zero rows |
| UnsupportedQuery | dimension/scope/measure not executable from snapshot |
| InvalidQuery | null query, or Limit ≤ 0 |

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

## Future ObjectRows Execution

The same `DashboardWidgetQuery` should execute against object-level rows:

- Scope still CurrentProject (later other scopes filter rows first)
- DimensionFieldId indexes a column / field catalog id
- Measure Count = row count per group
- Sort/Limit unchanged

Snapshot engine is a **capability-limited** implementation, not a different query language.

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

Unsupported (needs ObjectRows):

```
Dimension=attribute:12:RemarkType
→ UnsupportedQuery
```
