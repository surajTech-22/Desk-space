@echo off
setlocal
set "BUNDLE=%~dp0"
if not exist "%BUNDLE%Run_R111_Excel_LiveAcceptance.ps1" (
  echo ERROR: Extract the entire ZIP before running this test.
  pause
  exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BUNDLE%Run_R111_Excel_LiveAcceptance.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
echo DeskSpace R111 isolated Excel test has completed or was blocked.
echo Check your Windows Downloads folder for DeskSpace_R111_LiveExcel_*.zip.
pause
exit /b %RESULT%
