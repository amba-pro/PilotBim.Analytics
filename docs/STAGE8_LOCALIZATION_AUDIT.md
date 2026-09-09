# Stage 8 Localization Audit

Date: 2026-09-09  
Code HEAD at audit: `fc8eb5a`  
Scope: Stage 8.0–8.1 — **analysis + docs only**  
Production code changes: **none**

---

## Baseline

| Check | Result |
|-------|--------|
| HEAD | `fc8eb5a` — Stage 7.4: finalize analytics UI review |
| origin/main | `b8df8f3` |
| Ahead/behind | **ahead by 9** (Stages 6–7.4) |
| Working tree | clean |
| Build | PASS — **0** errors / **0** warnings |
| Tests | **111** PASS / **0** failed / **0** skipped |

---

## Existing Localization Infrastructure

| Check | Result |
|-------|--------|
| `*.resx` / `Properties/Resources.*` | **None** |
| WPF `ResourceDictionary` for strings | **None** |
| `ResourceManager` / `CurrentUICulture` for UI | **None** |
| csproj `EmbeddedResource` for strings | **None** (`UseWPF` SDK project only) |
| Culture usage today | `InvariantCulture` for CSV/numeric formats; UI is hardcoded **Russian** |

**Verdict: NONE**

Do not create infrastructure on Stage 8.1.

---

## String Inventory

Approximate useful counts (not absolute uniqueness):

| Source | Count | Notes |
|--------|------:|-------|
| XAML hardcoded `Text`/`Header`/`Content`/`ToolTip`/`Title` | **~203** | Non-binding attribute literals |
| C# lines containing Cyrillic literals | **~238** | UI + KPI + status + dual-use |
| Menu / toolbar display headers | **6** | Main/context/toolbar |
| MessageBox / dialog prompts | **~20–25** | Windows + command service |
| Status / hint / ProgressText | **~25–35** | VM + Window code-behind |
| Internal keys (EN Nav/Chart/Widget) | **~50–70** | Must not localize as values |
| CSV headers + filenames | **~80–100** cells / files | English machine contract |
| Persistence paths / JSON CLR names | **~40–60** | Files + schema |
| Dual-use RU (display + logic) | **~15–25** | ScanDiff Area/Metric, KPI Labels |

### By classification

| Cat | Name | Approx. | Localize in Stage 8.2? |
|-----|------|--------:|------------------------|
| A | USER_VISIBLE_UI | ~320–380 | Later (after infra) |
| B | USER_VISIBLE_STATUS | ~25–35 | Later / sample only |
| C | DIALOG_MESSAGE | ~20–25 | Low-risk sample OK |
| D | TOOLTIP / LABEL / HEADER | ~15–25 chrome + many grid Headers in A | Low-risk chrome only |
| E | CSV / EXPORT CONTRACT | ~80–100 | **Never** (or versioned later) |
| F | PERSISTENCE CONTRACT | paths + JSON names; stored Titles as values | **Never** for keys/paths |
| G | INTERNAL_KEY | Nav Key, ShowPanel, Chart Id, WidgetKind | **Never** |
| H | LOG / DIAGNOSTIC | AnalyticsLogger EN ids | **Never** |
| I | SDK / ATTRIBUTE / TYPE NAME | command ids, `bimObjectId`, type names | **Never** |
| J | FILE / PATH / FORMAT | Snapshots/Exports filenames | **Never** |
| K | TEST_ONLY | asserts locking RU Area/Metric | Do not break |

---

## XAML Strings

Hardcoded attribute literals ≈ **203** across Views.

| File | Approx. | Localize? |
|------|--------:|-----------|
| `AnalyticsWindow.xaml` | ~124 | Yes later (chrome/headers); not keys |
| `InventoryWindow.xaml` | ~65 | Yes later; many EN headers |
| `DashboardWidgetEditorWindow.xaml` | ~9 | Yes later |
| `SimplePromptWindow.xaml` | ~4 | Yes later |
| `ChartCanvasControl.xaml` | 0 | — |

### Representative table

