# Dashboard DB-9 Integration

End-to-end Query widget activation: V2 persistence, coordinator session, explicit Preview, sequential TypeId loads, and renderer adapter.

Runtime validation in Pilot is **not** part of DB-9.

## End-to-End Flow

Open Analytics → Dashboard → **+ Аналитический виджет** → Query Editor V2 → Type / filters / Group By / Count / visualization → **Предпросмотр** (explicit) → Save → widget on dashboard → close/reopen → V2 restore → sequential query execute → render.

**+ Виджет** still opens the legacy editor. Legacy KPI / BIM / Responsible / Chart widgets keep their existing execute/render paths.

## Dashboard Ownership

`AnalyticsDashboardPresenter` owns:

- `DashboardDefinition` (runtime source of truth when V2 is active)
- one `DashboardFieldCatalog` per data session
- one `DashboardQueryCoordinator` per data session

Composition root: `AnalyticsWindow` + `InventoryService` (`GetDatabaseId`, `CreateTypeDatasetProvider`, `DiscoverTypes`). No MEF export, no service locator, no static singleton, no coordinator in Window code-behind.

Without runtime options the presenter stays on V1 `DashboardLayoutStore` (existing tests).

## V2 Load

On Analytics dashboard init (V2 runtime):

| Store result | Behavior |
|--------------|----------|
| Success | use V2 definition |
| Missing | load V1 via `TryLoad()` (no write); if absent, in-memory `Default()`; migrate with `DashboardDefinitionV2Migrator`; **do not write V2** |
| Corrupt / Invalid / IoFailure | degraded; legacy fallback display; **do not overwrite** |
| UnsupportedVersion | degraded; **do not overwrite** |
| ProjectMismatch | degraded; **do not overwrite** |

## Legacy Migration

In-memory only until the first explicit user mutation. Specialized widgets stay `ContentKind=Legacy`. They are not rewritten as Query widgets.

## First V2 Save

Add / edit / delete / reorder / visibility after an in-memory migration writes `%LOCALAPPDATA%\PilotBim.Analytics\Dashboards\<guid-D>\dashboard.json` atomically.

`dashboard-layout.json` is left untouched (rollback source). Opening Analytics does not write V2.

## Coordinator Session

Constructed with current snapshot + catalog + production `IDashboardTypeDatasetProvider`. Constructor does not materialize.

Scan / type-metadata refresh:

1. increment apply generation
2. dispose previous coordinator (cancels session token, no Wait)
3. new catalog + coordinator
4. sequential Query refresh

Window close: `Dispose()` coordinator. No synchronous Wait.

## Type Dataset Reuse

Coordinator cache: one materialization per TypeId per session. Widgets never call the materializer. Same TypeId, different dimensions/filters: still one provider call.

## Sequential Type Loading

Query widgets refresh in dashboard order with `await ExecuteAsync` one after another. Distinct TypeIds are not launched concurrently. Same-Type widgets after the first hit cache.

No `Task.WhenAll` over the dashboard. No eager load of every project TypeId. No Query widgets → zero complete-type materializations.

## Query Widget Runtime State

Not persisted. Not a `WidgetDataset` on disk.

| Status | UI |
|--------|----|
| Idle | waiting for session refresh |
| Loading | «Загрузка данных...» |
| Success | adapter output (KPI / chart / table) |
| Empty | «Нет данных» — no empty chart frame |
| Incomplete | «Не удалось получить полный набор данных» — **no partial numbers** |
| Unsupported | «Эта комбинация пока не поддерживается» |
| Invalid | «Конфигурация виджета недоступна» |
| Error | «Не удалось загрузить данные» (exception to `AnalyticsLogger` only) |

One widget Error does not stop the rest.

## Preview

User-initiated **Предпросмотр** only. Opening the editor does not materialize.

Preview generation counter: late Preview #1 cannot replace Preview #2. Caller cancellation is not used to abort shared TypeId loads.

Cancel editor: dashboard definition unchanged. Session dataset cache may remain.

## Save

Transactional: clone definition → `Save` → on success publish clone and rebuild; on failure keep previous runtime definition and previous file.

Edit Query: same Id, same layout. Edit Legacy: `DashboardWidgetEditorWindow`.

## Rendering

`DashboardWidgetDatasetAdapter` maps `WidgetDataset` + visualization onto existing `ChartCanvasControl` / KPI / table models. No SDK in the chart control.

Preview and dashboard use the same adapter.

**Auto (DB-11):** `DashboardVisualizationRecommendationService` — scalar → KPI; grouped 1–5 → Bar; 6–15 → HorizontalBar; ≥16 → Table. Never auto-Pie or auto-Line. Persisted type stays `Auto`. Persisted Line without an ordered dimension → Unsupported (not a fabricated axis).

Charts size from WPF layout (`ValueRatio` × available size). See `docs/DASHBOARD_VISUALIZATION_V1.md`.

## Error States

See runtime table. IncompleteData never renders a numeric chart.

## Degraded Persistence Mode

Corrupt / future / mismatch: non-modal warning, mutations disabled (add/edit/delete/move/visibility), V2 file not overwritten, legacy fallback shown when V1 can be read.

## Refresh

Rescan rebuilds catalog/type options and replaces the coordinator. Persisted FieldIds that disappeared (including rename-sensitive `attribute:{typeId}:{Name}`) stay Invalid/Unsupported — no silent remap.

## Window Close

Dispose coordinator; ignore late UI applies via apply generation.

## Performance

- Per-widget SDK scans: 0
- Same-Type materialization: 1 per session
- Different TypeId full loads: sequential
- UI thread is not blocked on materialization (`ExecuteAsync` + dispatcher posts)

## Grid lifecycle (DB-10)

Layout edits (drag / resize / hide / show / add / delete) clone the definition, run `DashboardGridLayoutEngine`, save the current schema (`SchemaVersion = 4` after DB-12) transactionally, then publish. MouseMove only updates a temporary preview.

## Dashboard filter overlay (DB-12)

Dashboard-level filters are persisted on the definition (`DashboardFilters[]`) and applied at execute time:

`DashboardEffectiveQueryBuilder.Build(baseQuery, dashboardFilters, widget, catalog)` → `DashboardQueryCoordinator.ExecuteAsync(effective)`.

The persisted widget query is not rewritten. Filter add/edit/remove invalidates only old ∪ new target Query widgets, then sequential refresh. Coordinator instance and TypeId cache stay. Widget Editor Preview remains base-query only.

Details: `docs/DASHBOARD_FILTERS_V1.md`.

Moving or resizing a widget does **not** replace `DashboardQueryCoordinator`, rematerialize TypeIds, or re-run widget queries. Query runtime status is restored across the layout rebuild.

Edit mode: **Редактировать** / **Готово**. Degraded persistence: edit/drag/resize unavailable.

Details: `docs/DASHBOARD_GRID_LAYOUT.md`.

## Known V1 Limitations

- Enum / User / Reference filter values: stable-key text (no picklists)
- Dashboard grid is DB-10 (logical 12-column layout)
- Dashboard-level filters: explicit Query bindings, TypeId + FieldId identity (DB-12). No cross-filter / drillthrough.
- Auto visualization is `DashboardVisualizationRecommendationService` (DB-11)
- Preview is explicit, not live-on-keystroke

## Runtime Validation Required

YES. Not executed in DB-9. Do not claim Pilot runtime success until DB-R2.
