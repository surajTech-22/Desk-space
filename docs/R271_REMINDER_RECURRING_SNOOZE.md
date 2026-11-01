# R271 — prevent repeat reminders from firing immediately after snooze

An actual bug in R83: `Snooze(...)` moved the effective due time forward, but `MarkAlert(...)` cleared the snooze while leaving the **original recurring Due** in the past. Therefore the scheduler could immediately create another due alert after the first snooze alert was dismissed. It was display-only, but repeated reminders could become persistent noise.

When a recurring snooze notification is acknowledged, the scheduler now advances a past Due to the next scheduled occurrence strictly after the snooze-dismissal time. If the original due date is still in the future, it is preserved. The snoozed alert still never launches a URL, file, app, or automation a second time. One-shot snoozed reminders remain acknowledged with `LastAlert`. Existing serialized ReminderItem fields and the original notification/undo system remain unchanged.

Native regression covers an overdue daily reminder, snooze expiry, immediate second scheduler scan, snoozing early before a future event, and one-off reminders. Windows CI validates the amended canonical source, patch reconstruction, complete WPF build and source hashes. No release until device acceptance.
