# R243 — media Deck survives independent monitor window shutdown

The older media/Deck integration (R123) set one static DeckIntegration flag. Closing *any* widget called `DeskSpaceMediaV18.DeckIntegrationV123=false`, even if a second monitor still had a Deck. That could cause media controls to reappear as an unwanted floating overlay when another widget remained active.

R243 removes the unconditional per-widget reset, and computes media integration from the number of surviving registered Deck views **after** that particular Deck is removed. Media controls remain integrated until the last widget Deck closes. The existing close button still dismisses only the media panel without stopping playback. Existing user media settings and shared held files are not modified.

Windows native WPF smoke constructs two independent Deck instances, closes the first and verifies integrated controls remain active; closes the second and verifies integration stops. This test does not represent actual physical dual-monitor acceptance, and release remains blocked by the consolidated readiness manifest.
