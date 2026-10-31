$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r257/DeskSpaceWindowRestorePlanV257.cs' -Destination "$root/DeskSpaceWindowRestorePlanV257.cs" -Force
$p="$root/DeskSpaceWindowLayoutsV179.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R257 "+$why+" expected 1 anchor, found "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '    private static List<Profile> Load()' @'
    internal static DeskSpaceWindowRestorePlanV257.Plan Preview(Profile profile)
    {
        var live=VisibleWindows().Select(w=>
            new DeskSpaceWindowRestorePlanV257.Live(w.Process,w.Hash)).ToArray();
        var screens=Forms.Screen.AllScreens
            .GroupBy(screen=>screen.DeviceName,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.First().WorkingArea,
                StringComparer.OrdinalIgnoreCase);
        return DeskSpaceWindowRestorePlanV257.Build(profile.Windows,live,screens);
    }

    private static List<Profile> Load()
'@ 'read-only projection of running windows and connected monitors'
Swap @'
        foreach(var saved in profile.Windows)
        {
'@ @'
        foreach(var saved in profile.Windows)
        {
            // Guard before any later fallback: never rehome a window from a
            // disconnected saved monitor onto a different display.
            if(!screens.Any(screen=>string.Equals(screen.DeviceName,
                    saved.Monitor,StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }
'@ 'strict monitor identity on actual restore'
Swap @'
        var status=new TextBlock{
            Text="No changes have been made.",
            Foreground=Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,12,0,0)};
'@ @'
        var status=new TextBlock{
            Text="No changes have been made.",
            Foreground=Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,12,0,0)};
        var previewRowsV257=new StackPanel{
            Margin=new Thickness(0,9,0,0)};
        string? previewedProfileV257=null;
        string? previewProofV257=null;
        string ProofV257(DeskSpaceWindowRestorePlanV257.Plan plan)=>
            string.Join("|",plan.Rows.Select(row=>
                row.Process+"|"+row.Monitor+"|"+row.Ready+"|"+
                row.Bounds.X+","+row.Bounds.Y+","+
                row.Bounds.Width+","+row.Bounds.Height+"|"+row.Reason));
'@ 'inline preview model; do not persist window titles or file paths'
Swap @'
                Save(profile);Refresh();
'@ @'
                Save(profile);Refresh();
                previewedProfileV257=null;
                previewProofV257=null;
                previewRowsV257.Children.Clear();
'@ 'invalidate prior preview after capture replaces profile'
Swap @'
        var restore=new Button{Content="Restore selected",
            Padding=new Thickness(9,6,9,6)};
'@ @'
        var preview=new Button{Content="Preview selected",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,6,0)};
        preview.Click+=(_,_)=>{
            previewedProfileV257=null;
            previewProofV257=null;
            previewRowsV257.Children.Clear();
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var plan=Preview(profile); // No SetWindowPos or process launch.
            previewedProfileV257=selected;
            previewProofV257=ProofV257(plan);
            status.Text="Preview only: "+plan.Ready+" matching windows, "+
                plan.Skipped+" skipped. No windows moved.";
            foreach(var row in plan.Rows)
            {
                var location=row.Ready?
                    "("+row.Bounds.X+","+row.Bounds.Y+") "+
                    row.Bounds.Width+" x "+row.Bounds.Height:
                    row.Reason;
                previewRowsV257.Children.Add(new TextBlock{
                    Text=row.Process+" · "+row.Monitor+" · "+location,
                    Foreground=row.Ready?Brushes.Gainsboro:Brushes.Orange,
                    TextWrapping=TextWrapping.Wrap,
                    Margin=new Thickness(0,0,0,5)});
            }
        };
        var restore=new Button{Content="Restore selected",
            Padding=new Thickness(9,6,9,6)};
'@ 'visible non-mutating preview action'
Swap @'
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            if(MessageBox.Show(
                "Restore positions of up to "+profile.Windows.Count+
'@ @'
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            // Require explicit current preview and reject any topology or
            // matching-window changes since preview. No silent restore.
            if(!string.Equals(previewedProfileV257,selected,
                StringComparison.Ordinal) || previewProofV257==null)
            {
                status.Text="Preview this profile before restoring. No windows moved.";
                return;
            }
            var current=Preview(profile);
            if(!string.Equals(previewProofV257,ProofV257(current),
                StringComparison.Ordinal))
            {
                previewedProfileV257=null;
                previewProofV257=null;
                status.Text="Windows or monitors changed. Preview again before restore.";
                return;
            }
            if(current.Ready==0)
            {
                status.Text="No matching windows can be restored. Nothing changed.";
                return;
            }
            if(MessageBox.Show(
                "Restore positions of up to "+current.Ready+
'@ 'require reviewed profile, live fingerprint and explicit consent'
Swap @'
            var (done,skipped)=Restore(profile);
            status.Text="Restored "+done+" windows; skipped "+skipped+
'@ @'
            var (done,skipped)=Restore(profile);
            previewedProfileV257=null;
            previewProofV257=null;
            status.Text="Restored "+done+" windows; skipped "+skipped+
'@ 'clear single-use restoration consent'
Swap @'
        buttons.Children.Add(capture);buttons.Children.Add(restore);
'@ @'
        buttons.Children.Add(capture);
        buttons.Children.Add(preview);
        buttons.Children.Add(restore);
'@ 'preview button beside existing capture/restore'
Swap @'
        layout.Children.Add(status);
'@ @'
        layout.Children.Add(status);
        layout.Children.Add(previewRowsV257);
'@ 'show redacted target/window details inside existing scroll panel'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R257: explicit non-destructive window layout preview, strict display identity, consented restore.'
