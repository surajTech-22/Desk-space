$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'DeskSpaceWindowLayoutsV179.cs'
if(!(Test-Path $file)){throw 'R343 shipping Windows layouts source missing'}
Copy-Item 'r343/DeskSpaceMonitorRestoreV343.cs' (Join-Path $root 'DeskSpaceMonitorRestoreV343.cs') -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R343 $label expected one anchor, found $hits"}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        var pick=new ComboBox{MinHeight=28,Margin=new Thickness(0,0,0,8)};
'@ @'
        var pick=new ComboBox{MinHeight=28,Margin=new Thickness(0,0,0,8)};
        var monitorChoiceV343=new ComboBox{
            MinHeight=28,Margin=new Thickness(0,0,0,8),
            ToolTip="Choose which saved monitor receives restored window positions"};
'@ 'monitor selection input'
One @'
        string? proofV257=null;
        string SignatureV257(Profile profile,
'@ @'
        string? proofV257=null;
        void ClearPreviewV343()
        {
            previewedV257=null;proofV257=null;
            previewRowsV257.Children.Clear();
        }
        monitorChoiceV343.SelectionChanged+=(_,_)=>ClearPreviewV343();
        pick.SelectionChanged+=(_,_)=>{
            var selected=pick.SelectedItem as string;
            var profile=selected==null?null:
                Load().FirstOrDefault(p=>p.Name==selected);
            monitorChoiceV343.ItemsSource=profile==null?
                new[]{DeskSpaceMonitorRestoreV343.All}:
                DeskSpaceMonitorRestoreV343.Options(profile);
            monitorChoiceV343.SelectedIndex=0;
            ClearPreviewV343();
        };
        string SignatureV257(Profile profile,
'@ 'invalidate preview on monitor/profile switch'
One @'
            var latestV257=DetailedPreviewV257(savedV257);
            if(!string.Equals(proofV257,SignatureV257(savedV257,latestV257),
'@ @'
            var selectedV343=DeskSpaceMonitorRestoreV343.Select(savedV257,
                monitorChoiceV343.SelectedItem as string);
            var latestV257=DetailedPreviewV257(selectedV343);
            if(!string.Equals(proofV257,SignatureV257(selectedV343,latestV257),
'@ 'restore proof based on selected physical monitor'
One @'
            var before=Preview(profile);
'@ @'
            var chosenV343=DeskSpaceMonitorRestoreV343.Select(profile,
                monitorChoiceV343.SelectedItem as string);
            var before=Preview(chosenV343);
'@ 'filter before restore preview'
One @'
            var (done,skipped)=Restore(profile);
'@ @'
            // Restore only the exact reviewed monitor selection; never
            // silently remap a saved monitor to another one.
            var (done,skipped)=Restore(chosenV343);
'@ 'restore only approved monitor'
One @'
            var planV257=DetailedPreviewV257(profile);
            previewedV257=selected;
            proofV257=SignatureV257(profile,planV257);
'@ @'
            var chosenV343=DeskSpaceMonitorRestoreV343.Select(profile,
                monitorChoiceV343.SelectedItem as string);
            var planV257=DetailedPreviewV257(chosenV343);
            previewedV257=selected;
            proofV257=SignatureV257(chosenV343,planV257);
'@ 'review only selected monitor'
One @'
            status.Text="Preview only: "+planV257.Ready+" matching; "+
                planV257.Skipped+" unavailable. No windows moved.";
'@ @'
            var summaryV343=DeskSpaceMonitorRestoreV343.Describe(
                profile,chosenV343);
            status.Text="Preview only: "+planV257.Ready+" matching; "+
                planV257.Skipped+" unavailable; "+
                summaryV343.Excluded+" outside selected monitor. No windows moved.";
'@ 'explain excluded monitor windows'
One @'
        layout.Children.Add(pick);layout.Children.Add(buttons);
'@ @'
        layout.Children.Add(pick);
        layout.Children.Add(new TextBlock{
            Text="Restore scope — saved physical monitor",
            Foreground=Brushes.White});
        layout.Children.Add(monitorChoiceV343);
        layout.Children.Add(buttons);
'@ 'monitor scope in existing layout UI'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('Restore(chosenV343)') -or
   !$s.Contains('monitorChoiceV343.SelectedIndex=0')){
 throw 'R343 monitor-specific restore or invalidation missing'
}
Write-Host 'R343: existing layout restore supports monitor-specific preview, invalidates approval on monitor switch, never affects unselected monitor.'
