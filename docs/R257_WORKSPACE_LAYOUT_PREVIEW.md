# R257 — review window layout restore before consent

DeskSpace's existing **Window layout profiles** could capture and restore window locations with a Yes/No confirmation, but offered no per-window preview. Worse, the old restore silently substituted the primary monitor when the original saved monitor was disconnected.

R257 adds **Preview selected** to the existing Layouts window. It produces a purely read-only, current list of matchable running applications, their saved display identity, and projected position and dimensions. Missing/reconnected monitors, changed window titles (stored as SHA-256 only) and invalid geometry are displayed as skipped. Raw window titles, filenames and document text are not displayed or persisted by preview. The restore command now requires an explicit preview of the selected profile, rechecks the current projected plan immediately before confirmation, and invalidates consent after restore. Disconnected saved monitors are **skipped**, never silently rehomed to primary. The old confirmation and no-focus/no-new-app behavior remain.

Native Windows tests supply multiple simulated windows, a negative-origin work area and a disconnected display; verify ready/skipped/geometry results and ensure a visible Preview button exists alongside Restore. Real multi-monitor/mixed-DPI restore and actual user acceptance are still required. No Stable/Tester release.
