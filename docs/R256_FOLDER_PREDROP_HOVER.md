# R256 — folder ZIP or Copy before dropping

The old folder choice appeared after the user dropped the folder. R256 attaches WPF PreviewDragEnter, PreviewDragOver and PreviewDrop to both the widget and the separate Popup window. The chooser appears over the owning circle during a folder drag. Hover ZIP or Copy and release over the highlighted option. Drop without selecting, or with a changed source list, is blocked and no post-drop prompt opens.

The existing bounded, cancellable, SHA-256-verified ZIP/copy staging pipeline is reused. The chooser stays local to its monitor, while held Deck data is shared. Abandoned drag state automatically clears after 12 seconds without drag events. Ordinary file-only drops continue through their original route.

Windows tests validate the pre-release action choice, unchanged source identity, and no-choice rejection. Explorer native drag and actual two-monitor mixed-DPI acceptance are still required. No Stable or Tester release is made.
