# DeskSpace v1.5.1098 recovery hotfix — 2026-10-30

The user supplied a real failing log from v1.5.1097. Failure included rapid repeated installer supervisor handoffs, mutex contention, widget relaunch and missing primary WPF Loaded receipt.

**R227 changes:** (1) loser supervisor exits without fallback widget relaunch; (2) app preflights the shared supervisor mutex; (3) matching target-executable host detection; (4) atomic cross-process release-hint suppression for 8 minutes so ntfy's cached hourly replay cannot spawn repeated installers; (5) safe bounded receipt failure logs preserving rollback snapshots.

**Verification prerequisite:** Windows Actions run 41037745780 passed on commit 19ca1eda061767da66ee55225fd3cf74c553b202. This includes real Windows mutex-contention test of the actual supervisor, synthetic full-install rollback, SHA256 checks, native WPF tests, independent source rebuild.

The next release uses the exact SHA-256-verified source artifact, stamps a higher app version, and publishes identical installers to Stable and Tester. **Do not emit an immediate release event** to older clients stuck in a restart loop. The previously installed application cannot acquire new code until a safe update is completed; if the old install remains blocked, the user can close DeskSpace windows/tray normally and use the signed-by-GitHub-publication (SHA256-verified, not Authenticode-signed) official installer manually once. Do not forcibly kill users' apps. Real-PC confirmation remains outstanding.

The final roadmap release gate stays unaltered; pending Excel and multi-monitor physical tests are not marked complete.
