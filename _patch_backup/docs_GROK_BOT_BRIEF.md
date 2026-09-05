# PilotBim.Analytics — бриф для Grok Bot

## Проект
- **Путь (локально):** `c:\Users\Вячеслав\Desktop\Plagin2\PilotBim.Analytics`
- **Стек:** C# / .NET Framework 4.7.2 / WPF / MEF plugin для Pilot-BIM
- **Сборка:** `scripts\build.cmd` → `dist\PilotBim.Analytics.ext2.dll`
- **Deploy:** закрыть Pilot → `scripts\deploy-dev.ps1`
- **Логи:** `%LOCALAPPDATA%\PilotBim.Analytics\Logs\analytics.log`
- **Текущая версия:** 0.5.1

## Правила
1. Только **read-only** относительно данных Pilot (не менять карточки/модели в Pilot).
2. Не коммитить секреты, не трогать git config.
3. BIM-элементы считать через **IModelSearchService** (индекс), не через полный LoadElements без viewer.
4. UI: явные цвета текста/фона таблиц (тема Pilot ломает контраст).
5. После изменений — build; deploy только если Pilot закрыт.

## Что уже есть
- Каталог данных + Обзор проекта
- BIM index analytics (модели/части/IFC)
- CSV export, графики, фильтр по модели
- Remarks → bimObjectId → GlobalId, KPI remarks/1000
- UserState OPEN/CLOSED (эвристика + state-mapping.json)

## Бэклог (приоритет)
1. Runtime-проверка v0.5.1 на живом проекте (BIM counts > 0, скан не зависает).
2. RemarkTransfer «Ранее перенесено» после удаления копии на цели (LiveTargetReconciler / ledger) — соседний проект `Plagin1\PilotBim.RemarkTransfer`.
3. OrgUnit → Person (ФИО ответственного).
4. Сравнение двух scan'ов во времени.
5. SLA / сроки закрытия — только если SDK даст историю переходов.

## Как работать двум ботам
- **Analytics Dev** — пишет код, правит баги, готовит diff/PR.
- **Analytics Review** — ревьюит изменения Dev, проверяет build/риски, не дублирует фичи без нужды.
- Общайтесь в общем треде; передавайте ownership явно («Dev: сделай X» / «Review: проверь Y»).
