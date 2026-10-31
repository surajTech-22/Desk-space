# DeskSpace — P0/P1 Execution Matrix
**Source:** user-supplied *Complete Feature Research & Expansion Plan*, 31 October 2026.  
**Branch:** `feat/r201-consolidated-roadmap-bundle`, draft PR #79.  
**Rule:** Implemented ≠ native Windows CI tested ≠ physical acceptance ≠ released. **No P2/P3 feature work ahead of P0/P1 unless it blocks a P0/P1 requirement.**

## P0 — reliability and correctness (do first)

| Requirement from plan | Actual repository status | Required next evidence |
|---|---|---|
| Independent widgets and their positions | R278 replaced process-local and corrupt-map-overwriting widget position save with monitor-keyed, cross-process mutex, strict JSON validation, atomic writes. Windows CI #530/#540 PASS. R228/R235 already handle independent Deck offsets and shared held-file store | Physical mixed-DPI two-screen hover, widget moving, saved offsets across restart, simultaneous updates |
| Monitor identity, docking, inward popup and reconnect | R152/R153, R190, R214, R231/R236, R247, R264 have native HWND monitor fences, per-monitor deck geometry, resume reconciliation and synthetic topology tests | Real attach/detach and sleep/reconnect; prove separate widgets never attract and popup/flyouts do not cross screens |
| Safe updates, all-process graceful closure and rollback | R219–R230, R249/R250 provide staged download, no-force WM_CLOSE, installer checks, synthetic full rollback and candidate-hash/provenance policies | Installed primary+satellite+tray update with actual old/new app processes, heartbeat/failed-update rollback, preserved data; valid publisher certificate |
| Authoritative source and repeatable Windows build | R267 committed `src/DeskSpace` and requires exact regenerated source SHA parity, independent build and a verified archive. CI #540 PASS | Final signed build and identical Stable/Tester deliverables after acceptance |
| Strict Excel paste transaction | R107/R139–R147, R242/R251/R252/R266/R268 implement full-range gate, formula+format transaction, SaveCopyAs receipt, formula readback/rollback structure | Three real Windows Microsoft Excel workbooks with COM, tables, formulas, dates and deliberate failure/rollback. No Office installation in current GH runner |

## P1 — finish existing modules before adding utilities

| Requirement from plan | Actual source status | Required next work/evidence |
|---|---|---|
| Captured Sheet + Mappings, separate paste engines | R126 already has exactly two unified tabs and routes Information vs Formats/Formulas separately. This is **implemented code**, not an unstarted feature | Actual Windows Excel UI origin selection, copy/format/formula/table/blank-values, source library and save/delete behavior |
| Running Apps, grouped thumbnails, quick controls | R261 grouped order; R93 stationary hover and monitor-side flyout; R279 added explicit per-window Minimize/Restore controls alongside normal Close and exact stale-HWND title guards. Native CI #540 PASS | Real multi-window Explorer/Office selection, focus/minimize/restore/close and dual-monitor positions |
| Shared Deck/media/files | R210/R211 staged file ZIP integrity and duplication; R243/R272 media Deck controls and reopen | Media session on user's PC, two widgets showing shared held data while deck placement is independent, real Explorer drag |
| Active-app Smart Context Deck | R237 event-driven process-category suggestions exist as **a separate opt-in panel**. R263 Excel exact-signature suggestions exist in the Deck | Missing real unified Deck presentation for approved context actions; explicit opt-in and stop/wipe/privacy audit, no auto-paste |
| Pinned apps recent documents | R262 per-app recent-document choices + click-time revalidation exist | Real Windows Recent/Office/Explorer validation |
| Settings stability and keyboard navigation | R255 guarded Features→Settings, R207 keyboard donut, R246 help, R208 contrast | Real settings failure cases and keyboard/screen-reader navigation for every donut level, multi-DPI |
| Search and indexing | R269 shared progressive multi-drive index, R259 palette facets, R260 index health | Real multi-drive end-to-end latency, cancel/stale searches, optional native Everything IPC |
| Workspace layouts | R179 and R257 existing capture/review/restore safety | Physical multi-monitor Save/Restore and missing apps/monitor reconnection |

## P2 / P3 deliberately **deferred**
The plan places OCR, extra clipboard tools, batch rename additions, vault expansion, visual workflow builders and optional AAH connectors *after* P0 and the unfinished P1 workflows. R277 already provides durable rename recovery in development; do not continue refining it at the expense of core reliability.

## Release gate
- Synthetic GitHub Windows CI proves a **build** and the tested file/WPF scenarios, not physical dual-monitor devices, installed Microsoft Office, real audio/Explorer, or a production updater.
- Neither a code-signing identity nor physical machine control can be inferred from a successful GitHub Actions run.
- Preserve the **single consolidated release** for Stable and Tester after every required test passes; until then branch/PR-only code with no claimed auto-update delivery.
- When resuming work, advance a P0 or unimplemented P1 item and attach a test to its real module. Do not mark a roadmap item **released** merely because its code exists.
