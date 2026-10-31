$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckOffsetV228.cs')
foreach($needle in @(
 'using System.Threading;',
 'using System.Security.Cryptography;',
 'private static string MutexName=>',
 'Path.GetFullPath(FilePath).ToUpperInvariant()',
 'using var crossProcess=new Mutex(false,MutexName);',
 'crossProcess.WaitOne(TimeSpan.FromSeconds(2));',
 'catch(AbandonedMutexException){acquired=true;}',
 'finally{crossProcess.ReleaseMutex();}',
 'map[device]=Clamp(value);',
 'File.Move(tmp,FilePath,true);'
)){
 if(!$s.Contains($needle)){throw ('R235 missing cross-process persist guard: '+$needle)}
}
$start=$s.IndexOf('internal static void Save(')
$end=$s.Length
$save=$s.Substring($start,$end-$start)
if($save.IndexOf('crossProcess.WaitOne') -lt 0 -or
   $save.IndexOf('crossProcess.WaitOne') -gt $save.IndexOf('map[device]=Clamp(value)')){
 throw 'R235 Deck must acquire mutex BEFORE reading/updating the device map'
}
Write-Host 'R235 STATIC PASS: per-monitor Deck state protected by storage-scoped OS mutex, atomic save preserved.'
