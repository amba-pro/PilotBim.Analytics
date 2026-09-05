# PilotBim.Analytics — статус

## Версия
**0.9.0** (`0.9.0-rich-diff-to-dashboard`)

## Стек
C# / .NET 4.7.2 / WPF / MEF → Pilot-BIM  
Сборка: `scripts\build.cmd` · Deploy: закрыть Pilot → `scripts\deploy-dev.ps1`  
Логи: `%LOCALAPPDATA%\PilotBim.Analytics\Logs\analytics.log`

## Правила
1. Read-only к данным Pilot.
2. BIM-элементы — через `IModelSearchService` (индекс).
3. UI: явные цвета таблиц (тема Pilot).
4. Deploy только при закрытом Pilot.

## Готово
| Версия | Что |
|--------|-----|
| 0.6.0 | Конструктор одного графика |
| 0.7.0 | Мультивиджетный дашборд |
| 0.8.0 | Именованная история сканов |
| **0.9.0** | **«На дашборд»** из конструктора; diff типов / OPEN-CLOSED / IFC / замечаний; фильтр «Только изменения» |

## Smoke 0.9.0
1. **Обновить** → Конструктор → настроить график → **На дашборд** → Сводка показывает виджет  
2. **Обновить** ещё раз → Сравнение сканов: строки Типы / IFC / OPEN-CLOSED / Замечания  
3. Чекбокс **Только изменения** сужает таблицу  
4. Старые снимки без новых полей — diff по ним частичный (без ошибки)  

## Бэклог
1. RemarkTransfer «Ранее перенесено» — Plagin1 (Classify null уже → NotFound; проверить smoke).  
2. SLA — Blocked (нет history transitions в SDK).  
3. Drag-drop / свободный resize сетки дашборда.
