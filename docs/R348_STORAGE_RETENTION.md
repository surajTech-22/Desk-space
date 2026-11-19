# R348 — verified post-update DeskSpace storage retention

This is future-update code, **not** a manual cleanup command and **not** a stable release. It is applied to the source that ships the independent update supervisor by `r348/apply-r348.ps1`.

## Why

The user-provided November 3, 2026 full C: audit reported about **6.12 GiB** in the old `%LOCALAPPDATA%\DeskSpace\Search\filenames-v330.sqlite`, and about **4.40 GiB** in `%LOCALAPPDATA%\DeskSpace\Updates\Rollback`. Exact reclaimed space depends on retention and file locks.

## Execution gate

Only the *already successful* updater supervisor runs R348, after the installed new app returns a matching transaction/version/PID readiness receipt. Errors are contained in a try/catch and logged to `%LOCALAPPDATA%\DeskSpace\update-status.log`, and cannot fail installation or trigger rollback.

- Refuse paths outside exactly `%LOCALAPPDATA%\DeskSpace\Updates` and `%LOCALAPPDATA%\DeskSpace\Search`.
- Refuse when `Updates\PostInstall\pending.json` exists.
- Never touch the running `filenames-v336.sqlite`, its WAL/SHM, or any arbitrary files in the Search directory.
- Old `filenames-v330.sqlite` plus its WAL/SHM/journal are candidates only when v336 has a valid SQLite header, **every** running DeskSpace process is from the newly installed executable, and the whole legacy file family passes exclusive-open checks. If any check fails, leave all legacy files untouched. SQLite caches are rebuildable, not user documents.
- Only prior GUID-named snapshots created by the verified R185 updater are candidates. Require `snapshot\DeskSpace.exe`, JSON SHA manifest, no reparse points; never delete `failed-install` quarantines, current transaction, or unrecognized directories. Retain the three newest verified *prior* snapshots; protect anything younger than 48 hours. The current transaction's snapshot is retained in addition to the three.
- No touching backups outside DeskSpace, personal files, app preferences, mapping files, Excel workbooks, AAH data, or manual builds in Downloads.
- Preview mode and exact reclaimed-byte totals are available for automated tests.

## Verification

`r348/test-r348.ps1` uses synthetic directories and SQLite files only. It covers pending transactions, preview/non-mutation, valid/invalid paths, multiple running process variants, locked old index, current v336 preservation, failed install retention, exact byte counts, and repeat invocation. `.github/workflows/test-r348-storage-retention.yml` runs it under Windows PowerShell 5.1 and verifies the assembled updater hook syntax. The full R339 Windows regression workflow now incorporates the shipping source patch and tests.

The R329 ten-feature release gate remains unchanged: no stable/tester release until all release requirements and hardware acceptance pass.
