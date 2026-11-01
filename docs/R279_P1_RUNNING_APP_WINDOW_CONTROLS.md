# R279 — P1 Running Apps: explicit per-window controls, no extra panel

The user's 31 October feature-expansion plan lists **quick window controls** under existing P1 Running Applications/Deck. The app had grouped thumbnails, a click-to-focus/minimize toggle, and WM_CLOSE buttons, but lacked separately labeled Minimize and Restore actions on individual windows.

R279 adds small accessible **Minimize** and **Restore** buttons beside the existing normal **Close** control in each grouped window's hover flyout; the original 3-column Running Apps grid and monitor-local flyout remain intact. No extra floating panel, background Windows scan or process-kill action is introduced.

Every Minimize/Restore/Focus/Close action validates the exact live window title and HWND before acting, refusing stale or reused handles. Minimize and Restore are ordinary `ShowWindowAsync` requests; Close continues to post `WM_CLOSE` so applications can prompt for unsaved work. The controls do not reposition another window or change monitor-session data.

Native Windows UI regression checks exact-title guard, stale-handle rejection, accessible WPF controls, and actual Minimize/Restore requests on a temporary real WPF window. Physical dual-monitor positioning, keyboard-only flyout navigation and Windows protected/elevated applications still require device acceptance.
