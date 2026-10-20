$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('DeskSpaceExcelStoreV16.cs','DeskSpaceExcelBridgeV16.cs','DeskSpaceExcelV16.cs')){
    Copy-Item -LiteralPath "r16/$name" -Destination "$root/$name" -Force
}
function ReadSource([string]$file) {
    [IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
}
function SaveSource([string]$file,[string]$source){
    [IO.File]::WriteAllText($file,$source,[Text.UTF8Encoding]::new($false))
}
function Swap([string]$old,[string]$new){
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:source.Contains($old)){throw "R16 missing patch anchor: $old"}
    $script:source=$script:source.Replace($old,$new)
}
$path="$root/WidgetHost.cs"
$source=ReadSource $path
Swap '    private DeskSpaceSearchV11? activeSearch;' @'
    private DeskSpaceSearchV11? activeSearch;
    private DeskSpaceExcelV16? activeExcel;
'@
Swap @'
        else
        {
            Width=602;Height=686;
            surface=activeModule.Build(name);
        }
'@ @'
        else if(name=="Excel")
        {
            var area=ModuleArea();
            Width=Math.Min(area.Width,710);
            Height=Math.Min(area.Height,720);
            activeExcel=new DeskSpaceExcelV16(ReturnFromModule);
            surface=activeExcel.Create();
        }
        else
        {
            Width=602;Height=686;
            surface=activeModule.Build(name);
        }
'@
Swap '        search?.Dispose();inline?.Dispose();old.Dispose();' @'
        search?.Dispose();inline?.Dispose();
        activeExcel?.Dispose();activeExcel=null;
        old.Dispose();
'@
Swap '        menu.Items.Add("About / Version", null, (_, _) => Dispatcher.Invoke(ShowAbout));' @'
        menu.Items.Add("Excel · reusable data",null,(_,_)=>Dispatcher.Invoke(
            ()=>OpenInWidget("Excel")));
        menu.Items.Add("About / Version", null, (_, _) => Dispatcher.Invoke(ShowAbout));
'@
Swap @'
                else if(command==2)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Search");
                });
'@ @'
                else if(command==2)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Search");
                });
                else if(command==3)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Excel");
                });
'@
Swap '    public static void EnsureRunning(bool force = false)' @'
    // Open Excel mapping inside the existing widget from the main application.
    public static void OpenExcel()
    {
        EnsureRunning(force:true);
        _=Task.Run(async()=>{
            for(int i=0;i<6;i++)
            {
                try{
                    using var client=new NamedPipeClientStream(
                        ".","DeskSpace.Widget.V3",PipeDirection.Out);
                    await client.ConnectAsync(700);
                    client.WriteByte(3);await client.FlushAsync();
                    return;
                }
                catch(TimeoutException){}
                catch(IOException){}
                await Task.Delay(250);
            }
        });
    }
    public static void EnsureRunning(bool force = false)
'@
SaveSource $path $source

$path="$root/MainWindow.xaml"
$source=ReadSource $path
Swap @'
            <Button Tag="Tools" Content="⚒  Tools &amp; Search" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
'@ @'
            <Button Tag="Tools" Content="⚒  Tools &amp; Search" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Excel" Content="▦  Excel reusable data" Click="ExcelModule_Click" Padding="20,15" Margin="0,0,10,12"/>
'@
SaveSource $path $source

$path="$root/MainWindow.xaml.cs"
$source=ReadSource $path
Swap '    private void SafeSave()' @'
    private void ExcelModule_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenExcel();
    }
    private void SafeSave()
'@
SaveSource $path $source

# Prevent prematurely deleting staged files when another app merely accepts
# a drop. Excel/Explorer can still be reading the file after DoDragDrop returns.
$path="$root/DeskSpaceDropShelfV15.cs"
$source=ReadSource $path
Swap '                if(effect==DragDropEffects.Copy)Remove(item);' @'
                // A successful drop effect is not a completed destination read.
                // Retain the staged file until explicit Clear or Remove.
                _=effect;
'@
Swap @'
        try { if(Directory.Exists(staging))Directory.Delete(staging,true); }
        catch(IOException){}catch(UnauthorizedAccessException){}
'@ @'
        // Keep staged bytes after app shutdown so delayed external paste reads
        // cannot unexpectedly see an empty/missing file. User can Clear.
'@
SaveSource $path $source
Write-Host 'R16 Excel capture and guarded autofill wired to main app and floating widget; staged file early deletion fixed.'
