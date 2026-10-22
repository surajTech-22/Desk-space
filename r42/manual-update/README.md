# DeskSpace R42 — GitHub-sourced manual update

The canonical R42 source is maintained in the private branch `r42-excel-mapping-editor`.

The **Windows V3 UPDATE_APP.bat launcher** uses the user's authenticated GitHub CLI to resolve the current branch commit, downloads the private source archive for exactly that commit, and reconstructs/builds the original Windows project locally. This avoids GitHub Actions minutes and does not require Git.

The V2 diagnostic revealed that PowerShell's current location and the Win32 process working directory diverged. The V3 launcher synchronizes both paths before every workflow step, fixing the R2 `MainWindow.xaml.cs` file-not-found error.

For later corrections, modify source and patches in this branch. The same V3 launcher can be rerun to build the newest branch revision; it does not use the old hard-coded commit.

**Release gate:** Do not install or present R42 as compiled until the local Windows build, visual tests, installer checksum, and packaging have completed. Compilation failures produce `DeskSpace_R42_DIAGNOSTICS_*.zip` directly under Windows Downloads. The existing installed DeskSpace data remain unchanged when a build fails.

The V3 launcher itself is distributed as a compact, user-run Windows ZIP in the ChatGPT conversation. GitHub stores the application source and all R42 application changes; it is **not** a public unauthenticated executable update endpoint.

## V4 Windows install recovery (October 22, 2026)

An already-built R42 installer encountered **DeleteFile failed, code 5** while the installed `DeskSpace.exe` (v1.4.266) remained running. This is an installer file-in-use failure, not a new C# compiler error. Do not skip the locked EXE in the installer: cancel the old wizard.

The GitHub Windows workflow now runs `r42/apply-r42-installer.ps1` before version assignment and compilation. It sets Inno Setup `CloseApplications=yes`, `RestartApplications=no`, and the current compiled version label, while preserving the original `AppId`, install path, and all application data.

The V4 local launcher includes `INSTALL_EXISTING_R42.bat` for the **already-compiled, checksum-verified** `DeskSpace_Setup_R42_Local_*.exe` in Downloads, so no recompilation is needed. It asks the running installed DeskSpace to shut down gracefully; if the tray process remains, it requires the user to exit it rather than force-killing it. Then it makes a user-data backup outside the installed app, launches the installer with explicit close-application flags, records an installation log, and reports nonzero installer exit codes as failures.

For future source changes, use `UPDATE_APP.bat` from the V4 ZIP. The build still pulls the latest R42 private GitHub branch and uses no GitHub Actions minutes.

## V5 — Excel Functions hierarchy and private tester installer

The R42 main donut now has one **Excel Functions** segment, not separate Excel and Excel Clipboard segments. It opens **Clipboard** and **Information** child segments in the same curved donut style. Under Information, mappings with an empty optional group path appear directly. Mappings classified into `Clients/Audit` appear under **Clients → Audit**; there is no third main grouping segment. Hover is read-only; an explicit mapping selection is still required for paste.

Excel Mapping in the Workspace has a compact short-window layout and an editable optional **Information group** field. The success notice is dismissed three seconds **only after a successful Excel paste**. Failed/blocked paste messages and other modules are not timed out.

**Installer distribution:** The V5 `UPDATE_APP.bat` runs the pinned private R42 Windows build. Only after the full source gates, .NET compilation, WPF smoke checks, installer checksum, and guarded local installation succeed does it invoke `r42/manual-update/PUBLISH_VERIFIED_INSTALLER.ps1`. That script uploads the setup EXE and checksum as a **private GitHub pre-release** for testers. It does not replace the stable release, does not run GitHub Actions and refuses to overwrite an existing test release. If the Windows compiler or installation fails, no R42 installer is published.

This repository currently provides the implementation and publication mechanism; **the new V5 installer binary is not yet built/verified/uploaded by this chat session** because native Windows compilation must occur on a Windows computer. The resulting private test release will appear under [GitHub Releases](https://github.com/surajTech-22/Desk-space/releases) after the Windows build succeeds.
