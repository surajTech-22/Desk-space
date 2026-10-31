$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r256/DeskSpaceFolderPreDropV256.cs' -Destination "$root/DeskSpaceFolderPreDropV256.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R256 "+$why+" expected exactly one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
# The existing R160 choice is already an adorner ON THE CIRCLE, not a
# Popup. Harden that existing path; never replace it with another HWND.
Swap @'
    private string[]? pendingFolderDrop;
'@ @'
    private string[]? pendingFolderDrop;
    private DeskSpaceFolderPreDropV256.Choice hoverChoiceV256;
'@ 'capture ZIP/Copy as user hovers existing adorner'
Swap @'
                folderAdorner?.Hover(point);
'@ @'
                folderAdorner?.Hover(point);
                var hovered=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),point);
                hoverChoiceV256=hovered.HasValue?
                    (hovered.Value?DeskSpaceFolderPreDropV256.Choice.Zip:
                        DeskSpaceFolderPreDropV256.Choice.Copy):
                    DeskSpaceFolderPreDropV256.Choice.None;
'@ 'record selected half before release'
Swap @'
                var sources=pendingFolderDrop??dropped;
                HideFolderChoice();
                e.Effects=selected.HasValue?DragDropEffects.Copy:DragDropEffects.None;
                e.Handled=true;
                if(!busy&&selected is bool zip && sources.Length>0)
                    _=CommitFolderDropAsync(sources,zip);
'@ @'
                // ZIP/Copy requires the same sources and same selected
                // half during BOTH DragOver and Drop. No late prompt.
                bool approved=DeskSpaceFolderPreDropV256.Authorized(
                    hoverChoiceV256,pendingFolderDrop,dropped) &&
                    selected.HasValue &&
                    (selected.Value?
                        hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Zip:
                        hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Copy);
                bool zip=hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Zip;
                HideFolderChoice();
                e.Effects=approved?DragDropEffects.Copy:DragDropEffects.None;
                e.Handled=true;
                if(!busy && approved)
                    _=CommitFolderDropAsync(dropped,zip);
'@ 'require pre-drop adorner consent and matching paths'
Swap @'
        pendingFolderDrop=null;
'@ @'
        pendingFolderDrop=null;
        hoverChoiceV256=DeskSpaceFolderPreDropV256.Choice.None;
'@ 'clear consent when native drag leaves, ends or widget closes'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R256: existing in-circle R160 ZIP/Copy adorner now requires prior matching DragOver choice on Drop.'
