# R233 — one graceful-close request per updater event (development)

R230 introduced a per-installation Windows event that remains signaled while the supervisor waits for all DeskSpace processes to close normally. Previously the app attempted normal WPF Close repeatedly every eight seconds while the same event remained set, even when a Closing handler had cancelled because unsaved work was open.

R233 changes the listener to a rising-edge latch: one normal-close attempt for each supervisor request, then no retries until the event is reset. This prevents repeated unsaved-work prompts and accidental repeated close attempts under one install. New update attempts are enabled after reset. The scope remains the exact DeskSpace installation; unrelated DeskSpace installations and user data remain untouched.

Validation: static patch regression plus native Windows WPF smoke (signal true/held/false/new true) through the consolidated CI workflow. This patch is NOT a claim of physical two-monitor or installed-PC acceptance, and does not bypass the R204 consolidated release gate.
