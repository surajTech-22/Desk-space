# R254 — read-only batch filename preview

DeskSpace Features now includes **Batch rename preview**. Users can select a folder and preview prefix and suffix changes for at most 128 visible, direct, non-system files. Extensions are retained. The plan fails closed for empty/no-op requests, invalid Windows characters, excessive names, duplicate source names, resulting name collisions and names that are already present. Hidden/system/reparse files and nested folders are not scanned.

No file is renamed, written, copied, moved, executed, deleted or sent over the network by this feature. The UI deliberately has no Apply button until a separately verified transaction, user confirmation and rollback engine exist. Native Windows tests use disposable files to verify exact proposed counts, collision detection, hidden/nested exclusion, and no source modifications. This moves FILE-01 forward without pretending that irreversible batch operations are ready.

Full UI/hardware acceptance and the consolidated release readiness checks remain mandatory.
