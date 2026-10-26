$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$wheel="$root/DeskSpaceExcelWheelV42.cs"
$editor="$root/DeskSpaceExcelEditorV42.cs"
$ctrl="$root/DeskSpaceExcelUnifiedV126.cs"
function Swap([string]$path,[string]$old,[string]$new,[string]$label) {
  $lf=[string][char]10;$crlf=[string][char]13+$lf
  $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
  $a=$old.Replace($crlf,$lf);$b=$new.Replace($crlf,$lf)
  $count=([regex]::Matches($s,[regex]::Escape($a))).Count
  if($count -ne 1){throw "R130 $($label): expected one anchor; found $count"}
  [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
# The Excel donut MUST NOT load both saved libraries into its Information
# branch. Its root contains separate Information and Formats & Formulas
# categories. Both have the EXISTING recursive group / hover subrings.
Swap $wheel @'
    private List<DeskSpaceExcelTemplateV16> formats=new();
'@ @'
    // Legacy 'formats' field actually holds INFORMATION value mappings.
    // Keep it for older previews but never mix in format-only snapshots.
    private List<DeskSpaceExcelTemplateV16> formats=new();
    private List<DeskSpaceExcelTemplateV16> formatOnlyV130=new();
    private enum SavedKindV130 { Information, Formats }
    private SavedKindV130 selectedKindV130=SavedKindV130.Information;
    private List<DeskSpaceExcelTemplateV16> ActiveSavedV130()=>
        selectedKindV130==SavedKindV130.Formats?formatOnlyV130:formats;
    private DeskSpaceExcelTemplateV16? ResolveSavedV130(Guid? id)=>
        id.HasValue?ActiveSavedV130().FirstOrDefault(x=>x.Id==id.Value):null;
    private void PreviewSavedV130(DeskSpaceExcelTemplateV16 template)
    {
        if(selectedKindV130==SavedKindV130.Formats)PreviewFormatV130(template);
        else Preview(template);
    }
'@ 'separate typed donut libraries'
Swap $wheel @'
    public int SavedCount=>formats.Count;
'@ @'
    public int SavedCount=>formats.Count+formatOnlyV130.Count;
'@ 'combined saved count'
Swap $wheel @'
                if(displayed[slot].Command=="information")
                    ExpandRing(1,"",RootAngle(slot));
'@ @'
                var command=displayed[slot].Command;
                if(command=="information" || command=="format-library")
                {
                    selectedKindV130=command=="format-library"?
                        SavedKindV130.Formats:SavedKindV130.Information;
                    ExpandRing(1,"",RootAngle(slot));
                }
'@ 'hover navigation chooses correct origin library'
Swap $wheel @'
        foreach(var format in formats)
        {
            var path=Group(format);
'@ @'
        foreach(var format in ActiveSavedV130())
        {
            var path=Group(format);
'@ 'recursive grouped donut branch source'
Swap $wheel @'
        foreach(var format in formats.Where(x=>
            string.Equals(Group(x),selectedGroup,StringComparison.OrdinalIgnoreCase))
'@ @'
        foreach(var format in ActiveSavedV130().Where(x=>
            string.Equals(Group(x),selectedGroup,StringComparison.OrdinalIgnoreCase))
'@ 'recursive grouped donut leaf source'
Swap $wheel @'
            var item=formats.FirstOrDefault(x=>x.Id==entry.Id);
            if(item!=null)Preview(item);
'@ @'
            var item=ResolveSavedV130(entry.Id);
            if(item!=null)PreviewSavedV130(item);
'@ 'hover nested leaf uses appropriate preview'
Swap $wheel @'
        var format=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(format==null)return;
        if(!ExcelIsOpen()){Preview(format);return;}
        HideArc();collapseParent();_=PasteAsync(format);
'@ @'
        var selected=ResolveSavedV130(item.Id);
        if(selected==null)return;
        if(!ExcelIsOpen()){PreviewSavedV130(selected);return;}
        HideArc();collapseParent();
        if(selectedKindV130==SavedKindV130.Formats)
            _=PasteFormatV130(selected);
        else _=PasteAsync(selected);
'@ 'nested menu paste dispatch by origin'
Swap $wheel @'
            formats=DeskSpaceExcelStoreV16.Load()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
            level=ExcelLevel.Functions;
'@ @'
            formats=DeskSpaceExcelStoreV16.Load()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
            formatOnlyV130=DeskSpaceExcelStoreV16.LoadFormats()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
            selectedKindV130=SavedKindV130.Information;
            level=ExcelLevel.Functions;
'@ 'load BOTH encrypted stores without cross-storing'
Swap $wheel @'
            entries.Add(("Information","information","",null));
            return entries;
'@ @'
            entries.Add(("Information","information","",null));
            entries.Add(("Formats & Formulas","format-library","",null));
            return entries;
'@ 'Excel donut top-level separate branches'
Swap $wheel @'
        foreach(var format in formats)
        {
            var g=Group(format);
'@ @'
        foreach(var format in ActiveSavedV130())
        {
            var g=Group(format);
'@ 'remaining nonnested grouped list'
Swap $wheel @'
        foreach(var entry in formats.Where(x=>
            string.Equals(Group(x),groupPath,StringComparison.OrdinalIgnoreCase))
'@ @'
        foreach(var entry in ActiveSavedV130().Where(x=>
            string.Equals(Group(x),groupPath,StringComparison.OrdinalIgnoreCase))
'@ 'remaining nonnested selected kind entries'
Swap $wheel @'
        if(item.Command=="information"||item.Command=="group")
'@ @'
        if(item.Command=="information"||item.Command=="format-library"||
           item.Command=="group")
'@ 'root hover for both library categories'
Swap $wheel @'
            var format=formats.FirstOrDefault(x=>x.Id==item.Id);
            if(format!=null)Preview(format);
'@ @'
            var saved=ResolveSavedV130(item.Id);
            if(saved!=null)PreviewSavedV130(saved);
'@ 'root leaf preview by library'
Swap $wheel @'
        if(item.Command=="information")
        {
            ExpandRing(1,"",RootAngle(slot));
            return;
        }
'@ @'
        if(item.Command=="information"||item.Command=="format-library")
        {
            selectedKindV130=item.Command=="format-library"?
                SavedKindV130.Formats:SavedKindV130.Information;
            ExpandRing(1,"",RootAngle(slot));
            return;
        }
'@ 'root category click dispatch'
Swap $wheel @'
        var entry=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(entry==null)return;
        if(!ExcelIsOpen()){Preview(entry);return;}
        HideArc();collapseParent();
        _=PasteAsync(entry);
'@ @'
        var entry=ResolveSavedV130(item.Id);
        if(entry==null)return;
        if(!ExcelIsOpen()){PreviewSavedV130(entry);return;}
        HideArc();collapseParent();
        if(selectedKindV130==SavedKindV130.Formats)
            _=PasteFormatV130(entry);
        else _=PasteAsync(entry);
'@ 'root leaf paste dispatch'
# Format-only preview uses the SAVED VISUAL, not the old information values.
# The paste path always previews the exact selected destination and displays
# an explicit confirmation BEFORE the safety-checked format-only writer.
Swap $wheel @'
    private void Preview(DeskSpaceExcelTemplateV16 entry)
'@ @'
    private void PreviewFormatV130(DeskSpaceExcelTemplateV16 entry)
    {
        shelfTitle.Text=entry.Name;
        shelfBody.Children.Clear();
        var visual=entry.SheetVisualV101;
        if(visual!=null)
        {
            shelfBody.Children.Add(new TextBlock{
                Text="Format / Formula mapping · "+visual.Rows+" × "+
                    visual.Columns+" cells",
                FontSize=12,Foreground=Ink,FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(5,4,4,2)
            });
            shelfBody.Children.Add(new TextBlock{
                Text="Collection: "+(string.IsNullOrWhiteSpace(entry.GroupPath)?
                    "(ungrouped)":entry.GroupPath),
                FontSize=11,Foreground=Ink,Margin=new Thickness(5,2,4,2)
            });
            shelfBody.Children.Add(new TextBlock{
                Text="Stored cell formatting and formula structure only. "+
                    "Original values are not copied.",
                FontSize=11,Foreground=Ink,TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(5,2,4,2)
            });
        }
        shelfMessage.Text=visual==null?
            "This saved format is incomplete. Recapture it in Excel Mapping.":
            !ExcelIsOpen()?
                "Open Microsoft Excel to preview the destination. "+
                "This saved format can still be inspected without Excel.":
                "Select the destination start cell in Excel, then click "+
                "to verify the complete footprint and approve formatting. "+
                "Mixed partial data is blocked; a backup is mandatory.";
        Position();
        shelf.IsOpen=true;
    }
    private async System.Threading.Tasks.Task PasteFormatV130(
        DeskSpaceExcelTemplateV16 entry)
    {
        if(disposed)return;
        if(!ExcelIsOpen()){PreviewFormatV130(entry);return;}
        if(!formatOnlyV130.Any(x=>object.ReferenceEquals(x,entry)))
        {
            Status("Format mapping blocked",
                "Selected entry is not from the formats library. No changes made.");
            return;
        }
        pasting=true;
        bool applying=false;
        Status("Checking Excel formatting destination",
            "Verifying the entire selected Excel range before any change…");
        try
        {
            if(entry.SheetVisualV101==null)
                throw new InvalidOperationException(
                    "Saved formatting layout is incomplete. Recapture before pasting.");
            var check=await DeskSpaceExcelWorkerV41.PreviewFormatAsync(entry);
            if(disposed)return;
            if(!check.Eligible)
                throw new InvalidOperationException(check.ToString());
            var consent=MessageBox.Show(owner,
                "Apply saved formats/formulas from '"+entry.Name+"' to "+
                check.Workbook+" / "+check.Sheet+" "+check.Destination+"?"+
                Environment.NewLine+check.PlannedActions+
                Environment.NewLine+"Only a fully empty or fully compatible "+
                "destination is allowed. Mixed layouts are blocked."+
                Environment.NewLine+"Existing cell values and formulas "+
                "are protected, and an Excel backup will be created.",
                "DeskSpace · Confirm Format Mapping",
                MessageBoxButton.YesNo,MessageBoxImage.Warning);
            if(consent!=MessageBoxResult.Yes)
            {
                Status("Format mapping cancelled","No workbook changes made.");
                return;
            }
            applying=true;
            Status("Applying verified formats",
                "Rechecking the destination, backing up and applying saved formats…");
            var result=await DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,check);
            if(disposed)return;
            Status("Formats applied",result);
            successTimer.Start();
        }
        catch(Exception ex)
        {
            if(!disposed)
                Status("Format mapping blocked",
                    ex.Message+(applying?
                        " Examine the workbook and its backup for any partial result.":
                        " No workbook changes were made."));
        }
        finally{pasting=false;}
    }
    private void Preview(DeskSpaceExcelTemplateV16 entry)
'@ 'read-only format preview + reviewed independent format paste'

# The original information header/value paste is UNCHANGED and retains the
# verified-header requirement from R125.  Format paste never calls it.
Write-Host 'R130 wheel: distinct recursive Information and Format & Formula subdonuts, both loaded from separate encrypted stores with correct independent paste gates.'

# RESTORE GROUPING IN THE UNIFIED EDITOR, without creating another module.
# Information's mappingGroupV42 already exists in the data header. The format
# header must have its own collection/group field and persist GroupPath.
Swap $ctrl @'
    private bool formatModeV126;
'@ @'
    private bool formatModeV126;
    private TextBox? formatGroupV130;
    private static string NormalizeGroupV130(string? value)
    {
        var segments=(value??"").Replace('\\','/')
            .Split('/',StringSplitOptions.RemoveEmptyEntries|
                       StringSplitOptions.TrimEntries);
        if(segments.Length>8 || segments.Any(x=>x.Length>60))
            throw new InvalidOperationException(
                "A collection may have up to eight group levels, each 60 characters.");
        return string.Join("/",segments);
    }
'@ 'separate editable format collection with group validation'
Swap $editor @'
        formatHeadV126.Children.Add(formatNameV124);
        formatHeadV126.Children.Add(HintV42(
'@ @'
        formatHeadV126.Children.Add(formatNameV124);
        formatHeadV126.Children.Add(new TextBlock{
            Text="Collection / group name",FontWeight=FontWeights.SemiBold,
            Foreground=NavyV42,Margin=new Thickness(1,5,1,2)});
        formatGroupV130=Field("Example: Clients / Audit");
        formatGroupV130.Margin=new Thickness(0,0,0,5);
        formatHeadV126.Children.Add(formatGroupV130);
        formatHeadV126.Children.Add(HintV42(
'@ 'make format grouping visible and editable'
Swap $ctrl @'
            if(formatNameV124!=null)formatNameV124.Clear();
            pendingFormatReviewV109=null;
'@ @'
            if(formatNameV124!=null)formatNameV124.Clear();
            if(formatGroupV130!=null)formatGroupV130.Clear();
            pendingFormatReviewV109=null;
'@ 'new format clears its collection'
Swap $ctrl @'
        // Only the structural visual survives in the format library.
        snapshot.Cells.Clear();
'@ @'
        // Only structural visual and group metadata survive in the format library.
        snapshot.GroupPath=NormalizeGroupV130(formatGroupV130?.Text);
        snapshot.Cells.Clear();
'@ 'format copy retains collection metadata'
Swap $ctrl @'
        edited.Name=title;
        edited.Cells.Clear();edited.Anchors.Clear();
'@ @'
        edited.Name=title;
        edited.GroupPath=NormalizeGroupV130(formatGroupV130?.Text);
        edited.Cells.Clear();edited.Anchors.Clear();
'@ 'save format collection instead of dropping it'
Swap $ctrl @'
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase)))
'@ @'
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase) &&
           string.Equals(x.GroupPath,edited.GroupPath,
               StringComparison.OrdinalIgnoreCase)))
'@ 'same name allowed in different collections'
Swap $ctrl @'
        if(formatNameV124!=null)formatNameV124.Text=item.Name;
            pendingFormatReviewV109=null;
'@ @'
        if(formatNameV124!=null)formatNameV124.Text=item.Name;
            if(formatGroupV130!=null)formatGroupV130.Text=item.GroupPath;
            pendingFormatReviewV109=null;
'@ 'select saved format restores original collection'
# R128 replace of the selection handler is the one in use.
# Make the existing grouped Saved Mappings column DISPLAY the collection,
# rather than merely sorting by GroupPath without showing the group.
Swap $editor @'
        savedRow.AppendChild(itemName);
        templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};
'@ @'
        var groupAndName=new FrameworkElementFactory(typeof(StackPanel));
        groupAndName.SetValue(StackPanel.OrientationProperty,Orientation.Vertical);
        var groupLabel=new FrameworkElementFactory(typeof(TextBlock));
        groupLabel.SetBinding(TextBlock.TextProperty,new Binding("GroupPath"));
        groupLabel.SetValue(TextBlock.FontSizeProperty,10.0);
        groupLabel.SetValue(TextBlock.ForegroundProperty,
            new SolidColorBrush(Color.FromRgb(116,129,150)));
        groupLabel.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
        var groupStyle=new Style(typeof(TextBlock));
        var emptyGroup=new DataTrigger{Binding=new Binding("GroupPath"),Value=""};
        emptyGroup.Setters.Add(new Setter(TextBlock.VisibilityProperty,Visibility.Collapsed));
        groupStyle.Triggers.Add(emptyGroup);
        groupLabel.SetValue(TextBlock.StyleProperty,groupStyle);
        groupAndName.AppendChild(groupLabel);
        groupAndName.AppendChild(itemName);
        savedRow.AppendChild(groupAndName);
        templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};
'@ 'show collection label in shared saved list without duplicate modules'
Write-Host 'R130 grouping: show collection names, preserve info and format group paths; grouped donuts and correct formats-only storage/pasting.'
