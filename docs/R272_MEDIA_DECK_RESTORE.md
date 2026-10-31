# R272 — Media Player Deck controls can be reopened after Close

The Deck already had transport buttons and a close button, but its media player `dismissedV123` flag prevented a user from reopening the **same track**. This meant the media controls could vanish for the rest of a podcast or long track. R272 keeps the close behavior (no playback interruption), and displays a single, compact `Show media controls` button while that track is active and dismissed. Clicking it restores normal previous/play-pause/next/mute/stop controls; it never starts playback by itself.

The restore logic rejects invisible or disposed players and ignores repeated restores. The Deck transport handler now rejects post-disposal, invisible, or dismissed clicks and limits commands to the five explicitly supported transport actions. The original media popup and shared Deck integrations are retained.

A native WPF test creates a disposable media controller without launching media, simulates availability, dismisses the Deck, restores it once, verifies subsequent repeated restoration is refused, and tests command suppression after disposal. Static CI checks ensure the actual Deck control and player logic are connected.

**Still open:** installed-PC GlobalSystemMediaTransportControls hardware/player testing, and physical multi-widget acceptance. No Stable/Tester release is authorized by this development change.
