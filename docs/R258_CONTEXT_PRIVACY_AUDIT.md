# R258 — opt-in foreground context privacy audit and complete Stop cleanup

DeskSpace already provides an explicit, session-scoped Windows foreground-app suggestion listener (R237). R258 adds a **Show privacy audit** control, reporting only whether the listener is active, a bounded count of recognized application *category* events and the latest approved category. No process identifiers, raw window titles, browser URLs, opened files, clipboard contents, secrets or diagnostic history are recorded or displayed.

Start resets all prior session data. **Stop** unhooks the WinEvent listener, clears the last category and event count, removes the in-session recommendations and updates the audit display. Crucially, a previously queued dispatcher callback now checks that the live hook still exists before restoring a suggestion after Stop. Closing the window already calls Dispose/Stop. No new background listeners, persistent telemetry or automatic actions are introduced.

Native Windows tests verify Off redaction, count limits, unknown process text exclusion, and actual Session Stop clearing WPF recommendations and counters. Full physical-screen and consent UI acceptance remain required; this is a partial CTX-03 implementation, not a completed automated recommendation system.
