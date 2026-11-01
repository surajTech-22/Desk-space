# DeskSpace unified implementation flow — 31 October 2026

**Decision:** Make DeskSpace a context-aware, local-first Windows productivity workspace. Keep the widget/donut lightweight; every display owns its own UI state while approved business data is shared. Preserve previous workflows, captured Excel content, notes, mapping libraries, pins, reminders, held Deck files and user configuration.

## Operational contract
1. Implement on a dedicated development branch, with exact-source patching, reproducible hashes and a draft PR. Never publish an unverified build as "finished".
2. Every push runs Windows x64 compilation, WPF smoke, native integration regression, packaging checks, checksum validation and negative safety tests. Tests use disposable files in Downloads; avoid real user content.
3. **Release decision**: `docs/CONSOLIDATED_RELEASE_READINESS.json` plus `r204/require-consolidated-ready.ps1` are the hard gate. **All** checkpoints must be `verified`, with nonempty evidence and `release_status=ready`. A green CI run is necessary but not sufficient. Real mixed-DPI dual-monitor, real Excel/Office, and installed-PC updater/rollback acceptance require actual evidence.
4. After a verified candidate, publish Stable and Tester from **one exact SHA-256-verified installer**, with independent hashes and release/event receipt checks. Preserve channel selection and data. No old-client retry storms. Do not advertise new capabilities until confirmed installed and working.
5. Never auto-close unrelated applications, bypass unsaved-work cancellation, force-kill user processes, auto-fill passwords, silently modify Excel/file data or copy sensitive clipboard contents.
6. For test failures: retain changes on the development PR, record the failing step and correct before repeating CI. Do not label development changes as released.
7. GitHub Actions executes tests after pushes; it does **not** imply autonomous ongoing development between user requests.

## Workstream A — Stability & multi-monitor (P0)
- One widget per monitor, independent donut/Deck/hover/focus/expanded state and independent persisted positions.
- Monitor-device identity through disconnect/reconnect; negative desktop origins; per-monitor DPI and physical coordinate fencing; no magnetic snap at seams; explicit opt-in dock.
- Bound all popups (Information, Search, media, app thumbnails, Deck) to originating monitor; recover orphaned popup positions.
- Make app startup/tray/sleep-resume/recovery safe; user-data-preserving crash loop recovery.
- Synchronize shared state across processes; optional read-only diagnostics and CPU/RAM/GPU/handle budgets; event-based refresh.
- Consolidate one authoritative source tree with a verified independent build; remove reconstruction only after comparison.
- Release supervisor: verified full-install snapshot, coordinated normal WPF exit (unsaved-work veto honored), atomic install, stability heartbeat, full rollback, old client compatibility, signed-release policy.

## Workstream B — Existing Deck and applications (P1)
- Correct multi-window grouping: windows before alphabetical order; place thumbnails up/down/left/right rather than collapsing into center.
- Stationary-cursor live preview; action to activate/minimize selected window, close and move with explicit intent.
- Pinned apps and their recent file paths, with opt-in retention and access checks.
- Shared staged held-file list, but separate monitor-local Deck coordinates and scroll/focus states.
- Folder drag: **pre-drop** Zip/Copy targets in circle; cancellable, bounded, recoverable ZIP/copy; per-file checksum, duplicate and keep-both/skip with no source deletion.
- Read-only bounded previews for PDF, Office, image, WAV, archives, text/code; no macros or unsafe extensions.
- Media deck integration: play/pause, mute, close panel, track info when supported by source.
- Consistent settings, icons, context menus, small delete control, cleanup of temporary files.

