# R247 — event-driven monitor sleep/resume recovery

DeskSpace currently tracks monitor changes through Windows DisplaySettingsChanged and a 45-second missed-event fallback. On wake from sleep, Windows can report an incomplete connected monitor list before display drivers settle. R247 listens to Windows **PowerModeChanged: Resume** only while the primary widget exists. It schedules exactly three bounded, dispatcher-owned follow-up reconciliations at **1.5, 5, and 12 seconds**, re-reading the existing display preference and using the existing per-monitor identity and containment rules. This is not another continuous polling loop.

Only the original primary widget owns the subscription. On shutdown, the handler is unsubscribed and the resume timer stopped and discarded. A stale callback cannot act on a new/different primary widget. The change never wipes user data, clears a disconnected device's assignment, shares a Deck position, or modifies notes/mappings/settings. The existing screen-change events remain authoritative.

Windows CI verifies finite schedule and source lifecycle; real suspend/reconnect and mixed-DPI physical acceptance remain mandatory before release. This feature is development-only.
