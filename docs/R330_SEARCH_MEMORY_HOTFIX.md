# DeskSpace R330 — P0 memory regression hotfix

**Observed on user's actual Windows desktop:** 2 November 2026 screenshot of Task Manager shows **DeskSpace approximately 1,037.9 MB** and CPU approximately 3.9%, while the circular widgets and taskbar are visible on two monitors.

## Root cause

The all-drive filename crawl retains every discovered path in `DeskSpaceSearchIndexV13.Entries`, a process-wide `List<Entry>`. Each entry contains a duplicated display filename and full path plus object/list overhead. The source also retains every filesystem change in `LivePaths` and `LiveFoldersV324`. The implementation grows with all files on all drives and rescans, and per-query scans allocate/inspect those entries. This is not a bounded cache.

## Hotfix implementation

R330 stores the whole filename catalog in a local SQLite FTS5 trigram database under the existing user's `%LOCALAPPDATA%\DeskSpace\Search`, preserving full file-name coverage. It has 384-row scan transactions, max 640 DB matches per drive/category, bounded 4,096-event queue, resumable scan-epoch cleanup, no full-volume path inventory in managed heap, and maintains read-only file search with Windows permissions. Historic Search, Deck, updater and multi-monitor code remain present.

The disk index is a **rebuildable local cache**, not a substitute for original files. No user documents are deleted, uploaded or modified. The first scan may use disk and CPU; any real-device memory target must be measured after install, not inferred from CI.

## Release decision and exception

This is a P0 **performance/reliability incident**, **not a new feature** and **not a counted R329 capability**. The explicit user instruction to solve the observed ~1 GB DeskSpace memory problem first supersedes feature expansion work. Under [R329 ten-feature release policy](R329_TEN_FEATURE_BATCH_POLICY.md), a separately documented emergency data-safety/reliability hotfix may be considered after full native Windows regressions, RAM stress tests, tester and stable checksum parity. Routine 2–3-feature releases remain prohibited; the R329 10-feature batch is unchanged.

### Acceptance evidence required before publication

- Build exact signed/unsigned-provenance verified shipping source with R330 applied.
- 45,000 synthetic filenames indexed with below-85 MiB sustained managed heap increase on CI runner.
- 120 repeated indexed searches below 35 MiB retained heap growth.
- Actual deep filename search and both ordinary and hidden accessible files maintained.
- Watcher event flood never exceeds 4,096 pending entries.
- Previous R324 all-drive, R326 search filter, R328 text compare, Deck and updater regressions PASS.
- Stable/Tester installer SHA-256 parity and original user data preservation.
- **Physical PC check still required:** installed version and stabilized Task Manager memory after full indexing, on both monitors.
