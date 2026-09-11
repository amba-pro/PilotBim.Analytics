# Pilot-BIM Runtime Validation Checklist

Plugin: `PilotBim.Analytics.ext2.dll`  
Build under test: Stage 10 / HEAD after Stage 9.3 (`489a151` or later Stage 10 docs commit)  
Deploy target (dev): `%LOCALAPPDATA%\ASCON\Pilot-BIM\Development\PilotBim.Analytics\`  
Log file: `%LOCALAPPDATA%\PilotBim.Analytics\Logs\analytics.log`  
Deploy helper: `scripts\deploy-dev.ps1` (close Pilot-BIM first)

**Verdict options per item:** `PASS` | `FAIL` | `NOT_REPRODUCED` | `SKIPPED`

Mark FAIL only with evidence (screenshot, log excerpt, steps).  
Do not treat `NOT_REPRODUCED` as PASS.

---

## Preflight

| Step | Check | Result | Notes |
|------|-------|--------|-------|
| P1 | Close Pilot-BIM | | |
| P2 | `scripts\build.cmd` succeeds (0 errors / 0 warnings) | | |
| P3 | `scripts\test.cmd` → 138 PASS | | |
| P4 | `dist\PilotBim.Analytics.ext2.dll` exists (only that file in `dist`) | | |
| P5 | Deploy via `scripts\deploy-dev.ps1` | | |
| P6 | Note InformationalVersion from deploy output / log | | Expected product version string includes `0.9.0-rich-diff-to-dashboard` until metadata bump |

---

## Group 1 — Plugin load

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 1.1 | Pilot-BIM starts | | |
| 1.2 | Plugin loads without MEF composition error | | Log: `area=plugin load` |
| 1.3 | No missing-dependency / FileNotFound for Ascon.* SDK | | Host must provide SDK; plugin must not ship SDK DLLs |
| 1.4 | No duplicate plugin instance / double menus | | One Analytics catalog + overview entry each |
| 1.5 | Main menu / toolbar / context menu items appear | | Catalog + Overview |
| 1.6 | Analytics (overview) opens | | |
| 1.7 | Inventory (catalog) opens | | |

**Log to inspect:** `plugin load`, `command-service-ctor`, `*-menu-build`, `toolbar-build`

---

## Group 2 — Initial Analytics open

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 2.1 | AnalyticsWindow opens | | |
| 2.2 | No unhandled UI exception | | |
| 2.3 | Localized chrome/resources display (no raw key names) | | |
| 2.4 | Dashboard renders | | |
| 2.5 | Charts render | | |
| 2.6 | Navigation switches panels | | |
| 2.7 | No clipping / blocking UI regression vs prior build | | |
| 2.8 | No missing resource strings in chrome | | |

---

## Group 3 — Inventory scan (real project)

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 3.1 | Start Inventory scan | | |
| 3.2 | Progress updates | | |
| 3.3 | Completes | | |
| 3.4 | Type counts plausible for project | | |
| 3.5 | Document samples populated when docs exist | | |
| 3.6 | Creator / responsible aggregates populated when data exists | | |
| 3.7 | Timed-out types show Partial/timeout warning — **not** Available | | Stage 4/9 correctness |
| 3.8 | No duplicate completion symptoms (double trees / double rows) | | Stage 9.3 TD-14 |
| 3.9 | Log clean of unexpected ERROR spam | | |

**Log to inspect:** `progress`, `sample-type`, `inventory`, zone errors

---

## Group 4 — Repeated scan (same window)

Critical after Stage 6 buffer lifecycle + Stage 9 callback hardening.

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 4.1 | Perform scan A; record key counts / samples | | |
| 4.2 | Perform scan B in the **same** window | | |
| 4.3 | No stale DocumentSamples from A | | |
| 4.4 | No stale creator/responsible entries from A | | |
| 4.5 | No duplicate rows attributable to A∪B merge | | |
| 4.6 | No evidence of old callback mutation after B finishes | | |
| 4.7 | Visible result belongs only to scan B | | |

---

## Group 5 — Cancellation

Critical after Stage 9.2 (`ScanSessionScope`).

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 5.1 | Start scan | | |
| 5.2 | Cancel scan | | |
| 5.3 | UI remains responsive | | |
| 5.4 | No error dialog caused **solely** by cancellation | | |
| 5.5 | Start second scan | | |
| 5.6 | Second scan completes normally | | |
| 5.7 | No `ObjectDisposedException` in UI or log | | |
| 5.8 | No orphan callback corruption of UI/report | | |

---

## Group 6 — Window close during scan

Critical TD-22 verification.

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 6.1 | Start scan | | |
| 6.2 | Close window while scan active | | |
| 6.3 | Window closes immediately | | |
| 6.4 | No crash of Pilot-BIM | | |
| 6.5 | No orphan scan continues mutating closed UI | | |
| 6.6 | Reopen window | | |
| 6.7 | New scan works | | |
| 6.8 | Logs show no disposal/cancellation exception storm | | |

---

## Group 7 — Timeout / late callback

Use a large/slow project if available. If not practical: `NOT_REPRODUCED`.

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 7.1 | Trigger long sample/search callback | | |
| 7.2 | Timeout status correct (Partial + warning) | | |
| 7.3 | Late callback does not mutate finalized report for that type | | Stage 9.3 TD-25 |
| 7.4 | Subsequent scan remains clean | | |

---

## Group 8 — Analytics data

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 8.1 | Project analytics loads | | |
| 8.2 | KPI values plausible | | |
| 8.3 | Chart data plausible | | |
| 8.4 | Remark counts plausible | | |
| 8.5 | OPEN/CLOSED values correct vs project | | dual-use strings — do not “fix” |
| 8.6 | Responsible data correct | | |
| 8.7 | BIM filter behaves as before | | |
| 8.8 | No duplicated/rebuilt visual corruption | | |

---

## Group 9 — Scan snapshots

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 9.1 | Save snapshot | | |
| 9.2 | Reload snapshot | | |
| 9.3 | Compare snapshots | | |
| 9.4 | Changes-only filter works | | |
| 9.5 | Delete snapshot | | |
| 9.6 | Restart window | | |
| 9.7 | History remains consistent | | |

---

## Group 10 — CSV export

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 10.1 | Export analytics CSV | | |
| 10.2 | File created | | |
| 10.3 | Headers unchanged vs known good | | |
| 10.4 | Numeric decimal format as expected (invariant export path) | | |
| 10.5 | Encoding correct for target workflow | | UTF-8 |
| 10.6 | File opens in target workflow | | |

---

## Group 11 — Logging

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 11.1 | `analytics.log` created under Logs path | | |
| 11.2 | Scan operation identifiable by area/messages | | |
| 11.3 | Timeout/error identifiable | | WARNING/ERROR + area |
| 11.4 | Exceptions contain useful type/message context | | |
| 11.5 | Log path writable | | |
| 11.6 | No excessive unexpected spam | | TD-03: file grows unbounded — watch size |

---

## Group 12 — Regression smoke

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| 12.1 | Close/reopen Analytics repeatedly | | |
| 12.2 | Close/reopen Inventory repeatedly | | |
| 12.3 | Scan multiple times | | |
| 12.4 | Navigate all main sections | | |
| 12.5 | Chart tabs render | | |
| 12.6 | Dashboard widgets render | | |
| 12.7 | Dialogs OK/Cancel work | | |
| 12.8 | No new WPF binding errors in diagnostics | | |

---

## Sign-off

| Field | Value |
|-------|-------|
| Tester | |
| Pilot-BIM version | |
| Plugin InformationalVersion | |
| Project used | |
| Date | |
| Overall | `PASS` / `FAIL` / `PARTIAL` |
| Blockers found | |
| Log excerpt path | |

**Expected Stage 10 gate:** checklist executed → feed results into release decision.  
Automated suite alone does **not** prove `PRODUCTION_READY`.
