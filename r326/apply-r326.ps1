$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$index=Join-Path $root 'DeskSpaceSearchIndexV13.cs'
$donut=Join-Path $root 'DeskSpaceSearchV11.cs'
$workspace=Join-Path $root 'UniversalSearch.cs'
foreach($p in @($index,$donut,$workspace)){
 if(!(Test-Path -LiteralPath $p)){throw ("R326 missing actual shipping source: "+$p)}
}
Copy-Item 'r326/DeskSpaceSearchFiltersV326.cs' (Join-Path $root 'DeskSpaceSearchFiltersV326.cs') -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
function ReplaceOne([string]$path,[string]$old,[string]$new,[string]$reason)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw ("R326 "+$reason+" expected one source match, found "+$hits)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
ReplaceOne $index @'
        var ranked=new DeskSpaceSearchRankingV192.Collector(fileQuery);
        bool Add(SearchHit hit)=>ranked.Add(hit);
'@ @'
        // Unified, explicit and local-only search facets. Parse before the
        // ranking collector so irrelevant hits never consume its per-category
        // result budget (files must not be buried by folder matches).
        var facetsV326=DeskSpaceSearchFiltersV326.Parse(fileQuery);
        fileQuery=facetsV326.Term;
        var ranked=new DeskSpaceSearchRankingV192.Collector(fileQuery);
        bool Add(SearchHit hit)=>
            facetsV326.Matches(hit)&&ranked.Add(hit);
'@ 'shared search filter before result quota'
ReplaceOne $donut @'
        if(appPinMode)top.ToolTip="Applications · type to find and pin an installed app";
'@ @'
        if(appPinMode)top.ToolTip="Applications · type to find and pin an installed app";
        else top.ToolTip="Advanced file search: report ext:pdf · type:sheet · in:Downloads · after:2026-01-01 · min:1mb";
'@ 'donut search hint'
ReplaceOne $donut @'
            :"Search files, apps, Windows Settings and notes";
'@ @'
            :"Search everything accessible on this PC. Examples: invoice ext:pdf, type:sheet, in:Downloads, after:2026-01-01, min:1mb";
'@ 'donut input accessible hint'
ReplaceOne $workspace @'
        top.Children.Add(query);top.Children.Add(category);top.Children.Add(status);
'@ @'
        query.ToolTip="Search names using: invoice ext:pdf · type:folder · type:sheet · in:Downloads · after:2026-01-01 · min:1mb · max:20mb";
        top.Children.Add(query);top.Children.Add(category);
        var filtersHelpV326=new TextBlock {
            Text="Filters: ext:pdf  type:sheet  in:Downloads  after:2026-01-01  min:1mb",
            FontSize=11,Foreground=System.Windows.Media.Brushes.DimGray,
            Margin=new Thickness(13,0,13,5),
            TextWrapping=TextWrapping.Wrap
        };
        top.Children.Add(filtersHelpV326);top.Children.Add(status);
'@ 'Workspace advanced filename filter hint'
$shared=Get-Content $index -Raw
$tool=Get-Content $workspace -Raw
if(!$shared.Contains('facetsV326.Matches(hit)&&ranked.Add(hit)') -or
 !$tool.Contains('filtersHelpV326')){
 throw 'R326 shipping source lacks shared search filters or Workspace guidance'
}
Write-Host 'R326: same advanced file-name filters in donut and Workspace, applied BEFORE ranking; no duplicate index, no file content access.'
