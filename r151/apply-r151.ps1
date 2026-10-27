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
Write-Host 'R151 installed real interactive widget per monitor; monitor hotplug and persisted independent display setting.'
