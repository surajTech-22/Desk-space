# R257 — detailed preview and single-use consent for saved window layouts

The canonical DeskSpace application **already includes** an aggregate window-layout Preview action, portable import/export, a native Yes/No restore confirmation, and a strict exact-monitor restore that skips disconnected displays. R257 preserves these.

This increment enhances the existing **Preview restore** button with a per-window read-only list: application process category/name, saved physical monitor identity, projected target bounds, and the reason a window would be skipped. No raw window titles, documents, paths or contents are exposed. It also requires the user to preview the selected profile, recomputes a SHA-256-only in-memory proof of the profile and current projected targets immediately before restoring, and consumes that preview consent once. A changed monitor/window layout triggers re-preview rather than moving windows unexpectedly. The preexisting Yes/No confirmation is still required.

Native Windows tests use simulated candidate window identities, a negative-origin display, and a disconnected monitor; check containment, skipped matches, and the actual WPF Preview control. Live multi-monitor acceptance remains pending. No Stable/Tester release.
