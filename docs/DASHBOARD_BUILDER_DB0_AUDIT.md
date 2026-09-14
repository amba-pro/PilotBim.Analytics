# Dashboard Builder DB-0 Audit

Date: 2026-09-14  
Track: **DASHBOARD BUILDER** (not refactor Stage 11)  
Scope: **AUDIT ONLY** — no production implementation

## Baseline

| Item | Value |
|------|-------|
| HEAD | `3401ec8` — Stage 10: finalize runtime readiness review |
| Branch | `main` |
| origin/main | **in sync** (ahead/behind 0 at audit start) |
| Working tree | **clean** |
| Build | PASS — 0 errors / 0 warnings |
| Tests | **138 PASS**, 0 failed, 0 skipped |
| Plugin | `PilotBim.Analytics.ext2` 0.9.0 / `0.9.0-rich-diff-to-dashboard` |
| Framework | .NET Framework 4.7.2, WPF, MEF, SDK `Private=false` |
| Prior verdict | READY_FOR_PILOT_RUNTIME_VALIDATION |

## Product Goal

No-code dashboard constructor inside Pilot-BIM:

create dashboard → add/place/resize widgets → choose **scope (set of objects)** → filter → dimensions → measures → visualization (manual or recommended) → persist → later dashboard filters / drill / cross-filter.

Conceptual pipeline (target):

```
Pilot SDK
  → project / analytics data snapshot
  → field catalog / semantic layer
  → widget query
  → filters → aggregation
  → widget dataset
  → visualization renderer
```

Renderer must receive normalized Category|Value rows and must **not** contain Pilot SDK query logic.

Domain principle: analyze a **SCOPE / SET** of Pilot objects and their fields — not “one object → one chart”.

## Current Dashboard Architecture

Today’s “dashboard builder” is a **layout + fixed-source chart picker** over `ProjectAnalyticsSnapshot` aggregates. It is **not** a Metabase/Grafana-style query builder.

### Inventory table

| File | Class | Responsibility | LOC (approx) | State owned | Persistence | Pilot SDK? | Reusable? | Decision |
|------|-------|----------------|-------------:|-------------|-------------|------------|-----------|----------|
| `ViewModels/AnalyticsDashboardPresenter.cs` | `AnalyticsDashboardPresenter` | Layout CRUD, visibility, reorder, rebuild widget content | 241 | layout + OCs | via store | No | Yes as shell | **EXTEND** |
| `ViewModels/DashboardSectionVm.cs` | `DashboardWidgetVm`, `DashboardLayoutItemVm` | UI VM for widgets / builder list | 181 | display rows/series | No | No | Yes | **EXTEND** |
| `Services/DashboardLayoutStore.cs` | `DashboardLayoutStore` | Load/save/normalize layout JSON | 233 | file | **Yes** | No | Yes (schema evolve) | **EXTEND** |
| `Models/DashboardLayoutModels.cs` | `DashboardLayoutState`, `DashboardWidgetState`, kinds/ids | Persisted layout schema | 59 | — | schema | No | Partial | **EXTEND** |
| `Services/ChartDataService.cs` | `ChartDataService` | Snapshot/ifc → Category|Value series | 175 | none | No | No | Yes as adapter | **ADAPT** |
| `Models/ChartModels.cs` | enums + DTOs | Chart kinds/sources/points | 58 | — | Id strings | No | Partial | **EXTEND** |
| `Views/ChartCanvasControl.xaml(.cs)` | `ChartCanvasControl` | Renders `ChartSeriesPoint` | 343 | none | No | No | Yes | **EXTEND** |
| `Views/DashboardWidgetEditorWindow.*` | editor dialog | Title/kind/source/kind/topN/span | 146 + XAML | dialog | No | No | Evolve | **REWORK_INCREMENTALLY** |
| `Views/AnalyticsWindow.xaml(.cs)` | host UI | Dashboard panel, add/edit/remove/move | large | window | No | Indirect | Keep host | **KEEP** |
| `ViewModels/AnalyticsWindowViewModel.cs` | root VM | Chart option lists; façade to presenter; snapshot | 565 | snapshot/filter | No | No | Keep façade | **KEEP** / thin |
| `Services/ProjectAnalyticsService.cs` | snapshot builder | Report → `ProjectAnalyticsSnapshot` | 503 | none | No | No | Shared source | **KEEP** / feed |
| `Services/InventoryService.cs` | scan façade | Full inventory scan | ~370 | buffers | No | **Yes** | Data producer | **KEEP** |
| `Models/AnalyticsModels.cs` | `ProjectAnalyticsSnapshot` + rows | Aggregate analytics DTO | — | snapshot | scan JSON | No | V1 source | **EXTEND** later |
| `Models/InventoryModels.cs` | `ProjectInventoryReport` + attrs | Rich scan report | — | report | No (in-mem) | No | Catalog seed | **EXTEND** |

## Current User Flow

```
Open Analytics (MEF toolbar/menu)
  → VM ctor → AnalyticsDashboardPresenter.InitLayout
  → DashboardLayoutStore.LoadOrDefault()  (%LOCALAPPDATA%\PilotBim.Analytics\dashboard-layout.json)
  → Normalize (legacy Blocks→Widgets; ensure kpi/bim/responsible builtins)
  → RebuildUi → visible widgets
  → (empty ChartSeries until Snapshot set)

User runs scan (Refresh)
  → InventoryService.Run → ProjectAnalyticsService.Build → Snapshot
  → RebuildDashboardWidgets → ChartDataService.BuildSeries / KPI rows

Add widget
  → DashboardWidgetEditorWindow (modal)
  → pick WidgetKind + (if Chart) ChartSource / ChartKind / TopN / ColumnSpan
  → presenter.AddWidget → Save → RebuildUi

Edit / remove / ▲▼ / visibility checkbox
  → mutate layout → Save → RebuildUi
```

### What user can do today

- Show/hide builtins: KPI, BIM summary, Responsible top-10.
- Add chart widgets from a **fixed** list of 9 sources × 4 chart kinds × TopN.
- Reorder widgets; half/full width for charts only (`ColumnSpan` 1|2).
- Persist layout across sessions.

