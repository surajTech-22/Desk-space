$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$file,[string]$from,[string]$to)
{
    $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
    $from=$from.Replace([string][char]13+[char]10,[string][char]10)
    $to=$to.Replace([string][char]13+[char]10,[string][char]10)
    if(!$s.Contains($from)){throw "R10 unified-search anchor missing: $file : $from"}
    [IO.File]::WriteAllText($file,$s.Replace($from,$to),[Text.UTF8Encoding]::new($false))
}
$w="$root/WidgetHost.cs"
Swap $w '    public static void EnsureRunning(bool force = false)' @'
    // Opens the existing widget's Search view. No second Search window.
    public static void OpenSearch()
    {
        EnsureRunning(force:true);
        _ = Task.Run(async () =>
        {
            for(int i=0;i<5;i++)
            {
                try
                {
                    using var client=new NamedPipeClientStream(".","DeskSpace.Widget.V3",PipeDirection.Out);
                    await client.ConnectAsync(700);
                    client.WriteByte(2);await client.FlushAsync();
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
Swap $w '                if (server.ReadByte() == 1) await Dispatcher.InvokeAsync(Reveal);' @'
                var command=server.ReadByte();
                if(command==1)await Dispatcher.InvokeAsync(Reveal);
                else if(command==2)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Search");
                });
'@
Swap $w '    private void OpenSearch() {new UniversalSearchWindow(ReadWorkspace,prefs).Show();}' @'
    private void OpenSearch() {OpenInWidget("Search");}
'@
Swap $w '    private void OpenRecent() {new RecentFilesWindow(recent).Show();}' @'
    private void OpenRecent() {OpenInWidget("Search");}
'@
$main="$root/MainWindow.xaml.cs"
Swap $main @'
    private void UniversalSearch_Click(object sender,RoutedEventArgs e)
    {
        new UniversalSearchWindow(() => data, WidgetSettingsV3.Load()).Show();
    }
'@ @'
    private void UniversalSearch_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
'@
Swap $main @'
    private void RecentFiles_Click(object sender,RoutedEventArgs e)
    {
        var service = new RecentFilesService();
        service.Start();
        var window = new RecentFilesWindow(service);
        window.Closed += (_,_) => service.Dispose();
        window.Show();
    }
'@ @'
    private void RecentFiles_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
'@
Swap $main @'
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        new AppPinsWindow(WidgetSettingsV3.Load()).Show();
    }
'@ @'
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
'@
Write-Host 'R10 files/apps/settings/results use one Search view inside the widget.'
