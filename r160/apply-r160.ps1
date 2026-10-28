$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r160/DeskSpaceFolderDragAdornerV160.cs' "$r/DeskSpaceFolderDragAdornerV160.cs" -Force
$p="$r/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R160 $why needs one anchor, found $n"}
 $script:s=$script:s.Replace($a,$b)
}
Swap '    private readonly Popup folderChoice;' @'
    private System.Windows.Documents.AdornerLayer? folderLayer;
    private DeskSpaceFolderDragAdornerV160? folderAdorner;
    private UIElement? folderAdorned;
'@ 'remove detached folder Popup HWND'

$start=$s.IndexOf('        var choicePanel=new StackPanel{Margin=new Thickness(10,8,10,9)};')
$end=if($start -ge 0){$s.IndexOf('        host.Closed+=(_,_)=>HideFolderChoice();',$start)}else{-1}
if($start -lt 0 -or $end -lt $start){throw 'R160 missing R159 rectangular popup block'}
$end+=('        host.Closed+=(_,_)=>HideFolderChoice();').Length
$replacement=@'
        // Folder actions are drawn inside this widget's actual WPF window,
        // before the user releases a dragged folder. Never use a Popup.
        host.PreviewDragEnter+=FolderDragEnter;
        host.PreviewDragLeave+=FolderDragLeave;
        host.IsVisibleChanged+=FolderChoiceVisible;
        host.Closed+=(_,_)=>HideFolderChoice();
'@
$s=$s.Substring(0,$start)+$replacement.Replace($crlf,$lf).TrimEnd()+$s.Substring($end)

Swap '        if(folderChoice.IsOpen)PositionFolderChoice();'+$lf '' 'no detached-choice positioning on widget movement'

Swap @'
    private void DragOver(object sender,DragEventArgs e)
    {
'@ @'
    private void DragOver(object sender,DragEventArgs e)
    {
        // DragEnter/DragOver, not Drop, must reveal the two choice wedges.
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(paths?.Any(Directory.Exists)==true)
            {
                if(!busy)
                    ShowFolderChoice(paths);
                var point=e.GetPosition(host);
                folderAdorner?.Hover(point);
                e.Effects=busy||
                    DeskSpaceFolderDragAdornerV160.ChoiceAt(
                        new Size(host.ActualWidth,host.ActualHeight),point)==null
                        ?DragDropEffects.None:DragDropEffects.Copy;
                e.Handled=true;
                return;
            }
        }
'@ 'show circular choices during DragOver without default post-drop popup'

Swap @'
        // Folder drops are intercepted BEFORE the existing file-drop route.
        // File-only drops continue through the unchanged original handler.
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var dropped=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(dropped?.Any(Directory.Exists)==true)
            {
                e.Effects=DragDropEffects.Copy;
                e.Handled=true;
                if(!busy&&pendingFolderDrop==null)
                    ShowFolderChoice(dropped);
                return;
            }
        }
'@ @'
        // DragOver already showed the two halves of the circle.
        // Releasing over ZIP (left) or Copy (right) immediately performs
        // the corresponding action; there is NO after-drop confirmation.
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var dropped=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(dropped?.Any(Directory.Exists)==true)
            {
                var selected=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),e.GetPosition(host));
                var sources=pendingFolderDrop??dropped;
                HideFolderChoice();
                e.Effects=selected.HasValue?DragDropEffects.Copy:DragDropEffects.None;
                e.Handled=true;
                if(!busy&&selected is bool zip && sources.Length>0)
                    _=CommitFolderDropAsync(sources,zip);
                return;
            }
        }
'@ 'drop on the hovered ZIP or Copy region, not into a separate popup'

$start=$s.IndexOf('    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)')
$end=if($start -ge 0){$s.IndexOf('    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)',$start)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R160 cannot locate old popup choice lifecycle methods'}
$methods=@'
    private void FolderDragEnter(object sender,DragEventArgs e)=>DragOver(sender,e);

    private void FolderDragLeave(object sender,DragEventArgs e)
    {
        if(pendingFolderDrop==null)return;
        // Nested WPF elements can generate a DragLeave without leaving
        // the host; do not flicker when crossing internal donut graphics.
        var p=e.GetPosition(host);
        if(p.X>=0&&p.Y>=0&&p.X<=host.ActualWidth&&p.Y<=host.ActualHeight)
            return;
        HideFolderChoice();
    }

    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!(bool)e.NewValue)HideFolderChoice();
    }

    private void ShowFolderChoice(string[] incoming)
    {
        if(disposed||busy)return;
        var valid=incoming.Where(path=>
            (File.Exists(path)||Directory.Exists(path))&&
            !IsOwnPath(path,staging)).ToArray();
        if(valid.Length==0)return;
        // The only shared state is the *completed* Deck item. This drag
        // choice belongs exclusively to the widget under the pointer.
        pendingFolderDrop=valid;
        if(folderAdorner!=null)return;
        if(host.Content is not UIElement content)return;
        var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(content);
        if(layer==null)
        {
            // Default WPF Window template normally has an AdornerLayer.
            // Provide one locally if a customized host template omits it.
            var decorator=new System.Windows.Documents.AdornerDecorator{Child=content};
            host.Content=decorator;
            layer=decorator.AdornerLayer;
        }
        folderAdorned=content;
        folderLayer=layer;
        folderAdorner=new DeskSpaceFolderDragAdornerV160(content);
        layer.Add(folderAdorner);
    }

    private void HideFolderChoice()
    {
        pendingFolderDrop=null;
        if(folderAdorner!=null&&folderLayer!=null)
        {
            try{folderLayer.Remove(folderAdorner);}
            catch(InvalidOperationException){}
        }
        folderAdorner=null;
        folderLayer=null;
        folderAdorned=null;
    }

    private void CommitFolderDrop(bool zipFolders)
    {
        // Only called by tests/compatibility; the actual interaction commits
        // the hovered half directly in Receive when the folder is dropped.
        var paths=pendingFolderDrop;
        HideFolderChoice();
        if(disposed||paths==null||paths.Length==0)return;
        _=CommitFolderDropAsync(paths,zipFolders);
    }
'@
$s=$s.Substring(0,$start)+$methods.Replace($crlf,$lf).TrimEnd()+$lf+$s.Substring($end)

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R160: folder ZIP and Copy are in-donut adorner wedges shown on DRAG OVER, chosen on DROP. No detached Popup or after-drop dialog.'
