$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r126/DeskSpaceExcelUnifiedV126.cs' "$root/DeskSpaceExcelUnifiedV126.cs" -Force
$editor="$root/DeskSpaceExcelEditorV42.cs"
function Swap([string]$path,[string]$old,[string]$new,[string]$label){
    $lf=[string][char]10;$crlf=[string][char]13+$lf
    $text=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($text,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R126 $($label): wanted one anchor, got $count"}
    [IO.File]::WriteAllText($path,$text.Replace($old,$new),
       [Text.UTF8Encoding]::new($false))
}
# Keep the two top choices, but turn them into real COPY actions. Selection
# of library is done before capture and subsequent Save/Preview/Apply.
Swap $editor @'
        mainActions.Children.Add(ButtonV42(
            "Information / Data · matched headers",()=>{
                if(routesV124!=null)routesV124.SelectedIndex=0;
            },true));
        mainActions.Children.Add(ButtonV42(
            "Format / Formula mappings · copy / paste",()=>{
                if(routesV124!=null)routesV124.SelectedIndex=1;
            },true));
'@ @'
        mainActions.Children.Add(RequiresExcel(
            "Information / Data · matched headers",()=>{
                ActivateLibraryV126(false);
                Run(Capture);
            },true));
        mainActions.Children.Add(RequiresExcel(
            "Format / Formula mappings · copy / paste",()=>{
                ActivateLibraryV126(true);
                Run(CaptureFormatDraftV126);
            },true));
'@ 'top COPY buttons must invoke distinct capture routes'
Swap $editor @'
        mainActions.Children.Add(ButtonV42("Refresh saved mappings",()=>Run(()=>ReloadSavedV42(true))));
'@ @'
        mainActions.Children.Add(ButtonV42("Refresh saved mappings",()=>Run(RefreshUnifiedV126)));
        mainActions.Children.Add(ButtonV42("New Mapping",()=>Run(NewUnifiedMappingV126)));
        mainActions.Children.Add(ButtonV42("Save Mapping",()=>Run(SaveUnifiedMappingV126),true));
'@ 'new/save beside refresh'
# Saved Mappings retains its original location, but each row has a right-side
# delete icon that deletes exactly this record from exactly its saved library.
Swap $editor @'
            MinHeight=165,DisplayMemberPath="Name",Margin=new Thickness(0,0,0,4),
'@ @'
            MinHeight=165,Margin=new Thickness(0,0,0,4),
'@ 'custom saved row template'
Swap $editor @'
        templates.SelectionChanged+=(_,_)=>OnLibrarySelectedV42();
'@ @'
        var savedRow=new FrameworkElementFactory(typeof(DockPanel));
        var trash=new FrameworkElementFactory(typeof(Button));
        trash.SetValue(Button.ContentProperty,"×");
        trash.SetValue(Button.ToolTipProperty,"Delete saved mapping");
        trash.SetValue(Button.WidthProperty,27.0);
        trash.SetValue(Button.HeightProperty,25.0);
        trash.SetValue(Button.MarginProperty,new Thickness(4,1,0,1));
        trash.SetValue(Button.PaddingProperty,new Thickness(0));
        trash.SetValue(DockPanel.DockProperty,Dock.Right);
        trash.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,e)=>{
            e.Handled=true;
            if((sender as Button)?.DataContext is DeskSpaceExcelTemplateV16 item)
                Run(()=>DeleteUnifiedEntryV126(item.Id,formatModeV126));
        }));
        savedRow.AppendChild(trash);
        var itemName=new FrameworkElementFactory(typeof(TextBlock));
        itemName.SetBinding(TextBlock.TextProperty,new Binding("Name"));
        itemName.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);
        itemName.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
        savedRow.AppendChild(itemName);
        templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};
        templates.SelectionChanged+=(_,_)=>OnUnifiedLibrarySelectedV126();
'@ 'inline delete and selected item's library'
# Remove redundant embedded editor commands; top copy/new/save actions own
# them. Delete is exclusively per-row in the saved list.
Swap $editor @'
        editorHead.Children.Add(editButtons);
'@ @'
        // New, Save and Copy are in the top bar; delete is beside saved rows.
'@ 'avoid duplicated actions below editor'
# Full-sheet expand should display the current format snapshot when that
# library is active, including a freshly captured UNSAVED draft.
Swap $editor @'
            var current=editingTemplateV42;
'@ @'
            var current=formatModeV126?
                (formatDraftV126??selectedFormatV121):editingTemplateV42;
'@ 'preview the selected library snapshot'
# The previous UI already has the correct inner two tabs. Keep exactly
# them and rename as requested.
Swap $editor @'
            Header="Captured sheet · formats & formulas",
            Content=previewPane});
        tabs.Items.Add(new TabItem{
            Header="Mappings · edit & paste",
            Content=mappingPane});
'@ @'
            Header="Captured Sheet",
            Content=previewPane});
        tabs.Items.Add(new TabItem{
            Header="Mappings",
            Content=mappingPane});
'@ 'only captured sheet and mappings tabs'
# The old R124 separate formatting PAGE is never added to the UI.
# Reparent only its needed format name and controls to the SAME main editor.
Swap $editor @'
        routesV124=new TabControl{Margin=new Thickness(5,0,5,4)};
        routesV124.Items.Add(new TabItem{
            Header="Information / Data · header match",Content=content
        });
        routesV124.Items.Add(new TabItem{
            Header="Format / Formula mappings · separate library",
            Content=new ScrollViewer{
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=formatPageV124
            }
        });
        routesV124.SelectionChanged+=(_,e)=>{
            if(!ReferenceEquals(e.Source,routesV124))return;
            var mappings=routesV124.SelectedIndex==0;
            advanced.Visibility=mappings?Visibility.Visible:Visibility.Collapsed;
            shortcutPane.Visibility=mappings?Visibility.Visible:Visibility.Collapsed;
        };
        root.Children.Add(routesV124);
