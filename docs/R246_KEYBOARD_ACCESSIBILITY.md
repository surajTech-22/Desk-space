# R246 — Desktop keyboard shortcut guide and F1 access

DeskSpace's Workspace Features page now offers a **Keyboard shortcuts guide**, and pressing **F1** in Workspace opens the same guide. It lists the currently implemented, context-scoped actions: Ctrl+K for Action Palette; Search Up/Down, Page Up/Down, Enter and F8 (eligible files only); Root donut Left/Right, Up/Down, Home/End when donut keyboard navigation is active; Escape to close the guide.

The guide uses native WPF text, accessible AutomationProperties names, standard Tab navigation, and an explicit Close control. It does not register global hotkeys, steal keys from other applications, inject input, scan windows, read user text or persist anything. It does **not** claim to have implemented every Excel or Windows shortcut. This increment documents and surfaces verified existing support; full screen-reader and keyboard-only end-to-end user acceptance remains pending.

Native WPF tests inspect the 10 verified scoped entries, construct and close the actual dialog. Static tests verify both Features and F1 entries and ban global hooks. Existing Ctrl+K remains unchanged. Release requires the full consolidated readiness gates.
