$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function Swap([string]$before,[string]$after,[string]$name){
 $before=$before.Replace($crlf,$nl);$after=$after.Replace($crlf,$nl)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R182 $name anchor mismatch: $n"}
 $script:s=$script:s.Replace($before,$after)
}
$method=@'
    // No windows are moved during preview. A missing or renamed monitor is
    // reported separately, never silently remapped to the primary display.
    internal sealed record RestorePreview(
        int Eligible,int MissingWindows,int MissingMonitors);
    internal static RestorePreview Preview(Profile profile)
    {
        if(!ValidProfile(profile))
            return new(0,profile?.Windows?.Count ?? 0,0);
        var screens=Forms.Screen.AllScreens;
        var candidates=VisibleWindows();
        var consumed=new HashSet<IntPtr>();
        int eligible=0,missingWindows=0,missingMonitors=0;
        foreach(var saved in profile.Windows)
        {
            var matches=candidates.Where(c=>
                c.Process.Equals(saved.Process,StringComparison.OrdinalIgnoreCase) &&
                c.Hash==saved.TitleHash).ToArray();
            if(saved.Instance<0 || saved.Instance>=matches.Length ||
                !consumed.Add(matches[saved.Instance].Handle))
            {
                missingWindows++;
                continue;
            }
            if(!screens.Any(s=>s.DeviceName.Equals(saved.Monitor,
                StringComparison.OrdinalIgnoreCase)))
            {
                missingMonitors++;
                continue;
            }
            eligible++;
        }
        return new(eligible,missingWindows,missingMonitors);
    }
'@
Swap '    internal static Window Create(Window owner)' ($method+'    internal static Window Create(Window owner)') 'read-only restore preview'
Swap @'
            if(MessageBox.Show(
                "Restore positions of up to "+profile.Windows.Count+
                " already-running windows? Windows will not be activated.",
'@ @'
            var before=Preview(profile);
            if(before.Eligible==0)
            {
                status.Text="Nothing to restore: "+before.MissingWindows+
                    " missing windows; "+before.MissingMonitors+" unavailable monitors.";
                return;
            }
            if(MessageBox.Show(
                "Request position restore for "+before.Eligible+
                " matching running windows? "+before.MissingWindows+
                " unmatched and "+before.MissingMonitors+
                " on disconnected monitors will be skipped. "+
                "Windows will not be activated.",
'@ 'preflight before moving running windows'
Swap @'
            status.Text="Restored "+done+" windows; skipped "+skipped+
                " unmatched or unavailable windows.";
'@ @'
            status.Text="Restore requested for "+done+
                " windows; skipped "+skipped+
                ". The operating system completes moves asynchronously.";
'@ 'accurate asynchronous native move feedback'
Swap @'
        var buttons=new WrapPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(restore);
        buttons.Children.Add(export);buttons.Children.Add(import);
'@ @'
        var preview=new Button{Content="Preview restore",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        preview.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var check=Preview(profile);
            status.Text="Ready: "+check.Eligible+
                " matching windows. Missing: "+check.MissingWindows+
                " windows, "+check.MissingMonitors+
                " monitors. No windows have moved.";
        };
        var buttons=new WrapPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(preview);
        buttons.Children.Add(restore);
        buttons.Children.Add(export);buttons.Children.Add(import);
'@ 'explicit read-only preview control'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R182: safe read-only restore preview and status for missing windows/displays.'
