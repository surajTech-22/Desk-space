$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r183/DeskSpaceStartupRecoveryV183.cs' -Destination "$root/DeskSpaceStartupRecoveryV183.cs" -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$name)
{
    $before=$before.Replace($crlf,$lf)
    $after=$after.Replace($crlf,$lf)
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $n=([regex]::Matches($s,[regex]::Escape($before))).Count
    if($n -ne 1){throw ("R183 "+$name+": required one anchor, found "+$n)}
    [IO.File]::WriteAllText($path,$s.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
Swap "$root/WidgetHost.cs" @'
        Loaded+=(_,_)=>DeskSpaceMultiMonitorV151.Register(this);
'@ @'
        Loaded+=(_,_)=>{
            // The only primary widget owns short-crash recovery. Never create
            // satellites while recovery mode is active; preserve one tray,
            // all saved settings, existing held files and the normal updater.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceStartupRecoveryV183.Start(this);
            if(!DeskSpaceStartupRecoveryV183.SafeMode)
                DeskSpaceMultiMonitorV151.Register(this);
        };
'@ 'primary-only startup recovery gate'
Swap "$root/DeskSpaceComponentHealthV175.cs" @'
        result.Add(new("File search","Index health not yet independently tested"));
'@ @'
        result.Add(new("File search","Index health not yet independently tested"));
        result.Add(new("Startup recovery",DeskSpaceStartupRecoveryV183.Status));
'@ 'read-only recovery state in health dashboard'
Swap "$root/DeskSpaceComponentHealthV175.cs" @'
        controls.Children.Add(export);
'@ @'
        controls.Children.Add(export);
        if(DeskSpaceStartupRecoveryV183.SafeMode)
        {
            var resume=new Button{
                Content="Resume secondary widgets next launch",
                Margin=new Thickness(8,0,0,0),
                Padding=new Thickness(8,6,8,6)};
            resume.Click+=(_,_)=>{
                var restored=DeskSpaceStartupRecoveryV183.ResumeFullStartupNextLaunch();
                MessageBox.Show(restored?
                    "Normal multi-monitor startup will be attempted on the next launch. "+
                    "The current widget and saved data are unchanged.":
                    "Could not save recovery preference; retry from the same panel.",
                    "DeskSpace recovery",MessageBoxButton.OK,
                    restored?MessageBoxImage.Information:MessageBoxImage.Warning);
                Refresh();
            };
            controls.Children.Add(resume);
        }
'@ 'opt-in recovery without automatic restart'
Write-Host 'R183: startup-safe primary/tray, crash-loop satellite gate, optional retry and status.'
