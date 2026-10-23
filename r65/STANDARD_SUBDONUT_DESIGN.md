# DeskSpace — Standard sub-donut contract (R65)

- **Main donut:** Keep all current rings, modules, and compact/expanded behavior unchanged.
- **Child origin:** Every sub-donut begins at the selected parent sector’s center; nested levels follow the same geometry.
- **Capacity:** At most five visible items per ring; use one-item wheel/touchpad scrolling, with the standard ease animation.
- **Overflow:** On each side with hidden items, the numeral sits **at the radial midpoint of the existing vertical end divider** of the first/last VISIBLE wedge. The original divider line continues on either side of the numeral; no floating badge, separate tab, arrows or extra wedge is drawn. Each embedded numeral remains clickable; the normal mouse wheel works while pointed over any visible item. The actual divider outline must contain a short open gap around the numeral, with no paint mask, background Border, badge, or default rectangular button chrome. The text-only control uses bold Segoe UI and adaptive WCAG-style contrast (target ≥4.5:1) on both light and dark wedges.
- **Maximum spread:** A ring can cover the parent sector and up to half of each immediately neighboring sector; deeper levels inherit the same parent-relative limit.
- **Apps:** Display the actual Windows application or shortcut icon beside its label, with text fallback if extraction fails; icon lookup must never execute the program.
- **Preview:** Hover may show temporary Information previews; explicitly clicked items persist, and unselected previews close on exit.
- **Placement:** Expanded arcs may be fitted temporarily at monitor edges, but the user’s compact circle always returns to the exact pre-expansion location on collapse.
- **Extensibility:** New modules provide a source list, action handler and optional icon; all layout, paging, counters and keyboard/mouse behavior remains in the shared standard.
