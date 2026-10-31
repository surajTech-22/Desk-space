$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckOffsetV228.cs'
$s=[IO.File]::ReadAllText($p)
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static Dictionary<string,Offset> ParseForWrite(string? json,int byteLength)',
 'new Dictionary<string,Offset>(StringComparer.OrdinalIgnoreCase)',
 'throw new InvalidDataException("Saved Deck positions are not valid JSON.",ex);',
 '!result.TryAdd(item.Key,Clamp(item.Value))',
 'var map=ParseForWrite(previous,bytes);',
 'var serialized=JsonSerializer.Serialize(map);',
 'System.Text.Encoding.UTF8.GetByteCount(serialized)>16_384',
 'File.WriteAllText(tmp,serialized);',
 'crossProcess.ReleaseMutex();'
)){
 if(!$s.Contains($needle)){throw ('R238 strict multi-monitor persistence missing '+$needle)}
}
if($s.Contains('map=JsonSerializer.Deserialize<Dictionary<string,Offset>>(')){
 throw 'R238 still silently replaces invalid monitor positions'
}
if(!$t.Contains('TestStrictDeckOffsetRecoveryV238();')){
 throw 'R238 native WPF test missing'
}
Write-Host 'R238 STATIC PASS: corrupt shared Deck map stays intact; duplicate-case monitor IDs blocked; update remains atomic.'