### Hardcoded vs generic

| Aspect | Reality |
|--------|---------|
| Chart “sources” | Fixed `AnalyticsChartSource` enum + EN Id strings (`Types`, `Creators`, …) |
| KPI/BIM/Responsible | Single-instance builtins by fixed Ids `kpi`/`bim`/`responsible` |
| Layout | Vertical order + WrapPanel; **not** X/Y grid |
| Data | Pre-aggregated snapshot collections — **not** ad-hoc GroupBy |
| Appears generic | Editor combo boxes — but options are closed sets |

## Existing Widget Model

Persisted `DashboardWidgetState` (`DashboardLayoutModels.cs`):

| Property | Role | Classification |
|----------|------|----------------|
| `Id` | Widget identity (`kpi`/`bim`/`responsible` or `chart-{guid:N}`) | **SAFE_STABLE_ID** / **CONTRACT** |
| `Title` | Display title (RU defaults) | **DISPLAY_ONLY** (persisted — dual-use if localized later) |
| `WidgetKind` | `Kpi` \| `Bim` \| `Responsible` \| `Chart` | **CONTRACT** |
| `IsVisible` | Visibility | **CONTRACT** |
| `Order` | Vertical order index | **CONTRACT** |
| `ColumnSpan` | 1 = half (~420px), 2 = full | **CONTRACT** (not grid cells) |
| `ChartSource` | Enum name string (`Types`, …) | **CONTRACT** (stable EN) |
| `ChartKind` | Enum name string (`HorizontalBar`, …) | **CONTRACT** |
| `TopN` | Truncation; 0 = all | **CONTRACT** |

`DashboardWidgetKinds` / `DashboardBlockIds` / `AnalyticsChartSource` / `AnalyticsChartKind`: **CONTRACT**.

RU titles in defaults (`"KPI / сводка"`, `"Ответственные"`): **DISPLAY_ONLY** today; risk if used as match keys later → **DUAL_USE_RISK** (already known for ScanDiff elsewhere).

**No** Scope, Filters, Dimensions, Measures, Query, SchemaVersion on widget today.

## Existing Persistence

| Item | Value |
|------|-------|
| Path | `%LOCALAPPDATA%\PilotBim.Analytics\dashboard-layout.json` |
| Serializer | `DataContractJsonSerializer` |
| Schema version field | **None** |
| Atomic write | `.tmp` then replace |
| Corrupt / missing | catch → `Default()` + Save |
| Legacy | `Blocks[]` migrated into `Widgets[]` by `Normalize`; Blocks cleared on save |
| Multiple dashboards | **No** — single file, single layout |
| Migration support | Ad-hoc Normalize only; no versioned migrations |

## Existing Layout

| Capability | Today |
|------------|-------|
| Ordering | `Order` int + ▲▼ |
| Dimensions | `ColumnSpan` 1\|2 only |
| Move | Reorder only |
| Resize | Not free resize; half/full width |
| Grid X/Y/W/H | **Absent** |
| UI panel | `WrapPanel` + MinWidth 520 / Width 420 when half |
| Evolve to 12-col grid | Possible by **adding** optional `X,Y,Width,Height` with defaults derived from `Order`+`ColumnSpan`; keep old fields for round-trip. **Migration risk: MEDIUM** if old clients ignore new fields (safe) or new clients require them without defaults (unsafe). |

Do **not** change persistence in DB-0.

## Existing Chart Pipeline

Input to charts: **`ProjectAnalyticsSnapshot`** (+ optional IFC rows / BIM model filter), **not** raw Pilot objects.

`ChartDataService.BuildSeries` → `List<ChartSeriesPoint>` (Label, Value, geometry precompute with **420/220** constants — TD-08).

`ChartCanvasControl` redraws using ActualWidth/Height but still scales via `BarWidth/420` and `ColumnHeight/220` — **PARTIAL** coupling to fixed geometry (resize of control works; constants are relative units).

### Three widget traces

1. **Chart Types / HorizontalBar**  
   `Snapshot.ObjectsByType` → ExtractRaw → ToSeries → `ChartCanvasControl.DrawHorizontal`.

2. **Chart StateSemantic / Pie**  
   `Snapshot.ObjectsByUserStateSemantic` → series → `DrawPie`.

3. **Builtin Responsible**  
   `Snapshot.ObjectsByResponsible.Take(10)` → `AnalyticsKpiRow` Label/Value/Detail → DataGrid (not ChartCanvas).

Renderer never calls Pilot SDK. **KEEP** this separation.

## Existing Data Snapshot

### `ProjectAnalyticsSnapshot` (dashboard feed)

Pre-aggregated lists: Summary KPIs, ObjectsByType/Creator/CreatedMonth/UserState/StateSemantic/Responsible, DocumentVersions, Bim*, DataQuality, Remarks, RemarkLinks, ScanDiffRows, Limitations.

**No** object-level row store. **No** arbitrary attribute values for GroupBy.

### `ProjectInventoryReport` (scan producer)

Types + Attributes metadata, States, Persons, Orgs, sample buffers, creator/responsible **counts** (sample or budgeted search), BIM analytics, remark samples/links, diagnostics.

Object-level samples exist only in capped buffers (`DocumentSamples`, history/system field samples, remark links) — **not** a full row warehouse.

### Field / concept matrix (code evidence only)

