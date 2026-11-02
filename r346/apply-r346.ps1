$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'DeskSpaceSearchIndexHealthV260.cs'
if(!(Test-Path -LiteralPath $file)){
 throw 'R346 shipping search index health framework missing'
}
Copy-Item 'r346/DeskSpaceSearchDiskHealthV346.cs' (Join-Path $root 'DeskSpaceSearchDiskHealthV346.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R346 $why expected one matching source, got $hits"}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        var start=new Button{
            Content="Start indexing names",
'@ @'
        var diskHealthV346=new Button{
            Content="Inspect disk cache",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var start=new Button{
            Content="Start indexing names",
'@ 'new disk index health command in existing Search index health panel'
One @'
        buttons.Children.Add(refresh);buttons.Children.Add(start);
'@ @'
        buttons.Children.Add(refresh);buttons.Children.Add(diskHealthV346);
        buttons.Children.Add(start);
'@ 'reuse existing index health actions'
One @'
        refresh.Click+=(_,_)=>Update();
'@ @'
        refresh.Click+=(_,_)=>Update();
        diskHealthV346.Click+=(_,_)=>
            DeskSpaceSearchDiskHealthV346.Show(window);
'@ 'open read-only cache report only when user clicks'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R346: persistent compact search index footprint and old-cache warning visible from existing Search health panel; no files modified.'
