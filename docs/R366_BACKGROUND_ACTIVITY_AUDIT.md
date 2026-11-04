# R366 — DeskSpace background activity and idle-resource audit

Date: 2026-11-04  
Base: `feat/r364-tally-winman-universal-openwith` (commit `e935a55f76d60c59c0b4ee8d755505d5f1007d2e`)  
Working branch: `fix/r366-background-activity-audit`

## Observed on a Windows desktop

Resource Monitor showed `DeskSpace.exe` (PID 16792) at ~15.33% host CPU with ~716.1 MiB working set, 8.24 MiB/s process read I/O and 4.76 MiB/s process write I/O over 10 s. Resource Monitor independently showed ~5.9 MB/s writes dominated by `%LOCALAPPDATA%\DeskSpace\Search\filenames-v336.sqlite` and associated WAL. Process I/O counters include logical I/O, not just physical disk transfers. These observations isolate a major search-index workload; they do not prove the only CPU hotspot.

## Full checked-in source inventory

`r366/audit-background.ps1` enumerated **213 C# files** under `src/DeskSpace` on GitHub Actions Windows. Its regex inventory recorded:

| Code signature | Occurrences |
| --- | ---: |
| Timer constructions | 54 |
| File-system watcher constructions | 4 |
| Wait loops / delays | 74 |
| Directory traversals | 36 |
| Process/window enumeration calls | 8 |
| File writes / database SQL execution calls | 168 |

These are *static code occurrences*, not counts of running timers, per-second operations, confirmed bugs, or the complete generated/reconstructed shipping tree. Older versioned classes and user-initiated operations appear among them. The separate shipping search patch sources, including `r330/` and `r324/`, were also reviewed manually.

## Corrections prepared

Seven avoidable operations/patterns across two high-activity subsystems:

1. **Search watcher event duplication:** one pending state per normalized path, with latest changed/deleted state retained; 50,000 same-path notifications use one queue item.
2. **Search index path exclusion:** cache database path is computed without an unnecessary directory-creation call on each watcher event; the directory is created at initialization.
3. **Unchanged event SQL writes:** an unchanged `scan_epoch=0` entry no longer forces an UPDATE.
4. **Index overflow rescan churn:** repeated dirty-volume recovery requests are coalesced and rate-limited per root; no deletion of user files.
5. **Self indexing during full-drive crawl:** scanner skips DeskSpace's own SQLite cache and WAL directory, consistent with the existing watcher exclusion intent.
6. **Weather contrast styling:** freeze and reuse identical light/dark brushes and outlines instead of allocating and reassigning them each second.
7. **Widget host idle clock styling:** do not instantiate temporary brushes/shadows on an idle tick only to overwrite them; preserve the original visual effect during an actual update-progress overlay.

The whole-PC search scope, ranking, live file notifications, automatic updates, two-monitor widgets, Running Apps deck, drag-and-drop, Excel mappings, media, reminders, original source files and user data are intentionally left unchanged.

## Other recurring work reviewed without modification

- Whole-drive indexing on startup and actual overflow recovery is expensive but needed for full-PC filename coverage; no reliable user-machine steady-state measurement is available after this patch.
- Recent-file watchers do a bounded startup reconciliation and periodic recovery crawl. In a two-monitor session, instances may duplicate work; not modified because independently displayed recent-file suggestions must be preserved.
- Running Apps deck enumerates windows on a 5-second timer only when shown; its thumbnail/hover flyout has a separate short-lived timer.
- Clock/widget state, reminder scheduling, media playback, Excel availability, monitor connectivity and release checking have timers. Many are gated by active UI, display ownership or event changes. Static timer presence is not itself a fault.
- There is no evidence that a rewrite of these unrelated modules would preserve behavior. Further runtime tracing is needed before changing them.

## Verification and release gate

- Native Windows search-index regression: 45,000 synthetic indexed files, bounded heap and 120 repeated searches, 20,000 unique path notifications, 50,000 duplicate path notifications, search result accuracy.
- Full-source static inventory from Windows GitHub Actions, with a JSON artifact.
- Native Windows clock visual test: same colours, shadows, and weather-mode switching, with 10,000 repeated applications and reference reuse.
- Reconstructed/shipping WPF build and search/deck regression on the test branch; source patch must be applied and compiled into the shipping source, **not merely checked in under `src/`**.
- No public/stable installer, user-machine installation, or user-file migration is authorized by this audit. **Do not merge or publish until all tests pass and idle CPU/RAM/I/O are checked against the user's original baseline**.
- Recheck with both widgets visible and idle, after initial index completion, using Windows 10-second or longer process CPU/I/O measurements; also compare startup indexing separately from steady idle.

## Known limitations

This is a whole-source *static inventory* plus targeted source/Windows regression review, **not** a claim of having individually proved that every source method is optimal. The number of faulty runtime actions outside the two confirmed areas is not yet knowable from source alone. The initial idle CPU/disk benchmark must be remeasured on the user's actual machine after an opt-in tested release.
