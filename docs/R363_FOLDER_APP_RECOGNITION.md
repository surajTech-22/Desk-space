# R363 — Folder application recognition

## Existing interaction preserved
Explorer folder dragging continues to display DeskSpace's existing two-part ZIP / Copy circle. A cancelled drag does nothing and returns to the circle (R359). After a folder is **successfully dropped onto ZIP or Copy**, DeskSpace detects which installed app can open its original source folder. A compact **Folder · <name>** section appears in the current shared Running Apps Deck, not in a new window. Buttons open the project with a specific installed app or the original folder with File Explorer. No app is launched automatically by hover, drop, startup, background refresh, or updates.

## Recognition
Inference uses at most 256 immediate folder entries; no recursion, content/OCR, cloud calls, or permanent record. Strong signals:
- `.sln` / `.slnx` → Visual Studio solution (Visual Studio, VS Code).
- `settings.gradle[.kts]` with Gradle wrapper or build file → Android Studio, VS Code.
- `.Rproj` → RStudio, VS Code.
- `.code-workspace` → Visual Studio Code.
- `pyproject.toml`, `Pipfile`, `requirements.txt`, `setup.py` → VS Code, also PyCharm if `.idea` is present.
- `package.json`, `tsconfig.json`, `.vscode` → VS Code.
- `.csproj`, `.fsproj`, `.vbproj` → Visual Studio, VS Code.
- Exactly one recognized Office/PDF/design document with no other root files → Windows default app for the document.
- No confident signature, many ambiguous documents, missing installed app → File Explorer fallback.

The app finder uses Windows App Paths registration or fixed conventional install locations only. It does **not** run an executable, script or shortcut found within the dropped folder. Buttons for unavailable known apps are disabled. Selection is explicit, with command arguments structured separately. A project-file target is revalidated to be an ordinary file within the original folder on click.

## Security and release behavior
No automatic execution, file rename, copy, deletion, or recursive scan is introduced by recognition. Existing ZIP/Copy actions perform only their original user-selected transactions. Recognition is session-only and shared across the two monitor Deck views. It is cleared when the last widget closes and dismissed by the user. The underlying ZIP/Copy wedge design and Desktop, Excel, Search, update supervisor and tray logic remain unchanged.

## Verification
- Native Windows signature, ambiguity, unsupported folder, app-installed and process-launch consent tests: `r363/Tests/FolderAppRecognitionTests.csproj`.
- Source and native WPF/Deck compilation: `test-r363-recognition.yml`.
- Rebuilt R232 published-installer shipping source plus R353/R355/R359/R361 compatibility, WPF, updater, Search and Deck regressions: `test-r363-shipping-full.yml`.
- These workflows **do not publish** Stable/Tester installers. Real drag/drop behavior on the user's machine remains to be accepted after an explicit release.
