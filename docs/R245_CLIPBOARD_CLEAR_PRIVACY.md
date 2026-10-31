# R245 — Clear local clipboard conversion preview without changing Windows clipboard

The existing DeskSpace Clipboard conversions feature is an explicitly invoked, local-only transformer. R245 adds **Clear local preview** alongside **Read clipboard**. It clears the source and output in-memory strings, both visible text boxes, disables Copy and Save As, and shows a truthful statement that the actual Windows clipboard has **not** changed. Re-reading the clipboard still requires pressing the existing user-invoked Read button. Closing the dialog continues to clear both local views and buffers.

The new control never reads, writes or clears the Windows clipboard, does not create history, does not persist raw contents or passwords, and does not upload anything. It is a bounded privacy and usability increment; it does not implement general clipboard history or sensitive-provider classification.

Windows native WPF tests create the real converter view, populate two test text fields, click Clear and verify both fields are empty and downstream Copy/Save are disabled. Static checks reject clipboard/file/network actions in the new click handler. The consolidated release gate stays blocked pending broader clipboard/security and real-device acceptance.
