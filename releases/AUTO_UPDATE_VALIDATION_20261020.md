# Automatic update validation — 2026-10-20

Purpose: Trigger an unchanged-code, version-incremented DeskSpace Windows release through the existing private GitHub Actions build-and-release process.

Expected behavior:
- Existing R5 v1.4.26 on the user's Windows PC detects the newer verified GitHub release.
- The independent widget downloads the release from the authenticated private repository.
- SHA256 is verified before the silent installer runs.
- The widget restarts after the update.
- Existing %LOCALAPPDATA%\DeskSpace data is preserved.

This validation document is not packaged into the application.
