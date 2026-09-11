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

---

## Stage 8.2 Result

Date: 2026-09-09  
HEAD: (commit Stage 8.2)

### Infrastructure

| Item | Result |
|------|--------|
| Approach | Standard `Properties/Resources.resx` + checked-in strongly typed `Resources.Designer.cs` |
| Access | `PilotBim.Analytics.Properties.Resources.*` (in Window code-behind: alias `UiResources` to avoid clash with `Window.Resources`) |
| MSBuild generation | **Disabled** (`Generator` empty) so CLI builds do not overwrite Designer as `internal` |
| LocalizationService | Not added |
| ResourceDictionary framework | Not added |

### Resource Files

- `src/PilotBim.Analytics/Properties/Resources.resx`
- `src/PilotBim.Analytics/Properties/Resources.Designer.cs`
- `PilotBim.Analytics.csproj` EmbeddedResource update (no generator)

Embedded manifest name: `PilotBim.Analytics.Properties.Resources.resources` inside `dist\PilotBim.Analytics.ext2.dll`  
WPF `*.g.resources` unchanged. No satellite assemblies.

### Naming Convention

Semantic keys: `{Area}_{Purpose}`  

Examples: `Common_ScanRequiredFirst`, `Widget_EnterTitle`, `Catalog_OpenFailedPrefix`, `Analytics_OpenFailedPrefix`, `Snapshot_EnterName`  

Avoid: `Button1`, `Text3`, sequential junk names.

### Smoke Strings Migrated

| Key | Original location | Risk |
|-----|-------------------|------|
| `Common_ScanRequiredFirst` | AnalyticsWindow + InventoryWindow MessageBox | LOW |
| `Widget_EnterTitle` | DashboardWidgetEditorWindow MessageBox | LOW |
| `Catalog_OpenFailedPrefix` | AnalyticsCommandService MessageBox prefix | LOW |
| `Analytics_OpenFailedPrefix` | AnalyticsCommandService MessageBox prefix | LOW |
| `Snapshot_EnterName` | AnalyticsWindow MessageBox | LOW |

