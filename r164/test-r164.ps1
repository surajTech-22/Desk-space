$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$deck=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$copy=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFolderCopyV164.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    'private static bool copyInProgress;',
    'private static CancellationTokenSource? copyCancellation;',
    'Content="Cancel Copy"',
    'cancelCopy.Click+=(_,_)=>copyCancellation?.Cancel();',
    'DeskSpaceFolderCopyV164.Copy(',
    'cancellation.Token.ThrowIfCancellationRequested();',
    'DeletePath(destination);',
    'catch(OperationCanceledException)',
    'originalByStaged[destination]=Path.GetFullPath(originalSource);',
    'private static readonly List<Held> held=new();'
)){
    if(!$deck.Contains($token)){throw "R164 shared Deck integration missing: $token"}
}
foreach($token in @(
    'string partial=destination+".partial";',
    'Action<long,long,int,int>? onProgress=null',
    'cancellation.ThrowIfCancellationRequested();',
    'if(folder)Directory.Move(partial,destination);',
    'else File.Move(partial,destination);',
    'if(File.Exists(partial))File.Delete(partial);',
    'else if(Directory.Exists(partial))Directory.Delete(partial,true);',
    'FileAttributes.ReparsePoint',
    '200,000 file staging limit'
)){
    if(!$copy.Contains($token)){throw "R164 copy worker safety missing: $token"}
}
foreach($token in @(
    'TestCancellableCopyV164();',
    'private static void TestCancellableCopyV164()',
    'R164 cancelled copy must delete only incomplete Deck staging',
    'R164 copy must stage intact nested content'
)){
    if(!$smoke.Contains($token)){throw "R164 WPF acceptance missing: $token"}
}
Write-Host 'R164 PASS: cancel UI in shared Deck, streamed copy with atomic staging and real-file WPF regression installed.'
