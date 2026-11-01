# R270 — Quick Notes, independent of Search/Excel/monitor/updater work

Adds a direct **Quick note** button in Workspace Features and Action Palette. The editor is owned by the existing MainWindow, uses the active in-memory Workspace and stores a standard NoteItem. Users may type a title and content, or choose **Read clipboard (on demand)** to load up to 16,384 characters into the editable preview. Nothing is copied automatically, monitored in the background, uploaded or submitted before clicking **Save new note**.

Title and content are bounded and reject hidden controls. Read-only recovery mode blocks saving to prevent an incomplete workspace overwriting original data. An ordinary successful save calls the existing atomic WorkspaceStore mutex/backup transaction. On expected IO/permissions failure, the new note is removed from memory, the editor remains open and the user can retry. No new notes data file or background watcher exists. Existing notes are unchanged.

The shortcut behaves safely when opened via a floating widget: if Workspace is not open, it navigates to Notes rather than creating an independent, unsynchronized workspace writer. Future work can extend this to direct linked Quick Notes after verified per-process state synchronization.

Static safety assertions and native WPF model tests cover empty and oversized input, Unicode/CRLF handling, unsupported controls, explicit clipboard gating, recovery mode, real Workspace Notes bindings and rollback on storage failure. Physical Windows clipboard and installed-PC acceptance remain pending. This does not release any build.
