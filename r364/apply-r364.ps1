$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'DeskSpaceDropShelfV15.cs'
$recognizer=Join-Path $root 'DeskSpaceFolderAppRecognitionV363.cs'
if(!(Test-Path -LiteralPath $file) -or !(Test-Path -LiteralPath $recognizer)){
    throw 'R364 requires actual assembled R363 WPF Deck and recognition engine'
}
# The vendor recognizer is identical between regular source and shipping
# assembly. Do not replace the older R232 Deck with the development Deck.
Copy-Item 'src/DeskSpace/DeskSpaceFolderAppRecognitionV363.cs' $recognizer -Force
Copy-Item 'src/DeskSpace/DeskSpaceAccountingFileRecognitionV364.cs' (Join-Path $root 'DeskSpaceAccountingFileRecognitionV364.cs') -Force
$s=[IO.File]::ReadAllText($file)
$lf=[string][char]10
$s=$s.Replace(([string][char]13+$lf),$lf)
function One([string]$old,[string]$replacement,[string]$what){
    $old=$old.Replace(([string][char]13+$script:lf),$script:lf)
    $replacement=$replacement.Replace(([string][char]13+$script:lf),$script:lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){
        throw "R364 shipping $what requires one anchor; actual $count"
    }
    $script:s=$script:s.Replace($old,$replacement)
}
One @'
        if(files?.Length>0)_=StageFilesAsync(files);
'@ @'
        if(files?.Length==1 && File.Exists(files[0]))
        {
            // Direct file open only. No stage, ZIP, copy or import.
            e.Effects=TryDirectOpenDroppedPathV365(files[0])==
                DirectOpenV365.Opened?DragDropEffects.Link:DragDropEffects.None;
            return;
        }
        e.Effects=DragDropEffects.None;
'@ 'standalone file drop direct-open only'
One @'
                if(!busy&&selected is bool zip && sources.Length>0)
                {
                    SuggestFolderAppsV363(sources);
                    _=CommitFolderDropAsync(sources,zip);
                }
'@ @'
                if(!busy&&selected is bool zip && sources.Length>0)
                {
                    var opened=TryDirectOpenDroppedPathV365(sources[0]);
                    if(opened==DirectOpenV365.NotRecognized)
                        _=CommitFolderDropAsync(sources,zip);
                    else e.Effects=opened==DirectOpenV365.Opened
                        ?DragDropEffects.Link:DragDropEffects.None;
                }
'@ 'recognized folder open instead of copy, preserve original unrecognized folder action'
One @'
        if(dropped.Length!=1 || !Directory.Exists(dropped[0]))
        {
            RefreshViews();
            return;
        }
        string folder=dropped[0];
        _=Task.Run(()=>DeskSpaceFolderAppRecognitionV363.Recognize(folder))
'@ @'
        if(dropped.Length!=1 ||
           (!Directory.Exists(dropped[0]) && !File.Exists(dropped[0])))
        {
            RefreshViews();
            return;
        }
        string source=dropped[0];
        _=Task.Run(()=>DeskSpaceFolderAppRecognitionV363.RecognizePath(source))
'@ 'detect safe registered single files and vendor formats'
$sourceDeck=[IO.File]::ReadAllText('src/DeskSpace/DeskSpaceDropShelfV15.cs')
$sourceDeck=$sourceDeck.Replace(([string][char]13+$lf),$lf)
$begin=$sourceDeck.IndexOf('    private enum DirectOpenV365')
$finish=$sourceDeck.IndexOf('    private void SuggestFolderAppsV363(',$begin)
if($begin -lt 0 -or $finish -lt $begin){
    throw 'R365 missing tested direct-open-only function in checked-in WPF source'
}
$directOpen=$sourceDeck.Substring($begin,$finish-$begin)
One '    private void SuggestFolderAppsV363(string[] dropped)' ($directOpen+'    private void SuggestFolderAppsV363(string[] dropped)') 'direct-open helper in actual shipping widget'
One @'
            var folderName=Path.GetFileName(match.Folder.TrimEnd(
                Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            top.Children.Add(Text("Folder · "+folderName,11,Brushes.White));
'@ @'
            bool targetIsFile=!string.Equals(match.Target,match.Folder,
                StringComparison.OrdinalIgnoreCase);
            var sourceName=targetIsFile?Path.GetFileName(match.Target):
                Path.GetFileName(match.Folder.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            top.Children.Add(Text((targetIsFile?"File · ":"Folder · ")+
                sourceName,11,Brushes.White));
'@ 'original file/folder label'
One @'
            foreach(var choice in recognizedFolderV363.Take(3))
'@ @'
            // Show Explorer fallback even for mixed Tally and Winman folders.
            foreach(var choice in recognizedFolderV363.Take(5))
'@ 'multiple vendor candidates and Explorer'
One @'
                        :choice.AppId=="Default"
                            ?"Open document with Windows default"
                            :"Open with "+choice.AppName,
'@ @'
                        :choice.AppId=="Default"
                            ?"Open with "+choice.AppName
                            :"Open "+choice.AppName,
'@ 'show Windows registered application friendly name'
if(!$s.Contains('CommitFolderDropAsync(sources,zip)') -or
   !$s.Contains('folderLayer.Remove(folderAdorner)') -or
   !$s.Contains('DeskSpaceFolderAppRecognitionV363.RecognizePath(source)') -or
   !$s.Contains('DeskSpaceFolderAppRecognitionV363.TryOpen(choice,out var error)') -or
   !$s.Contains('TryDirectOpenDroppedPathV365') -or
   !$s.Contains('DirectOpenV365.Opened')){
    throw 'R364 lost shipping ZIP/Copy or recognized-application click handling'
}
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R364: actual shipping Deck recognizes Tally/Winman + safe associated standalone files; original ZIP/Copy unchanged.'
