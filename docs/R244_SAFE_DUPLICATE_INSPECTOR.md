# R244 — on-demand local duplicate-file inspector

This adds a **File operations** feature to the existing DeskSpace Features tab, with **Duplicate-file inspector**. Nothing is scanned unless the user selects a folder. It inspects only visible, non-system **direct child files** (no directory recursion, no reparse-point traversal), up to 128 files, each at most 64 MiB, and at most 256 MiB total. Only local streaming SHA-256 fingerprints are calculated. Matching file names and a truncated hash are shown; full folder paths and file contents are not shown or saved. Cancellation is supported. The inspector has no delete, move, rename, upload, clipboard, background watcher, or permanent data store.

The scan checks file budgets before reading. It rejects inaccessible/invalid folders, size-limit violations, and files changing length during reads. Hash equality indicates matching SHA-256; it does not itself authorize deletion. The UI exposes only user-initiated scans and Cancel.

Acceptance: Windows native WPF fixture tests create duplicate, distinct, hidden, nested, and oversized files; validate exact groups, source SHA unchanged, cancellation, preflight file-count ceiling, no recursion and Features visibility. Simulated CI cannot prove every real filesystem ACL/NTFS edge, so physical-device acceptance is still required.

**Publication stays blocked** by the consolidated release readiness manifest; this is incremental P2 FILE-01 work, not proof that full batch rename/transform tools have been completed.
