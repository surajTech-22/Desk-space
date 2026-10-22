# DeskSpace

**DeskSpace** is a Windows desktop workspace with a floating circular (donut) widget, a larger Workspace window, quick tools, information management and Excel mapping.

It is designed to make common tasks accessible without opening multiple unrelated panels. Existing modules include notes, calendar, reminders, clock/timer, weather, calculator shortcut, universal search, files, clipboard, custom information and Excel mapping. Features vary by build.

## For people testing DeskSpace

**Start here: [Complete Windows installation, feature testing and bug-report guide](docs/TESTING_GUIDE.md).**

The guide explains exactly how to install and open DeskSpace, how to exercise every main section safely, how to test Excel capture and paste using a *dummy workbook*, and what screenshots or logs to send back.

### Quick setup (Windows)

1. Get the **approved installer** from the owner, or—if you have access—from the [private Releases page](https://github.com/surajTech-22/Desk-space/releases/latest).
2. Close any running DeskSpace Workspace, floating circle and system-tray process **gracefully**.
3. Verify its SHA-256 checksum against the accompanying file when provided, then run the installer.
4. Open **Start → DeskSpace**. If needed, use `%LOCALAPPDATA%\Programs\DeskSpace\DeskSpace.exe`.
5. Click **Show Live Donut Widget** on Home. Explore the modules and note the **version badge**.
6. Report unexpected behavior using the [test checklist and issue template](docs/TESTING_GUIDE.md#6-what-a-useful-failure-report-contains).

### Important test and data safety

Use only **invented test data**. The current preview UI may display **“local data is not encrypted”** and **“no password vault yet.”** Do not test with genuine passwords, private client records or confidential documents. Excel paste tests should use disposable workbook copies.

### Build/version status

- The latest **official GitHub release as checked on 22 Oct 2026** is **v1.4.266**.
- **R42** is the development/manual Windows test stream in branch [`r42-excel-mapping-editor`](https://github.com/surajTech-22/Desk-space/tree/r42-excel-mapping-editor). A locally compiled R42 instance may show a higher build number; that alone does **not** mean it is a published release.
- This repository is **private**; testers must be invited, or receive the approved installer/guide from the owner.
- Automatic GitHub Actions Windows builds are disabled to conserve the exhausted free Actions allocation. Local owner-side builds use the [manual update documentation](r42/manual-update/README.md) on the R42 branch.

### Feature navigation

The **Workspace** sidebar is for editing and detailed views. The floating **donut** is for quick navigation, hover preview and mouse interactions. The Excel Mapping Workspace is where saved mappings can be reviewed and edited. For Excel paste, the target workbook must already be open in Microsoft Excel desktop, and all safety gates must pass.

**Excel wheel redesign under development:** A single **Excel Functions** main sector will contain **Clipboard** and **Information** children. Inside Excel Information, ungrouped saved mappings appear directly and grouped mappings appear under the chosen group name, retaining the current nested donut navigation. Grouping is *not* a separate Excel function. Also planned: compact Excel Workspace layout and an Excel-only paste-success notice that disappears after three seconds. Do not report these as completed until the specific build implements them.

## Feedback and issues

[Test instructions, checklist and bug-report format](docs/TESTING_GUIDE.md) · [Private repository issue tracker](https://github.com/surajTech-22/Desk-space/issues)

A report should include **installed version, test ID, expected/actual behavior, reproduction steps, and a screenshot/log**. Never share tokens or real private Excel data in issue screenshots.
