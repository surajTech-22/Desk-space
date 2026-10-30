# R232 graceful update reliability release

The accepted R230+R231+R232 source is SHA-256-frozen by Windows Actions run 41085251970 on commit e96d3c6147af85ae324d2e2bfddda6b2b4253cb8. Stable and Tester use exactly the same binary after SHA256 checks.

The supervisor requests normal exit of all same-installation DeskSpace Workspace/widget/tray processes and waits for zero peers, with rollback and no forced termination. Secondary WPF windows are closed before main to allow unsaved-work veto. Compiled native WPF WM_CLOSE cancellation and normal exit test passed. The previously released R231 monitor seam and deck-position fixes remain included. No physical-device acceptance or all-feature completion is claimed; avoid old-client instant push storms.