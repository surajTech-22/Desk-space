# R260 — aggregate local search index health

DeskSpace already maintains a progressive multi-drive filename index in its own process. R260 adds a read-only **Search index health** window under Workspace Features and in the Action Palette. It shows only aggregate numbers: tracked volumes, fully scanned volumes and total indexed **names**. It never displays file paths, filenames, document contents, search terms, raw registry entries or credentials.

**Refresh status** reads existing in-memory counters only. **Start indexing names** requires explicit Yes/No approval and calls the existing idempotent WarmStart action. Starting may enumerate names on all ready Windows drives, without opening the contents of files. The new window adds no watcher, background poll, timer, independent file scan, telemetry or external provider. A normal search continues to use the original index.

Native Windows WPF tests cover not-started, in-progress, completed and invalid status displays, aggregate counter availability, discoverable Action Palette entry, and real manual Refresh/Start/Close buttons without activating a scan. This is partial SEARCH-01 work; resilient incremental indexing, real multi-drive latency and optional Everything IPC remain pending.
