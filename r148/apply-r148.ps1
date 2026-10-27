$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$widget="$r/WidgetHost.cs"
$main="$r/MainWindow.xaml.cs"
Copy-Item 'r148/DeskSpaceSettingsStudioV148.cs' "$r/DeskSpaceSettingsStudioV148.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $b=$before.Replace($crlf,$lf);$a=$after.Replace($crlf,$lf)
    $hits=([regex]::Matches($s,[regex]::Escape($b))).Count
    if($hits -ne 1){throw ("R148 "+$label+": expected one anchor; got "+$hits)}
    [IO.File]::WriteAllText($path,$s.Replace($b,$a),
        [Text.UTF8Encoding]::new($false))
}
Swap $widget @'
    private const double CompactDiameter = 116;
    private const double OpenDiameter = 408;
'@ @'
    // Scale the ORIGINAL 292/420-DIP viewbox as a single unit, preserving
    // precise donut hit areas and all nested arc anchors.
    private static double CompactDiameter=>
        116*DeskSpaceAppearanceV148.Current.Scale;
    private static double OpenDiameter=>
        408*DeskSpaceAppearanceV148.Current.Scale;
'@ 'whole widget scaling'
Swap $widget @'
    private readonly Brush light = new SolidColorBrush(Color.FromRgb(238, 235, 251));
    private readonly Brush accent = new SolidColorBrush(Color.FromRgb(115, 100, 211));
'@ @'
    private Brush light=>DeskSpaceAppearanceV148.LightBrush;
    private Brush accent=>DeskSpaceAppearanceV148.AccentBrush;
'@ 'live theme brushes'
$source=[IO.File]::ReadAllText($widget).Replace($crlf,$lf)
$token='new SolidColorBrush(Color.FromRgb(227,221,246))'
$count=([regex]::Matches($source,[regex]::Escape($token))).Count
if($count -ne 2){throw "R148 second donut colour expected twice; actual $count"}
$source=$source.Replace($token,'DeskSpaceAppearanceV148.SecondaryBrush')
$old='Foreground=new SolidColorBrush(Color.FromRgb(63,52,99))'
if(!$source.Contains($old)){throw 'R148 parent donut labels source changed'}
$source=$source.Replace($old,'Foreground=DeskSpaceAppearanceV148.InkBrush')
[IO.File]::WriteAllText($widget,$source,[Text.UTF8Encoding]::new($false))
Swap $widget @'
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        BuildCompact();
'@ @'
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        DeskSpaceAppearanceV148.Apply(this);
        BuildCompact();
'@ 'initial opacity'
Swap $widget @'
        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{
            if(activeModule==null && !ringExpanded)BuildCompact();
            else UpdateCenter();
        });
'@ @'
        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{
            if(activeModule==null && !ringExpanded)BuildCompact();
            else UpdateCenter();
        });
        menu.Items.Add("Donut toolbar, themes & presets",null,(_,_)=>Dispatcher.Invoke(
            ()=>DeskSpaceSettingsStudioV148.Show(this,prefs,
                RefreshAppearanceV148,
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.Save();prefs.ApplyWindowsStartup();
                }).Show())));
'@ 'tray settings studio'
Swap $widget @'
    private void ShowAbout()
'@ @'
    private void RefreshAppearanceV148()
    {
        DeskSpaceAppearanceV148.Apply(this);
        if(activeModule==null)
        {
            if(ringExpanded)BuildDonut();
            else BuildCompact();
        }
        else UpdateCenter();
    }
    private void ShowAbout()
'@ 'live widget refresh'
Swap $main @'
        new WidgetOptionsWindow(settings,() => { settings.Save();settings.ApplyWindowsStartup(); }).Show();
'@ @'
        DeskSpaceSettingsStudioV148.Show(this,settings,()=>{
            settings.Save();settings.ApplyWindowsStartup();
        },()=>new WidgetOptionsWindow(settings,
            ()=>{settings.Save();settings.ApplyWindowsStartup();}).Show());
'@ 'Workspace Settings integration'
Write-Host 'R148: integrated donut Appearance and Presets studio, real widget palette/scale/opacity, pinned shortcut order, and retained legacy General settings.'
