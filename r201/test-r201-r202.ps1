$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$profile=[IO.File]::ReadAllText("$root/DeskSpaceProfilePortabilityV201.cs")
$storage=[IO.File]::ReadAllText("$root/DeskSpaceWorkspaceProfilesV85.cs")
$vault=[IO.File]::ReadAllText("$root/DeskSpaceVaultContextV202.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceVaultContextWindowV202.cs")
$features=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($term in @(
 'Unsupported widget-only profile format',
 'entries.GetArrayLength()>24',
 'Duplicate profile name',
 'Invalid profile name or field limits',
 'No passwords, notes, Excel data or reminders',
 'ImportNewProfilesV201(profiles)'
)){
 if(!$profile.Contains($term)){throw ("R201 missing profile safety gate "+$term)}
}
# R281 supersedes the original R201 import/write route. Checking for
# old source fragments would wrongly require the lossy Load()-based writer.
# The replacement MUST validate the existing file after a named OS lock,
# preserve established names and make a recovery copy before atomic import.
$strict=[IO.File]::ReadAllText("$root/DeskSpaceProfileStoreV281.cs")
foreach($term in @(
 'DeskSpaceProfileStoreV281.SaveAt(Filename,profile)',
 'DeskSpaceProfileStoreV281.ImportAt(Filename,incoming)'
)){
 if(!$storage.Contains($term)){throw ("R201/R281 profile writer not routed: "+$term)}
}
foreach($term in @(
 'var current=ReadForWrite(path);',
 'new Mutex(false,MutexName(path))',
 'rows.Count+fresh.Length>Limit',
 'rows.AddRange(fresh);',
 'File.Copy(path,backup,false);',
 'File.Move(temp,path,true);',
 'throw new InvalidDataException('
)){
 if(!$strict.Contains($term)){throw ("R201/R281 profile safety regression: "+$term)}
}
foreach($term in @(
 'uri.Scheme!=Uri.UriSchemeHttps',
 'IPAddress.TryParse(uri.Host,out _)',
 'uri.IdnHost',
 'vault.IsUnlocked',
 'StringComparison.Ordinal',
 'new Suggestion(entry.Id,entry.Label,actual)'
)){
 if(!$vault.Contains($term)){throw ("R202 missing exact-site or locked-vault protection "+$term)}
}
foreach($term in @(
 'vault.Unlock(password.Password)',
 'password.Clear();',
 'vault.Dispose()',
 'DeskSpaceVaultContextV202.Match(vault,website.Text)',
 'No passwords are filled or copied'
)){
 if(!$ui.Contains($term)){throw ("R202 missing manual UI privacy gate "+$term)}
}
foreach($term in @(
 'DeskSpaceProfilePortabilityV201.ExportWithDialog(this)',
 'DeskSpaceProfilePortabilityV201.ImportWithDialog(this)',
 'DeskSpaceVaultContextWindowV202.Show(this)'
)){
 if(!$features.Contains($term)){throw ("R201/202 not visible in Workspace Features "+$term)}
}
if(!$palette.Contains('new("vaultlookup"') -or
   !$palette.Contains('DeskSpaceVaultContextWindowV202.Show(owner)')){
 throw 'R202 manual vault lookup not discoverable in Action Palette'
}
foreach($term in @('TestPortableProfilesV201();','TestManualVaultSuggestionsV202();')){
 if(!$smoke.Contains($term)){throw ("R201/202 native test absent "+$term)}
}
if($vault.Contains('.Password') -or $ui.Contains('Clipboard.SetText') -or
   $ui.Contains('SendKeys(') -or $ui.Contains('Process.Start(')){
 throw 'R202 must never expose password values or automate filling'
}
Write-Host 'R201-R202 STATIC PASS: atomic non-overwrite profiles, bounded JSON, exact HTTPS metadata-only vault lookup.'
