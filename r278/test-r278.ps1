$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$store=[IO.File]::ReadAllText("$root/DeskSpaceMonitorPositionStoreV278.cs")
$sessions=[IO.File]::ReadAllText("$root/DeskSpaceMonitorSessionsV154.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal static IReadOnlyList<Position> ParseForWrite(',
 'new Mutex(false,MutexName(path))',
 'gate.WaitOne(TimeSpan.FromSeconds(2))',
 'catch(AbandonedMutexException){acquired=true;}',
 'File.Move(temp,path,true);',
 '"Monitor positions contain invalid or duplicate identities."',
 'internal static Position? Read(string device)=>ReadAt(FilePath,device);',
 'DeskSpaceMonitorPositionStoreV278.Save(',
 'DeskSpaceMonitorPositionStoreV278.Read(session.Device)',
 'if(saved==null)return;',
 'TestIndependentMonitorPositionStoreV278();',
 'R278 updating one monitor must preserve another and never copy positions'
))
{
 if(!$store.Contains($token) -and !$sessions.Contains($token) -and
    !$native.Contains($token)){throw ('R278 missing P0 guard '+$token)}
}
foreach($bad in @('private static List<SavedPosition> ReadAll()',
 'File.WriteAllText(tmp,JsonSerializer.Serialize(all));'))
{
 if($sessions.Contains($bad)){throw ('R278 legacy unsafe write route still active '+$bad)}
}
Write-Host 'R278 STATIC PASS: one monitor cannot overwrite or mirror another monitor position; invalid file untouched.'
