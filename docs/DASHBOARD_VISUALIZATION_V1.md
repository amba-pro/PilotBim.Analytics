# Dashboard Visualization V1

DB-11 polish for Query widget visualizations. Query semantics, filters, coordinator, and Pilot SDK are unchanged.

## Architecture

Pipeline:

Query → `WidgetDataset` → `DashboardWidgetDatasetAdapter` → renderer (`ChartCanvasControl` / KPI / Table)

Visualization consumes only:

- `WidgetDataset`
- visualization type (persisted, including `Auto`)
- available card size from WPF layout (`ActualWidth` / `ActualHeight`)

It does **not** know Pilot SDK, TypeId materialization, `DashboardQueryCoordinator`, widget query execution, filters, or inventory scanning.

Available size is rendering context. It is not part of the query definition and is not persisted.

Preview and dashboard cards call the same adapter.

## Dataset Contract

`WidgetDataRow`:

- `Key` — machine identity
- `Label` — display text from the query layer (`""` when missing)
- `Value` — `long` Count

The renderer never merges rows that share a display label. Distinct keys stay distinct bars, slices, and table rows. Dataset order is preserved (no renderer sort, no hidden TopN).

## Visualization Types

Persisted `Visualization.Type`:

| Type | Query path |
|------|------------|
| Auto | resolved at runtime; stored value stays Auto |
| Kpi | scalar Count |
| Bar | grouped Count |
| HorizontalBar | grouped Count |
| Pie | grouped Count, explicit only |
| Table | scalar or grouped |
| Line | invalid on TypeId V1 query path |

Legacy Line charts (Charts tab / Chart Builder / Legacy dashboard widgets) are unchanged.

## Auto Recommendation

`DashboardVisualizationRecommendationService` is deterministic and unit-tested. No AI. No localized field-name heuristics.

| Shape | Rule |
|-------|------|
| Scalar (no dimension) | KPI |
| Grouped, 1–5 rows | Bar |
| Grouped, 6–15 rows | HorizontalBar |
| Grouped, ≥16 rows | Table |
| Pie | never auto-selected |
| Line | never auto-selected |

Thresholds follow V1 conservative defaults: Horizontal Bar fits Pilot names once the category set is no longer tiny; Table is the fallback for large sets. Pie is useful only as an explicit part-to-whole choice.

`Visualization.Type = Auto` remains Auto on disk. A later refresh may resolve to a different concrete type if the row count changes.

Editor Preview may show `Авто → …` for the current dataset. That string is not persisted.

## Compatibility

`DashboardVisualizationCompatibility`:

- Scalar: Auto, Kpi, Table
- Grouped: Auto, Bar, HorizontalBar, Pie, Table
- Line: invalid for TypeId Query widgets
- Grouped + KPI: invalid (even one row)
- Scalar + Bar / HorizontalBar / Pie: invalid

Incompatible explicit types render as Invalid / Unsupported. Auto is always allowed and then resolved.

## KPI

Query KPI is a single dominant value in a Viewbox (scales down in small cards, does not grow past the designed size). Card title stays in the header. There is no fake category label.

Display formatting uses `ru-RU` `N0` (for example `1 234`). Count `0` after a successful scalar query shows `0`, not «Нет данных». Empty / Error / Incomplete remain distinct runtime states.

## Vertical Bar

Bars fill the measured plot area. Baseline is 0 (Count is non-negative). Zero values occupy no fill. Category labels sit on the axis, ellipsis when needed, full text in the tooltip. Dataset labels are not truncated.

## Horizontal Bar

Preferred Auto choice for 6–15 categories. Labels on the left, values on the right, bars fill remaining width. If the card is shorter than the row list, the chart scrolls. Rows are not dropped.

## Pie

Explicit user choice. Circle uses `min(available width, height)` (no fixed 420×220). Legend is a side column when the card is wide enough, otherwise wrapped. Tooltips show full label, formatted count, and share.

Grouped Count is a valid part-to-whole: category counts sum to matched rows. Scalar Pie is invalid.

