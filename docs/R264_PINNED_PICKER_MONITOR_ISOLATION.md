# R264 — Originating-monitor bounded pinned document picker

R262 introduced an explicit right-click recent-document chooser for each pinned app. On a second display, a normal WPF CenterOwner / CenterScreen dialog can open partly across a mixed-DPI seam or on the primary display. R264 switches this chooser to explicit owner-HWND native-pixel placement, reusing the assigned monitor of an owning widget. It centers initially and subsequently clamps drag/resize to that monitor, without moving either underlying widget or its Deck.

The scrollable picker shrinks when the target working area is smaller, rather than spilling to a second screen. It does not create new persistent state, retain document paths or change app launch/Excel behavior. The existing no-background-scan and explicit-click-only requirements are unchanged.

Windows validation: synthetic left negative-origin, right seam and top overflow geometry; owner-linked WPF window render; strict script patch anchors; consolidated Release compilation and WPF smoke. **Physical two-monitor mixed-DPI and installed-device input behavior remain pending**, so this addition does not permit Stable/Tester publication.