## Workstream C — Excel capture, mapping & paste (P0/P1)
- Keep ONE module with **Captured Sheet** and **Mappings** tabs. Preserve both original copy routes; Information and Mapping have distinct libraries and paste rules. Top action placement and single saved mapping list retained; group naming and navigation restored.
- Capture exact range, rich-cell formatting, underline, number/date types, text vs number, tables/ListObjects, formulas via Excel Formula2, metadata and supported structural details. Label unsupported properties "Not captured".
- Preview a real-looking selected range, inline expandable formulas, 100% zoom, no invented zeros for blank cells.
- Entire destination guard: only fully empty or fully compatible populated source layout, including structural blanks; **reject partial** and mismatched data everywhere. No blind subset pastes.
- Separate Information header/value matching and Formats+Formulas pasting; no header prompt for format route; preserve business data; correctly handle formulas and real tables.
- Back up, apply, verify readback for every supported property, rollback on any incomplete result; no VBA/macros or key replay.
- Suggest saved mappings when current Excel workbook/range has a VERIFIED exact signature. Suggestions are read-only and never auto-apply.
- Native Office acceptance with three real but privacy-safe workbooks + negative cases.

## Workstream D — Intelligent context deck (P1)
- First stage: user-invoked current-application classification and read-only recommended **existing** tools; no window titles, document content, passwords, background capture or automatic actions.
- Next stage: user-enabled, event-driven active-app state; provide scoped suggestions for Excel, browser, Explorer, document apps and media. Show the basis for each suggestion and easy dismiss.
- Strict allowlists, permission screens, context tests, accessibility and CPU budget.

## Workstream E — Search, clipboard, OCR and files (P2)
- Universal command palette, progressive search, optional Everything IPC, exact file/setting navigation and reliable keyboard actions.
- Advanced clipboard: explicit-history opt-in, format/JSON/text conversion, privacy exclusion, no passive secret retention.
- Local screen OCR on user-selected rectangle with verification and clear copy-to-note/Excel behavior.
- Smart Drop for URLs, screenshots, PDFs, image actions, file-to-note; never automatic destructive transformations.
- File tools: batch rename/dry-run, dedup SHA-256, compare, ZIP/unzip recovery, QR, colour picker, clipboard snippets, quick text tools.

## Workstream F — Workspace, reminders, Vault and automations (P2/P3)
- Named Workspace profiles: app layout by monitor, disconnected-monitor preflight, opt-in restore, no automatic app launch.
- Reminders: snooze, repeat, history, actionable Windows notifications.
- Information Vault: Windows-protected secrets, exact HTTPS origin matching, unlock-gated reveal/copy; no automatic plaintext fill; redact diagnostics.
- Visual no-code workflow builder with explicit allowlisted steps, dry-run, audit, cancellation and undo/backup.
- Optional integrations (e.g. local AAH status) use permissioned endpoints and do not silently transfer confidential financial data.

## Workstream G — Accessibility, UI, performance (cross-cutting)
- Keyboard-only donut arcs, focus order, screen-reader names, contrast/reduced motion, per-monitor display scale.
- Proper panel anchoring, hover-dismiss boundaries and layout pixel-regression screenshots.
- Idle CPU/RAM/GPU budgets, startup latency, memory leaks, exception/cancellation handling, zero unnecessary foreground interruption.

## Relative priority and tests
| Order | Workstream | Test gate |
|---|---|---|
| 0 | Stability, monitor isolation, full update rollback, source migration | Native Windows, physical mixed-DPI pair, installed second PC |
| 1 | Complete Excel, Deck, running apps, media, settings | WPF tests, real Office readback, source file/data unchanged |
| 2 | Context suggestions, Search, clipboard and preview safety | Event-latency, permissions, read-only and privacy negative tests |
| 3 | New file/OCR/workspace/Vault/automation | Security and accessibility audit, explicit user approvals |
| 4 | Stable + Tester coordinated release | Every manifest gate verified, same binary SHA, install/rollback evidence |

## Exact progress recording
Use `docs/FEATURE_EXPANSION_BACKLOG.json` for planning status; **planning status never implies release acceptance**. The authoritative shipping decision remains `docs/CONSOLIDATED_RELEASE_READINESS.json`. Record source commits, relevant CI run URLs, native test evidence and real-PC tests per checkpoint. Keep unfinished items pending. Prior verified Windows CI alone is not physical acceptance.

### Starting increment
R236: optional, user-invoked Smart Context suggestions inside existing Features interface. Its first version only identifies the *foreground application category*, never document names or content; it only offers links to existing permitted DeskSpace tools. R237+: incremental fixes under separate acceptance, not an automatic promise of every feature in one session.
