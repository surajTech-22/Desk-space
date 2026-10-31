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
foreach($term in @(
 'lock(Gate)',
 'current.Count+fresh.Count>24',
 'known.Contains(x.Name)',
 'File.Copy(Filename,backup,false)',
 'File.Move(temp,Filename,true)'
)){
 if(!$storage.Contains($term)){throw ("R201 missing atomic non-overwrite gate "+$term)}
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
