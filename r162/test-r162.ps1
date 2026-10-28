$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$deck=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$zip=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFolderZipV162.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'private static bool zipInProgress;',
 'private static int zipFilesWritten;',
 'private static CancellationTokenSource? zipCancellation;',
 'private static readonly Dictionary<string,string> pendingZipSources=',
 'Content="Cancel ZIP"',
 'cancel.Click+=(_,_)=>zipCancellation?.Cancel();',
 'DeskSpaceFolderZipV162.Create(',
 'pendingZipSources[zipPath]=source;',
 'pendingZipSources.Remove(zipPath);',
 'originalByStaged[destination]=Path.GetFullPath(originalSource);',
 'if(AlreadyHeldSource(source))continue;',
 'catch(OperationCanceledException)',
 'DeskSpaceFolderZipV162.PruneAbandoned('
)){
 if(!$deck.Contains($token)){throw "R162 ZIP Deck integration missing: $token"}
}
foreach($token in @(
 'cancellation.ThrowIfCancellationRequested();',
 'new FileStream(partial,FileMode.CreateNew,',
 'File.Move(partial,destination);',
 'File.Delete(partial);',
 'FileAttributes.ReparsePoint',
 'Guid.TryParseExact(name,"N",out _)',
 'DateTime.UtcNow-File.GetLastWriteTimeUtc(p)<grace'
)){
 if(!$zip.Contains($token)){throw "R162 streaming ZIP safety missing: $token"}
}
foreach($token in @(
 'TestCancellableZipV162();',
 'private static void TestCancellableZipV162()',
 'R162 ZIP must report file count',
 'R162 cancelled ZIP must not leak',
 'R162 cleanup must only remove old app-generated ZIP'
)){
 if(!$smoke.Contains($token)){throw "R162 WPF acceptance missing: $token"}
}
Write-Host 'R162 PASS: ZIP cancellation and shared progress controls, original-source dedup, streaming file checks and safe cleanup.'
