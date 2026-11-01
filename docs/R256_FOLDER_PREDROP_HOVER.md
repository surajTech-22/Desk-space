# R256 — enforce the existing ZIP / Copy choice during folder drag

DeskSpace already has the R160 native on-circle ZIP/Copy **adorner**, shown during DragEnter/DragOver, with the hovered half selected on release. It does not use a separate popup. R256 hardens this existing path: while dragging, record which half (ZIP left, Copy right) was hovered; on Drop, require that exact same option and the same original folder paths before calling the existing bounded, cancellable, SHA-verified ZIP/copy staging engine.

A drag that never showed a choice, that moves outside the circle, or whose source identity changes is rejected without any post-drop prompt. The consent state is cleared with the existing drag-leave/window-close cleanup. No second popup window, new drag event subscription, source file mutations, background timer, or other monitor's UI state is created.

Native Windows tests verify ZIP/Copy hover identity and reject mismatched source, unselected, oversized and absent drag state. Real Explorer drag on multiple physical mixed-DPI monitors remains a mandatory user-device acceptance test before release.
