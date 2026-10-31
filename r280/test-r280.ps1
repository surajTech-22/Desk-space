$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$engine=[IO.File]::ReadAllText("$root/DeskSpaceSmartContextDeckV280.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'SetWinEventHook(ForegroundEvent,ForegroundEvent,',
 'DeskSpaceContextSuggestionsV237.Classify(',
 'string category="None";',
 'private static long session;',
 'if(activeHook==IntPtr.Zero || session!=version)return;',
 'if(changed)Changed?.Invoke();',
 'internal static void Stop()',
 'UnhookWinEvent(old);',
 'category="None";events=0;',
 'internal static Border CreatePanel(',
 'Content=state.Listening?"Stop":"Start"',
 'internal static void Open(Window owner,string id)',
 'DeskSpaceContextAuditV258.Summary(',
 'DeskSpaceSmartContextDeckV280.Changed+=ContextChangedV280',
 'DeskSpaceSmartContextDeckV280.Changed-=ContextChangedV280',
 'if(Views.Count==0)DeskSpaceSmartContextDeckV280.Stop();',
 'list.Children.Add(DeskSpaceSmartContextDeckV280.CreatePanel(',
 'TestSmartContextDeckOptInV280();'
)){
 if(!$engine.Contains($needle) -and !$deck.Contains($needle) -and
    !$tests.Contains($needle)){throw ('R280 missing Smart Context protection: '+$needle)}
}
foreach($bad in @('GetWindowText(', 'Clipboard.Get', 'File.ReadAllText(',
    'File.WriteAllText(', 'GetWindowThreadProcessId(hwnd,out uint pid); X')){
 if($engine.Contains($bad)){
  throw ('R280 session must not read/write content: '+$bad)
 }
}
Write-Host 'R280 STATIC PASS: opt-in WinEvent, app-category only, no persistence, shared Deck, Stop on last widget.'
