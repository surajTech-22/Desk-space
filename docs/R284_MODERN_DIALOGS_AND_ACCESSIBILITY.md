# R284 — Modern design continuation: widget panels, context and accessibility

R283 brought a cohesive modern visual system to the main Workspace, donut, Deck, Settings and action palette. R284 extends it to previously unchanged everyday workflows, **without creating a new tool or altering user files**.

- Modern, keyboard-focusable WPF buttons now show explicit accent border feedback on hover and keyboard focus even through their rounded native `ControlTemplate`. Existing Click events and automation peers remain.
- The legacy Widget Details dialog uses a card, structured title and readable text. Widget Options is scrollable in smaller windows, with its original weather, startup, fullscreen and center-mode controls intact. All toggles keep their native CheckBox and accessible target sizes. Pinned Apps retains the actual list, Pin/Launch/Unpin actions with new styling.
- Smart Context's existing opt-in Start/Stop and manual recommendation buttons now use the same card and native button styling; there is **no additional listener**, no automatic data access and no changed session privacy.
- Running Apps stationary-hover and multi-window popup surfaces now match the Deck with rounded dark gradients while preserving HWND verification, hover-only interaction, same monitor bounds and preview positioning.
- Native Windows tests instantiate the actual Widget Options and Pinned Apps WPF windows, inspect the original controls and focus/hover triggers, and verify scroll access. Existing R201–R283 regression and source parity required.

Remaining work: real interactive screenshot acceptance, legacy module surfaces outside these dialogs, high contrast on all main Workspace pages, mixed-DPI physical monitor checks, Excel Office tests and installed updater rollback. No Stable or Tester release.
