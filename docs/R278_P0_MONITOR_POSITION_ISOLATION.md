# R278 — P0 monitor widget position isolation

This work follows the user's 31 October feature plan: **P0 reliability and existing P1 workflows before P2 utilities**.

The real `DeskSpaceMonitorSessionsV154.SavePosition` was still storing the JSON position map using only a process-local `lock(FileGate)`. Two DeskSpace processes could read the old map concurrently, overwrite each other's saved display positions, and silently reset a corrupt map to empty before overwriting it. The Deck offsets already had a cross-process mutex (R235), but independent widget positions lacked the same protection.

R278 moves widget coordinates into `DeskSpaceMonitorPositionStoreV278` while keeping the existing `widget-independent-displays-v154.json` list format, filename, and monitor-name keys. Saves are guarded by a storage-keyed named OS mutex, always read the current map after acquiring it, validate every monitor identity, refuse malformed/oversized/duplicate-cased maps without modifying them, and replace the saved JSON atomically after updating only the selected monitor. Restore looks up the exact monitor identity; if no valid record exists it does not fall back to or clone another monitor's location. The native HWND monitor fence, separate Deck offsets, shared held files, and legacy primary position rules remain unchanged.

Native Windows CI exercises real stored files, independent negative-coordinate positions, concurrent writes for multiple monitor identities, named OS mutex contention, duplicate-case corruption rejection, and preservation of damaged saved maps. No physical multiple monitors are assumed or claimed; the separate physical mixed-DPI/seam test remains required before release.
