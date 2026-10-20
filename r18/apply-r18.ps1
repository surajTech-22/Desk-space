$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r18/DeskSpaceSearchIndexV18.cs' "$root/DeskSpaceSearchIndexV13.cs" -Force
Copy-Item 'r18/DeskSpaceWindowsSearchV18.cs' "$root/DeskSpaceWindowsSearchV18.cs" -Force
Copy-Item 'r18/DeskSpaceMediaV18.cs' "$root/DeskSpaceMediaV18.cs" -Force

function Load([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$value){[IO.File]::WriteAllText($p,$value,[Text.UTF8Encoding]::new($false))}
function ReplaceRequired([string]$old,[string]$new){
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R18 source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
$p="$root/DeskSpace.csproj";$s=Load $p
if($s.Contains('net8.0-windows10.0.19041.0')){
    Write-Host 'Windows SDK target already enabled.'
}elseif($s.Contains('net8.0-windows')){
    ReplaceRequired 'net8.0-windows' 'net8.0-windows10.0.19041.0'
}else{throw 'R18 unknown Windows target framework'}
Save $p $s
$p='DeskSpace_Windows_R1/DeskSpace.Tests/DeskSpace.Tests.csproj';$s=Load $p
if($s.Contains('net8.0-windows10.0.19041.0')){
    Write-Host 'Tests already target Windows SDK.'
}elseif($s.Contains('net8.0-windows')){
    ReplaceRequired 'net8.0-windows' 'net8.0-windows10.0.19041.0'
}else{throw 'R18 unknown Windows test target framework'}
Save $p $s
$p="$root/DeskSpaceSearchV11.cs";$s=Load $p
ReplaceRequired 'foreach(var hit in found.Take(7))' 'foreach(var hit in found.Take(45))'
ReplaceRequired 'status.Text=found.Count+" matches · hover for actions"+' 'status.Text=found.Count+" matches · files, apps, settings"+'
Save $p $s
$p="$root/WidgetHost.cs";$s=Load $p
ReplaceRequired '    private DeskSpaceExcelV16? activeExcel;' @'
    private DeskSpaceExcelV16? activeExcel;
    private readonly DeskSpaceMediaV18 media=new();
    private bool mediaDisplayed;
    private object? surfaceBeforeMedia;
'@
ReplaceRequired '            attachedShelf.Enable();' @'
            attachedShelf.Enable();
            media.VisibilityChanged+=OnMediaVisibility;
            media.Start();
'@
ReplaceRequired '        Closing += (_,_)=>{activeModule?.Dispose();attachedShelf?.Dispose();};' @'
        Closing += (_,_)=>{
            media.VisibilityChanged-=OnMediaVisibility;
            media.Dispose();
            activeModule?.Dispose();
            attachedShelf?.Dispose();
        };
'@
ReplaceRequired @'
    private void OpenInWidget(string name)
'@ @'
    // The media view occupies exactly the compact circle rather than
    // creating another popup or taskbar app. Search/editors have priority.
    private void OnMediaVisibility(bool canDisplay)
    {
        if(!Dispatcher.CheckAccess()){
            Dispatcher.BeginInvoke(new Action(()=>OnMediaVisibility(canDisplay)));
            return;
        }
        if(activeModule!=null||ringExpanded||hiddenAtEdge||!canDisplay){
            LeaveMediaMode();
            return;
        }
        if(mediaDisplayed)return;
        surfaceBeforeMedia=Content;
        Content=media.CreateView();
        mediaDisplayed=true;
    }
    private void LeaveMediaMode()
    {
        if(!mediaDisplayed)return;
        mediaDisplayed=false;
        if(surfaceBeforeMedia!=null)Content=surfaceBeforeMedia;
        surfaceBeforeMedia=null;
    }
    private void ResumeMediaMode()
    {
        if(media.Visible && activeModule==null && !ringExpanded)
            OnMediaVisibility(true);
    }
    private void OpenInWidget(string name)
'@
ReplaceRequired @'
        if(activeModule!=null)ReturnFromModule();
        CollapseRing();
'@ @'
        LeaveMediaMode();
        if(activeModule!=null)ReturnFromModule();
        CollapseRing();
'@
ReplaceRequired '        BuildCompact();SavePosition();UpdateCenter();' @'
        BuildCompact();SavePosition();UpdateCenter();
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
            System.Windows.Threading.DispatcherPriority.Background);
'@
ReplaceRequired @'
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(activeModule!=null)return;
'@ @'
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(activeModule!=null)return;
        LeaveMediaMode();
'@
Save $p $s
Write-Host 'R18: indexed and full-drive search, scrollable result list, native media circle; Excel unchanged.'
