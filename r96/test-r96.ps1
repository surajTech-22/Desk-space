$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$a=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
$h=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
foreach($token in @(
  'private const uint WmClose=0x0010',
  'private static extern bool PostMessage(',
  'internal static bool RequestClose(Entry entry)',
  'string.Equals(WindowText(entry.Handle),entry.WindowTitle,',
  'PostMessage(entry.Handle,WmClose,IntPtr.Zero,IntPtr.Zero)')){
 if(!$a.Contains($token)){throw "R96 missing normal close request: $token"}
}
foreach($token in @(
  'MakeCloseButton(DeskSpaceRunningAppsV88.Entry window)',
  'DeskSpaceRunningAppsV88.RequestClose(window)',
  'e.Handled=true',
  'DockPanel.SetDock(close,Dock.Right)',
  'windowItems.Children.Add(row)',
  'if(choices.Length==1)',
  'tile.Children.Add(close)')){
 if(!$h.Contains($token)){throw "R96 missing independent close UI: $token"}
}
if($a.Contains('Process.Kill(') -or $h.Contains('Process.Kill(')){
 throw 'R96 must not force-kill applications'
}
Write-Host 'R96 PASS: close window by normal WM_CLOSE, no process kill, independent per-window and single-window app buttons'
