# Dashboard Grid Layout

## Purpose

DB-10 replaces Order + ColumnSpan + WrapPanel with a 12-column logical dashboard grid. Users arrange Legacy and Query widgets by dragging and resizing in Edit mode. Layout is independent from query execution.

## Logical Grid

The dashboard is a logical grid of 12 columns and an unbounded number of rows. Coordinates are integers. Pixel size is computed only at render time from the current dashboard width.

## Coordinates

Persisted fields:

- `X` — column origin, `0..11`
- `Y` — row origin, `>= 0`
- `Width` — column span, `1..12`
- `Height` — row span, `>= 1`

Invariant: `X + Width <= 12`.

## 12 Columns

`DashboardPersistenceV2.GridColumns = 12`. Column pixel width is derived from available width minus gutters. Logical column count never changes with window size.

## Widget Rect

`DashboardGridRect` is the in-memory rectangle. `DashboardWidgetLayoutDefinition` stores the same integers. `Order` and `ColumnSpan` remain on the document for V2 migration and deterministic serialization; they are not V3 layout authority.

## Validation

`DashboardGridLayoutEngine.TryValidate` rejects negative X/Y, zero size, width greater than 12, and `X + Width > 12`.

Move/resize/placement use `Clamp`:

- default min width 3, min height 2
- visualization-aware mins supplied by the caller (see below)
- max width 12, max height 24
- X is shifted left if needed so the rect stays in 12 columns

Visible widgets must not overlap after a successful layout operation. Edge-touch is not overlap. Invalid persisted V3 (including visible overlap) is rejected on load; the file is not rewritten.

## New Widget Placement

`PlaceNew` scans rows top → bottom, columns left → right (`FirstFit`) using the widget default size. Existing widgets are not moved. Compact default (`6×2`) is used for KPI / BIM / Responsible and scalar Query widgets. Other widgets default to `6×3`.

## Drag

Available only in Dashboard Edit mode (`Редактировать` → `Готово`). Drag starts from the widget header handle, after the WPF minimum drag distance. Chart/table body clicks do not drag. During move the engine previews a temporary layout; nothing is written until drop.

## Resize

Edit mode shows a bottom-right grip. Resize snaps to grid columns and rows, then uses the same clamp and collision policy as move. Visualization-aware minimums are supplied by the presenter; the engine stays generic.

## Visualization size constraints (DB-11)

Not persisted. `DashboardVisualizationConstraints` maps Query visualization + query shape to a min rect. The grid engine receives `minWidth`/`minHeight` only.

| Visualization | Min |
|---------------|-----|
| KPI / Auto scalar | 3×2 |
| Bar / HorizontalBar / Pie / Table / Auto grouped | 4×3 |
| Legacy Chart | 4×3 |
| Other Legacy | 3×2 |

Auto uses query shape (scalar vs grouped), not current row count. Resize below the min clamps. Saving a visualization that needs a larger card expands the rect, then resolves collisions (push-down).

Details: `docs/DASHBOARD_VISUALIZATION_V1.md`.

## Collision Policy

The widget being moved or resized keeps its requested valid rect. Other visible widgets are processed in `(Y, X, Id)` order and pushed **down** to the first non-overlapping Y, keeping X/Width/Height. No random left/right reflow.

## Reflow

Collision cascade is deterministic: if B is pushed onto C, C is pushed down, and so on. Unrelated widgets that never overlap stay put.

## Compaction Policy

No automatic vertical compaction after ordinary drag, resize, or delete. Empty rows may remain. An explicit “Уплотнить” action is not part of DB-10.

Migration and new-widget placement use first-fit. They do not rearrange widgets that already have V3 coordinates.

## Visibility

Hidden widgets are excluded from visible occupancy. Their rectangle stays stored. Showing a widget again keeps that rectangle, then runs collision resolution, then saves. Hidden widgets never overwrite another visible widget in place.

## Persistence V3

`SchemaVersion = 3`. Current writer version is `DashboardPersistenceV2.CurrentSchemaVersion`. V2 (`SchemaVersion = 2`) remains a supported load/migration input and is not silently redefined.

Open does not write. The first explicit mutation writes SchemaVersion 3 atomically. Corrupt / future (`> 3`) / project-mismatch files are not overwritten. Degraded dashboards disable edit, drag, resize, add, edit content, and delete.

## V2 → V3 Migration

Deterministic row-major placement from `Order` then `Id`.

- ColumnSpan `1` (half) → Width `6`
- ColumnSpan `2` (full) → Width `12`
- Compact kinds → Height `2`, else Height `3`

Hidden widgets are stacked below the visible bottom. Query and Legacy payloads are copied unchanged. Repeating the same V2 input yields the same V3 coordinates.

## V1 → V3 Migration

V1 `dashboard-layout.json` → V2 in memory → V3 in memory. The V1 file is not rewritten. Opening Analytics does not create `dashboard.json`. The first mutation writes V3.

## Transactional Save

Clone definition → apply layout → `DashboardDefinitionStore.Save` → on success publish the clone and rebuild UI from it → on failure keep the previous published definition and restore the preview. Drag/resize preview never mutates the published object.

Save runs on drag complete and resize complete, not on MouseMove.

## Responsive Rendering

`DashboardGridPanel` maps logical rects to device-independent pixels.

- Gutter: 10 DIP
- Logical row height: 112 DIP
- Minimum dashboard width: 720 DIP; narrower windows get a horizontal ScrollViewer instead of unusable columns

Row count is persisted; pixel row height is a single central render setting.

## Scrolling

Vertical extent is `max(Y + Height)` mapped to pixels. The summary ScrollViewer grows with the grid. Bottom widgets are not clipped by a fixed WrapPanel height.

## Runtime Data Independence

Move and resize:

- do not execute widget queries
- do not rematerialize TypeId datasets
- do not replace `DashboardQueryCoordinator`
- restore already-loaded Query runtime status/data on UI rebuild (no Loading flash)

## Known Limitations

- Keyboard arrow-key nudging is not implemented; drag/resize is the layout UI
- No “Уплотнить” compaction command
- Visualization-aware minimum sizes (DB-11); pixels are still not persisted
- No dashboard-level filters (later stage)
- Eight-direction resize handles are not provided (bottom-right only)