| File | Context | Current text | Category | Localize? |
|------|---------|--------------|----------|-----------|
| AnalyticsWindow.xaml | Window Title | Pilot-BIM Analytics — Обзор проекта | A | Yes (later) |
| AnalyticsWindow.xaml | RadioButton | Быстро / Стандарт / Полный | A | Yes |
| AnalyticsWindow.xaml | Button | Обновить / Экспорт CSV / Папка CSV / Отмена | A | Yes |
| AnalyticsWindow.xaml | ToolTip | Открыть папку Exports | D | Yes |
| AnalyticsWindow.xaml | Label | Модель: / Данные: / Тип: / Объём: | D | Yes |
| AnalyticsWindow.xaml | Button | + Виджет / На дашборд | A | Yes |
| AnalyticsWindow.xaml | TabItem | Типы (top), Создатели, … | A | Yes |
| AnalyticsWindow.xaml | DataGrid Header | Показатель, Значение, Детали, … | A | Yes |
| AnalyticsWindow.xaml | DataGrid Header | TypeId, PersonId, ModelId, Fill % | A/I | Prefer leave EN ids |
| AnalyticsWindow.xaml | ScanDiff chrome | База:, Сравнить, Сохранить как…, Только изменения | A | Yes |
| AnalyticsWindow.xaml | Footer | Ограничения (read-only) | A | Yes |
| InventoryWindow.xaml | Title | Pilot-BIM Analytics — Каталог данных | A | Yes |
| InventoryWindow.xaml | Buttons | Сканировать, Скопировать отчёт, … | A | Yes |
| InventoryWindow.xaml | Most Headers | Type, Objects, Attributes, … | A (EN) | Optional consistency |
| DashboardWidgetEditorWindow.xaml | Title/labels | Виджет дашборда, Заголовок, … | A | Yes |
| SimplePromptWindow.xaml | Buttons | Отмена / OK | A | Yes |

Bindings (`{Binding ProgressText}`, `{Binding Title}`, etc.) are **not** XAML literals; their values come from code (B/A).

---

## Code-Behind Strings

### `AnalyticsWindow.xaml.cs`

| Kind | Examples | Category | Localize? |
|------|----------|----------|-----------|
| MessageBox | Сначала выполните сканирование. | C | Yes (low risk) |
| MessageBox | Экспорт сохранён в:\n… | C | Yes |
| MessageBox | Полный режим может занять много времени… / title Полный scan | C | Yes |
| MessageBox | Удалить виджет… / Удалить снимок… | C | Yes |
| MessageBox | Укажите имя снимка. / Выберите снимок… | C | Yes |
| ProgressText | Сканирование... / Готово. Объектов: … / Ошибка: … | B | Yes (format carefully) |
| Prompt | Сохранить снимок / Имя снимка скана: / Снимок {date} | C | Yes |
| **ShowPanel keys** | `"Summary"`, `"Charts"`, … `"ScanDiff"` | G | **NO** |

### `InventoryWindow.xaml.cs`

| Kind | Examples | Category | Localize? |
|------|----------|----------|-----------|
| Dialogs | Full-mode confirm; Сначала выполните сканирование.; Отчёт сохранён | C | Yes |
| Progress | Scanning... (EN), Готово. Status=…, Отмена запрошена... | B | Yes |
| **ShowPanel keys** | Overview…Diagnostics | G | **NO** |

### Other

| File | Examples | Category | Localize? |
|------|----------|----------|-----------|
| `DashboardWidgetEditorWindow.xaml.cs` | Укажите заголовок.; Titles for kinds/spans | A/C | Titles yes; **Ids NO** |
| `AnalyticsCommandService.cs` | Не удалось открыть каталог/аналитику | C | Yes |
| Menu commands | Аналитика — обзор проекта / источники данных; toolbar Аналитика / Каталог данных | A | Yes |
| Command **names** | `PilotBim_Analytics_*` | I/G | **NO** |

---

## ViewModel / Presenter Strings

### `AnalyticsWindowViewModel`

| Group | Key / Id (G) | Title / text | Safe replace Title only? |
|-------|--------------|--------------|--------------------------|
| Nav ×18 | Summary…ScanDiff | Сводка…Сравнение сканов | **Yes** (Key stays EN) |
| Chart kinds | HorizontalBar… | Горизонтальные столбцы… | **Yes** |
| Chart sources | Types…Remarks | Типы объектов… | **Yes** |
| TopN | 8/12/20/0 | Топ 8…Все | **Yes** |
| Filter | — | Все модели | Yes |
| Status | — | Progress / HeaderSubtitle / ChartBuilderHint | Yes (B) |
| Defaults | — | Данные / График fallback titles | Yes |

