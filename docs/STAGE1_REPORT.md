# PilotBim.Analytics — Stage 1

**Version:** 0.2.0-stage1  
**Status:** `READY_FOR_RUNTIME_VALIDATION`

## Что сделано

Read-only окно **«Аналитика — обзор проекта»** (меню Pilot-BIM) с 9 срезами на основе данных Stage 0 inventory:

| Срез | Метрики (статус) |
|------|------------------|
| Сводка | объекты, типы, BIM, пользователи, fill % |
| По типам | READY — распределение по TypeId |
| По создателям | READY — CreatorId на сэмпле |
| По дате создания | READY — Created по месяцам (сэмпл) |
| По статусам | READY — UserState на сэмпле |
| Версии документов | READY — PreviousFileSnapshots |
| BIM | READY — модели / части |
| Качество данных | READY — fill rate, обязательные поля |
| Замечания к модели | NEEDS_MAPPING — типы issue/remark + bimObjectId fill |

## Ограничения (не трогаем в Stage 1)

- Нет графиков / dashboard
- Нет SLA, сроков закрытия, возвратов по переходам статусов
- Нет ModifiedDate
- Нет полного скана BIM-элементов
- Создатель / дата / UserState — только по сэмплированным объектам

## Как проверить

1. Закройте Pilot-BIM.
2. `powershell -ExecutionPolicy Bypass -File scripts\deploy-dev.ps1`
3. Откройте базу → **Аналитика — обзор проекта** → **Стандарт** → **Обновить**.

Каталог данных (Stage 0) остаётся в **Аналитика — источники данных**.
