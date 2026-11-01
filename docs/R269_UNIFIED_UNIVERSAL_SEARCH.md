# R269 — one working Universal Search, not two competing disk crawlers

**User-facing change:** The existing Workspace Universal Search window previously enumerated up to 350,000 files on every fixed drive **per keystroke**, ignored removable/network volumes, duplicated Windows settings data, and offered a 'Commands' filter that returned nothing.

R269 replaces this legacy search with the same progressive search engine already used by the widget (`DeskSpaceSearchIndexV13.Search`). It combines Windows Search, named files across ready drives, installed applications, settings, notes, reminders, calendar and information into one ranked result set, with proper category tabs. Search now starts using the shared index, not a new filesystem recursion. It cancels old requests on each edit/category switch, rejects stale completed work, deduplicates case-insensitive file targets and caps displayed hits.

**Discoverability:** Both Features > Universal Search and Action Palette > Universal Search open the new search window. The existing Action Palette is explicitly accessible via the 'Commands' button rather than an inert search category. Keyboard Escape closes the window. File/folder navigation, copy path, copy file and pin actions stay user-initiated; Information clipboard copying requires explicit confirmation.

**Safety:** No automatic app launch, file deletion, clipboard history, source file writes or web/network upload added. Existing Search-index behavior is unchanged; file indexing may scan names on ready drives only after user-invoked search. No real user documents are needed for CI tests. Synthetic WPF tests cover category visibility, dedupe, caps and cancellation race protection. The existing source hash-parity gate protects the regenerated/checked-in WPF source.

**Shipping status:** Compiled development code only. Real multi-drive Windows/Explorer interaction and installed-PC acceptance remain pending; Stable and Tester release gate is unchanged.
