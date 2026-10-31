$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceProfileStoreV281.cs")
$profile=[IO.File]::ReadAllText("$root/DeskSpaceWorkspaceProfilesV85.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal static List<DeskSpaceProfileV85> ReadForWrite(',
 'new Mutex(false,MutexName(path))',
 'mutex.WaitOne(TimeSpan.FromSeconds(3))',
 'var current=ReadForWrite(path);',
 'if(!names.Add(item.Name))',
 'DeskSpaceProfilePortabilityV201.Validate(item);',
 'File.Move(temp,path,true);',
 'File.Copy(path,backup,false);',
 'internal static void SaveAt(',
 'internal static int ImportAt(',
 'DeskSpaceProfileStoreV281.SaveAt(Filename,profile);',
 'DeskSpaceProfileStoreV281.ImportAt(Filename,incoming);',
 'TestWorkspaceProfileStorageV281();',
 'R281 damaged profile database must survive Save and Import'
)){
 if(!$source.Contains($token) -and !$profile.Contains($token) -and
    !$native.Contains($token)){throw ('R281 missing profile safety: '+$token)}
}
if($profile.Contains('var profiles=Load().ToList();') -or
   $profile.Contains('var current=Load().ToList();')){
 throw 'R281 old lossy profile write based on empty-list fallback remains'
}
Write-Host 'R281 STATIC PASS: Workspace profile saves and imports preserve corrupt storage and independent writers.'
