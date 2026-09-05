# Refactor Stage 0 — Regression Baseline

**Date:** 2026-09-05  
**Purpose:** Freeze working state before Stages 1–6. No production code changes in Stage 0.  
**Git root:** `PilotBim.Analytics/` (remote intended: `https://github.com/amba-pro/PilotBim.Analytics.git`)

---

## Plugin version (from csproj)

| Property | Value |
|---|---|
| AssemblyName | `PilotBim.Analytics.ext2` |
| Version | `0.9.0` |
| FileVersion | `0.9.0` |
| InformationalVersion | `0.9.0-rich-diff-to-dashboard` |
| TargetFramework | `net472` |
| UseWPF | `true` |
| PlatformTarget | `AnyCPU` |

Source: `src/PilotBim.Analytics/PilotBim.Analytics.csproj`

---

## Analytics window — navigation sections (regression keys)

Default selected: `Summary`

| Key | Title |
|---|---|
| Summary | Сводка |
| Charts | Графики |
| ChartBuilder | Конструктор |
| Types | По типам |
| Creators | По создателям |
| Created | По дате создания |
| States | По статусам |
| StateSemantic | Статусы OPEN/CLOSED |
| Responsible | По ответственным |
| Documents | Версии документов |
| Bim | BIM — сводка |
| BimModels | BIM — модели |
| BimParts | BIM — части |
| BimTypes | BIM — типы IFC |
| Quality | Качество данных |
| Remarks | Замечания к модели |
| RemarkLinks | Замечания → BIM |
| ScanDiff | Сравнение сканов |

Source: `ViewModels/AnalyticsWindowViewModel.cs` (Navigation collection)

### Chart builder options (baseline)

**Kinds:** HorizontalBar, VerticalBar, Pie, Line  
**Sources:** Types, Creators, CreatedMonth, UserStates, StateSemantic, Responsible, IfcTypes, BimModels, Remarks  
**Top limits:** 8, 12, 20, 0 (Все)

### Dashboard widget kinds / legacy block ids

**WidgetKinds:** Kpi, Bim, Responsible, Chart  
**BlockIds (legacy):** kpi, bim, responsible

---

## Inventory window — navigation sections (regression keys)

Default selected: `Overview`

| Key | Title |
|---|---|
| Overview | Обзор |
| Types | Карточки |
| Attributes | Атрибуты |
| SystemFields | Системные поля |
| States | Статусы |
| Persons | Пользователи |
| Organisations | Организации |
| Structure | Структура |
| Documents | Документы / версии |
| History | История |
| Bim | BIM |
| Capabilities | Возможности аналитики |
| Diagnostics | Диагностика |

Source: `ViewModels/InventoryWindowViewModel.cs` (Navigation collection)

---

## Build / deploy scripts (baseline)

- Build: `scripts/build.cmd` → `dist/PilotBim.Analytics.ext2.dll`
- Deploy DEV: `scripts/deploy-dev.ps1` → `%LOCALAPPDATA%\ASCON\Pilot-BIM\Development\PilotBim.Analytics\`

## Tests

None at Stage 0 baseline (test project arrives in Stage 2).

## Notes for later stages

- `_patch_backup/` is **kept** in this baseline commit; remove in Stage 1.
- `_sdk_reflection/` lives under `Plagin2/` (parent of this git root) and is **out of scope** for this repository.
- Stage 7 (UI string extraction) and Stage 8 (VM split) are deferred per plan confirmation.
