$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'private static readonly List<Held> held=new();',
 'private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();',
 'private static readonly string staging=Path.Combine(',
 'private static bool busy;',
 'AppDomain.CurrentDomain.ProcessExit',
 'public static bool IsActive=>Views.Count>0;',
 'Views.Add(this);',
 'Views.Remove(this);',
 'RefreshViews();',
 'public static IReadOnlyList<string> ActivePaths=>',
 'public static void ImportClipboardFiles()=>Views.FirstOrDefault()?.ImportFromClipboard();',
 'public static void ClearActive()=>Views.FirstOrDefault()?.ClearAll();',
 'if(!held.Remove(item))return;',
 'if(held.Count>=25)break;'
)){
 if(!$s.Contains($token)){throw "R155 shared held-file integration missing: $token"}
}
foreach($unsafe in @('active=this;','if(ReferenceEquals(active,this))active=null;',
 'if(disposed){DeletePath(destination);break;}',
 'if(Directory.Exists(staging))Directory.Delete(staging,true); }')){
 if($s.Contains($unsafe)){throw "R155 still has single-view or deletion regression: $unsafe"}
}
foreach($token in @('TestSharedHeldFilesV155();',
 'private static void TestSharedHeldFilesV155()',
 'DeskSpaceClipboardV10.Shelf.Count==1',
 'R155 secondary deck must display file held on first monitor',
 'R155 closing primary widget must not delete secondary',
 'R155 removing a file on either monitor clears shared queue')){
 if(!$smoke.Contains($token)){throw "R155 real two-widget smoke missing: $token"}
}
Write-Host 'R155 PASS: shared held-files and staging, non-destructive monitor detach, two interactive Deck views and WPF tests installed.'
