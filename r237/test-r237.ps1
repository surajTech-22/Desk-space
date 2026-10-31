$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceContextSuggestionsV237.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static string Classify(string? processName)',
 'GetWindowThreadProcessId(hwnd,out uint pid);',
 'SetWinEventHook(ForegroundEvent,ForegroundEvent,',
 'UnhookWinEvent(hook);',
 'if(pid==0 || pid==Environment.ProcessId)return;',
 'string proposed=Classify(process.ProcessName);',
 'panel.Closed+=(_,_)=>session.Dispose();',
 'start.Click+=(_,_)=>session.Start();',
 'stop.Click+=(_,_)=>session.Stop();',
 'Nothing is saved, sent or pasted'
)){
 if(!$s.Contains($needle)){throw ('R237 missing opt-in privacy guard '+$needle)}
}
foreach($forbidden in @('GetWindowText','Clipboard.Get','Clipboard.Set',
    'File.ReadAll','HttpClient','File.WriteAll','PasswordBox','Environment.Exit','Process.Kill')){
 if($s.Contains($forbidden)){throw ("R237 context must not capture/modify user data: "+$forbidden)}
}
if(!$hub.Contains('DeskSpaceContextSuggestionsV237.Show(this)')){
 throw 'R237 Context Suggestions not reachable from Features'
}
if(!$t.Contains('TestExplicitContextSuggestionsV237();')){
 throw 'R237 context classification native test is not registered'
}
Write-Host 'R237 STATIC PASS: explicit temporary foreground hook, no window titles/docs, no password, clipboard, file or network access.'
