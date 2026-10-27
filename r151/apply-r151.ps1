$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget="$root/WidgetHost.cs"
Copy-Item -LiteralPath 'r151/DeskSpaceMultiMonitorV151.cs' -Destination "$root/DeskSpaceMultiMonitorV151.cs" -Force
$nl=[string][char]13+[string][char]10
function Swap([string]$from,[string]$to,[string]$tag){
    $source=[IO.File]::ReadAllText($script:widget).Replace($nl,[string][char]10)
    $before=$from.Replace($nl,[string][char]10)
    $after=$to.Replace($nl,[string][char]10)
    $count=([regex]::Matches($source,[regex]::Escape($before))).Count
    if($count -ne 1){throw "R151 $tag anchor count $count"}
    [IO.File]::WriteAllText($script:widget,$source.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
Swap @'
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        DeskSpaceAppearanceV148.Apply(this);
        BuildCompact();
'@ @'
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        DeskSpaceAppearanceV148.Apply(this);
        // Register only the primary host. Satellite windows have no secondary
        // update channel or monitor watchers.
        Loaded+=(_,_)=>DeskSpaceMultiMonitorV151.Register(this);
        BuildCompact();
'@ 'real widget display manager'
Swap 'timer.Stop(); SavePosition(); tray.Visible = false; tray.Dispose();' @'
timer.Stop();
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))SavePosition();
            tray.Visible = false; tray.Dispose();
'@ 'do not overwrite primary position when secondary widgets close'
Swap @'
        ringExpanded = false;
        KeepOnVisibleDesktop();
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
'@ @'
        ringExpanded = false;
        // A replica's physical monitor is assigned after construction.
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))KeepOnVisibleDesktop();
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
'@ 'do not clamp secondary display widgets to the primary work area'
Swap 'Text = "DeskSpace Live Widget", Visible = true };' @'
Text = "DeskSpace Live Widget",
            Visible = !DeskSpaceMultiMonitorV151.ConstructingSatellite };
'@ 'one tray icon across all monitors'
Swap '            DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);' @'
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);
'@ 'only original widget controls Windows startup'
Swap @'
            commandShortcut ??= new DeskSpaceCommandShortcutV85(
                this,()=>OpenInWidget("Search"));
            DeskSpaceSearchIndexV13.WarmStart();
'@ @'
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            {
                commandShortcut ??= new DeskSpaceCommandShortcutV85(
                    this,()=>OpenInWidget("Search"));
                DeskSpaceSearchIndexV13.WarmStart();
            }
'@ 'global hotkey and file index only once'
Swap '        Loaded+=(_,_)=>dueShelf?.Start();' @'
        Loaded+=(_,_)=>{
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))dueShelf?.Start();
        };
'@ 'only one reminder scheduler'
Swap '        recent.Start(); Tick();' @'
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))recent.Start();
        Tick();
'@ 'only one recent file collector'
Swap @'
        Loaded += (_, _) => DeskSpaceUpdater.StartAutomaticChecks(this);
        _ = ListenForShowRequests();
'@ @'
        Loaded += (_, _) =>{
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceUpdater.StartAutomaticChecks(this);
        };
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            _ = ListenForShowRequests();
'@ 'one updater and one named pipe host'

# All drag/sizing paths call this screen-fit routine. Secondary widgets
# must stay in their own monitor's bounds, never snap back to the primary.
$whole=[IO.File]::ReadAllText($widget).Replace($nl,[string][char]10)
$match=[regex]::Match($whole,'(?m)^\s*private\s+void\s+KeepOnVisibleDesktop\s*\([^)]*\)\s*\{')
if(!$match.Success){throw 'R151 display clamp method not found'}
$whole=$whole.Insert($match.Index+$match.Length,
    ([string][char]10+'        if(DeskSpaceMultiMonitorV151.IsSatellite(this))return;'))
[IO.File]::WriteAllText($widget,$whole,[Text.UTF8Encoding]::new($false))
# Exercise actual WPF page creation and repeat navigation, not just source text.
$smoke='r36/UiSmoke/Program.cs'
$sm=[IO.File]::ReadAllText($smoke).Replace($nl,[string][char]10)
$call='            TestSubArcScreenFit();'
if(([regex]::Matches($sm,[regex]::Escape($call))).Count -ne 1){
    throw 'R151 WPF smoke test call anchor changed'
}
$sm=$sm.Replace($call,$call+[string][char]10+'            TestSettingsPagesV151();')
$method='    private static void TestDeckVisibilityV89()'
if(([regex]::Matches($sm,[regex]::Escape($method))).Count -ne 1){
    throw 'R151 WPF smoke method anchor changed'
}
$test=@'
    private static void TestSettingsPagesV151()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSettingsStudioV148",true)!;
        var ctor=type.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance)
            .Single(x=>x.GetParameters().Length==4);
        var prefs=new WidgetSettingsV3();
        var window=(Window)ctor.Invoke(new object[]{
            new Window(),prefs,(Action)(()=>{}),(Action)(()=>{})});
        var navigate=type.GetMethod("Navigate",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        var body=(StackPanel)type.GetField("body",BindingFlags.Instance|
            BindingFlags.NonPublic)!.GetValue(window)!;
        foreach(var page in new[]{"Theme","Donut toolbar","Theme",
            "Donut toolbar","Displays","Presets","General",
            "Donut toolbar"})
        {
            navigate.Invoke(window,new object[]{page});
            Assert(!body.Children.OfType<TextBlock>().Any(x=>
                x.Text?.StartsWith("This settings page could not be opened",
                    StringComparison.Ordinal)==true),
                "R151 "+page+" must re-open without WPF logical-parent exceptions");
        }
        Console.WriteLine("R151 PASS: real settings pages navigate repeatedly, "+
            "pinned applications list reparented, Displays loads safely.");
    }

'@
$sm=$sm.Replace($method,$test.Replace($nl,[string][char]10)+$method)
[IO.File]::WriteAllText($smoke,$sm,[Text.UTF8Encoding]::new($false))

Write-Host 'R151 installed real interactive widget per monitor; monitor hotplug and persisted independent display setting.'