| Field / concept | In Report? | In Snapshot? | Stable ID? | Type known? | Filter? | Group? | Needs live SDK? |
|-----------------|------------|--------------|------------|-------------|---------|--------|------------------|
| ObjectId | samples only | DocumentVersions / RemarkLinks | Guid | Guid | No (agg) | No | for full set: yes |
| ParentId | system field example | No | Guid | Guid | No | No | yes for tree |
| TypeId / TypeName | Types | ObjectsByType (TypeId+Name) | TypeId int | int/string | via enum source | via Types source | metadata: no |
| Created / CreatedMonth | counts | ObjectsByCreatedMonth | period string `yyyy-MM` | date bucket | No | Yes (fixed) | no for current agg |
| Creator | Creator*Counts | ObjectsByCreator (CreatorId) | CreatorId int | int | No | Yes (fixed) | no for current |
| State | ObservedStateCounts / States | UserState + Semantic | StateId Guid | Guid | No | Yes (fixed) | no for current |
| Responsible | ResponsibleSampleCounts | ObjectsByResponsible (OrgUnitId/PersonId) | org/person ids | int | No | Yes (fixed) | no for current |
| Attributes (arbitrary) | AllAttributes metadata + fill stats | DataQuality (fill only) | `attr.Name` as AttributeId | ValueType string | **No values** | **No** | values need load |
| BIM model | BimModelAnalytics | same | ModelId Guid | Guid | UI filter only | BimModels source | index path |
| BIM part | BimPartAnalytics | same | Part id | — | No | No | index path |
| Remark type | Types + RemarkAnalytics | ModelRemarks | TypeId | int | No | Remarks source | no for counts |
| GlobalId / bimObjectId | RemarkLinks / index | RemarkLinks | string | string | No | No | sampling |

## Pilot Metadata Capability

Evidence: `TypeDiscoveryService`, `AttributeDiscoveryService`, `PilotSdkDiscoveryService`, `IObjectsRepository.GetTypes()`.

| Question | Status | Evidence |
|----------|--------|----------|
| Enumerate object types? | **CONFIRMED** | `_repository.GetTypes()` → TypeId, Name, Title, Kind, IsService, … |
| Enumerate attributes per type? | **CONFIRMED** | `type.Attributes` → `BuildAttributeShells` |
| Stable attribute ID? | **CONFIRMED** (as used) | `AttributeId = attr.Name` (Pilot attribute name, not Title) |
| Display name? | **CONFIRMED** | `attr.Title` fallback Name |
| Data type? | **CONFIRMED** | `attr.Type.ToString()` → `AttributeType` enum |
| Allowed enum values? | **PARTIAL** | `Configuration2(attr)` truncated into ConfigurationSummary; full enum parse **SDK_CAPABILITY_TO_VERIFY** / incomplete |
| User/person/reference attrs? | **PARTIAL** | `AttributeType.OrgUnit` / `UserState` mapped; OrgUnit value shape **NeedsRuntime** |
| System vs custom fields? | **PARTIAL** | `attr.IsService`; system fields via static matrix + `IDataObject` props (Id, ParentId, Creator, Created, …) |

## Scope Capability

| Scope | Availability | Existing code | Performance | Correctness | V1? |
|-------|--------------|---------------|-------------|-------------|-----|
| CurrentProject | **CONFIRMED** (implicit) | Full `InventoryService.Run` / search / hierarchy walk from root | Mode budgets (Fast/Std/Full) | Project-wide only | **YES** (only V1 scope) |
| CurrentSelectedObject | **NOT_IMPLEMENTED** | `ObjectsViewContext` passed to toolbar/menu but **never read** for selection | — | — | No until verified |
| ObjectAndDescendants | **PARTIAL** | `LoadChildren` / `HierarchyWalkSampler` BFS; not dashboard-scoped | Budgeted walk | Truncation | Later |
| ObjectType | **PARTIAL** | Search/sample by TypeId | Per-type search | Good for typed widgets | V1.5 |
| BimModel | **PARTIAL** | BIM filter on IFC types UI; snapshot BimModelAnalytics | Index budgets | Filter not in layout persist | Later |
| BimModelPart | **PARTIAL** | Part analytics rows | Index | No widget scope | Later |

Selection / open-object navigation: **SDK_CAPABILITY_TO_VERIFY** (context type exists; no usage proven).

## Field Catalog Feasibility

**Feasible for V1 catalog** from existing scan metadata without new SDK surface:

- System fields: Ids from `PilotSdkDiscoveryService` matrix (`ObjectId`, `TypeId`, `ParentId`, `CreatorId`, `CreatedDate`, …) with CanGroup/CanFilter flags derived carefully.
- Attributes: `AttributeInventoryRecord` → `attribute:{TypeId}:{Name}` or `attribute:{Name}` if names unique enough — **prefer `attribute:{typeId}:{name}`** because names can repeat across types.
- DisplayName = Title; Id never = Title.

Enum domain discovery incomplete → CanFilter Equals on UserState/OrgUnit **PARTIAL**.

## Query Engine Options

| Option | Fit |
|--------|-----|
| A. Query `ProjectAnalyticsSnapshot` | Matches **today’s** widgets; closed source set |
| B. Extend Snapshot with generic fields | Tempting; risks bloating persisted scan JSON / dual purpose |
| C. Normalized `DashboardObjectRow[]` from scan | Required for arbitrary GroupBy/Filter; not present |
| D. SDK per widget | **Reject** — N scans, races, violates invariant |
| E. Hybrid Snapshot + ObjectRows | Best product fit |

## Recommended Data Strategy

**E — Hybrid**

1. **Keep** one scan → `ProjectInventoryReport` → `ProjectAnalyticsSnapshot` as the shared refresh unit (invariant: **one dashboard refresh ≠ one scan per widget**).
2. **V1 widgets** continue to bind known aggregate sources via Snapshot (current ChartDataService path) while introducing query contracts that *describe* those sources.
3. **Introduce ObjectRows** (system fields + selected attributes) as a second in-memory product of the **same** scan when generic GroupBy is needed — not a second SDK pass.
4. **Never** D.

Why not A alone long-term: cannot GroupBy arbitrary attributes.  
Why not C alone now: ObjectRows do not exist yet; forcing them before contracts would be a large scan change.  
Why E: preserves current UX while opening the semantic pipeline.

## V1 Query Semantics (conceptual — not implemented)

```
WidgetQueryDefinition
  Scope: CurrentProject          // only CONFIRMED V1
  EntityType: optional TypeId?   // null = mixed/all as today
  Filters[]: limited             // see below
  Dimensions[]: 0..1 for V1
  Measures[]: Count (DistinctCount later if cheap)
  Sort: ValueDesc | LabelAsc
  Limit: TopN (0 = all)
```

### Filters possible with **current** data