'@ @'
        // One Saved Mappings column, one Captured Sheet/Mappings tab control.
        // Neither library gets an additional module/tab wrapper.
        unifiedTabsV126=tabs;
        dataMappingsPaneV126=mappingPane;
        dataHeadV126=editorHead;
        formatPageV124.Children.Remove(formatNameV124);
        formatHeadV126=new StackPanel{Visibility=Visibility.Collapsed,
            Margin=new Thickness(4,2,4,6)};
        formatHeadV126.Children.Add(new TextBlock{
            Text="Format mapping name",FontWeight=FontWeights.SemiBold,
            Foreground=NavyV42});
        formatHeadV126.Children.Add(formatNameV124);
        formatHeadV126.Children.Add(HintV42(
            "Formats and formulas only. Source data values are not saved.",11));
        // The dropdown and duplicate copy/delete actions from R121 are
        // retired. Preview and Apply remain hooked to the format engine.
        formatGroup.Children.Remove(formatControls);
        formatControls.Children.RemoveAt(formatControls.Children.Count-1);
        formatControls.Children.RemoveAt(0);
        formatDetailsV126=new DataGrid{
            AutoGenerateColumns=false,IsReadOnly=true,
            CanUserAddRows=false,CanUserDeleteRows=false,
            HeadersVisibility=DataGridHeadersVisibility.Column,
            Margin=new Thickness(3,4,3,4)};
        formatDetailsV126.Columns.Add(new DataGridTextColumn{
            Header="Row",Binding=new Binding("Row"),Width=55});
        formatDetailsV126.Columns.Add(new DataGridTextColumn{
            Header="Column",Binding=new Binding("Column"),Width=65});
        formatDetailsV126.Columns.Add(new DataGridTextColumn{
            Header="Input type",Binding=new Binding("StoredType"),Width=120});
        formatDetailsV126.Columns.Add(new DataGridTextColumn{
            Header="Number format",Binding=new Binding("NumberFormat"),
            Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        formatDetailsV126.Columns.Add(new DataGridTextColumn{
            Header="Formula R1C1",Binding=new Binding("FormulaR1C1"),
            Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        formatMappingsPaneV126=new DockPanel{LastChildFill=true};
        var formatFoot=new StackPanel{Margin=new Thickness(3,2,3,4)};
        formatFoot.Children.Add(formatControls);
        formatPageV124.Children.Remove(formatSummaryV124);
        formatFoot.Children.Add(formatSummaryV124);
        DockPanel.SetDock(formatFoot,Dock.Bottom);
        formatMappingsPaneV126.Children.Add(formatFoot);
        formatMappingsPaneV126.Children.Add(formatDetailsV126);
        // The final child still fills the right-side editor.
        right.Children.Remove(tabs);
        DockPanel.SetDock(formatHeadV126,Dock.Top);
        right.Children.Add(formatHeadV126);
        right.Children.Add(tabs);
        root.Children.Add(content);
'@ 'unified visible editor without duplicate pages'
# Same library panel dynamically displays either saved values or saved
# formats; it keeps ID/name, filter and per-item delete actions.
Swap $editor @'
        var term=librarySearchV42?.Text.Trim()??"";
        var display=saved
'@ @'
        var term=librarySearchV42?.Text.Trim()??"";
        if(formatModeV126)
        {
            var matches=formatLibraryV121
                .Where(x=>term.Length==0 ||
                    x.Name.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                    x.SourceSheet.Contains(term,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
            selectionGuardV42=true;
            try{
                var id=formatDraftV126==null?selectedFormatV121?.Id:null;
                templates.ItemsSource=matches;
                templates.SelectedItem=matches.FirstOrDefault(x=>x.Id==id);
            }
            finally{selectionGuardV42=false;}
            libraryInfoV42.Text=formatLibraryV121.Count+
                " saved format mappings · "+matches.Length+" shown";
            return;
        }
        var display=saved
'@ 'library-aware saved mappings list'
Swap $editor @'
                try {ReloadSavedV42(false);} catch(Exception ex) {
'@ @'
                try {ReloadSavedV42(false);ReloadFormatsV121();
                    FilterLibraryV42();} catch(Exception ex) {
'@ 'refresh both stores on workspace activation'
Swap $editor @'
        ReloadSavedV42(false);
        RefreshExcelActions();
'@ @'
        ReloadSavedV42(false);
        ReloadFormatsV121();
        FilterLibraryV42();
        RefreshExcelActions();
'@ 'refresh initial saved formats too'
# Capturing a new unsaved format must not have its name or preview overwritten
# by an old unrelated dropdown selection.
Swap $editor @'
            if(selectedFormatV121!=null) {
'@ @'
            if(selectedFormatV121!=null && formatDraftV126==null) {
'@ 'ignore stale format selection during draft capture'
# WPF acceptance tests should inspect the new single tabs after the R103
# helper changes. No child of a second outer routing TabControl exists.
$smoke='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($smoke)
$s=$s.Replace('"Mappings · edit & paste"','"Mappings"')
$s=$s.Replace('"Captured sheet · formats & formulas"','"Captured Sheet"')
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R126: top copy routes, one Saved Mappings list with inline delete, one Captured Sheet/Mappings pair, two untouched library-specific paste engines.'