### `AnalyticsScanComparePresenter`

| String | Category | Notes |
|--------|----------|-------|
| Many `ScanDiffHint` RU lines | B | Localize later OK |
| `Авто · {timestamp}` | A/F value | Display/archive name |
| `Area = "Ошибка"`, `Metric = "Сравнение сканов"` | A + **G** | Used by `IsMeaningfulChange` |
| Compare filter `Area == "Скан" && Metric == "Время скана"` | **G** | **NOT** safe to localize directly |

### `AnalyticsDashboardPresenter`

Almost no hardcoded RU; uses EN kinds/block ids + titles from store/editor.

### `InventoryWindowViewModel`

Nav Titles RU / Keys EN; Progress defaults; `Отчёт ещё не сформирован.`

### Services feeding UI

| Service | User-visible | Risk |
|---------|--------------|------|
| `ProjectAnalyticsService` | KPI Labels, BIM KPIs, status translations, Limitations bullets | Labels also matched by ScanDiff |
| `CapabilityMatrixService` | ~20 RU metric titles | Display; Inventory grid |
| `ScanDiffService` | Area/Metric/Notes RU | **Dual-use + tests** |
| `DashboardLayoutStore` | Default Titles KPI/BIM/… | Persisted **values**; kinds EN |
| `ChartDataService` | No RU chrome; data-driven labels | Low |
| `AnalyticsCsvExporter` | EN headers/filenames | Contract |

---

## Dialogs / Status Messages

| Bucket | Approx. | Risk for Stage 8.2 |
|--------|--------:|--------------------|
| MessageBox / confirms | ~20–25 | **LOW** |
| ProgressText / ScanDiffHint / ChartBuilderHint | ~25–35 | **MEDIUM** (dynamic format) |
| Menu/toolbar headers | 6 | **LOW** |
| SimplePrompt injected strings | few | **LOW** |

---

## Internal Keys

| String | Visible to user? | Used as key? | Safe to localize directly? |
|--------|------------------|--------------|----------------------------|
| Nav `Key` Summary…ScanDiff | No (Title shown) | Yes — ShowPanel / SelectedNavKey | **NO** |
| Inventory Nav Keys | No | Yes — ShowPanel | **NO** |
| ChartOption `Id` (HorizontalBar, Types, `"12"`) | No | Yes — enum/TopN | **NO** |
| Nav/Chart `Title` | Yes | No | **YES** (Key/Id fixed) |
| `DashboardWidgetKinds` / BlockIds | No | Yes — JSON + presenter | **NO** |
| ScanDiff `Area` `"Скан"` / `"Ошибка"` | Yes | **Yes** — filter | **NO** |
| ScanDiff `Metric` `"Время скана"` | Yes | **Yes** — filter | **NO** |
| KPI Label `"Всего объектов"` etc. | Yes | **Yes** — `ScanDiffService.KpiLabels` exact match | **NO** |
| Heuristic `"замечан"` / issue / remark | No | Discovery | **NO** |
| Pilot command ids `PilotBim_Analytics_*` | No | Yes | **NO** |
| CSV headers / filenames | Export | Contract | **NO** |
| JSON property / file names | No | Persistence | **NO** |
| Widget Title in layout JSON | Yes | Value only | Careful (user-editable) |

---

## SDK / Persistence / Export Contracts

**Do not localize:**

