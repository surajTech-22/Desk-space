$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r21/DeskSpaceMediaV21.cs' "$root/DeskSpaceMediaV18.cs" -Force
function Load([string]$file){[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$file,[string]$content){[IO.File]::WriteAllText($file,$content,[Text.UTF8Encoding]::new($false))}
function Must([string]$old,[string]$replacement)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R21 search anchor missing: $old"}
    $script:s=$script:s.Replace($old,$replacement)
}
$p="$root/DeskSpaceSearchV11.cs";$s=Load $p
# Compact search is intentionally narrow; only the list grows. No verbose
# per-drive status strings filling multiple lines and obscuring results.
Must @'
                status.Text=found.Count+" results · case-insensitive"+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · scanning":" · scanned")+
                    (coverage.Length>0?" · "+coverage:" · awaiting drives")+
'@ @'
                status.Text=found.Count+" results"+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · indexing drives":" · ready")+
'@
Must 'FontSize=10,TextWrapping=TextWrapping.Wrap,MaxHeight=31,Margin=new Thickness(8,2,0,3)' 'FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(8,3,3,4)'
Must 'var coverage=DeskSpaceSearchIndexV13.Coverage;' @'
                var coverage=DeskSpaceSearchIndexV13.Coverage;
                status.ToolTip=coverage.Length>0?coverage:"Waiting for accessible drives";
'@
Must 'Background=new SolidColorBrush(Color.FromRgb(250,248,255))' 'Background=new SolidColorBrush(Color.FromRgb(250,251,254))'
Must 'BorderBrush=new SolidColorBrush(Color.FromRgb(164,146,212))' 'BorderBrush=new SolidColorBrush(Color.FromRgb(190,198,218))'
Must 'Background=Brushes.White,CornerRadius=new CornerRadius(9)' 'Background=Brushes.White,CornerRadius=new CornerRadius(11)'
Save $p $s
Write-Host 'R21: pill player retains artwork/title/play, compact media with optional advanced controls and elegant search results.'
