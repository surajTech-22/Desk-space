$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$path,[string]$before,[string]$after,[string]$label) {
  $nl=[string][char]10;$crlf=[string][char]13+$nl
  $s=[IO.File]::ReadAllText($path).Replace($crlf,$nl)
  $a=$before.Replace($crlf,$nl);$b=$after.Replace($crlf,$nl)
  $count=([regex]::Matches($s,[regex]::Escape($a))).Count
  if($count -ne 1){throw "R130 $($label) expected one anchor; found $count"}
  [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
$e="$r/DeskSpaceExcelEditorV42.cs";$c="$r/DeskSpaceExcelUnifiedV126.cs"
Swap $e @'
    private TextBox? librarySearchV42,mappingNameV42,mappingGroupV42;
'@ @'
    private TextBox? librarySearchV42,mappingNameV42,mappingGroupV42;
    private TextBox? formatGroupV130;
'@ 'new format collection field'
Swap $e @'
        formatHeadV126.Children.Add(formatNameV124);
        formatHeadV126.Children.Add(HintV42(
'@ @'
        formatHeadV126.Children.Add(formatNameV124);
        formatHeadV126.Children.Add(new TextBlock{
            Text="Collection name (group related format mappings)",
            FontWeight=FontWeights.SemiBold,Foreground=NavyV42,
            Margin=new Thickness(0,6,0,3)});
        formatGroupV130=Field("Example: Clients / Audit");
        formatGroupV130.Margin=new Thickness(0,0,0,6);
        formatHeadV126.Children.Add(formatGroupV130);
        formatHeadV126.Children.Add(HintV42(
'@ 'restore collection field for formats'
Swap $e @'
        templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};
'@ @'
        templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};
        // Native WPF collection header groupings, retaining all entries.
        var groupHeading=new FrameworkElementFactory(typeof(TextBlock));
        groupHeading.SetBinding(TextBlock.TextProperty,new Binding("Name"));
        groupHeading.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);
        groupHeading.SetValue(TextBlock.ForegroundProperty,NavyV42);
        groupHeading.SetValue(TextBlock.MarginProperty,new Thickness(3,8,2,3));
        templates.GroupStyle.Add(new GroupStyle{
            HeaderTemplate=new DataTemplate{VisualTree=groupHeading}
        });
'@ 'group headings in Saved Mappings'
Swap $e @'
            templates.ItemsSource=display.Select(x=>x.Entry).ToArray();
            templates.SelectedItem=display.FirstOrDefault(x=>
'@ @'
            templates.ItemsSource=display.Select(x=>x.Entry).ToArray();
            var grouped=CollectionViewSource.GetDefaultView(templates.ItemsSource);
            if(grouped.CanGroup)
            {
                grouped.GroupDescriptions.Clear();
                grouped.GroupDescriptions.Add(new PropertyGroupDescription("GroupPath"));
            }
            templates.SelectedItem=display.FirstOrDefault(x=>
'@ 'group combined library items without filtering'
Swap $c @'
    private void ActivateLibraryV126(bool formats)
'@ @'
    private static string NormalizeGroupV130(string raw)
    {
        var parts=(raw??"").Replace('\\','/')
            .Split('/',StringSplitOptions.RemoveEmptyEntries|
                       StringSplitOptions.TrimEntries);
        if(parts.Length>8 || parts.Any(p=>p.Length>60))
            throw new InvalidOperationException(
                "Collections allow up to eight levels of 60 characters.");
        return string.Join("/",parts);
    }
    private void ActivateLibraryV126(bool formats)
'@ 'normalize format group labels'
Swap $c @'
        if(formatModeV126)
        {
            formatDraftV126=null;
            selectedFormatV121=null;
'@ @'
        if(formatModeV126)
        {
            formatDraftV126=null;
            selectedFormatV121=null;
            if(formatGroupV130!=null)formatGroupV130.Clear();
'@ 'new format mapping clears old group'
Swap $c @'
        if(formatNameV124!=null)formatNameV124.Text=title;
        pendingFormatReviewV109=null;
        ShowFormatV126(snapshot);
'@ @'
        if(formatNameV124!=null)formatNameV124.Text=title;
        snapshot.GroupPath=NormalizeGroupV130(formatGroupV130?.Text??"");
        pendingFormatReviewV109=null;
        ShowFormatV126(snapshot);
'@ 'capture format group path'
Swap $c @'
        edited.Name=title;
        edited.Cells.Clear();edited.Anchors.Clear();
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase)))
'@ @'
        edited.Name=title;
        edited.GroupPath=NormalizeGroupV130(formatGroupV130?.Text??"");
        edited.Cells.Clear();edited.Anchors.Clear();
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase) &&
           string.Equals(NormalizeGroupV130(x.GroupPath),edited.GroupPath,
               StringComparison.OrdinalIgnoreCase)))
'@ 'save collection and scoped duplicate detection'
Swap $c @'
            if(formatNameV124!=null)formatNameV124.Text=item.Name;
            pendingFormatReviewV109=null;
'@ @'
            if(formatNameV124!=null)formatNameV124.Text=item.Name;
            if(formatGroupV130!=null)formatGroupV130.Text=item.GroupPath;
            pendingFormatReviewV109=null;
'@ 'load correct saved format collection'
Write-Host 'R130 grouped shared mappings and restored format collection field with persisted GroupPath.'
