# Stage 8 Final

Date: 2026-09-11  
HEAD: Stage 8.5 finalize low-risk UI localization

## Goal

Introduce standard strongly typed RESX localization infrastructure and migrate pure display UI chrome without touching logic keys, contracts, or dual-use semantic strings.

## Localization Infrastructure

| Item | Result |
|------|--------|
| Approach | `Properties/Resources.resx` + checked-in public `Resources.Designer.cs` |
| Access | C#: `Properties.Resources` / `UiResources` alias; XAML: `{x:Static props:Resources.Key}` |
| MSBuild generation | Disabled (empty Generator) for deterministic CLI builds |
| Embedded resource | `PilotBim.Analytics.Properties.Resources.resources` in `PilotBim.Analytics.ext2.dll` |
| Satellites | None |
| LocalizationService / markup extensions | Not added |

## Migrated UI Chrome

| Stage | Keys (approx.) | Focus |
|-------|----------------:|-------|
| 8.2 | 5 | MessageBox smoke |
| 8.3 | 29 | Toolbar/menu/dialog chrome |
| 8.4 | 29 | Shared columns, ScanDiff chrome, chart tabs (safe), widget labels |
| 8.5 | 61 | Remaining Analytics RU Headers + Inventory Headers/tabs/chrome |
| **Total** | **~124** | Display-only |

## Resource Strategy

Semantic keys (`Area_Purpose`). Shared keys only when display semantics match. Dual-use Russian values left literal until stable identity codes exist.

## Tests

117 → **118** PASS (representative resource key checks; no full UI snapshot).

## Build

PASS — 0 errors / 0 warnings through Stages 8.2–8.5.

## Runtime Culture Switching

**NOT_IMPLEMENTED**

## Additional Languages

**NOT_ADDED** (default culture resources only; current RU text as default values).

## Remaining Medium-Risk Strings

ProgressText, ScanDiffHint, ChartBuilderHint, HeaderSubtitle, branching MessageBoxes, formatted status lines, some persisted default Titles.

## Deferred Dual-Use Strings

1. ScanDiff `Area`/`Metric` (`Скан`, `Время скана`, `Ошибка`, `Ответственные`, `OPEN/CLOSED`, …) used in filters/tests  
2. `KpiLabels` / Summary Label exact match  
3. Nav Keys / ShowPanel routing (EN keys already; Titles later after care)  
4. Chart Ids / WidgetKinds / BlockIds  
5. Remark heuristic `замечан`  
6. Path folder brand `PilotBim.Analytics`  

## Contract Strings Explicitly Excluded

CSV headers/filenames, JSON schema names, persistence paths, SDK type/attribute names, command ids, bimObjectId, export machine labels.

## Risks

| Risk | Mitigation |
|------|------------|
| Accidental localization of dual-use values | Explicit skip lists + exact-literal searches each batch |
| Window.Resources name clash | `UiResources` alias in Window code-behind |
| Designer overwrite as internal | Generator disabled; Designer checked in |
| Over-generalized shared keys | Prefer semantic keys over aggressive dedupe |

## Recommendation

**STOP_STAGE_8**

Low-risk XAML chrome is substantially migrated. Remaining work is either technical-id headers (low value), medium-risk dynamic text, or dual-use strings requiring a separate stable-code design stage — not more chrome batches.
