# Dashboard Widget Editor V2

Internal Query-widget editor introduced in DB-8. Wired into the live dashboard in **DB-9**.

## Purpose

Let a user define a generic query widget:

Scope + Object Type + Filters + Count + Group By + Sort + Top N + Visualization + Title

and produce a DB-7 `DashboardWidgetDefinition` (`ContentKind = Query`).

## Preview (DB-9)

Real query execution is **user-initiated** (`Предпросмотр`). Opening the editor does not materialize a TypeId.

Flow: `TrySave` candidate → `coordinator.ExecuteAsync` → preview runtime state → existing renderer adapter.

Preview does not persist. Cancel leaves the dashboard unchanged. A generation counter drops late Preview #1 if Preview #2 has started.

Idle copy: «Настройте виджет и нажмите Предпросмотр».

## User Flow

1. Caller builds `DashboardFieldCatalog` + `DashboardObjectTypeOption` list from already-scanned inventory metadata.
2. `DashboardQueryWidgetEditorViewModel` is constructed with that metadata and an optional existing Query definition.
3. User edits selectors. Cancel discards VM state. Save returns a **new** validated definition.
4. Caller owns persistence (`DashboardDefinitionStore` is not called).

Legacy Kpi / Bim / Responsible / Chart widgets still use `DashboardWidgetEditorWindow`.

## Editor Inputs

- `DashboardFieldCatalog` (DB-1)
- `IEnumerable<DashboardObjectTypeOption>` (`TypeId` + display title)
- optional `DashboardWidgetDefinition` (Query content only)

No `IObjectsRepository`, search, materializer, coordinator, or filesystem.

## Editor Output

`DashboardWidgetDefinition`:

- `ContentKind = Query`
- `Query` = same DB-2/4/5 contract via `DashboardQueryPersistence`
- `Visualization.Type` = Auto | Kpi | Bar | HorizontalBar | Pie | Table (Line not offered for TypeId queries)
- `Layout` cloned from existing, or default `Order=0`, `IsVisible=true`, `ColumnSpan` 2 (scalar) / 1 (grouped) for new widgets
- New id: `query-{guid:N}`

## Type Selection

**DB-8 V1 is Object Type only.** Project-wide snapshot widgets stay as existing Legacy KPI/chart widgets. No hidden TypeId inference from Group By.

Display title is never identity. Options sorted by display (`OrdinalIgnoreCase`) then `TypeId`. New editor selects the first available type.

Changing type **resets** grouping and filters (hint shown in UI).

## Field Selection

From `catalog.ForObjectType(TypeId)`:

- Group By: `CanGroup` and ObjectRows-executable
- Filters: `CanFilter` and ObjectRows-executable

Excluded (would fail ObjectRows): `DateTime`, `Unknown`, `system:createdMonth`.

System fields of the selected type remain; attributes of other types do not.

## Group By

First option: **Без группировки** → `DimensionFieldId = null` (scalar Count).

## Filters

Inline rows: Field · Operator · Value · remove. Add Filter. AND only.

Operators: Equals, NotEquals, IsEmpty, IsNotEmpty.

IsEmpty / IsNotEmpty hide the value editor.

## Filter Operators

Machine ids are enum names. UI labels are resources.

## Typed Filter Values

| Field type | Editor |
|------------|--------|
| Text | TextBox |
| Integer / Number | TextBox, invariant parse |
| Boolean | Да/Нет (machine `true`/`false`) |
| Guid | TextBox + Guid parse |
| Enum / User / Reference | stable-key TextBox (not DisplayText) |

## Enum / user / reference availability

| Kind | Allowed-value catalog in editor metadata |
|------|------------------------------------------|
| ENUM | **NOT_AVAILABLE** (no structured enum list on `DashboardFieldCatalog`) |
| USER | **NOT_AVAILABLE** (no person/org picker without extra inventory + no data query) |
| REFERENCE | **NOT_AVAILABLE** |

Equals/NotEquals use **stable-key** input. IsEmpty/IsNotEmpty remain available. No fake ComboBox lists.

## Sort

`ValueDescending` / `ValueAscending` / `LabelAscending` / `LabelDescending` with Russian labels. Identity is the enum name.

## Top N

Blank → `Limit = null`. Positive integer valid. 0 / negative / non-numeric invalid. Grouped new selection may default to 10 when Limit was blank. Scalar KPI is not forced.

## Visualization

Scalar: Auto, KPI, Table.  
Grouped: Auto, Bar, HorizontalBar, Pie, Table.  
**Line is not shown** — ObjectRows has no ordered time dimension (`created` / `createdMonth` are not executable).

Auto persists as `"Auto"` and shows: «Тип визуализации будет выбран автоматически». No DB-11 recommendation engine.

Incompatible persisted values (e.g. Line) stay visible and **block Save** until the user picks a valid type.

## Validation

`IsValid` / `ValidationMessage`. Save enabled only when valid.

Covers: type, title, dimension availability, filters, limit, visualization compatibility. Not a second QueryEngine.

## Edit vs Create

Edit copies into VM state. Original definition is not mutated as controls change. Cancel: original unchanged. Save: new object, same `Id` / `Layout`.

## Missing Type / Field Handling

Missing `EntityTypeId` or FieldId is shown as **Недоступный тип/поле** and is **not** remapped. Save blocked until the user fixes it. Opening the editor does not rewrite persistence.

Rename-sensitive `attribute:{typeId}:{name}` ids are preserved until the user replaces them.

## Legacy Widgets

Not edited by this UI. Mixed dashboards remain valid at the persistence layer.

## No Query Execution in DB-8

DB-8 shipped without `ExecuteAsync`. DB-9 connects Preview and dashboard tiles to `DashboardQueryCoordinator`.

## DB-9 Integration

See `docs/DASHBOARD_DB9_INTEGRATION.md`. The dashboard host:

1. Builds catalog + type options from inventory metadata (`DiscoverTypes` / scan report).
2. Opens this editor for create/edit of Query widgets only.
3. On Save, inserts/replaces the widget in V2 and persists per `GetDatabaseId()`.
4. Uses one session `DashboardQueryCoordinator` for Preview and tiles.
5. Keeps Legacy widgets on the existing renderer/path.
