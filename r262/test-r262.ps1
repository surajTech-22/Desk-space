$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePinnedRecentV122.cs')
$choice=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePinnedDocumentChoiceV262.cs')
$picker=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePinnedRecentPickerV262.cs')
$apps=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceApplicationsV55.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const int MaxResults=10;',
 'FileAttributes.ReparsePoint',
 'DeskSpacePinnedRecentV122.SameExecutableV122(',
 'Directory.EnumerateFiles(folder,"*.lnk")',
 '.OrderByDescending(File.GetLastWriteTimeUtc).Take(70)',
 'internal static bool CanLaunchRecentV262(string pinned,string document)',
 'if(!DeskSpacePinnedRecentV122.CanLaunchRecentV262(pinned,document))',
 'canvas.PreviewMouseRightButtonDown+=RecentRightClickV262;',
 'DeskSpacePinnedRecentPickerV262.Show(owner,pinnedPath)',
 'Process.Start(new ProcessStartInfo(document)',
 'TestPinnedRecentDocumentChoicesV262();'
)){
 if(!$source.Contains($needle) -and !$choice.Contains($needle) -and
    !$picker.Contains($needle) -and !$apps.Contains($needle) -and
    !$smoke.Contains($needle)){
  throw 'R262 pinned recent actions missing '+$needle
 }
}
foreach($bad in @('File.WriteAll','File.Append','File.Move','Clipboard.',
    'DispatcherTimer','FileSystemWatcher','HttpClient','Task.Run(')){
 if($picker.Contains($bad) -or $choice.Contains($bad)){
  throw 'R262 chooser must not persist private document paths or run in background: '+$bad
 }
}
Write-Host 'R262 STATIC PASS: per-app validated recent shortcut chooser, explicit right click, no background/path persistence.'
