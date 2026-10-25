# DeskSpace R111 portable Excel acceptance (development only)

This ZIP is an **isolated test**, NOT a DeskSpace installer and NOT a stable/tester update.

1. Save your Excel work and close all existing Microsoft Excel windows normally.
2. Extract all files to a local folder; do not launch the app executable.
3. In Windows Explorer, right-click `Run_R111_Excel_LiveAcceptance.ps1` → Run with PowerShell (if your system allows local scripts), or execute it from PowerShell in the extracted folder.
4. The test creates a temporary Microsoft Excel session and disposable sample workbooks, exercises *real Excel Table creation*, merged areas, underlines, formulas, completely empty target, fully matching target, partial-data rejection, header mismatch and injected-failure rollback, then closes the new Excel instance.
5. Upload the diagnostic `DeskSpace_R111_LiveExcel_YYYYMMDD_HHMMSS.zip` created under your Windows Downloads folder to the development conversation. A failed/blocked result is useful and must not be called a pass.

It does not install DeskSpace, change your existing DeskSpace workspace, run VBA, or intentionally open/edit any personal workbook. If Excel is already running, the script blocks before the test begins. Use a disposable Windows session if possible.

**Release rule:** No Stable/Tester publishing unless this real Excel COM test passes and the exact current development code also passes Windows compilation, WPF visual checks and installer checksum checks.
