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
 if($n -ne 1){throw ("R257 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
# The verified canonical R201 source already provides an aggregate Preview,
# strict per-monitor Restore, portable import/export and a Preview button.
# Enhance that model; do not duplicate or overwrite its safeguards.
Swap @'
    internal static RestorePreview Preview(Profile profile)
'@ @'
    internal static DeskSpaceWindowRestorePlanV257.Plan DetailedPreviewV257(Profile profile)
    {
        if(!ValidProfile(profile))
            return new(0,0,
                Array.Empty<DeskSpaceWindowRestorePlanV257.Row>());
        var live=VisibleWindows().Select(w=>
            new DeskSpaceWindowRestorePlanV257.Live(w.Process,w.Hash)).ToArray();
        var screens=Forms.Screen.AllScreens
            .GroupBy(screen=>screen.DeviceName,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.First().WorkingArea,
                StringComparer.OrdinalIgnoreCase);
        return DeskSpaceWindowRestorePlanV257.Build(profile.Windows,live,screens);
    }
    internal static RestorePreview Preview(Profile profile)
'@ 'add detailed projection alongside existing privacy-safe aggregate'
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
        var previewRowsV257=new StackPanel{Margin=new Thickness(0,9,0,0)};
        string? previewedV257=null;
        string? proofV257=null;
        string SignatureV257(Profile profile,
            DeskSpaceWindowRestorePlanV257.Plan plan)
        {
            string projection=JsonSerializer.Serialize(profile)+"|"+
                string.Join("|",plan.Rows.Select(row=>
                    row.Process+"|"+row.Monitor+"|"+row.Ready+"|"+
                    row.Bounds.X+","+row.Bounds.Y+","+
                    row.Bounds.Width+","+row.Bounds.Height+"|"+row.Reason));
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(projection)));
        }
'@ 'ephemeral review proof without raw window titles or paths'
Swap @'
        restore.Click+=(_,_)=>{
'@ @'
        restore.Click+=(_,_)=>{
            // Review the exact saved profile and the current monitor/window
            // geometry before allowing the existing Yes/No confirmation.
            if(pick.SelectedItem is not string selectedV257 ||
                !string.Equals(previewedV257,selectedV257,
                    StringComparison.Ordinal) || proofV257==null)
            {
                status.Text="Preview the selected profile before restoring.";
                return;
            }
            var savedV257=Load().FirstOrDefault(p=>
                p.Name==selectedV257);
            if(savedV257==null)return;
            var latestV257=DetailedPreviewV257(savedV257);
            if(!string.Equals(proofV257,SignatureV257(savedV257,latestV257),
                StringComparison.Ordinal))
            {
                previewedV257=null;proofV257=null;
                status.Text="Windows or monitors changed. Preview again.";
                return;
            }
            if(latestV257.Ready==0)
            {
                status.Text="No matching windows available. Nothing moved.";
                return;
            }
            // Consent is single use, including if the confirmation is denied.
            previewedV257=null;proofV257=null;
'@ 'prevent restore without current explicit per-window preview'
Swap @'
            var check=Preview(profile);
            status.Text="Ready: "+check.Eligible+
                " matching windows. Missing: "+check.MissingWindows+
                " windows, "+check.MissingMonitors+
                " monitors. No windows have moved.";
'@ @'
            var planV257=DetailedPreviewV257(profile);
            previewedV257=selected;
            proofV257=SignatureV257(profile,planV257);
            previewRowsV257.Children.Clear();
            foreach(var row in planV257.Rows)
            {
                string location=row.Ready?
                    "("+row.Bounds.X+","+row.Bounds.Y+") "+
                    row.Bounds.Width+" x "+row.Bounds.Height:
                    row.Reason;
                previewRowsV257.Children.Add(new TextBlock{
                    Text=row.Process+" · "+row.Monitor+" · "+location,
                    Foreground=row.Ready?Brushes.Gainsboro:Brushes.Orange,
                    TextWrapping=TextWrapping.Wrap,
                    Margin=new Thickness(0,0,0,5)});
            }
            status.Text="Preview only: "+planV257.Ready+" matching; "+
                planV257.Skipped+" unavailable. No windows moved.";
'@ 'extend existing Preview restore button, not duplicate it'
Swap @'
        layout.Children.Add(status);
'@ @'
        layout.Children.Add(status);
        layout.Children.Add(previewRowsV257);
'@ 'show non-sensitive projected targets in original layouts panel'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R257: original layout Preview enhanced with redacted per-window targets; single-use consent and strict saved monitor restored.'