| Operator family | Possible now? |
|-----------------|---------------|
| On aggregate chart sources | Only by choosing a different source / BIM model filter — **not** first-class Filters[] |
| On ObjectRows (future) | Equals/NotEquals/IsEmpty on TypeId, CreatorId, StateId, Responsible ids; date Before/After on Created; attribute Equals if value retained |
| Contains on text | Future ObjectRows only |
| Number Between | Future numeric attributes only |

V1 recommendation: ship query **contracts** + Snapshot-backed execution for known dimensions; Filters[] on ObjectRows in a later DB stage.

## Field Type System

Proposed `DashboardFieldType` mapped from known Pilot/`AttributeType` usage:

| DashboardFieldType | Mapping evidence |
|--------------------|------------------|
| Text | String attributes / names |
| Integer | Integer attrs; TypeId; CreatorId; OrgUnit ids |
| Number | Double/Decimal if present in AttributeType |
| Boolean | Boolean AttributeType |
| DateTime | `IDataObject.Created`; date attrs if typed |
| Enum / UserState | `AttributeType.UserState` |
| User / OrgUnit | `AttributeType.OrgUnit` → person/org resolution |
| Guid | ObjectId, StateId, ModelId |
| Unknown | degrade: show as Text; CanAggregate=false |

Unsupported → safe degrade (exclude from GroupBy or treat as Text without Sum).

## Visualization Capability

| Viz | Status | Notes |
|-----|--------|-------|
| KPI (table of Label/Value/Detail) | **SUPPORTED** | DataGrid path |
| HorizontalBar | **SUPPORTED** | |
| VerticalBar | **SUPPORTED** | |
| Pie | **SUPPORTED** | |
| Line | **SUPPORTED** | |
| Table (generic Category|Value) | **PARTIAL** | KPI grid only; no dedicated table viz for series |
| Recommended viz | **MISSING** | Manual ChartKind only |

TD-08 420/220: does **not** block control resize; blocks clean absolute geometry. Fix later with DB visualization stage — **not DB-0**.

## Widget Editor UX

Current: modal dialog — Kind, Title, Source, ChartKind, TopN, Span.

Verdict: **REWORK_INCREMENTALLY**

- Keep modal or migrate to right-side panel later without ripping host.
- Evolve sections: DATA (scope/entity/filters/measure/group) → VISUAL (recommended + override) → INTERACTION (later).
- Do not REPLACE in first DB stages.

## Visualization Recommendation Rules (V1 design)

Deterministic, unit-testable (no AI):

| Shape | Recommend |
|-------|-----------|
| 0 dimensions + Count | KPI |
| 1 categorical dim + Count, n ≤ 8 | Pie optional; default **Bar** |
| 1 categorical dim + Count, n ≤ 20 | HorizontalBar |
| 1 categorical dim + Count, n > 20 | HorizontalBar + TopN default 12; else Table |
| 1 date dim + Count | Line |
| 2+ dimensions | Table (V1 unsupported chart) |
| Unknown | Table / HorizontalBar fallback |

Owner: future `VisualizationRecommendationService` (stateless pure function).

## Dashboard Filters

Future: `DashboardFilterContext` applied at query execution — **not** copied into each widget persistence.

Widgets may opt-in `ParticipatesInDashboardFilters` later.

No implementation in early DB stages until ObjectRows or query engine exists.

## Drill / Pilot Navigation Capability

| Action | Status |
|--------|--------|
| Obtain matching ObjectIds from query | **NOT_IMPLEMENTED** (need ObjectRows or search) |
| Open object card | **SDK_CAPABILITY_TO_VERIFY** |
| Select object in Pilot | **SDK_CAPABILITY_TO_VERIFY** (`ObjectsViewContext` unused) |
| Navigate project tree | **PARTIAL** (Inventory tree UI only) |
| Open BIM model/object | **SDK_CAPABILITY_TO_VERIFY** |

## Performance

| Observation | Evidence |
|-------------|----------|
| Scan budgets | Hierarchy visit Fast 2k / Std 25k / Full larger; creator budget 500 / 5k / 50k; sample limits via `ScanMode` enum values |
| Object-level data discarded | Most objects sampled then only aggregates retained |
| ObjectRows memory | Unknown exact; would scale with retained rows × fields — **no invented numbers**; start with system fields + capped attribute set |
| N+1 risk | Per-widget SDK query = **forbidden** |
| **Invariant** | **One dashboard refresh must not cause one full project scan per widget** |

## Caching

| Cache | V1 |
|-------|-----|
| Field catalog | **YES** — key: project identity if available / scan GeneratedAt; invalidate on new scan |
| Snapshot | **Already** held on VM after scan; invalidate on Refresh |
| Widget query result | **NOT_NEEDED_V1** if rebuild is cheap over in-memory aggregates; add when ObjectRows large |

## Persistence V2 (design only)

```
DashboardDefinition
  SchemaVersion: int
  Id, Title
  GlobalFilters[]
  Widgets[]: WidgetDefinition
    Id, Title
    Layout: { Order | X,Y,W,H, ColumnSpan }
    Query: WidgetQueryDefinition  // stable field IDs only
    Visualization: { Kind, Options }
```

Migration from current `dashboard-layout.json`:

1. Add SchemaVersion=1 implicitly for current file.
2. Map ChartSource → Query Dimension/Measure presets.
3. Keep reading old widgets without Query by adapter.

**Never** persist `"Ответственный"` as groupBy identity.

Multiple dashboards: recommend **V1.5 / V2** (folder of definitions or index file). V1 stays single dashboard to avoid UX/persistence blast radius.

## Security

- All data via current MEF-imported `IObjectsRepository` / search — **user-visible Pilot ACL only**.
- LocalAppData JSON must not be treated as cross-user safe; path is per Windows profile.
- Do not cache another project’s rows across root changes without invalidation (tie cache to scan timestamp / root id when available).

## Test Strategy (future)