XAML mass migration: **not done** (C# smoke only; avoids `Window.Resources` / markup infrastructure).

Safety table (pre-migrate):

| String | Occurrences (src) | Used in logic? | Safe? |
|--------|-------------------|----------------|-------|
| Сначала выполните сканирование. | 3 MessageBox | NO | YES |
| Укажите заголовок. | 1 MessageBox | NO | YES |
| Не удалось открыть каталог данных:\n | 1 MessageBox prefix | NO | YES |
| Не удалось открыть аналитику:\n | 1 MessageBox prefix | NO | YES |
| Укажите имя снимка. | 1 MessageBox | NO | YES |

### Contract Strings Explicitly Untouched

- Nav / ShowPanel keys  
- Chart Ids / WidgetKinds / BlockIds  
- KpiLabels / ScanDiff Area/Metric (`Скан`, `Время скана`, `Ошибка`)  
- CSV headers/filenames  
- JSON / path folder `PilotBim.Analytics` (also used as MessageBox caption — left literal)  
- SDK names / `замечан` heuristic  

### Runtime Culture Switching

**NOT_IMPLEMENTED**

### Additional Languages

**NOT_ADDED** (no `Resources.en.resx` / `Resources.ru.resx`)

### Tests

111 → **115** PASS (`LocalizationResourcesTests` ×4)  
0 failed / 0 skipped

### Build

PASS — **0** errors / **0** warnings

### Behavior

**UNCHANGED** (same default RU MessageBox text via resources)

### Recommended Stage 8.3

Migrate additional low-risk chrome (menu headers / button captions) via the same strongly typed Resources accessors; still exclude dual-use ScanDiff/KPI/key contracts.

---

## Stage 8.3 Result

Date: 2026-09-11  
Access pattern: XAML `{x:Static props:Resources.Key}` + C# `Resources.Key` for menus

### Migrated Strings

| Key | Original text | Location | Risk |
|-----|---------------|----------|------|
| `Common_Cancel` | Отмена | Analytics/Inventory/WidgetEditor/SimplePrompt | LOW |
| `Common_OK` | OK | WidgetEditor/SimplePrompt | LOW |
| `Common_Refresh` | Обновить | AnalyticsWindow button | LOW |
| `Common_Delete` | Удалить | Analytics tooltips + ScanDiff button | LOW |
| `Common_Configure` | Настроить | Analytics dashboard tooltip | LOW |
| `Common_ScanModeFast` | Быстро | Analytics + Inventory radios | LOW |
| `Common_ScanModeStandard` | Стандарт | Analytics + Inventory radios | LOW |
| `Common_ScanModeFull` | Полный | Analytics + Inventory radios | LOW |
| `Common_NameLabel` | Имя: | SimplePrompt default | LOW |
| `Analytics_WindowTitle` | Pilot-BIM Analytics — Обзор проекта | Title + header | LOW |
| `Analytics_ExportCsv` | Экспорт CSV | Analytics toolbar | LOW |
| `Analytics_ExportsFolder` | Папка CSV | Analytics toolbar | LOW |
| `Analytics_OpenExportsFolderTooltip` | Открыть папку Exports | ToolTip | LOW |
| `Analytics_ModelLabel` | Модель: | BIM filter bar | LOW |
| `Analytics_AddWidget` | + Виджет | Dashboard | LOW |
| `Analytics_DashboardBuilder` | Конструктор дашборда | Dashboard | LOW |
| `Analytics_ToDashboard` | На дашборд | Chart builder | LOW |
| `Analytics_AddChartToDashboardTooltip` | Добавить текущий график на Сводку | ToolTip | LOW |
| `Inventory_WindowTitle` | Pilot-BIM Analytics — Каталог данных | Inventory Title | LOW |
| `Inventory_Scan` | Сканировать | Inventory toolbar | LOW |
| `Inventory_CopyReport` | Скопировать отчёт | Inventory toolbar | LOW |
| `Inventory_SaveReport` | Сохранить отчёт | Inventory toolbar | LOW |
| `Widget_WindowTitle` | Виджет дашборда | Editor Title | LOW |
| `Widget_TitleLabel` | Заголовок | Editor label | LOW |
| `Widget_KindLabel` | Тип виджета | Editor label | LOW |
| `Menu_Overview` | Аналитика — обзор проекта | Main/context menu | LOW |
| `Menu_Catalog` | Аналитика — источники данных | Main/context menu | LOW |
| `Toolbar_Analytics` | Аналитика | Toolbar header | LOW |
| `Toolbar_Catalog` | Каталог данных | Toolbar header | LOW |

### Count

**29** new keys (Stage 8.3). Cumulative resources include Stage 8.2 smoke keys.

### Contract Strings Untouched

Nav/ShowPanel, KpiLabels, ScanDiff Area/Metric logic, Chart IDs, WidgetKinds, CSV/export, JSON/persistence paths, SDK names, discovery heuristics, command **ids**.

### XAML impact

**TEXT SOURCE ONLY** (`{x:Static}` on existing attributes)

### Layout impact

**NONE**

### Runtime language switching

**NOT_IMPLEMENTED**

### Additional languages

**NOT_ADDED**

### Tests

115 → **116** PASS (+ representative chrome key checks)  
0 failed / 0 skipped

### Build

PASS — **0** errors / **0** warnings  
Embedded: `PilotBim.Analytics.Properties.Resources.resources` (no satellites)

### Behavior

**UNCHANGED**

### Remaining Low-Risk Strings

- Many DataGrid column Headers (Analytics/Inventory)
- Chart TabItem headers
- ScanDiff chrome (`База:`, `Сравнить`, `Сохранить как…`, `Только изменения`)
- Chart builder labels (`Данные:`, `Тип:`, `Объём:`)
- Remaining widget editor labels (`Источник данных`, `Тип графика`, `Объём`, `Ширина`)
- Remaining MessageBox / ProgressText strings
- Footer `Ограничения (read-only)`

### Deferred Dual-Use Strings

- ScanDiff `Area`/`Metric` (`Скан`, `Время скана`, `Ошибка`)
- `KpiLabels` / Summary Labels
- Nav Keys / ShowPanel
- Chart Ids / WidgetKinds
- Heuristic `замечан`
- Path folder / brand `PilotBim.Analytics` where used as persistence

### Recommended Next Step

Continue small-batch chrome (grid Headers / remaining dialogs) **or** design stable codes for dual-use ScanDiff/KPI before localizing those display values — do not start without confirmation.

---

## Stage 8.4 Result

Date: 2026-09-11

### Migrated Strings

| Key | Original text | Location | Risk |
|-----|---------------|----------|------|
| `Column_Indicator` | Показатель | Analytics KPI grids | LOW |
| `Column_Value` | Значение | Analytics grids | LOW |
| `Column_Details` | Детали | Analytics grids | LOW |
| `Column_Caption` | Подпись | Chart builder grid | LOW |
| `Column_SharePercent` | Доля % | Analytics grids | LOW |
| `Column_Count` | Кол-во | Analytics grids | LOW |
| `Column_Scope` | Охват | Analytics grids | LOW |
| `Column_InSample` | В сэмпле | Analytics grids | LOW |
| `Column_Area` | Область | ScanDiff grid header | LOW |
| `Column_Metric` | Метрика | ScanDiff grid header | LOW |
| `Column_Previous` | Было | ScanDiff grid | LOW |
| `Column_Current` | Стало | ScanDiff grid | LOW |
| `Column_Notes` | Заметки | ScanDiff grid | LOW |
| `Analytics_DataLabel` | Данные: | Chart builder | LOW |
| `Analytics_TypeLabel` | Тип: | Chart builder | LOW |
| `Analytics_VolumeLabel` | Объём: | Chart builder | LOW |
| `Analytics_LimitationsHeader` | Ограничения (read-only) | Footer | LOW |
| `ChartTab_TypesTop` | Типы (top) | Charts tabs | LOW |
| `ChartTab_Creators` | Создатели | Charts tabs | LOW |
| `ChartTab_CreatedTimeline` | Динамика создания | Charts tabs | LOW |
| `ChartTab_IfcTypes` | IFC типы | Charts tabs | LOW |
| `ScanDiff_BaselineLabel` | База: | ScanDiff chrome | LOW |
| `ScanDiff_Compare` | Сравнить | ScanDiff chrome | LOW |
| `ScanDiff_SaveAs` | Сохранить как… | ScanDiff chrome | LOW |
| `ScanDiff_ChangesOnly` | Только изменения | ScanDiff chrome | LOW |
| `Widget_SourceLabel` | Источник данных | Widget editor | LOW |
| `Widget_ChartKindLabel` | Тип графика | Widget editor | LOW |
| `Widget_VolumeLabel` | Объём | Widget editor | LOW |
| `Widget_WidthLabel` | Ширина | Widget editor | LOW |

**Explicitly skipped (dual-use):** TabItem `OPEN/CLOSED`, TabItem `Ответственные` (also ScanDiff Area / widget Title defaults).

### Count

**29** new keys (Stage 8.4).

### Contract Strings Untouched

Nav/ShowPanel, KpiLabels, ScanDiff Area/Metric **cell values**, Chart IDs, WidgetKinds, CSV/export, JSON/persistence, SDK/discovery, command ids.

### XAML impact

**TEXT SOURCE ONLY**

### Layout impact

**NONE**

### Runtime language switching

**NOT_IMPLEMENTED**

### Additional languages

**NOT_ADDED**

### Tests

116 → **117** PASS  
0 failed / 0 skipped

### Build

PASS — **0** errors / **0** warnings  
Embedded `PilotBim.Analytics.Properties.Resources.resources`; no satellites

### Remaining Inventory

**Low-risk (~50–70):** remaining Analytics DataGrid Headers (`Тип`, `Статус`, `Создатель`, `Модель`, `Объект`, …); Inventory EN Headers/tabs; `Зоны`; `Type:`/`Attribute:`; `Загрузить корень (lazy)`; `Семантика` / `Пример статуса`; leftover MessageBox captions that are pure display.

**Medium-risk (~25–40):** ProgressText / ScanDiffHint / ChartBuilderHint / HeaderSubtitle; formatted MessageBoxes; default dashboard Titles as persisted values.

**Dual-use/high-risk:** ScanDiff Area/Metric (`Скан`, `Время скана`, `Ошибка`, `Ответственные`, `OPEN/CLOSED`, …); `KpiLabels` / Summary Labels; Nav Keys / ShowPanel; Chart Ids / WidgetKinds; `замечан` heuristic; path/`PilotBim.Analytics` folder name.

### Deferred Stable-Code Migration

1. ScanDiff `Area`/`Metric` identity codes separate from display  
2. KPI Summary Label match keys → stable ids  
3. Nav Key / ShowPanel (already EN keys; Titles display-only later)  
4. ChartOption/Widget Title defaults vs Ids/Kinds  
5. Remark discovery heuristic locale independence  

### Recommendation

**ONE_MORE_LOW_RISK_BATCH**

One further small batch for remaining unique Analytics/Inventory column headers and Inventory chrome, then prefer stopping low-risk migration before dual-use redesign.

---

## Stage 8.5 Result

Date: 2026-09-11

### Migrated Strings

**61** new keys covering:

- Analytics remaining RU DataGrid Headers (`Тип`, `Статус`, `Модель`, `Объект`, `Примечания`, …)
- Inventory chrome (`Зоны`, filter labels, load-root, BIM tabs)
- Inventory EN DataGrid Headers (`Type`, `Status`, `Name`, `Attribute`, …)

Skipped intentionally: dual-use tabs `OPEN/CLOSED` / `Ответственные`; technical id Headers (`TypeId`, `PersonId`, `bimObjectId`, …); brand `PilotBim.Analytics`; icon glyphs.

### Count

**61** (Stage 8.5). Cumulative UI chrome migration across 8.2–8.5 complete for practical purposes.

### Remaining Inventory

**Low-risk (~15–25 scattered):** technical id column Headers; icon button glyphs; brand title literals left for path dual-use caution.

**Medium-risk (~25–40):** ProgressText / hints / formatted MessageBoxes / HeaderSubtitle / persisted default Titles.

**Dual-use/high-risk:** ScanDiff Area/Metric identities; KpiLabels; Nav/ShowPanel keys; Chart Ids / WidgetKinds; SDK/persistence/CSV; heuristics; dual-use tab captions.

### Contract Safety

UNCHANGED for Nav/ShowPanel, KpiLabels, ScanDiff semantics, Chart IDs, WidgetKinds, CSV/export, persistence, SDK/discovery, command ids.

### Runtime Switching

**NOT_IMPLEMENTED**

### Additional Languages

**NOT_ADDED**

### Behavior

**UNCHANGED**

### Stage 8 Closure Recommendation

**STOP_STAGE_8**
