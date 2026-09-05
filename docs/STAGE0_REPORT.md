# PilotBim.Analytics — Stage 0A + 0B Report

**Date:** 2026-08-31  
**Status:** `READY_FOR_RUNTIME_INVENTORY` (plugin builds; project data requires Pilot runtime scan)

---

```
PILOTBIM ANALYTICS
STAGE 0A + 0B DATA INVENTORY REPORT

## ENVIRONMENT

Pilot version: 25.9.0.55929 (observed / SDK file version)
Target: Pilot-BIM desktop client
Architecture: x86 host (Ascon.Pilot.PilotBIM.exe), plugin AnyCPU (same packaging as RemarkTransfer)
Framework: .NET Framework 4.7.2
WPF: yes

## SDK ASSEMBLIES

Ascon.Pilot.SDK: 25.9.0.55929
  Path: C:\Program Files\ASCON\Pilot-BIM\Ascon.Pilot.SDK.dll
Ascon.Pilot.Bim.SDK: 25.9.0.18606
  Path: C:\Program Files\ASCON\Pilot-BIM\extensions\bim\Ascon.Pilot.Bim.SDK.dll
Ascon.Pilot.Bim.Search.SDK: present (referenced for GlobalId / model search)
  Path: C:\Program Files\ASCON\Pilot-BIM\extensions\bim\Ascon.Pilot.Bim.Search.SDK.dll

## SDK DISCOVERY

### Verified APIs (reflection of installed DLLs)

Objects / repository:
- IObjectsRepository.GetTypes / GetType / SubscribeTypes
- IObjectsRepository.SubscribeObjects
- IObjectsRepository.GetPerson / GetPeople / GetCurrentPerson
- IObjectsRepository.GetOrganisationUnit / GetOrganisationUnits
- IObjectsRepository.GetHistoryItems
- IObjectsRepository.GetCachedObject (often NotSupported in plugins — fallback SubscribeObjects)
- IObjectsRepository.GetRootObject (deprecated; still present)
- ObjectRepositoryExtensions.GetUserStates
- ObjectRepositoryExtensions.GetUserStateMachines

IDataObject:
- Id, ParentId, Created, Creator, Type, DisplayName
- Attributes (IDictionary)
- Children, TypesByChildren, Relations
- Files, ActualFileSnapshot, PreviousFileSnapshots
- ObjectStateInfo (State/Date/PersonId/PositionId)
- IsSecret, IsDeleted (deprecated), IsInRecycleBin (deprecated)
- State (DataState: Loaded/…)
- Access / Access2 / LockInfo / Subscribers

Types / attributes:
- IType: Id, Name, Title, Attributes, DisplayAttributes, Kind, HasFiles, IsService, IsDeleted, IsProject, Children, SvgIcon
- IAttribute: Name, Title, Type, IsObligatory, IsService, Configuration (prefer AttributeExtensions.Configuration2), Configuration metadata
- AttributeType: Integer, Double, DateTime, String, Decimal, Numerator, Array, UserState, OrgUnit, ElementBook, Inherited, Boolean

Persons / orgs / states:
- IPerson: Id, Login, DisplayName, ActualName, MainPosition, Positions, IsDeleted, CreatedUtc, …
- IOrganisationUnit: Id, Title, Children, IsPosition, IsChief, IsDeleted (NO ParentId property)
- OrganisationUnitExtensions.Person / Kind / …
- IUserState: Id, Name, Title, Color, IsDeleted
- IUserStateMachine / ITransition / ITransitionManager (possible transitions config)

Search:
- ISearchService.GetObjectQueryBuilder / Search / RunRefreshableSearch
- IQueryBuilder.Must / MustNot / MaxResults / WithTypeFilter / InContext / SortBy / AppendPlainQuery
- ObjectFields.TypeId / ParentId / CreatorId / CreatedDate / ObjectState / …
- AttributeFields.String/DateTime/Integer/OrgUnit/State/Bool
- ISearchResult.Result / Total / Kind

Files / versions:
- IFile: Id, Name, Size, Md5, Created, Modified, Accessed, Signatures
- IFilesSnapshot: Created, CreatorId, Reason, Files
- FileExtensions.CreatorId(IFile)

History:
- Ascon.Pilot.SDK.Data.IHistoryItem: Id, ObjectId, Reason, Created, CreatorId, Object
- DataObjectExtensions.HistoryItems(IDataObject) → ReadOnlyCollection<Guid>
- IObjectChangeHandler / IObjectChangeProcessor / Data.IChange (live change stream, not historical audit)

BIM:
- Ascon.Pilot.Bim.SDK.TypeNames: bim_coordinationModel, bim_modelPart, bim_modelPart_pointCloud, bim_modelRemarksFolder, bim_viewPointsFolder, bim_viewPoint
- IModelStorageProvider.GetStorage(Guid)
- IModelStorage: IsLoaded, ModelId, GetVersions, GetModelPartsIds, LoadRootElements, LoadElements, LoadElement, LoadChildElements, LoadElementProperties, …
- IModelElement / IModelElementId (ElementId, ModelPartId)
- IProperty / IPropertySet
- IModelSearchManager / IModelSearchService
- PropertyNames.GlobalId, GlobalIdReadable, ModelPartId, …
- Property.GlobalIdReadable / ModelPartId / …

Plugin host:
- IDataPlugin, IMenu<MainViewContext>, IMenu<ObjectsViewContext>
- IPilotServiceProvider.GetServices<T>()

### Not exposed / not verified

- IDataObject.Modified / LastModified → NOT_EXPOSED_BY_CURRENT_SDK
- Dedicated StateTransitionHistory / AuditTrail API → NOT_EXPOSED_BY_CURRENT_SDK
- IOrganisationUnit.ParentId → NOT_EXPOSED (infer via Children walk)
- Automatic Open/Closed UserState semantics → NOT_EXPOSED
- Full element count without loading → RUNTIME_VALIDATION_REQUIRED (search index preferred)
- bimObjectId as SDK member → NOT a BIM SDK type member; attribute name is config/runtime (known from other plugin usage)

### Critical analytics implications

| Need | Finding |
|---|---|
| Object counts by type | AVAILABLE via ISearchResult.Total |
| Created / Creator | AVAILABLE |
| Modified date | NOT_EXPOSED on IDataObject |
| UserState status | AVAILABLE via AttributeType.UserState attributes |
| Mean close time / SLA | BLOCKED / PARTIAL — no ClosedDate; transition history only possibly via IHistoryItem snapshots (runtime validate) |
| Document iterations | AVAILABLE via PreviousFileSnapshots |
| Object history | AVAILABLE via GetHistoryItems |
| BIM models/parts counts | AVAILABLE via search + TypeNames |
| BIM elements | AVAILABLE API; must sample/probe only |
| Remark→element | PARTIAL via bimObjectId attribute + GlobalId search |

## PROJECT SUMMARY

Types / objects / attributes / states / persons / orgs:
→ filled at runtime by command «Аналитика — источники данных» (not invented here)

## SYSTEM FIELDS (static capability matrix)

ObjectId: AVAILABLE — IDataObject.Id
TypeId: AVAILABLE — IDataObject.Type.Id / ObjectFields.TypeId
Parent: AVAILABLE — IDataObject.ParentId
State (UserState): PARTIAL — typed attributes, not fixed system prop
ObjectState: AVAILABLE — IDataObject.ObjectStateInfo.State
Creator: AVAILABLE — IDataObject.Creator
Created: AVAILABLE — IDataObject.Created
Modified: NOT_EXPOSED
Version: PARTIAL/AVAILABLE — ActualFileSnapshot + PreviousFileSnapshots; object history via GetHistoryItems
Files: AVAILABLE
Children: AVAILABLE
RelatedObjects: AVAILABLE — IDataObject.Relations

## DOCUMENT / VERSION CAPABILITIES

File metadata: AVAILABLE
Version history: AVAILABLE (file snapshots)
Version date: AVAILABLE (IFilesSnapshot.Created)
Version creator: AVAILABLE (IFilesSnapshot.CreatorId)

## HISTORY CAPABILITIES

Object history: AVAILABLE (GetHistoryItems / IHistoryItem)
State transition history (dedicated): NOT_EXPOSED_BY_CURRENT_SDK
Change author: AVAILABLE (IHistoryItem.CreatorId)
Change timestamp: AVAILABLE (IHistoryItem.Created)
Inference of UserState transitions from history: RUNTIME_VALIDATION_REQUIRED

## BIM CAPABILITIES

Models: AVAILABLE (TypeNames.CoordinationModel + search)
Model Parts: AVAILABLE (TypeNames.ModelPart + search / GetModelPartsIds)
Elements: AVAILABLE API (LoadElements) — do not full-scan automatically
bimObjectId: RUNTIME_VALIDATION_REQUIRED (object attribute)
GlobalId: AVAILABLE (PropertyNames.GlobalId / LoadElementProperties / search)
Element properties: AVAILABLE (LoadElementProperties)
Remark → element: PARTIAL
Element → model / model part: AVAILABLE

## ANALYTICS CAPABILITY MATRIX (pre-runtime, from SDK)

See plugin tab «Возможности аналитики» after scan. Highlights:
- Counts / by type / by creator / by created date → READY
- By UserState → READY after attribute discovery
- By responsible person → NEEDS_MAPPING (OrgUnit → Person)
- Mean close / reopen / SLA → BLOCKED or RUNTIME_VALIDATION_REQUIRED
- Document iterations → READY
- BIM model/part counts → READY
- Remarks/1000 elements → RUNTIME_VALIDATION_REQUIRED

## DATA QUALITY CAPABILITIES

Attribute fill / obligatory / empty: READY (sample-based)
Broken refs / BIM GlobalId gaps: PARTIAL (sample + runtime)
ModifiedDate completeness: BLOCKED (field absent)

## WARNINGS

- Full BIM element scan must never auto-run
- GetCachedObject / GetRootObject may be unsupported/deprecated in plugins
- IAttribute.Configuration deprecated → use Configuration2
- IsDeleted / IsInRecycleBin deprecated → use ObjectStateInfo
- Project-specific type inventory requires runtime

## ERRORS

- None at build time

## SDK MEMBERS USED

Listed in plugin report export after scan; discovery based on reflected members above only.

## SDK MEMBERS REQUIRING RUNTIME VALIDATION

- ISearchResult.Total accuracy
- GetHistoryItems content for UserState changes
- IModelStorage.LoadElements cost / IsLoaded behavior
- OrgUnit attribute value shape (int vs int[])
- bimObjectId presence on remark types

## RUNTIME NOTE (ISearchService null)

On this Pilot-BIM host, `ISearchService` often does not resolve via MEF/`GetServices` (zone previously ERROR). Object inventory then uses **hierarchy walk fallback**:

- Root: `IObjectsRepository.GetRootObject()` (obsolete) → else `SystemObjectIds.RootObjectId`
- BFS via `IDataObject.Children` + `SubscribeObjects`
- Visit budgets: Fast 2000 / Standard 25000 / Full 100000
- Search zone: **PARTIAL** (not ERROR) when walk runs
- BIM model/part counts: from walk type-name buckets when search is null

## PERFORMANCE NOTES

- Modes: FAST=50 / STANDARD=200 / FULL=10000 sample objects per type
- FULL requires explicit user confirmation
- Cancel supported
- Type sampling: search MaxResults + SubscribeObjects **or** hierarchy walk buckets
- BIM: models/parts via search or walk; elements probe ≤50, prefers LoadRootElements
- UI work on dispatcher; inventory Run on background Task

## FILES CREATED

- Plagin2/PilotBim.Analytics/ (solution, sources, scripts, docs)
- Reflection dumps: Plagin2/_sdk_reflection/
- Build output DLL below

## BUILD OUTPUT

DLL:
  c:\Users\Вячеслав\Desktop\Plagin2\PilotBim.Analytics\dist\PilotBim.Analytics.ext2.dll
Also:
  c:\Users\Вячеслав\Desktop\Plagin2\PilotBim.Analytics\src\PilotBim.Analytics\bin\Release\PilotBim.Analytics.ext2.dll

Build: SUCCESS (0 errors; obsolete-API warnings suppressed/avoided where possible)
Target: net472, AnyCPU, UseWPF, Private=false SDK refs

## FINAL STATUS

READY_FOR_RUNTIME_INVENTORY (hierarchy-walk fallback when ISearchService null)
```