| Area | Class |
|------|-------|
| FieldCatalog build from fake Type/Attribute records | **PURE_UNIT** |
| Query Count/GroupBy/Sort/TopN/null bucket | **PURE_UNIT** |
| Filter operators | **PURE_UNIT** |
| Viz recommendation table | **PURE_UNIT** |
| Persistence round-trip + migration | **PURE_UNIT** |
| Stable Id ≠ DisplayName | **PURE_UNIT** |
| Snapshot adapter (ChartSource → dataset) | **PURE_UNIT** |
| Scope CurrentSelection | **SDK_FAKE_REQUIRED** / **PILOT_RUNTIME_REQUIRED** |
| Drill open object | **PILOT_RUNTIME_REQUIRED** |

## Reuse Matrix

| Component | Decision | Reason |
|-----------|----------|--------|
| AnalyticsDashboardPresenter | **EXTEND** | Good CRUD/persist orchestration shell |
| DashboardLayoutStore | **EXTEND** | Keep path; versioned schema later |
| ChartDataService | **ADAPT** | Becomes Snapshot→Dataset adapter / one backend of query engine |
| ChartCanvasControl | **EXTEND** | Keep Category\|Value renderer |
| DashboardWidgetEditorWindow | **REWORK_INCREMENTALLY** | Expand DATA/VISUAL sections |
| ProjectAnalyticsSnapshot | **KEEP** (+ later companion ObjectRows) | Current shared aggregate source |
| ProjectAnalyticsService | **KEEP** | Snapshot builder |
| InventoryService | **KEEP** | Single scan owner |
| ProjectInventoryReport | **EXTEND** | Seed field catalog; later ObjectRows extraction |
| AnalyticsWindowViewModel | **KEEP** | Host façade; avoid fat query logic here |

## Target Architecture

```
Pilot SDK (IObjectsRepository / ISearchService / BIM index)
        │
        ▼
InventoryService.Run  (ONE scan per refresh)
        │
        ├── ProjectInventoryReport
        │         │
        │         ├── PilotFieldCatalog  (metadata)
        │         └── [future] ObjectRows (normalized)
        │
        └── ProjectAnalyticsService
                  │
                  ▼
         ProjectAnalyticsSnapshot  (aggregates)
                  │
        ┌─────────┴─────────┐
        │ WidgetQueryEngine │  ← WidgetQueryDefinition + optional DashboardFilterContext
        └─────────┬─────────┘
                  ▼
            WidgetDataset (Category|Value|… ; optional ObjectIds)
                  │
     ┌────────────┼────────────┐
     KPI/Table   ChartCanvas   [future interactions]
```

Widgets / renderers **do not** call Pilot SDK.

## Risks

| Level | Risk |
|-------|------|
| **HIGH** | Implementing per-widget SDK scans (perf + race regressions after Stage 4/9) |
| **HIGH** | Persisting RU display strings as field/query identity (dual-use debt) |
| **MEDIUM** | Big-bang ObjectRows without budgets → memory / scan time |
| **MEDIUM** | Layout migration to X/Y/W/H breaking existing `dashboard-layout.json` |
| **MEDIUM** | Assuming selection/drill APIs without verification |
| **LOW** | TD-08 geometry constants until resize-heavy grid |
| **LOW** | Single-dashboard limit for V1 |

## DB Roadmap

Adjusted for actual code (safer than jumping to grid/drill):

| Stage | Goal | Expected files | Tests | Behavior | Runtime? | Depends |
|-------|------|----------------|-------|----------|----------|---------|
| **DB-1** | Field catalog + stable IDs from existing report metadata | new catalog models/builder; tests | PURE_UNIT | **none** visible | No | — |
| **DB-2** | Query contracts + Snapshot-backed engine for **existing** chart sources (Count/GroupBy presets) | query models; engine; ChartDataService adapter | PURE_UNIT | optional internal only / feature flag | No | DB-1 |
| **DB-3** | Editor DATA/VISUAL wires to query contracts; live preview from Snapshot | editor + presenter | PURE_UNIT + light UI | additive UI | Optional | DB-2 |
| **DB-4** | ObjectRows extraction (system fields) from same scan; generic GroupBy Count | sampling/coordinator touch; row model | PURE_UNIT | richer widgets | **Yes** | DB-2 |
| **DB-5** | Grid layout X/Y/W/H + migration | layout models/store/XAML | PURE_UNIT | layout UX | Optional | DB-3 |
| **DB-6** | Viz recommendation + table viz | recommendation service; canvas/table | PURE_UNIT | better defaults | No | DB-2 |
| **DB-7** | Dashboard global filters | filter context + engine | PURE_UNIT | filter UX | Optional | DB-4 |
| **DB-8** | Scope picker (type / subtree) after SDK verify | scope + inventory reuse | mixed | scope UX | **Yes** | DB-4 |
| **DB-9** | Drill-to-object / selection | SDK verify first | runtime | navigation | **Yes** | DB-4 |
| **DB-10** | Cross-filter + templates / card library / multi-dashboard | defs store | mixed | product polish | **Yes** | DB-7+ |

## Recommended DB-1

### Title

**RECOMMENDED_DB_1 — Semantic field catalog from existing inventory metadata**

### Goal

Introduce an internal `PilotFieldCatalog` / `FieldDescriptor` model with **stable Ids** and display names, built from data already produced by type/attribute discovery and the system-field matrix — without UI, without persistence changes, without query execution, without scan behavior change.

### Why first

Every later query/persistence step depends on Id ≠ Title. Building the catalog first prevents baking `ChartSource` display titles or RU strings into a new contract. It is small, pure-unit-testable, and does not destabilize Stage 0–10.

### Exact scope

- Add descriptors for:
  - confirmed system fields (from existing matrix / `IDataObject` usage),
  - attributes from `AttributeInventoryRecord` / type shells (`AttributeId = attr.Name`, type-scoped id),
  - flags: CanFilter / CanGroup / CanAggregate / CanSort as **conservative** booleans (unknown → false).
- Builder input: `ProjectInventoryReport` **or** pure lists of types/attributes in tests (no Pilot runtime).
- Document Id scheme: e.g. `system:typeId`, `system:creatorId`, `system:createdMonth`, `attribute:{typeId}:{name}`.

### Expected files

