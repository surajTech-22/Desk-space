# R234 — independent Deck position recovery (development)

On either monitor, right-click that monitor's **Deck** and choose **Reset Deck position on this monitor**. The command resets only the stored drag offset for the Deck's assigned monitor, then requests a fresh position calculation within that monitor's working area.

The other monitor's Deck location remains unchanged. The two widgets still share held-file data, but do not share popup offsets or temporary hover/drag state. R234 does not remove staged files, reset saved mappings, change the widget's position, or enable automatic magnetic docking.

Validation is a strictly anchored reconstruction test and complete Windows build/WPF smoke in the consolidated branch. Physical mixed-DPI monitor acceptance remains required; do not treat this as a finished all-feature release.