- MEF / Pilot command and toolbar ids  
- Snapshot / dashboard JSON CLR / schema names  
- Paths: `%LOCALAPPDATA%\PilotBim.Analytics\`, `last-scan.json`, `dashboard-layout.json`, `history-index.json`, `Exports\`  
- CSV filenames (`objects_by_type.csv`, `scan_compare.csv`, …) and **English headers**  
- SDK attribute/type identifiers (`bimObjectId`, CoordinationModel, CreatorId, …)  
- Log event identifiers  

**Special coupling:** ScanDiff CSV cells and UI rows currently carry the same Russian `Area`/`Metric`/`Notes` strings that logic and tests depend on.

---

## Risk Groups

### LOW RISK
- Pure chrome: button Content, ToolTips, window titles, menu headers  
- MessageBox text that is not compared elsewhere  
- SimplePrompt labels  

### MEDIUM RISK
- Dynamic ProgressText / hints with interpolated counts/status  
- Nav/Chart **Titles** (safe if Keys/Ids untouched)  
- Default dashboard Titles (persisted as values; display-only change OK if kinds/ids unchanged)  

### HIGH RISK
- ScanDiff `Area` / `Metric` used in `IsMeaningfulChange`  
- `ScanDiffService.KpiLabels[]` exact string match to Summary Labels  
- Any string used as ShowPanel / Chart Id / WidgetKind / BlockId  
- Remark-type heuristic `IndexOf("замечан")`  

### DO NOT LOCALIZE
- SDK names, persistence keys/paths, CSV machine headers/filenames, log ids, command ids, internal EN keys  

---

## Localization Options

| Option | Complexity | WPF fit | Testability | Runtime culture switching | Risk |
|--------|------------|---------|-------------|---------------------------|------|
| **A. Properties/Resources.resx** + strongly typed access | Low–Med | Excellent for net472 WPF; `x:Static` + C# | Good | Later (satellite + culture) | **Lowest** for Stage 8.2 |
| **B. WPF ResourceDictionary** strings | Low for XAML | Strong XAML; weak for MessageBox/services | Fair | DynamicResource possible | Medium (split XAML vs C#) |
| **C. Custom localization service** | High | Flexible | Good if injected | Easy | Overbuilt now; more surface |

**Preferred direction:** Option **A**.

---

## Recommended Stage 8.2

**One change:** introduce **resx infrastructure only** (`Properties/Resources.resx` + Designer + csproj embedding).

Optionally (same stage only if still tiny): wire **≤5** low-risk chrome/dialog strings as a smoke test — e.g. one MessageBox, `Отмена`/`OK`, one menu header.

**Not** mass XAML replacement. **Not** ScanDiff/KPI/Nav Key changes.

---

## Deferred

- Mass XAML header migration (~200)  
- Nav/Chart Title migration (easy after infra)  
- KPI / Capability / Limitations / ScanDiff **display** strings until stable non-localized codes exist  
- Introducing `ScanDiffAreas` / `ScanDiffMetrics` / KPI ids (prerequisite for localizing dual-use strings)  
- CSV header localization / schema versioning  
- Remark heuristic i18n  
- Runtime culture switch UI  
- Inventory EN/RU header consistency pass  

---

## Stage 8.2 Recommendation

### Change

Add empty/minimal `Properties/Resources.resx` (+ generated Designer) and project embedding so Stage 8.3+ can migrate strings safely. **No mass string replacement.**

### Scope

Infrastructure only (optional ≤5 low-risk smoke lookups). Behavior-preserving.

### Why first

Unblocks localization without touching ShowPanel keys, dashboard JSON kinds, ScanDiff filter contracts, KPI label matching, CSV, or tests.

### Expected files

- `src/PilotBim.Analytics/Properties/Resources.resx`  
- `src/PilotBim.Analytics/Properties/Resources.Designer.cs` (if generated)  
- `src/PilotBim.Analytics/PilotBim.Analytics.csproj` (EmbeddedResource / settings)  
- Optionally 1–2 view/command files **only** if smoke wiring is included  

### Expected production behavior

**UNCHANGED**

### Explicit exclusions

- **no panel-key localization** (`ShowPanel` / Nav `Key`)  
- **no persistence-key localization** (JSON names, layout kinds/block ids, file names)  
- **no SDK-name localization** (type/attribute/command ids)  
- **no CSV contract changes** (headers, filenames, machine meta labels)  
- no ScanDiff `Area`/`Metric`/`KpiLabels` value changes  
- no remark heuristic string changes  
- no XAML architecture / MEF / public API redesign  

---

## Stop conditions check

| Condition | Status |
|-----------|--------|
| Cannot separate UI from contract strings | **Clear separation documented** — proceed |
| Complex existing localization framework | **NONE** — proceed |
| Would require public API change | No for 8.2 infra |
| Would require XAML architecture rewrite | No |
| net472/WPF resx conflict | None expected for standard Properties/Resources |
| Persistence+UI dual-use unclear | Dual-use identified and **excluded** from 8.2 |

**Stage 8.1 complete. Do not start Stage 8.2 until confirmed.**
