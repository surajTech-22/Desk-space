# R261 — running apps with multiple windows first, then alphabetical

DeskSpace already shows a hover-only running-app Deck with separate window thumbnails, but the app group strip was alphabetically sorted even when a program had many open windows. R261 adjusts **the existing** running apps grouping (no duplicate module): programs with more than one distinct window come first, sorted by descending window count; single-window programs remain alphabetically sorted. When every program has only one window, the entire list remains alphabetical. Duplicate HWND entries never inflate window counts.

The existing flyout continues showing every distinct window for an app. R261 gives those windows a stable, case-insensitive title order, preventing unwanted visual reshuffles caused by Windows enumeration order. The 24-icon Deck limit and original hover/click/minimize/preview behavior remain unchanged. This module does not read extra files, launch apps, change window focus, write data or create a new timer.

Native Windows tests cover a multiwindow app over singlewindow apps, duplicate HWND handling, alphabetical all-single apps, stable flyout windows and the 24-icon limit. Real mixed-DPI hover preview and independent monitor Deck position acceptance remain pending. This is partial DECK-01 work, with no release.
