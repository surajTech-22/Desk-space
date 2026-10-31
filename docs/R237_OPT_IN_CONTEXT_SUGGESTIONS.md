# R237 — explicitly opted-in contextual tool suggestions

This is the **first implemented** component of the Smart Context Deck plan, but not full automatic background intelligence. A user can open **Workspace → Features → Context suggestions**, select **Start session**, switch to an app and return. A temporary Windows foreground-change hook recognizes *only* a coarse process category (Excel, Documents, Presentations, Files, Browser, Media, Other). It does not retain the actual process name, window title, URLs, workbook names, clipboard, passwords or screen content. Each suggested action is an existing manual DeskSpace feature, never executed automatically.

**Stop** unregisters the hook. Closing the dialog disposes it. No hook is installed when the panel is not explicitly activated. No data is saved or uploaded. The current implementation does not track arbitrary unsupported applications, doesn't auto-apply mappings and doesn't expose real document context. Excel exact-signature suggestions are separately pending.

A Windows native test verifies categories and privacy-safe unknowns; a static test prevents hidden clipboard, file, secret, title and network accesses. Windows x64 compilation and WPF smoke are mandatory. Physical installed-user acceptance remains pending. Consolidated release gate stays blocked.
