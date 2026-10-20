$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r20/DeskSpaceMediaV20.cs' "$root/DeskSpaceMediaV18.cs" -Force
Copy-Item 'r20/DeskSpaceSearchIndexV20.cs' "$root/DeskSpaceSearchIndexV13.cs" -Force
function Load([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$value){[IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))}
function Must([string]$old,[string]$new){
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($old)){throw "R20 missing code anchor: $old"}
 $script:s=$script:s.Replace($old,$new)
}
$p="$root/WidgetHost.cs";$s=Load $p
Must 'attachedShelf?.SetMediaOffset(218);' 'attachedShelf?.SetMediaOffset(media.ShelfOffset);'
Save $p $s
$p="$root/DeskSpaceSearchV11.cs";$s=Load $p
Must 'status.Text=found.Count+" results"+' @'
                var coverage=DeskSpaceSearchIndexV13.Coverage;
                status.Text=found.Count+" results · case-insensitive"+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · scanning":" · scanned")+
                    (coverage.Length>0?" · "+coverage:" · awaiting drives")+
'@
Must 'ToolTip="Search files, apps, Windows Settings and notes"' 'ToolTip="Case-insensitive search of apps, settings and accessible drives. This PC lists drives; D: report restricts search; content:word searches indexed file contents."'
# Do not truncate the index status with the former fixed one-line title.
Must 'FontSize=12,Margin=new Thickness(8,2,0,3)' 'FontSize=10,TextWrapping=TextWrapping.Wrap,MaxHeight=31,Margin=new Thickness(8,2,0,3)'
Save $p $s
Write-Host 'R20: actual available drives with case-insensitive scoped queries, compact draggable media panel, supported-only controls, animated artwork, circle and player minimize.'
