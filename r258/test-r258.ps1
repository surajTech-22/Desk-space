$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$policy=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceContextAuditV258.cs')
$ui=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceContextSuggestionsV237.cs')
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static string Summary(bool listening,int events,string? category)',
 'if(!listening)',
 'Math.Clamp(events,0,10000)',
 'Show privacy audit',
 'new Session(panel,owner,status,suggestions,auditV258)',
 'auditButtonV258.Click+=',
 'if(disposed || hook==IntPtr.Zero || !dialog.IsLoaded)return;',
 'recognizedV258=0;',
 'suggestions.Children.Clear();',
 'TestContextPrivacyAuditV258();'
)){
 if(!$policy.Contains($needle) -and !$ui.Contains($needle) -and !$test.Contains($needle))
 {throw ('R258 context privacy invariant missing: '+$needle)}
}
foreach($needle in @('File.WriteAll','Clipboard.','HttpClient','Registry.',
 'Process.Start','SetWindowsHookEx','Task.Run','DispatcherTimer')){
 if($policy.Contains($needle)){throw ('R258 privacy model must be side-effect-free: '+$needle)}
}
Write-Host 'R258 STATIC PASS: on-demand category-only audit, queued event stop guard, no persisted foreground details.'