- `src/PilotBim.Analytics/Models/DashboardFieldModels.cs` (new)
- `src/PilotBim.Analytics/Services/PilotFieldCatalogBuilder.cs` (new) **or** under `Discovery/`
- `tests/PilotBim.Analytics.Tests/PilotFieldCatalogBuilderTests.cs` (new)
- Optional short note in this audit / DB-1 result doc

### Tests

- Stable Id ≠ DisplayName
- Attribute uses Name not Title as Id
- System fields present with expected ids
- Service attributes can be marked / skipped per rules
- Empty report → empty/minimal catalog without throw

### What NOT to change

- No XAML / editor / layout JSON
- No ChartDataService behavior
- No InventoryService scan pipeline
- No MEF / public API / SDK refs
- No TD-08 / Stage 9 SHOULD_FIX cleanup
- No ObjectRows yet
- No DB-2 query engine in the same commit

### Expected behavior

**UNCHANGED** for end users. Catalog is internal infrastructure only.

---

## Appendix — Current chart source contracts

Stable Ids already used (good precedent):

`Types`, `Creators`, `CreatedMonth`, `UserStates`, `StateSemantic`, `Responsible`, `IfcTypes`, `BimModels`, `Remarks`

`HorizontalBar`, `VerticalBar`, `Pie`, `Line`

These should map to FieldDescriptor/Measure presets in DB-2, not be replaced casually.

---

## DB-1 Implementation Result

Date: 2026-09-14  
Status: **COMPLETE**  
Commit message: `Dashboard DB-1: add semantic field catalog`

### Chosen field model

Internal types in `Models/DashboardFieldModels.cs`:

- `DashboardFieldDescriptor` — Id, DisplayName, FieldType, SourceKind, ObjectTypeId?, SourceName, Capabilities
- `DashboardFieldType` — Text, Integer, Number, Boolean, DateTime, Enum, User, Reference, Guid, Unknown
- `DashboardFieldSourceKind` — System | Attribute
- `DashboardFieldCapabilities` — CanFilter / CanGroup / CanSort (**no CanAggregate** — query engine not present)
- `DashboardFieldCatalog` — immutable list + Ordinal Id lookup + `ForObjectType`
- `DashboardFieldIds` — centralized id factory

Builder: `Services/PilotFieldCatalogBuilder` — input `ProjectInventoryReport` or `IEnumerable<TypeInventoryRecord>`; **no Pilot SDK calls**.

### Identity format

| Kind | Format | Example |
|------|--------|---------|
| System | `system:{key}` | `system:created` |
| Attribute | `attribute:{typeId}:{attributeName}` | `attribute:10:resp` |

DisplayName is never part of Id.

### Evidence — type identity

- SDK: `IType.Id` → **`System.Int32`** (reflected from Ascon.Pilot.SDK)
- Code: `TypeInventoryRecord.TypeId = type.Id` (`TypeDiscoveryService`)

### Evidence — attribute identity

- SDK `IAttribute` properties: Name, Title, Type, … — **no Guid / stronger key**
- Runtime values keyed by Name: `obj.Attributes.ContainsKey(attr.Name)` (`AttributeDiscoveryService.ProfileObject`)
- Inventory stores `AttributeId = attr.Name`

**Stronger-than-Name identity:** **NO**

**Name uniqueness within one type:** expected unique (dictionary key); if metadata duplicates Name, catalog **retains first**, skips later (`SkippedDuplicateIds`).

**Attribute Name stability:** **BEST_AVAILABLE_BUT_RENAME_SENSITIVE** — rename breaks persisted query ids later; migrations must account for this.

### Collision policy

**Retain first / skip later** (deterministic). Counted on builder (`SkippedDuplicateIds`, `SkippedEmptyAttributeNames`). No silent Dictionary overwrite of unequal metadata.

### Field-type mapping

From `AttributeInventoryRecord.ValueType` strings (`AttributeType.ToString()`), without referencing SDK assembly in the builder:

| ValueType | DashboardFieldType |
|-----------|-------------------|
| String, Numerator | Text |
| Integer | Integer |
| Double, Decimal | Number |
| Boolean | Boolean |
| DateTime | DateTime |
| UserState | Enum |
| OrgUnit | User |
| ElementBook | Reference |
| Array, Inherited, other/null | Unknown (capabilities all false) |

### System fields included

| Id | Source | Type | Backing |
|----|--------|------|---------|
| `system:objectId` | objectId | Guid | `IDataObject.Id` |
| `system:typeId` | typeId | Integer | Type.Id / ObjectsByType |
| `system:parentId` | parentId | Guid | `IDataObject.ParentId` |
| `system:creatorId` | creatorId | Integer | Creator / ObjectsByCreator |
| `system:created` | created | DateTime | Created / month aggregates |
| `system:objectState` | objectState | Enum | ObjectStateInfo.State (lifecycle) |

Not included as raw IDataObject-only fields in DB-1: UserState card status, ModifiedDate, Responsible.

DB-2 added snapshot-backed semantic system fields (still no ObjectRows):

| Id | Source | Type | Backing |
|----|--------|------|---------|
| `system:createdMonth` | createdMonth | Text | `ObjectsByCreatedMonth.Period` |
| `system:userState` | userState | Enum | `ObjectsByUserState.StateId` |
| `system:responsible` | responsible | User | `ObjectsByResponsible.OrgUnitId` |

### Custom attributes

- Skip null/whitespace Name
- Display: Title → Name → `(unnamed)`
- Same Name on different TypeIds → distinct Ids
- Deterministic order: system fixed order, then TypeId, then Name (Ordinal)

### Why no direct per-widget SDK

Catalog builds from normalized `TypeInventoryRecord` / attribute shells already produced by inventory. Pure unit tests; no new SDK lifetime; preserves **one scan ≠ per widget** invariant.

### Tests

`PilotFieldCatalogBuilderTests` — 15 facts. Suite **138 → 153 PASS**.

### Limitations

- Attribute ids rename-sensitive
- Catalog not wired into UI (by design)
- No ObjectRows / query engine yet
- System field DisplayNames are English technical labels (no UI / no resx expansion)

### Next recommended DB-2

**Title:** Snapshot-backed widget query contracts for existing chart sources  

