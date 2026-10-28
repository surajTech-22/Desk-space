$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$s=[IO.File]::ReadAllText($p)
foreach($needle in @('private void Render()','private async Task StageFilesAsync(','private async Task CommitFolderDropAsync(','private static bool busy;','private void Remove(Held item)')){
  $i=$s.IndexOf($needle,[StringComparison]::Ordinal)
  Write-Host "--- R162_INSPECT $needle index=$i ---"
  if($i -ge 0){Write-Host ($s.Substring($i,[Math]::Min(4200,$s.Length-$i)))}
}
