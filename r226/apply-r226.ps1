$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r226/DeskSpaceDeckHeaderV226.cs' -Destination "$root/DeskSpaceDeckHeaderV226.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R226 "+$label+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
        var heading=Text(busy?"Copying…":
            "Deck · "+runningApps.Count+" apps · "+
            held.Count+" held · "+detected.Count+" new",
            11,Brushes.White);
        heading.Margin=new Thickness(6,0,0,0);
'@ @'
        var heading=Text(
            DeskSpaceDeckHeaderV226.Format(busy,runningApps.Count,
                held.Count,detected.Count),11,Brushes.White);
        heading.Margin=new Thickness(6,0,0,0);
        // WPF ScrollViewer may measure its StackPanel with unbounded width:
        // cap header text separately from the Deck's three right buttons.
        // Ellipsis prevents the title from pushing an owned Popup across
        // the source monitor when data counts grow.
        heading.MaxWidth=Math.Max(70,panel.MaxWidth-103);
        heading.MinWidth=0;
        heading.TextTrimming=TextTrimming.CharacterEllipsis;
'@ 'bounded concise Deck title'
Swap @'
                heading.Text=busy?"Copying…":
                    "Deck · "+appsNow.Count+" apps · "+
                    held.Count+" held · "+detected.Count+" new";
'@ @'
                heading.Text=DeskSpaceDeckHeaderV226.Format(
                    busy,appsNow.Count,held.Count,detected.Count);
'@ 'live dynamic count refresh'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R226: compact nonzero Deck counts and bound the header width so buttons remain visible.'
