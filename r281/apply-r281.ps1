$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$original=[IO.File]::ReadAllText("$root/DeskSpaceWorkspaceProfilesV85.cs")
foreach($guard in @(
 'private static readonly object Gate=new();',
 'var profiles=Load().ToList();',
 'File.Move(temp,Filename,true);',
 'ImportNewProfilesV201('
)){
 if(!$original.Contains($guard)){
  throw ('R281 Workspace profile source baseline drift: '+$guard)
 }
}
Copy-Item -LiteralPath 'r281/DeskSpaceProfileStoreV281.cs' -Destination "$root/DeskSpaceProfileStoreV281.cs" -Force
Copy-Item -LiteralPath 'r281/DeskSpaceWorkspaceProfilesV281.cs' -Destination "$root/DeskSpaceWorkspaceProfilesV85.cs" -Force
Write-Host 'R281: Workspace profile Save and Import are cross-process and fail closed on malformed storage.'
