# Dashboard-Level Filters V1

DB-12. Runtime overlay on Query widgets. Explicit bindings. No query rewrite.

## Purpose

Let the user define a filter at **dashboard** level and bind it to several compatible Query widgets.

Example: Type = Remarks, Field = Responsible, Operator = Equals, Value = a stable user key, applied to “Remarks by type”, “Remarks by status”, and “Remarks KPI”. A Documents widget with a different TypeId stays unbound.

The saved widget definitions (base queries) remain unchanged.

## Explicit Binding

Each dashboard filter persists `TargetWidgetIds[]` — widget machine ids, never titles.

Only Query widgets may be bound. Legacy widgets are not selectable and are never filtered.

A new filter requires at least one target. A stale zero-target filter may remain on disk (the user can rebind later). Save from the editor still requires a target.

## TypeId Scope

A filter is scoped to one `EntityTypeId`.

It cannot bind to widgets of another TypeId. There is no cross-Type field mapping.

Two dashboard filters may exist independently (Type 100 vs Type 200) and affect their own explicit widget sets.

## FieldId Identity

Compatibility uses machine identity:

- `DashboardFilter.EntityTypeId == Widget.Query.EntityTypeId`
- `DashboardFilter.FieldId` is a catalog field that belongs to that TypeId, `CanFilter == true`, and is executable by the ObjectRows engine

`Id` is a stable Guid string. Changing Title, value, or bindings does not change `Id`.

## No Display-Name Matching

Never bind or apply by DisplayName, Title, localized label, or attribute title.

Type 10 `attribute:10:Status` and Type 20 `attribute:20:Status` may share the display name “Статус”. A filter for Type 10 must not apply to Type 20.

## Definition

Persisted type: `DashboardLevelFilterDefinition` (not `DashboardFilterDefinition`, which is a per-query filter).

| Member | Role |
|--------|------|
| Id | stable machine id |
| Title | user-facing only |
| EntityTypeId | TypeId scope |
| FieldId | catalog field id |
| Operator / ValueKind / Value | same encoding as widget filters |
| Disabled | `false` (missing) = enabled; persisted mute without delete |
| TargetWidgetIds[] | explicit Query widget ids |

Operators (same as DB-5): Equals, NotEquals, IsEmpty, IsNotEmpty.

Value identity is typed (Text / Integer / Number / Boolean / Guid / Enum / User / Reference stable key). DisplayText is not equality identity.

No Contains, ranges, dates, In, or OR groups.

## Persistence

Stored in the same per-database file:

`%LOCALAPPDATA%\PilotBim.Analytics\Dashboards\<database-guid>\dashboard.json`

No global filter store. No cross-project leakage.

Writer schema: **4**. Transient runtime (Loading / Error / LastApplied) is not persisted. Widget runtime states stay per widget.

## V3 → V4 Migration

In memory only. No write on load.

Chain: V1 → V2 → V3 → V4; V2 → V3 → V4; V3 → V4.

Widgets and grid rectangles are unchanged. `DashboardFilters` becomes `[]`.

The first explicit mutation writes SchemaVersion 4 atomically. Unknown V5+ / corrupt / project-mismatch files are not overwritten.

## Compatibility

`DashboardFilterCompatibility` (pure, no SDK):

1. widget `ContentKind == Query`
2. widget has `EntityTypeId`
3. TypeId exact match
4. FieldId exists in the catalog (else **Unavailable**)
5. field belongs to that TypeId
6. `CanFilter == true`
7. ObjectRows-executable (no DateTime, Unknown, `system:createdMonth`)
8. operator/value structurally valid

Statuses: Compatible / Incompatible / Unavailable.

## Runtime Overlay

Effective query = base widget filters + applicable dashboard filters.

`DashboardEffectiveQueryBuilder` clones the runtime query. It never mutates the persisted `WidgetQuery`.

Invalid/unavailable/disabled filters are not sent into the engine. Bound widgets run their base query (plus any still-valid overlays). The dashboard shows a warning on the filter chip. One stale filter does not block other widgets.

## Effective Query

Order: authored widget filters first, then dashboard filters in dashboard-filter list order. AND only.

Sort, dimension, measure, limit, and EntityTypeId are unchanged.

No semantic deduplication. Conflicting predicates (Status=A and Status=B) are valid and may yield zero rows.

Filtering happens **before** GroupBy / Count / sort / TopN. Never post-chart. Never on an already truncated dataset.

## AND Semantics

Multiple dashboard filters on one widget become additional DB-5 filters (AND). No OR.

## Query Cache Reuse

Changing a dashboard filter must not:

- dispose `DashboardQueryCoordinator`
- rebuild the catalog
- rematerialize an already cached TypeId
- invalidate the TypeDataset cache

Already materialized TypeId → in-memory query execution only (0 extra provider calls).

First use of a TypeId may materialize once per coordinator session. Same TypeId across bound widgets still materializes once.

## Add / Edit / Remove

Edit Mode: Add / Edit / Remove. Chips remain visible in normal mode. Edit opens the filter dialog (no keystroke query execution).

Flow: candidate copy → atomic save → on success publish and rerun **old ∪ new** target Query widgets.

Save failure keeps the previous filter configuration and does not rerun.

Remove: save without the filter → publish → rerun previously bound widgets. Empty-target filters are not auto-deleted.

## Widget Delete

Deleting a Query widget removes its id from every `TargetWidgetIds` in the same transaction. Filters with zero remaining bindings are kept.

On a stale file, missing targets are ignored at runtime and surfaced as configuration, not a crash.

## Widget Type Change

After a successful widget edit, structurally incompatible bindings (TypeId / not Query) are removed from filters in the same save. No hidden invalid binding.

Missing catalog fields do **not** unbind (the filter stays; it becomes Unavailable until metadata returns).

## Missing Field

If `attribute:…:OldStatus` disappears after a Pilot metadata change:

- the filter stays persisted
- chip: Title + “Поле недоступно”
- no remap by display title
- bound widgets execute without that overlay
- if the same FieldId returns later, the filter applies again

## Metadata Refresh

After inventory/type catalog refresh, compatibility is re-evaluated. Persisted definitions are not rewritten unless the user edits.

## Legacy Widgets

Unchanged. Not listed as selectable targets. Not filtered. Existing Legacy execute/render paths stay as they are.

## Preview Semantics

Widget Editor Preview uses the widget **base query only**. Dashboard overlays apply on the dashboard card after save.

Hint: “Фильтры дашборда применяются после сохранения”.

## Performance

Filter change on a cached TypeId: 0 SDK/provider calls. Query execution is in-memory ObjectRows. Sequential per-widget refresh (same as DB-9). Different-Type filters do not burst concurrent full loads.

## Known Limitations

- Enum / User / Reference values: stable-key text (no picklists)
- No inline chip value editor (dialog only)
- No drag ordering of filters
- No cross-filter from chart clicks
- No drillthrough
- No date / Contains / range / OR
- Opaque Guid/key may appear on chips when no friendly display exists
- Auto visualization may resolve differently after a filter changes category count; persisted Auto stays Auto