**Goal:** Introduce `WidgetQueryDefinition` + pure `WidgetQueryEngine` that maps existing `AnalyticsChartSource` presets to `WidgetDataset` (Category|Value) from `ProjectAnalyticsSnapshot`, without ObjectRows or SDK.  

**Input:** Snapshot + optional field catalog ids for presets.  
**Output:** WidgetDataset.  
**Scope:** Count + single dimension presets only; no UI persistence change required in same commit if adapter-only.  
**Tests:** PURE_UNIT for each ChartSource → dataset shape; TopN; empty snapshot.  
**Do not implement in DB-1.**

---

## DB-2 Implementation Result

Date: 2026-09-14  
Status: **COMPLETE**

### Query contract

Internal `DashboardWidgetQuery`: Scope, DimensionFieldId, Measure, Sort, Limit.  
Independent of `AnalyticsChartSource`.

### Supported

- Scope: `CurrentProject`
- Measure: `Count` (`long`)
- Dimensions (snapshot): `system:typeId`, `system:creatorId`, `system:createdMonth`, `system:userState`, `system:responsible`
- Sort: Value/Label Asc/Desc; tie-break Key then Label (Ordinal)
- Limit: null = all; ≤0 invalid

### Dataset

`WidgetDataRow` Key | Label | Value. No renderer types.

### Status

Success / Empty / UnsupportedQuery / InvalidQuery. No silent fallback.

### Snapshot mapping

| Field ID | Snapshot | Key | Label | Value |
|----------|----------|-----|-------|-------|
| (none) | sum `ObjectsByType.Count` | `""` | `""` | total |
| `system:typeId` | ObjectsByType | TypeId | TypeName | Count |
| `system:creatorId` | ObjectsByCreator | CreatorId | DisplayName | SampledCount |
| `system:createdMonth` | ObjectsByCreatedMonth | Period | Period | Count |
| `system:userState` | ObjectsByUserState | StateId `D` | StateTitle | Count |
| `system:responsible` | ObjectsByResponsible | OrgUnitId | DisplayName | SampledCount |

Null/whitespace labels → `""`.

### ChartSource parity

| Source | Classification | Notes |
|--------|----------------|-------|
| Types | EXACT_GENERIC_MATCH | `system:typeId` |
| Creators | EXACT_GENERIC_MATCH | sampled counts are snapshot fact, not chart special-case |
| UserStates | EXACT_GENERIC_MATCH | `system:userState` |
| Responsible | EXACT_GENERIC_MATCH | `system:responsible` added to catalog (decision A) |
| CreatedMonth | PARTIAL_MATCH | same counts; Chart keeps period order, engine uses query Sort |
| StateSemantic | SPECIALIZED_KEEP_EXISTING | OPEN/CLOSED inference |
| IfcTypes | SPECIALIZED_KEEP_EXISTING | BIM elements + optional extra `ifcRows`/model filter |
| BimModels | SPECIALIZED_KEEP_EXISTING | ElementCount, not Pilot objects |
| Remarks | SPECIALIZED_KEEP_EXISTING | remark-type subset, not all `system:typeId` |

### ChartDataService migration (not done)

**Option B:** later adapt exact-match sources through the query engine; keep ChartDataService for specialized charts until ObjectRows.

### Performance

0 SDK calls, 0 scans per widget. Sort O(n log n) on aggregate rows.

### Catalog delta

Added `system:createdMonth`, `system:userState`, `system:responsible` (semantic snapshot fields, not new SDK).

### Limitations

- No filters, no attributes, no ObjectRows, no UI/persistence
- Creator/responsible counts may be sampled
- Scalar Count uses type totals only

### Tests

Contract + parity for exact-match sources. ChartDataService production use **unchanged**.

### Next: DB-3

**Normalized DashboardObjectRows foundation** — snapshot presets cannot GroupBy arbitrary attributes. See Recommended DB-3 in the DB-2 report.

---

## DB-3 Implementation Result

Date: 2026-09-14  
Status: **BLOCKED_DB3_FULL_COVERAGE**  
Production code: **none** (docs only)

### Full-stream evidence

**FULL_OBJECT_STREAM: NO**

Search path loads `SearchByType(typeId, SampleLimit)` then `SubscribeObjects` those IDs (`PilotObjectSampler.SampleType`, `ObjectSamplingCoordinator.SampleAllTypes`).  
`ISearchResult.Total` is stored as `TypeInventoryRecord.ObjectCount` — count only.

Hierarchy fallback (`HierarchyWalkSampler.Walk`) uses visitBudget 2000 / 25000 / 100000 and keeps `bucket.Samples.Count < SampleLimit`. `Truncated` is a first-class result.

Creator aggregation is a **separate budgeted** sample (500 / 5000 / 50000).

Inventory explicitly notes: `"Attribute profiling uses sampled objects only."`

### Sample-stream evidence

| Stream | Cap |
|--------|-----|
| SampleAllTypes | SampleLimit = ScanMode (50 / 200 / 10000) |
| Walk Samples | same SampleLimit per type |
| CreatorAggregationService | global object budget |
| RemarkAnalyticsService | 10 / 30 / 100 per remark type |
| `_historySampleBuffer` / `_systemFieldSampleBuffer` / `_documentSampleBuffer` | 5 / 20 / 20 |

### Coverage invariant

**Not satisfiable** without a new full materialization. Rows were **not** built from samples.

### Object-row / value model

Not added.

### System field / custom attribute coverage

System fields and `obj.Attributes` exist on **loaded samples only**. Completing the project would require SubscribeObjects on remaining IDs = extra SDK load. **Not done.**

### Ownership / lifecycle / timeout / dedup

N/A — no collector.

### Memory / performance

UNCHANGED. No additional scan. No per-widget SDK.

### Tests

Unchanged **178 PASS**. No fake row tests against samples.

### Limitations

See `docs/DASHBOARD_OBJECT_ROWS.md`.

### Next

Architectural fork (not implemented):

- **DB-3.1** explicit once-per-refresh full object materialization (product cost), then ObjectRows + coverage metadata; **or**
- Stay on **DB-2 snapshot widgets** for preset dimensions until that cost is accepted.

