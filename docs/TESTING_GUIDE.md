# DeskSpace — Windows tester guide

> **Tester edition · October 2026.** This guide covers the published installer and separately supplied R42 local test builds. It does not certify an unreleased build.

## 1. What is DeskSpace?

DeskSpace is a Windows desktop workspace with a floating circular ("donut") widget and a conventional Workspace window. It combines shortcuts to notes, calendar, reminders, clock, weather, search, files, clipboard, Information/custom data, and Excel mapping. The donut is designed for quick mouse hover, click, scroll and drag access; the Workspace gives larger editing screens.

**Important limitation:** The interface in current screenshots says that local data is **not encrypted in this preview** and that the password vault is not ready. **Do not put real passwords, credentials, government IDs, bank data, private client records or other sensitive information into a test build.** Use invented examples.

The Excel feature captures an already-open workbook selection or a screenshot, lets you review saved header→value→cell relationships, and—**only with verified matching labels/positions**—attempts values-only paste back to an open workbook. Formulas, formatting and source reference labels must not be overwritten. Pasting may update the *open workbook in memory*; the user must review and save it. An independent backup is required before writing. Testing this feature requires Microsoft Excel desktop, not only a browser spreadsheet.

## 2. Which build am I testing?

| Build | Where to get it | Status |
|---|---|---|
| Published version | Private repo [Releases](https://github.com/surajTech-22/Desk-space/releases/latest): `DeskSpace_Setup.exe` | Latest publicly recorded release in the repository: **v1.4.266**, as checked on 22 Oct 2026 |
| R42 development build | A `DeskSpace_Setup_R42_Local_*.exe` installer **shared directly by the project owner**, or made using the owner's `UPDATE_APP.bat` Windows builder | **Testing only**, not the published release; build number varies (one reported local build was v1.4.29410) |
| Future releases | Shared by project owner | Test only after installer checksum and build results are provided |

This is a **private repository**. You need the owner's GitHub invitation to open it. Without access, ask the owner to share the installer and this test guide through a private channel. Do not download packages from unofficial mirrors.

## 3. Install and open on Windows

1. Use **Windows 10/11, 64-bit**. For Excel tests, install and open Microsoft Excel desktop separately.
2. Save all open documents and close any running DeskSpace **Workspace, floating donut and DeskSpace system-tray instance** before installing. Exit via the tray icon if necessary.
3. Download the supplied `DeskSpace_Setup.exe` (or approved local R42 test installer). If there is a matching `.sha256.txt`, compare the downloaded installer SHA-256 with it. In PowerShell: `Get-FileHash "C:\path\to\DeskSpace_Setup.exe" -Algorithm SHA256`. **Never bypass a checksum mismatch.**
4. Double-click the installer. Windows may ask for consent or warn about an unsigned file; verify it came from the owner before proceeding. Follow the installer prompts.
5. Open **Start → DeskSpace**, or launch `%LOCALAPPDATA%\Programs\DeskSpace\DeskSpace.exe` if installed to the default per-user location.
6. On first start, check the top version badge and open **Home**. To show the circular widget, click **Show Live Donut Widget**. The widget may also have a taskbar/system-tray control. Test both Workspace and widget.
7. To reopen later, launch DeskSpace from Start, or use the installed desktop shortcut if one was created. If already running, check the system tray before launching another copy.

**Installation problem:** If setup says `DeleteFile failed; code 5; Access denied`, do **not** choose **Skip this file**. Cancel, exit the Workspace and the tray/widget processes gracefully, and rerun setup. Do not use Task Manager's End task unless advised, because unsaved edits may be lost.

**No SDK required to use an installer.** The separate `UPDATE_APP.bat` developer/tester builder downloads sources and compilation tools; ordinary testers should not need the .NET SDK or PowerShell 7.

## 4. Safe preparation before testing

- Create **dummy notes, reminders and information records**. Do not use real client information.
- Make a disposable Excel workbook with a few visible header/value pairs, at least one formula, one blank input, and recognizable labels. Save a copy before testing.
- Record the installed version, Windows version, Excel version (if used), display scaling (100%/125%/150%), monitor arrangement, and whether DeskSpace was opened via Start or the tray.
- Prefer testing the normal window **and** a narrower/restored window; with one and multiple monitors if available.
- For each test, record **PASS**, **FAIL**, **NOT AVAILABLE**, or **NOT TESTED**. A feature that is still under development is not a pass.

## 5. Test checklist

Mark each item. If the feature is missing from this particular build, use **NOT AVAILABLE** rather than treating it as a crash.

| ID | Test steps | Expected result |
|---|---|---|
| INS-01 | Close old DeskSpace; install supplied version; open from Start | Installer finishes, version badge shows intended build, no missing-file error |
| INS-02 | Exit Workspace and widget; reopen | Existing dummy notes/settings remain; no blank-data reset |
| UI-01 | Click each sidebar section in Workspace | Correct page opens without unrelated module covering it |
| UI-02 | Restore a smaller Workspace window; open Excel Mapping | Saved list and mapping table stay usable; no clipped action buttons or large empty instructional panels |
| WHEEL-01 | Open **Show Live Donut Widget**; hover all wheel sectors | Hover feedback and visible labels; no unexpected selection or popup |
| WHEEL-02 | Test click, wheel scroll, child arc, back-to-circle and edge of display | Navigation stays anchored, is reversible, does not cover controls |
| WHEEL-03 | Minimize/collapse the widget and restore it | Minimized controls remain reachable |
| SEARCH-01 | Search a known app and a file using uppercase/lowercase variations | Matching apps/files appear case-insensitively |
| NOTES-01 | Create, edit and delete dummy note | Changes persist; cancellation does not remove existing data |
| CAL-01 | Open calendar; add and edit dummy event | Event appears under expected date; compact calendar remains usable |
| REM-01 | Add and complete a dummy reminder | State persists after reopening |
| CLOCK-01 | Open clock and inbuilt Windows calculator | Correct function launches or failure is clearly explained |
| WEATHER-01 | Open weather in light/dark background and with network disconnected | Text remains readable; cached/offline behavior does not pretend to be live |
| WEATHER-02 | In widget Settings, click **Use my location**, grant Windows location access, and refresh weather | Explicit opt-in; real device position used for approximate nearby weather; no silent fallback to another city |
| WEATHER-03 | Deny location permission and refresh | Manual city search and offline last-synced weather remain available, with clear failure status |
| WEATHER-04 | Search a city outside the original presets and select the correct country/region | Worldwide city lookup works, selected city persists, and source labels match the requested location |
| MEDIA-01 | Open compact media player; move/minimize and test supported controls | Controls remain visible; no unexpected audio or stuck overlay |
| CLIP-01 | Copy dummy text and files; try the appropriate clipboard | Original Windows clipboard continues working; files do not disappear unexpectedly |
| INFO-01 | Create/edit a dummy information item or custom tab | Saved values can be reviewed and copied again |
| EXCEL-01 | Open dummy Excel workbook; select a small rectangular range; capture in Workspace → Excel Mapping | Every captured source cell/label is reviewable with correct association |
| EXCEL-02 | Rename a saved mapping, edit a dummy value, save and reopen | New name and approved value persist; other saved mappings remain |
| EXCEL-03 | Hover a saved mapping in the donut | Read-only preview; **hover alone must never paste** |
| EXCEL-04 | Click a saved mapping with uniquely matching references in open dummy Excel sheet | Inputs are pasted to intended addresses only, only once, with a backup; Excel remains reviewable before manual save |
| EXCEL-05 | Try wrong sheet/ambiguous labels or screenshot-only unverified positions | Paste is safely blocked; workbook is unchanged |
| EXCEL-06 | Put a formula in a destination cell; test guarded paste using a copy | Formula and formatting remain intact; reference labels are untouched |
| EXCEL-07 | Test Excel clipboard separately with dummy copy | Copied Excel values do not merge with Files/general clipboard |
| EXCEL-08 | After a **successful** Excel paste, check notice visibility | In builds implementing the new behavior: success notice auto-hides after ~3 seconds; errors remain readable. Other modules unaffected |
| EXCEL-09 | Find **Excel Functions on the OUTER circle** and hover/click it | One outer Excel Functions parent appears, with Clipboard and Information children. No main Excel Clipboard and no inner Excel Functions sector |
| EXCEL-10 | Under Excel Functions → Information, save an ungrouped mapping and a mapping assigned to `Clients/Audit`; navigate by wheel | **R42 candidate pending Windows verification:** ungrouped mapping appears directly in Information; grouped mapping appears inside Clients → Audit. Grouping is not an Excel Functions child |
| SAFE-01 | Close a window while a dummy edit is unsaved | No silent data loss; clear save/discard choice where supported |
| PERF-01 | Scroll long lists and switch modules repeatedly | No freezing, runaway popup, access violation or repeated app launches |

## 6. What a useful failure report contains

Use this compact format for **each** issue (one issue per report):

```text
DeskSpace test report
Test ID:
Result: FAIL / NOT AVAILABLE / NOT TESTED
Installed DeskSpace version:
Installer filename and SHA-256 (if available):
Windows version and display scaling:
Excel version (only if relevant):
Steps to reproduce:
1.
2.
3.
Expected:
Actual:
How often: Always / Sometimes / Once
Does restarting DeskSpace reproduce it?:
Screenshots or video filenames:
Diagnostics ZIP/log filename:
Did any data change unexpectedly?:
```

**Always attach a screenshot for UI/layout issues** with the whole affected window visible. For crashes/build failures attach the exact error text and `DeskSpace_R42_DIAGNOSTICS_*.zip` or `DeskSpace_R42_INSTALL_DIAGNOSTICS_*.zip` from **Downloads**, if generated. For Excel issues use a **sanitized test workbook**, never a genuine customer workbook, and include before/after screenshots plus the exact sheet and cells used.

If no diagnostic ZIP was created, send a screenshot, the error window text and the version badge. **Remove personal paths, account names, access tokens, passwords and private workbook content before sharing publicly.**

## 7. How to submit results

- If you have repository access: open [Issues](https://github.com/surajTech-22/Desk-space/issues) and create one issue per failure. Use a title like `[R42][EXCEL-06] Formula overwritten on paste` and paste the report above. Keep the repository private.
- If the owner supplied the installer outside GitHub: send them the completed checklist and sanitized screenshots/diagnostics. The owner can forward them to development.
- When everything works, send back the checklist with the **actual installed build version**, not just “all OK.” Confirm Excel formula-preservation and reopen/persistence separately.

## 8. Building R42 from GitHub (owner / designated Windows build tester only)

1. Obtain the current **GitHub Pull V4** manual launcher ZIP from the owner; extract it to a normal Windows folder. A ZIP of source is **not** a standalone installed app.
2. Sign in to the authorized GitHub account in the official **GitHub CLI (`gh auth login`)** because the source repository is private.
3. Run `UPDATE_APP.bat` in the extracted V4 folder. It fetches the latest R42 branch from GitHub, pins a source revision for that build, and runs the local Windows compiler/tests. **Do not run GitHub Actions; free minutes are exhausted.**
4. If build fails, keep the current installation and send the generated **Downloads diagnostics ZIP** to the owner. If it succeeds, verify checksum, exit DeskSpace gracefully, back up user data and install the generated self-contained installer.
5. If an already-built, verified local installer exists, the V4 package's `INSTALL_EXISTING_R42.bat` may be used to install it with the running-app check rather than recompiling. Do not use it with an unknown or checksum-mismatched installer.

The test guide is descriptive, **not proof that all functions are present or correct** in any one build.


### R42 manual tester update

**R42 tester candidate is NOT yet verified.** The expected circle has 6 original inner sectors, 7 outer sectors, with **Excel Functions as the seventh outer sector**. Under Excel Functions are only **Clipboard** and **Information**. Information shows ungrouped saved mappings directly and grouped mappings through named child donut levels. Excel paste success notices auto-hide in three seconds; errors do not.

The Weather Settings **Use my location** button must ask Windows permission from the foreground. If denied or unavailable, use manual place search and an offline cached reading. Do not silently substitute Bengaluru or imply cached readings are live. Test at least one non-preset world city and current-location permission denial. Use disposable Excel workbooks and invented data.