All-zero series: «Все значения равны 0» (no divide-by-zero). One positive category: 100% slice.

More than 8 slices: still render, plus warning «Слишком много категорий для круговой диаграммы». Persisted Pie is not rewritten to another type.

## Table

Uses dataset order. Scrolls with the card. No extra TopN.

- Grouped: Категория \| Количество
- Scalar: Показатель \| Значение

## Line Limitation

TypeId Query V1 has no ordered time dimension. Line is not offered in the Query editor and is Unsupported if persisted. Legacy Line charts keep their existing path.

## Responsive Rendering

`ChartCanvasControl` redraws on `SizeChanged` / `Loaded` using the ScrollViewer viewport (fallback: `ActualWidth` / `ActualHeight`). No persisted pixel sizes.

`ChartSeriesPoint.ValueRatio` (0..1) is the dashboard geometry input. `ChartDataService` still fills `BarWidth`/`ColumnHeight` from 420/220 for the **Charts tab** list bars (`Width="{Binding BarWidth}"` is actual WPF pixels). Dashboard Query charts must not treat 420×220 as card size.

Resize does not execute queries, rematerialize TypeIds, or replace the coordinator.

## Widget Size Constraints

`DashboardVisualizationConstraints` (not persisted). The grid engine stays generic; the presenter supplies min width/height.

| Visualization | Min (columns × rows) |
|---------------|----------------------|
| KPI | 3×2 |
| Bar / HorizontalBar / Pie / Table | 4×3 |
| Auto scalar | 3×2 (query shape, not row count) |
| Auto grouped | 4×3 |
| Legacy Chart | 4×3 |
| Other Legacy | 3×2 |

Resize below the minimum clamps. Changing visualization on save expands the rect if needed, then runs the existing push-down collision resolver.

## Missing Values

Machine `Label` stays `""`. Visualization display uses resource «Не задано». The placeholder is not written back into query data.

## Tooltips

Charts: full display label + formatted value. No FieldId, TypeId, or StableKey.

## Empty State

Grouped query with zero rows: runtime Empty («Нет данных»). Not a blank canvas. Successful Count = 0 is KPI `0`.

## Loading State

«Загрузка данных...» fills the card. Previous chart/KPI/table is not shown (`IsChart`/`IsKpi`/`ShowTable` false). Overlay sits above content.

## Incomplete State

IncompleteData: warning text only. No numeric visualization (DB-9).

## Error States

Unsupported / Invalid / Error use short resource strings. No exception dump. Optional editor recovery is not added in DB-11.

## Preview Parity

`DashboardQueryWidgetEditorViewModel` and `AnalyticsDashboardPresenter` both call `DashboardWidgetDatasetAdapter.TryRender` with the same dataset + visualization + hasDimension.

## Legacy Compatibility

Legacy KPI (DataGrid), BIM, Responsible, Chart, StateSemantic, IfcTypes, BimModels, Remarks, and Charts-tab 420px bars are unchanged. Shared `ChartCanvasControl` now sizes from layout; Legacy `BuildSeries` still supplies ValueRatio plus the old pixel fields.

## TD-08

Stage 9: Chart 420/220 duplicated between `ChartDataService` and `ChartCanvasControl` (layout drift).

After DB-10 the dashboard card is variable-size; scaling `BarWidth/420` still assumed a 420×220 canvas.

DB-11: dashboard charts use `ValueRatio × available size`. 420/220 remain only as:

- Charts-tab `ChartBarRow.BarWidth` pixel contract
- fallback if `ValueRatio` is 0 but a legacy pixel field is set
- unrelated window/column widths (editor 420, DataGrid 220)

**Status: FIXED_IN_DB_11** for dashboard Query/Legacy `ChartCanvasControl` geometry.

## Known Limitations

- No dashboard-level filters (DB-12)
- No cross-filter / drillthrough
- No Line Auto (no time dimension)
- Pie is not Auto
- No interactive table sort
- No screenshot harness; visual review is source-level plus optional WPF run
- Charts-tab list bars remain fixed 420px by design