Recommended immediate product path after DB-3: do **not** start an ObjectRows query engine (would have nothing complete to query). **DB-3.1** added an explicit one-TypeId materializer instead of a whole-project load.

---

## DB-3.1 Result

Date: 2026-09-14  
Status: **COMPLETE_TYPE_DATASET_FOUNDATION**  
Production caller: **none** (dashboard / editor / snapshot engine unchanged)

### Decision

Whole-project eager load **rejected**. V1 object-level analytics boundary is **one explicit TypeId**.

PATH A (snapshot aggregates) unchanged.  
PATH B: `DashboardTypeDatasetMaterializer` loads that TypeId completely, or reports Partial/Failed.

### Search semantics

- `IQueryBuilder.MaxResults(Int32)` — no Skip/Offset
- `ISearchResult.Total` = `Int64`
- `ISearchResult.Result` = `IEnumerable<Guid>`
- Paging: **none**
- Backend cap: **unknown** (detect via LoadedUniqueCount vs Total)

Feasibility: **FULL_TYPE_LOAD_SUPPORTED_WITH_LIMIT** (limit = Int32.MaxValue for MaxResults; silent server caps → Partial, never Complete).

### What was added

- `DashboardObjectRow` / `DashboardFieldValue` / `DashboardTypeDataset` / `DashboardTypeCoverage`
- `DashboardObjectRowFactory` (SDK-free normalization)
- `DashboardObjectSourceAdapter` (`IDataObject.Attributes` in memory, no per-attribute SDK)
- `DashboardTypeDatasetAssembler` (coverage + first-wins Guid dedupe)
- `DashboardTypeDatasetMaterializer` (TypeId in, dataset out; not a widget query)

### Completeness

`Complete` iff succeeded and `LoadedUniqueCount == ExpectedCount`. Empty type is Complete. Timeout/cancel/fail/overflow are never Complete.

### Sample buffers / inventory

**Unused.** `PilotObjectSampler.SampleLimit`, `ObjectSamplingCoordinator`, Inventory scan limits **unchanged**.

### Wiring

No presenter, editor, persistence, or query-engine call.

### Next: DB-4

ObjectRows widget query engine on **Complete** datasets only; reject Partial. Same DB-2 contract + EntityTypeId + Count + one Dimension. Filters = later.

---

## DB-4 Result

Date: 2026-09-14  
Status: **OBJECTROWS_QUERY_ENGINE**  
Production caller: **none**

### Contract

Same `DashboardWidgetQuery` as DB-2.

Added: `int? EntityTypeId`. Snapshot: null OK; non-null `UnsupportedQuery`. ObjectRows: required, must match dataset TypeId.

Added status: `IncompleteData` — valid query, supported executor, Coverage != Complete. Empty numbers. Snapshot does not emit it.

### Executor

`ObjectRowsWidgetQueryEngine` — concrete class, no interface, no SDK, no materializer.

Input: `DashboardTypeDataset` + `DashboardFieldCatalog` + `DashboardWidgetQuery`  
Output: `WidgetQueryResult` / `WidgetDataset`

### Behavior unchanged

Dashboard, widgets, snapshot semantics (except shared sort helper + EntityTypeId rejection), materializer, Inventory, UI, persistence: **unchanged**.

### Next

Runtime canary harness exists (DB-R1); live Pilot execution is deferred until Dashboard Builder V1 is wired. Filters: DB-5.

---

## DB-5 Result

Date: 2026-09-14  
Status: **OBJECTROWS_FILTERS**  
Production caller: **none**

### Model

Same `DashboardWidgetQuery` + `Filters[]` (`DashboardFilterDefinition`: FieldId, Operator, typed `DashboardFilterValue`).

Operators: Equals, NotEquals, IsEmpty, IsNotEmpty. AND only.

### Semantics

NotEquals includes missing rows. IsEmpty matches DB-4 missing (absent / null / empty / whitespace). Equality uses StableKey / typed values, never DisplayText. DateTime filters UnsupportedQuery.

### Data quality

Added `SkippedUnsupportedFieldIds` on the dataset (factory/assembler). Queries that touch a skipped field → `IncompleteData`.

### Snapshot

No filters: unchanged. Filters present: `UnsupportedQuery`.

### Pipeline

validate complete dataset → validate filters → quality check → filter rows → Count/GroupBy → sort → limit.

### Next: DB-6

Implemented — see **DB-6 Result** below.

---

## DB-6 Result

Date: 2026-09-14  
Status: **QUERY_COORDINATOR**  
Production caller: **none** (UI unchanged)

### Coordinator

`DashboardQueryCoordinator` — internal, `IDisposable`, one session.

API: `Task<WidgetQueryResult> ExecuteAsync(DashboardWidgetQuery query, CancellationToken cancellationToken)`

Routing: `EntityTypeId == null` → Snapshot engine; non-null → Type dataset cache → ObjectRows engine.

Seam: internal `IDashboardTypeDatasetProvider` / `DashboardTypeDatasetProvider` (one production adapter around `DashboardTypeDatasetMaterializer`). No MEF. No public API.

Cache: TypeId → `Task<DashboardTypeDataset>`, instance-only, lazy, coalesced under a short lock. Complete/Partial/Failed/faulted cached. No query-result cache. No static cache.

Cancellation: session CTS on Dispose. Caller token **ignored** in V1 (must not cancel shared load). After Dispose: `ObjectDisposedException`. No sync wait on Dispose.

Threading: snapshot `Task.FromResult`; TypeId miss one `Task.Run` (materializer is blocking). UI must not block.

### Behavior unchanged

Dashboard, widgets, UI, persistence, Inventory, Snapshot engine, ObjectRows engine, DB-R1 canary: **unchanged**.

### Architecture proof

- A: project types widget → Snapshot, 0 materializations
- B: two widgets same TypeId, different filter/dimension → 1 materialization, 2 in-memory executes
- C: TypeId 100 and 200 → 2 materializations

### Next: DB-7 (not implemented)

Versioned Widget Definition + Dashboard Persistence V2. Persist generic Query + Visualization before Widget Editor. Migrate `dashboard-layout.json`. Not another backend engine.




