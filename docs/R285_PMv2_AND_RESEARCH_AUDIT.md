# R285 — P0 Per-Monitor V2 DPI awareness and actual DeskSpace process audit

## Source audit and diagnosis
The DeskSpace WPF executable previously had **no application manifest** configured in the checked-in project. Its monitor code already converts WPF DIPs to physical pixels and binds widget/Popup HWNDs to individual monitor work areas (R152, R153, R190, R195, R236, R278). Without explicitly declaring DPI awareness, Windows may treat a WPF app as **system-DPI-aware**, causing bitmap scaling and coordinate mismatches across different-scale monitors. Microsoft recommends Per-Monitor V2 for modern Windows desktop apps.

Official references:
- Microsoft Learn: *High DPI Desktop Application Development on Windows* — Per-Monitor V2 and WM_DPICHANGED.
- Microsoft Learn: *Application manifests* — dpiAwareness and its permonitorv2 value.
- Microsoft WPF Samples: *PerMonitorDPI/readme.md* — declare per-monitor DPI in app.manifest.

## Actual code changes
- `src/DeskSpace/app.manifest` declares PerMonitorV2,PerMonitor and legacy true/pm, with asInvoker and no elevated capabilities.
- `DeskSpace.csproj` embeds the manifest into the built **DeskSpace.exe**. No change to user themes, the 12-sector donut, mouse gestures or startup channel.
- `DeskSpaceDpiAuditV285` reports the actual Windows thread DPI awareness context and on-demand Screen.AllScreens bounds/effective scale. It is **read-only**, aside from its user-requested JSON report.
- `App.xaml.cs` recognizes `--dpi-audit` before creating the widget/tray and exits after writing a JSON diagnostic under Downloads; on failures it exits with nonzero status. No updater or recurrence is activated by this mode.
- The existing Settings Studio **Displays** page now provides a manual *Save actual monitor / DPI diagnostic to Downloads* button.

## Verification: actual EXE, not synthetic only
The Windows workflow must build the complete WPF application with embedded manifest, execute that built DeskSpace.exe with the --dpi-audit flag, read the resulting Downloads report and require **PerMonitorV2**. The real runner monitor-count/physical screen bounds are included in the non-release evidence artifact. The normal native WPF suite independently validates coarse classification, report structure, and monitor list. The deterministic source replay, full source parity, synthetic 100/125/150/200% matrix, existing updater guards, and independent build must remain green.

**A passing single-monitor CI runner is NOT physical mixed-DPI dual-monitor validation.** A real Windows PC still needs attach/detach, 100–200% screen DPI, stationary-hover previews, circle and Deck seam confinement, sleep/reconnect and normal updater restart evidence.

## Further opportunities from the actual code audit (prioritized)
1. **P0 / real updater gate:** Installed full supervisor shutdown, no-force/veto, 12-second heartbeat, failure rollback and preserved LocalAppData; not confirmable on a GitHub runner without an installed test machine.
2. **P0 / Excel COM:** Three independent real Microsoft Excel workbooks, including ListObject creation, formula/format fidelity, dates/text normalization and deliberately interrupted rollback. Do not claim completed from synthetic mocks.
3. **P0 / dual display:** Real negative-origin and mixed-DPI physical screens; use the new Downloads report to ground later physical acceptance and pinpoint incorrect scaling.
4. **P1 / UI accessibility:** Dynamically honor Windows high-contrast, keyboard focus and saved Light/System preferences throughout the modern Workspace XAML and legacy module dialogs. Existing modern settings alone do not guarantee that.
5. **P1 / running apps and previews:** Real Windows taskbar/app discovery, stagnant HWNDs, reliable stacked multi-window thumbnails, three-app counting, and monitor-local Deck hover.
6. **P1 / performance:** Instrument idle CPU, private bytes, HWND/thread count and event-driven search/media/monitor watchers over sustained real sessions, never heavy always-on telemetry.
7. **P2 (deferred):** User-triggered selected-region OCR, safe clipboard snippets, optional Everything IPC, more preview formats and guided, approved automations.

**Release policy:** Single signed, verified Stable + Tester update only after physical P0 gates; the R285 test and JSON report alone are not a production acceptance certificate.
