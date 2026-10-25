# DeskSpace v1.5.590 → tester update-channel recovery

**Symptoms:** Workspace says `DeskSpace is up to date (1.5.590)` although there is a newer tester prerelease.

This is a **channel mismatch**, not proof that the newer setup was installed. The v1.5.590 updater intentionally reads GitHub's stable-only `/releases/latest` endpoint; tester builds are tagged `tester-vX.Y.Z` and do not appear there. A GitHub **Clone** also only downloads source, not the installed app's update enrollment.

## Official recovery path

1. Save DeskSpace workspace edits and **exit DeskSpace completely from the Windows system tray** (closing just Workspace may leave the floating widget running).
2. Download [the newest verified tester installer from GitHub Releases](https://github.com/surajTech-22/Desk-space/releases/tag/tester-v1.5.595) and run **DeskSpace_Setup.exe**, not the repository ZIP/Clone. Verify its accompanying SHA-256 file.
3. Open DeskSpace from the Windows Start menu. The version badge must be newer than v1.5.590.
4. In **Settings → Update channel**, select **Tester — early builds**. The preference is written to `%LOCALAPPDATA%\DeskSpace\update-channel.txt`.
5. Check for updates again. Future tester builds will be checked every five minutes.

If the previous version remains visible, verify that the shortcut launches the *installed* copy under `%LOCALAPPDATA%\Programs\DeskSpace` (or your previous installation directory), not a cloned/source-built copy. The installer must update the original directory. Do not delete `%LOCALAPPDATA%\DeskSpace`: it can contain mappings and other workspace data.

## Dedicated recovery utility

A one-time Windows repair package is available through the owner in the ChatGPT conversation as `DeskSpace_Tester_Update_Repair.zip`. Its launcher checks for an installed DeskSpace executable, asks for a full application exit, fetches the latest GitHub tester release, verifies its published SHA-256, installs to the same folder, and **only on successful installation** selects the tester channel. If the verification or Windows installation fails, it writes a diagnostic log under `%LOCALAPPDATA%\DeskSpace\UpgradeDiagnostics`.

The repair utility is an optional one-time onboarding helper, **not** a replacement for verified GitHub installers or regular background updates. It is not yet independently tested on the affected Windows PC.

## Channels

- **Stable:** GitHub `/releases/latest`; excludes prereleases.
- **Tester:** GitHub `/releases?per_page=40`; chooses the highest published `tester-vX.Y.Z` prerelease; remains opt-in.
- Installations retain their workspace data when upgraded in place. Neither channel should silently downgrade a newer app.

The stable/up-to-date message in v1.5.590 is not a tester-channel check.