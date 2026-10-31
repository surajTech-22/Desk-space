# R281 — P1 Workspace profiles: safe concurrent and corruption-resistant storage

This implements a concrete missing safety rule from the user's 31 October core priority plan (named Workspace profiles and transactional local storage) without adding another utility.

Previously `DeskSpaceWorkspaceProfilesV85.Save` and `ImportNewProfilesV201` both used `Load()`; that method deliberately catches malformed JSON and returns an empty list to keep the UI responsive. But mutation would then **silently overwrite all existing profiles**. Its `lock(Gate)` protected only the local process, not two independently started widget processes.

R281 preserves the existing `widget-profiles.json` schema, import/export controls and preview/apply behavior. It routes **only mutations** through a new `DeskSpaceProfileStoreV281`. Every Save or Import acquires a per-store OS mutex before re-reading the existing file, validates every profile, case-insensitive uniqueness and 24-item/256-KiB bounds, and refuses damaged records rather than recreating an empty file. It atomically replaces the validated profile data using an adjacent unique temporary file. Imports preserve names already present and retain a pre-import recovery copy. No-op imports do not rewrite anything.

Native Windows tests use real disposable files for simultaneous saves, import retaining prior entries, a stored backup, duplicate-case and malformed JSON refusal, no-op import, and independent OS mutex contention. They do **not** claim to test real two-PC cloud sync or physical multi-monitor layout restoration.

The R280 Smart Context integration, R278 monitor widget positions, and all earlier WPF tests must remain green in the consolidated CI. No Stable/Tester update until actual installed-PC verification.
