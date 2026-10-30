$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r178/DeskSpaceSmartTextDropV178.cs' "$root/DeskSpaceSmartTextDropV178.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R178 $why unique anchor required, found $n"}
 $script:s=$script:s.Replace($old,$new)
}
One '    private UIElement? folderAdorned;' @'
    private UIElement? folderAdorned;
    private System.Windows.Documents.AdornerLayer? smartLayerV178;
    private DeskSpaceSmartTextAdornerV178? smartAdornerV178;
    private string? pendingSmartTextV178;
    private bool pendingSmartUrlV178;
'@ 'per-widget text/URL adorner fields'

One '        host.PreviewDragEnter+=FolderDragEnter;' @'
        host.PreviewDragEnter+=FolderDragEnter;
        host.PreviewDragLeave+=SmartDragLeaveV178;
'@ 'smart drag leave lifecycle'

One '        host.Closed+=(_,_)=>HideFolderChoice();' @'
        host.Closed+=(_,_)=>{HideFolderChoice();HideSmartDropV178();};
'@ 'ensure drag overlay disposed when host closes'

One @'
    private void DragOver(object sender,DragEventArgs e)
    {
'@ @'
    private void DragOver(object sender,DragEventArgs e)
    {
        // A user-initiated text or URL drag has its own choices.
        // Ordinary files and the folder ZIP/Copy arc remain unchanged.
        if(DeskSpaceSmartTextDropV178.TryExtract(e.Data,
            out var smartText,out var smartUrl))
        {
            HideFolderChoice();
            if(!busy)
                ShowSmartDropV178(smartText,smartUrl);
            var point=e.GetPosition(host);
            smartAdornerV178?.Hover(point);
            e.Effects=busy||
                DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),point)==null
                ?DragDropEffects.None:DragDropEffects.Copy;
            e.Handled=true;
            return;
        }
        HideSmartDropV178();
'@ 'smart dragover before folder/file handling'

One @'
    private void Receive(object sender,DragEventArgs e)
    {
'@ @'
    private void Receive(object sender,DragEventArgs e)
    {
        if(DeskSpaceSmartTextDropV178.TryExtract(e.Data,
            out var smartText,out var smartUrl))
        {
            var target=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                new Size(host.ActualWidth,host.ActualHeight),e.GetPosition(host));
            HideSmartDropV178();
            e.Effects=target.HasValue?DragDropEffects.Copy:DragDropEffects.None;
            e.Handled=true;
            if(!busy && !disposed && target is bool left)
                host.Dispatcher.BeginInvoke(new Action(()=>
                    DeskSpaceSmartTextDropV178.Commit(host,smartText,smartUrl,left)),
                    System.Windows.Threading.DispatcherPriority.Background);
            return;
        }
        HideSmartDropV178();
'@ 'safe text/url drop on hovered half without replacing normal file flow'

One @'
    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!(bool)e.NewValue)HideFolderChoice();
    }
'@ @'
    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!(bool)e.NewValue){HideFolderChoice();HideSmartDropV178();}
    }
    private void SmartDragLeaveV178(object sender,DragEventArgs e)
    {
        if(pendingSmartTextV178==null)return;
        var p=e.GetPosition(host);
        if(p.X>=0&&p.Y>=0&&p.X<=host.ActualWidth&&p.Y<=host.ActualHeight)
            return;
        HideSmartDropV178();
    }
    private void ShowSmartDropV178(string text,bool web)
    {
        if(disposed||busy)return;
        if(smartAdornerV178!=null &&
           pendingSmartUrlV178==web)
        {
            pendingSmartTextV178=text;
            return;
        }
        HideSmartDropV178();
        if(host.Content is not UIElement decorated)return;
        System.Windows.Documents.AdornerLayer? layer=null;
        if(decorated is System.Windows.Documents.AdornerDecorator existing &&
           existing.Child is UIElement inner)
        {
            layer=existing.AdornerLayer;
            decorated=inner;
        }
        layer??=System.Windows.Documents.AdornerLayer.GetAdornerLayer(decorated);
        if(layer==null)
        {
            host.Content=null;
            var wrapper=new System.Windows.Documents.AdornerDecorator{
                Child=decorated};
            host.Content=wrapper;
            layer=wrapper.AdornerLayer;
        }
        pendingSmartTextV178=text;
        pendingSmartUrlV178=web;
        smartAdornerV178=new DeskSpaceSmartTextAdornerV178(decorated,web);
        smartLayerV178=layer;
        layer.Add(smartAdornerV178);
    }
    private void HideSmartDropV178()
    {
        pendingSmartTextV178=null;
        if(smartAdornerV178!=null && smartLayerV178!=null)
        {
            try{smartLayerV178.Remove(smartAdornerV178);}
            catch(InvalidOperationException){}
        }
        smartAdornerV178=null;
        smartLayerV178=null;
    }
'@ 'text Smart Drop screen-local lifecycle'

One @'
        var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(content);
        if(layer==null)
'@ @'
        System.Windows.Documents.AdornerLayer? layer=null;
        if(content is System.Windows.Documents.AdornerDecorator priorDecorator &&
           priorDecorator.Child is UIElement inner)
        {
            layer=priorDecorator.AdornerLayer;
            content=inner;
        }
        layer??=System.Windows.Documents.AdornerLayer.GetAdornerLayer(content);
        if(layer==null)
'@ 'reuse adorner decorator across text and folder drags'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R178: content-aware text/URL wedge choices during drag; file and folder paths untouched.'
