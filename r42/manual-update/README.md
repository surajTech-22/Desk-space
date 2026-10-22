# DeskSpace R42 — GitHub-sourced manual update

The canonical R42 source is maintained in the private branch `r42-excel-mapping-editor`.

The **Windows V3 UPDATE_APP.bat launcher** uses the user's authenticated GitHub CLI to resolve the current branch commit, downloads the private source archive for exactly that commit, and reconstructs/builds the original Windows project locally. This avoids GitHub Actions minutes and does not require Git.

The V2 diagnostic revealed that PowerShell's current location and the Win32 process working directory diverged. The V3 launcher synchronizes both paths before every workflow step, fixing the R2 `MainWindow.xaml.cs` file-not-found error.

For later corrections, modify source and patches in this branch. The same V3 launcher can be rerun to build the newest branch revision; it does not use the old hard-coded commit.

**Release gate:** Do not install or present R42 as compiled until the local Windows build, visual tests, installer checksum, and packaging have completed. Compilation failures produce `DeskSpace_R42_DIAGNOSTICS_*.zip` directly under Windows Downloads. The existing installed DeskSpace data remain unchanged when a build fails.

The V3 launcher itself is distributed as a compact, user-run Windows ZIP in the ChatGPT conversation. GitHub stores the application source and all R42 application changes; it is **not** a public unauthenticated executable update endpoint.