---

## How to run Stage 0B in Pilot

1. Close Pilot-BIM if open.
2. Deploy DEV:
   ```powershell
   powershell -ExecutionPolicy Bypass -File "c:\Users\Вячеслав\Desktop\Plagin2\PilotBim.Analytics\scripts\deploy-dev.ps1"
   ```
3. Start Pilot-BIM, open a database.
4. Menu / context: **«Аналитика — источники данных»**
5. Choose **Стандарт** → **Сканировать** (do not auto-run Full).
6. Export: **Скопировать отчёт** / **Сохранить отчёт**  
   Reports: `%LOCALAPPDATA%\PilotBim.Analytics\Reports\`  
   Logs: `%LOCALAPPDATA%\PilotBim.Analytics\Logs\`

Production ZIP install is not performed automatically. Do not overwrite other plugins.

## Definition of Done checklist

| # | Item | Status |
|---|---|---|
| 1 | plugin builds | DONE |
| 2 | plugin loads in Pilot-BIM | AWAITING runtime |
| 3 | command exists | DONE (MainView + ObjectsView menus) |
| 4–14 | discovery features implemented | DONE (runtime fills real DB values) |
| 15 | no Pilot data modified | DONE (read-only; no IObjectModifier) |

**Stop here.** Wait for runtime inventory export before Stage 1.
