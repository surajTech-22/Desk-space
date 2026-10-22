using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// A single understandable saved-format view shared by the main Workspace and
// circle. Underlying exact Excel cell offsets/formulas are the original model.
// The review rows are editable COPYs until the user presses Save Mapping.
internal sealed partial class DeskSpaceExcelV16
{
    private sealed class MappingRowV42
    {
        internal DeskSpaceExcelCellV16? Cell;
        internal string OriginalRaw="";
        internal string OriginalLabel="";
        internal int HeaderRow=-1,HeaderCol=-1;
        public bool Include {get;set;}=true;
        public string Header {get;set;}="";
        public string ReferenceCell {get;set;}="";
        public string ValueCell {get;set;}="";
        public string Value {get;set;}="";
        public string Kind {get;set;}="Value";
        public string CellType {get;set;}="";
        public bool IsFormula=>CellType=="formula";
        public string Formula {get;set;}="";
    }

    private TextBox? librarySearchV42,mappingNameV42,mappingGroupV42;
    private TextBlock? libraryInfoV42,editorInfoV42;
    private DataGrid? mappingGridV42;
    private readonly List<MappingRowV42> mappingRowsV42=new();
    private DeskSpaceExcelTemplateV16? editingTemplateV42;
    private Guid editingIdV42=Guid.Empty;
    private bool editingDraftV42,selectionGuardV42;
    private bool editorBusyV42,hasUnsavedChangesV42,loadingEditorV42;

    private static Brush NavyV42=>new SolidColorBrush(Color.FromRgb(31,50,82));
    private static Brush BorderV42=>new SolidColorBrush(Color.FromRgb(204,215,230));
    private static Border PanelV42(UIElement child)=>new(){
        Background=Brushes.White,BorderThickness=new Thickness(1),
        BorderBrush=BorderV42,CornerRadius=new CornerRadius(10),
        Padding=new Thickness(12),Margin=new Thickness(0,0,10,0),Child=child
    };
    private static Button ButtonV42(string text,Action action,bool primary=false)
    {
        var button=ActionButton(text,action);
        button.Padding=new Thickness(12,7,12,7);
        button.Margin=new Thickness(3,3,6,3);
        button.MinHeight=34;
        if(primary)
        {
            button.Background=new SolidColorBrush(Color.FromRgb(41,96,164));
            button.Foreground=Brushes.White;
            button.BorderBrush=button.Background;
        }
        return button;
    }
    private TextBlock HintV42(string text,int size=12)=>new(){
        Text=text,FontSize=size,Foreground=muted,
        Margin=new Thickness(2,4,2,8),TextWrapping=TextWrapping.Wrap
    };

    public FrameworkElement Create()
    {
        // The main Workspace provides the mapping editor. In the circular
        // Excel wheel, selecting a saved format pastes values directly
        // without opening this heavyweight editor.
        var root=new DockPanel{
            Background=new SolidColorBrush(Color.FromRgb(247,249,254)),
            LastChildFill=true
        };
        var top=new DockPanel{
            Background=NavyV42,LastChildFill=true,Margin=new Thickness(0)
        };
        var exit=ButtonV42(inWorkspace?"← Workspace":"← Circle",back);
        DockPanel.SetDock(exit,Dock.Right);
        top.Children.Add(exit);
        top.Children.Add(new TextBlock{
            Text="Excel Mapping",FontSize=19,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,Margin=new Thickness(18,12,12,12)
        });
        DockPanel.SetDock(top,Dock.Top);
        root.Children.Add(top);

        // The toolbar is limited to three normal actions; the source/target
        // options remain accessible in a collapsible section.
        var head=new StackPanel{Margin=new Thickness(8,5,8,3)};
        head.Children.Add(new TextBlock{
            Text="Excel Mapping",FontSize=15,
            FontWeight=FontWeights.SemiBold,Foreground=ink});
        // Keep explanatory text in the optional section to maximize the
        // actual mapping area in short or restored Windows sizes.
        var mainActions=new WrapPanel();
        mainActions.Children.Add(ButtonV42(
            "Copy and analyze selected Excel cells",()=>Run(Capture),true));
        mainActions.Children.Add(ButtonV42(
            "Screenshot",()=>OpenScreenshot()));
        mainActions.Children.Add(ButtonV42("Refresh saved mappings",()=>Run(()=>ReloadSavedV42(true))));
        head.Children.Add(mainActions);
        DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);

        var content=new Grid{Margin=new Thickness(6,0,6,4),MinHeight=180};
        content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(198)});
        content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var left=new DockPanel{LastChildFill=true};
        var libraryHeader=new StackPanel();
        libraryHeader.Children.Add(new TextBlock{
            Text="SAVED MAPPINGS",FontSize=13,Foreground=ink,
            FontWeight=FontWeights.Bold,Margin=new Thickness(0,3,0,6)});
        librarySearchV42=Field("Search saved Excel formats");
        librarySearchV42.Margin=new Thickness(0,0,0,7);
        librarySearchV42.TextChanged+=(_,_)=>FilterLibraryV42();
        libraryHeader.Children.Add(librarySearchV42);
        libraryInfoV42=HintV42("Loading saved mappings…");
        libraryHeader.Children.Add(libraryInfoV42);
        DockPanel.SetDock(libraryHeader,Dock.Top);left.Children.Add(libraryHeader);
        templates=new ListBox{
            MinHeight=165,DisplayMemberPath="Name",Margin=new Thickness(0,0,0,4),
            BorderThickness=new Thickness(0),FontSize=13,
            Background=Brushes.White
        };
        templates.SelectionChanged+=(_,_)=>OnLibrarySelectedV42();
        left.Children.Add(templates);
        var libraryPanel=PanelV42(left);
        Grid.SetColumn(libraryPanel,0);
        content.Children.Add(libraryPanel);

        // Right-side editor: one row shows Header -> Value -> Destination.
        // All edits are staged and saved explicitly; mere selection never
        // changes a saved DPAPI file or a running Excel workbook.
        var right=new DockPanel{LastChildFill=true};
        var editorHead=new StackPanel();
        editorHead.Children.Add(new TextBlock{
            Text="COLLECTION & DATA",FontSize=13,
            FontWeight=FontWeights.Bold,Foreground=ink,
            Margin=new Thickness(2,3,2,7)});
        editorHead.Children.Add(new TextBlock{
            Text="Collection name (groups related data together)",
            FontWeight=FontWeights.SemiBold,Foreground=ink,
            Margin=new Thickness(2,0,2,2)});
        // GroupPath is optional and stored with each encrypted mapping.
        // A new collection appears in the wheel when the first mapping is saved.
        mappingGroupV42=Field("Example: Clients / Audit");
        mappingGroupV42.Margin=new Thickness(0,0,0,6);
        mappingGroupV42.TextChanged+=(_,_)=>{
            if(!loadingEditorV42 && editingTemplateV42!=null)
                hasUnsavedChangesV42=true;
        };
        editorHead.Children.Add(mappingGroupV42);
        editorHead.Children.Add(new TextBlock{
            Text="Data name (saved Excel mapping)",
            FontWeight=FontWeights.SemiBold,Foreground=ink,
            Margin=new Thickness(2,1,2,2)});
        mappingNameV42=Field("Example: GST details");
        mappingNameV42.Margin=new Thickness(0,0,0,3);
        mappingNameV42.TextChanged+=(_,_)=>{
            if(!loadingEditorV42 && editingTemplateV42!=null)
                hasUnsavedChangesV42=true;
            // Capture() still uses this original field in the advanced panel.
            // Keep it synchronized without changing Excel capture semantics.
            if(name!=null && name.Text!=mappingNameV42.Text)
                name.Text=mappingNameV42.Text;
        };
        editorHead.Children.Add(mappingNameV42);
        editorInfoV42=HintV42(
            "Enter a collection and data name, select cells in Excel, then click Capture. "+
            "Edit Data field / Data value rows below and save. "+
            "Collections appear in the wheel after their first data mapping is saved.",11);
        editorHead.Children.Add(editorInfoV42);

        var editButtons=new WrapPanel();
        editButtons.Children.Add(ButtonV42(
            "Capture selected Excel cells",()=>Run(Capture),true));
        editButtons.Children.Add(ButtonV42("Save collection & data",
            ()=>Run(SaveEditorV42),true));
        editButtons.Children.Add(ButtonV42("Delete selected data",
            ()=>Run(DeleteMappingV42)));
        editorHead.Children.Add(editButtons);
        DockPanel.SetDock(editorHead,Dock.Top);right.Children.Add(editorHead);

        var footer=new StackPanel{Margin=new Thickness(0,2,0,0)};
        var pasteButtons=new WrapPanel();
        pasteButtons.Children.Add(ButtonV42("Preview matching cells",()=>Run(Preview)));
        pasteButtons.Children.Add(ButtonV42("Apply autofill",()=>Run(Apply),true));
        footer.Children.Add(pasteButtons);
        matchSummary=HintV42("Preview before any Excel changes.",10);
        footer.Children.Add(matchSummary);
        previewRows=new ListBox{Height=42,Margin=new Thickness(0,2,0,3),FontSize=11};
        footer.Children.Add(previewRows);
        status=HintV42("Choose a saved mapping.",10);
        footer.Children.Add(status);
        DockPanel.SetDock(footer,Dock.Bottom);right.Children.Add(footer);

        mappingGridV42=new DataGrid{
            AutoGenerateColumns=false,IsReadOnly=false,
            HeadersVisibility=DataGridHeadersVisibility.Column,
            CanUserAddRows=false,CanUserDeleteRows=false,
            CanUserReorderColumns=true,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,
            AlternatingRowBackground=new SolidColorBrush(Color.FromRgb(243,248,255)),
            BorderThickness=new Thickness(1),BorderBrush=BorderV42,
            RowHeight=37,ColumnHeaderHeight=36,
            FrozenColumnCount=2,
            EnableRowVirtualization=true,EnableColumnVirtualization=true
        };
        mappingGridV42.Columns.Add(new DataGridCheckBoxColumn{
            Header="Save",Binding=new Binding("Include"){Mode=BindingMode.TwoWay},
            Width=51});
        AddColumnV42("Data field name","Header",210);
        AddColumnV42("Header cell","ReferenceCell",93);
        AddColumnV42("Data value","Value",210);
        AddColumnV42("Value cell","ValueCell",90);
        AddColumnV42("Type","Kind",98);
        mappingGridV42.CellEditEnding+=(_,_)=>{
            hasUnsavedChangesV42=true;
            latest=null;
            if(editorInfoV42!=null)editorInfoV42.Text=
                "Edited mapping (unsaved). Click Save mapping changes to keep it.";
        };
        right.Children.Add(mappingGridV42);
        var editorPanel=PanelV42(right);
        Grid.SetColumn(editorPanel,1);
        content.Children.Add(editorPanel);
        // Optional controls stay accessible without forcing extra steps for
        // simple capture, review, edit, save, paste.
        var advanced=new Expander{
            Header="More options and instructions",
            IsExpanded=false,Margin=new Thickness(7,1,7,2)
        };
        var options=new StackPanel{Margin=new Thickness(5)};
        options.Children.Add(HintV42(
            "A collection is an optional group name such as Clients. "+
            "A data name identifies each saved mapping inside it. "+
            "Each row contains a data field name and its value. "+
            "Verified Excel addresses remain protected; screenshot-only "+
            "formats must be linked to real Excel cells before pasting."));
        name=Field("Data name","Excel mapping");
        range=Field("Optional source address e.g. B20:D25","");
        options.Children.Add(new TextBlock{
            Text="Data name (also editable above)",Foreground=ink});
        options.Children.Add(name);
        options.Children.Add(new TextBlock{Text="Optional Excel range (leave empty for current selection)",Foreground=ink});
        options.Children.Add(range);
        formulas=new CheckBox{Content="Record source formulas for reference only",
            IsChecked=true,IsEnabled=false};
        options.Children.Add(formulas);
        var extraButtons=new WrapPanel();
        extraButtons.Children.Add(ButtonV42("Open workbook…",()=>Run(OpenWorkbook)));
        extraButtons.Children.Add(ButtonV42("Analyze marked image region",AnalyzeScreenshot));
        extraButtons.Children.Add(ButtonV42(
            "Verify screenshot against selected Excel range",()=>Run(LinkScreenshotToExcel)));
        extraButtons.Children.Add(ButtonV42(
            "Copy range to Excel-only shelf",DeskSpaceExcelClipboardV39.CaptureNow));
        options.Children.Add(extraButtons);
        var imageBorder=new Border{BorderThickness=new Thickness(1),
            BorderBrush=BorderV42,Background=Brushes.White};
        var imageScroll=new ScrollViewer{
            Height=135,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto
        };
        drawing=new Canvas{Width=250,Height=150,Background=Brushes.WhiteSmoke};
        picture=new Image{Stretch=Stretch.Fill};
        drawing.Children.Add(picture);
        mark=new System.Windows.Shapes.Rectangle{
            Visibility=Visibility.Collapsed,
            Stroke=new SolidColorBrush(Color.FromRgb(54,119,230)),
            StrokeThickness=2,
            Fill=new SolidColorBrush(Color.FromArgb(48,73,155,241)),
            IsHitTestVisible=false
        };
        drawing.Children.Add(mark);
        drawing.MouseLeftButtonDown+=DrawStart;
        drawing.MouseMove+=DrawMove;
        drawing.MouseLeftButtonUp+=DrawEnd;
        imageScroll.Content=drawing;imageBorder.Child=imageScroll;
        options.Children.Add(imageBorder);
        selectedCells=new ListBox{Height=110,MinHeight=80};
        options.Children.Add(selectedCells);
        cellSelectionInfo=HintV42("Capture or analyze a range to inspect individual source cells.");
        options.Children.Add(cellSelectionInfo);
        options.Children.Add(ButtonV42("Save checked values",()=>Run(SaveCheckedCells)));
        manual=new CheckBox{Content="Paste starting at the selected Excel cell",
            IsChecked=false};
        overwrite=new CheckBox{Content="Replace existing non-formula values",
            IsChecked=false};
        manual.Checked+=(_,_)=>latest=null;
        manual.Unchecked+=(_,_)=>latest=null;
        overwrite.Checked+=(_,_)=>latest=null;
        overwrite.Unchecked+=(_,_)=>latest=null;
        options.Children.Add(manual);options.Children.Add(overwrite);
        advanced.Content=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            MaxHeight=260,Content=options};
        DockPanel.SetDock(advanced,Dock.Bottom);
        root.Children.Add(advanced);
        root.Children.Add(content);

        root.IsVisibleChanged+=(_,e)=>{
            if(e.NewValue is true && !editorBusyV42 && !hasUnsavedChangesV42) {
                try {ReloadSavedV42(false);} catch(Exception ex) {
                    status.Text="Could not refresh encrypted mappings: "+ex.Message;
                }
            }
        };
        ReloadSavedV42(false);
        return root;
    }
    private void AddColumnV42(string title,string property,int width)
    {
        bool addressOrType=property is "ReferenceCell" or "ValueCell" or "Kind";
        mappingGridV42!.Columns.Add(new DataGridTextColumn{
            Header=title,Width=new DataGridLength(width),
            IsReadOnly=addressOrType,
            Binding=new Binding(property){
                Mode=BindingMode.TwoWay,
                UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},
            ElementStyle=new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis)}
            }
        });
    }
    private void FilterLibraryV42()
    {
        if(templates==null || libraryInfoV42==null)return;
        var term=librarySearchV42?.Text.Trim()??"";
        var display=saved
            .Where(x=>term.Length==0 ||
                x.Name.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                x.SourceSheet.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                x.GroupPath.Contains(term,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
            .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        selectionGuardV42=true;
        try
        {
            var current=editingIdV42;
            templates.ItemsSource=display;
            templates.SelectedItem=display.FirstOrDefault(x=>x.Id==current);
        }
        finally {selectionGuardV42=false;}
        libraryInfoV42.Text=saved.Count+" saved data entries · "+display.Length+
            " shown (collection / data name)";
    }
    private void ReloadSavedV42(bool explicitReload)
    {
        if(templates==null)return;
        if(hasUnsavedChangesV42 && editingTemplateV42!=null)
        {
            status.Text="You have unsaved mapping changes. Save them before refreshing.";
            return;
        }
        var existing=editingIdV42;
        // The two frontends are separate Windows processes. Load from DPAPI
        // again, never rely on the list captured at constructor time.
        var disk=DeskSpaceExcelStoreV16.Load();
        saved.Clear();
        saved.AddRange(disk);
        FilterLibraryV42();
        var selection=saved.FirstOrDefault(x=>x.Id==existing);
        if(selection!=null)
        {
            selectionGuardV42=true;
            templates.SelectedItem=selection;
            selectionGuardV42=false;
            LoadEditorV42(selection);
        }
        else if(explicitReload)
        {
            mappingRowsV42.Clear();
            mappingGridV42!.ItemsSource=null;
            editingTemplateV42=null;editingIdV42=Guid.Empty;
            editorInfoV42!.Text="Choose a saved mapping, or capture an Excel range.";
        }
        if(explicitReload)status.Text="Saved mappings refreshed from Windows encrypted storage.";
    }
    private void OnLibrarySelectedV42()
    {
        if(selectionGuardV42)return;
        if(templates.SelectedItem is not DeskSpaceExcelTemplateV16 entry)return;
        if(hasUnsavedChangesV42 && editingTemplateV42!=null &&
           entry.Id!=editingIdV42)
        {
            selectionGuardV42=true;
            try {
                templates.SelectedItem=saved.FirstOrDefault(x=>x.Id==editingIdV42);
            }
            finally {selectionGuardV42=false;}
            status.Text="Please save your current header/value changes before changing formats.";
            return;
        }
        LoadEditorV42(entry);
    }
    private void DeleteMappingV42()
    {
        if(editingTemplateV42==null)
            throw new InvalidOperationException("Select a saved mapping to delete.");
        if(editingDraftV42)
        {
            editingTemplateV42=null;
            editingIdV42=Guid.Empty;
            hasUnsavedChangesV42=false;
            mappingGridV42!.ItemsSource=null;
            mappingRowsV42.Clear();
            if(mappingNameV42!=null)mappingNameV42.Clear();
            status.Text="Unsaved capture discarded. Previously saved mappings are unchanged.";
            return;
        }
        if(hasUnsavedChangesV42)
        {
            status.Text="Save the current changes before deleting this mapping, "+
                "or reopen the saved format to discard them.";
            return;
        }
        // Delete by the saved immutable ID, not through the legacy
        // ListBox.Items.Clear() refresher. The modern library binds ItemsSource,
        // which throws if a legacy refresh tries to mutate Items directly.
        var nameToDelete=editingTemplateV42.Name;
        if(MessageBox.Show("Delete saved mapping '"+nameToDelete+"'?"+
            Environment.NewLine+"This will not change any Excel workbook.",
            "DeskSpace Excel",MessageBoxButton.YesNo,
            MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        var disk=DeskSpaceExcelStoreV16.Load();
        if(disk.RemoveAll(x=>x.Id==editingIdV42)!=1)
            throw new InvalidOperationException(
                "The selected mapping changed or was already deleted. "+
                "Refresh saved mappings.");
        DeskSpaceExcelStoreV16.Save(disk);
        editingTemplateV42=null;editingIdV42=Guid.Empty;
        hasUnsavedChangesV42=false;
        mappingRowsV42.Clear();
        mappingGridV42!.ItemsSource=null;
        saved.Clear();saved.AddRange(disk);
        FilterLibraryV42();
        status.Text="Deleted mapping '"+nameToDelete+
            "'. Original Excel workbooks and all other mappings are unchanged.";
    }
    private static DeskSpaceExcelTemplateV16 CloneV42(DeskSpaceExcelTemplateV16 entry)=>
        JsonSerializer.Deserialize<DeskSpaceExcelTemplateV16>(
            JsonSerializer.Serialize(entry))
        ??throw new InvalidOperationException("Cannot create editable copy of Excel mapping.");
    private void LoadEditorV42(DeskSpaceExcelTemplateV16 entry)
    {
        editingTemplateV42=CloneV42(entry);
        editingIdV42=entry.Id;editingDraftV42=false;
        loadingEditorV42=true;
        try {
            RenderMappingRowsV42(editingTemplateV42);
            if(mappingNameV42!=null)mappingNameV42.Text=entry.Name;
            if(mappingGroupV42!=null)mappingGroupV42.Text=entry.GroupPath;
        }
        finally {loadingEditorV42=false;}
        hasUnsavedChangesV42=false;
        if(editorInfoV42!=null)editorInfoV42.Text=
            entry.SourceWorkbook+" · "+entry.SourceSheet+" · "+
            entry.SourceAddress+" · "+entry.Cells.Count+" cells"+
            (entry.CoordinatesVerified?" · Verified addresses":" · Image positions only");
        latest=null;
        if(matchSummary!=null)matchSummary.Text="Review and save your edits. Preview before pasting into Excel.";
        if(previewRows!=null)previewRows.ItemsSource=null;
    }
    private void ShowCapturedV42(DeskSpaceExcelTemplateV16 candidate)
    {
        editingTemplateV42=candidate;
        editingIdV42=Guid.Empty;editingDraftV42=true;
        hasUnsavedChangesV42=true;
        loadingEditorV42=true;
        try {
            // Preserve the user's collection instead of silently dropping it
            // every time a new selection is captured.
            var requestedName=mappingNameV42?.Text.Trim()??"";
            if(requestedName.Length>0)candidate.Name=requestedName;
            mappingNameV42!.Text=candidate.Name;
        } finally {loadingEditorV42=false;}
        RenderMappingRowsV42(candidate);
        editorInfoV42!.Text="New capture · "+candidate.SourceSheet+" · "+
            candidate.SourceAddress+" · "+candidate.Cells.Count+
            " cells. Check each header and value, then Save mapping changes.";
        latest=null;
    }
    private void RenderMappingRowsV42(DeskSpaceExcelTemplateV16 entry)
    {
        mappingRowsV42.Clear();
        var references=entry.Cells.Where(c=>c.Role=="Reference").ToArray();
        var sources=entry.Cells
            .Where(c=>c.Role!="Reference" && c.Role!="Ignore").ToArray();
        foreach(var cell in sources)
        {
            var best=references
                .Where(c=>
                   (c.RowOffset==cell.RowOffset && c.ColumnOffset<cell.ColumnOffset) ||
                   (c.ColumnOffset==cell.ColumnOffset && c.RowOffset<cell.RowOffset))
                .OrderBy(c=>Math.Abs(c.RowOffset-cell.RowOffset)+
                    Math.Abs(c.ColumnOffset-cell.ColumnOffset)).FirstOrDefault();
            var bestAnchor=entry.Anchors
                .Where(a=>
                    (a.RowOffset==cell.RowOffset && a.ColumnOffset<cell.ColumnOffset) ||
                    (a.ColumnOffset==cell.ColumnOffset && a.RowOffset<cell.RowOffset))
                .OrderBy(a=>Math.Abs(a.RowOffset-cell.RowOffset)+
                    Math.Abs(a.ColumnOffset-cell.ColumnOffset)).FirstOrDefault();
            int refRow=best?.RowOffset??bestAnchor?.RowOffset??-1;
            int refCol=best?.ColumnOffset??bestAnchor?.ColumnOffset??-1;
            string refAddress=cell.HeaderCellAddress;
            if(string.IsNullOrWhiteSpace(refAddress) && refRow>=0 && entry.CoordinatesVerified)
                refAddress=CellReference(entry.SourceRow+refRow,entry.SourceColumn+refCol);
            string header=best?.RawValue??bestAnchor?.Label??"";
            if(!string.IsNullOrWhiteSpace(cell.HeaderCellAddress))
            {
                var specific=entry.Cells.FirstOrDefault(c=>
                    string.Equals(c.CellAddress,cell.HeaderCellAddress,StringComparison.OrdinalIgnoreCase));
                if(specific!=null){header=specific.RawValue;refRow=specific.RowOffset;refCol=specific.ColumnOffset;}
            }
            mappingRowsV42.Add(new MappingRowV42{
                Cell=cell,OriginalRaw=cell.RawValue,OriginalLabel=header,
                HeaderRow=refRow,HeaderCol=refCol,
                Header=header,ReferenceCell=refAddress,
                ValueCell=cell.CellAddress.Length>0?cell.CellAddress:
                    entry.CoordinatesVerified?CellReference(
                        entry.SourceRow+cell.RowOffset,entry.SourceColumn+cell.ColumnOffset):"Unverified",
                Value=cell.ValueKind=="formula"?cell.Display:cell.RawValue,
                Kind=cell.Role,CellType=cell.ValueKind,Formula=cell.FormulaR1C1,
                Include=true
            });
        }
        // Header-only ranges are not discarded; they are visible for
        // correction and retain their classification after saving.
        foreach(var header in references.Where(r=>
            !mappingRowsV42.Any(x=>x.HeaderRow==r.RowOffset && x.HeaderCol==r.ColumnOffset)))
        {
            mappingRowsV42.Add(new MappingRowV42{
                Cell=header,OriginalRaw=header.RawValue,
                HeaderRow=header.RowOffset,HeaderCol=header.ColumnOffset,
                Header=header.RawValue,OriginalLabel=header.RawValue,
                ReferenceCell=header.CellAddress,ValueCell="",
                Value="",Kind="Reference",CellType=header.ValueKind
            });
        }
        mappingGridV42!.ItemsSource=null;
        mappingGridV42.ItemsSource=mappingRowsV42.ToArray();
        if(editorInfoV42!=null)editorInfoV42.Text=
            (entry.CoordinatesVerified?"Verified Excel addresses":"Screenshot positions need Excel verification")+
            " · "+mappingRowsV42.Count+" visible header/value relationships";
    }
    private static void CommitCellEditsV42(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell,true);
        grid.CommitEdit(DataGridEditingUnit.Row,true);
    }
    private void SaveEditorV42()
    {
        if(editorBusyV42)return;
        if(editingTemplateV42==null)
            throw new InvalidOperationException("Select a saved mapping or capture Excel cells first.");
        editorBusyV42=true;
        try
        {
            CommitCellEditsV42(mappingGridV42!);
            var edited=CloneV42(editingTemplateV42);
            var title=mappingNameV42?.Text.Trim()??"";
            if(title.Length==0)throw new InvalidOperationException(
                "Enter a Data name above the table before saving.");
            if(title.Length>140)throw new InvalidOperationException("Use a shorter mapping name.");
            edited.Name=title;
            // Group segments are labels, not paths on the Windows filesystem.
            var parts=(mappingGroupV42?.Text??"").Replace('\\','/')
                .Split('/',StringSplitOptions.RemoveEmptyEntries|
                           StringSplitOptions.TrimEntries);
            if(parts.Length>8 || parts.Any(p=>p.Length>60))
                throw new InvalidOperationException(
                    "Information grouping supports up to eight names of 60 characters.");
            edited.GroupPath=string.Join("/",parts);
            var addedHeaders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in mappingRowsV42)
            {
                if(row.Cell==null)continue;
                var cell=edited.Cells.FirstOrDefault(c=>
                    c.RowOffset==row.Cell.RowOffset&&c.ColumnOffset==row.Cell.ColumnOffset);
                if(cell==null)continue;
                if(!row.Include){cell.Role="Ignore";continue;}
                cell.Role=row.Kind=="Reference"?"Reference":
                    row.Kind=="Formula"?"Formula":"Value";
                if(row.Kind=="Reference")
                {
                    cell.RawValue=row.Header;
                    cell.Display=row.Header;
                    continue;
                }
                // A screenshot's guessed cell address is never accepted as
                // authority: only workbook-verified destination positions
                // can be used by the existing Paste Special safety gate.
                if(edited.CoordinatesVerified &&
                    !string.IsNullOrWhiteSpace(row.ValueCell) &&
                    !string.Equals(row.ValueCell,cell.CellAddress,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Destination "+row.ValueCell+" differs from its verified "+
                        "Excel address "+cell.CellAddress+
                        ". Capture the correct range again to move a cell.");
                }
                if(cell.ValueKind!="formula" && row.Value!=row.OriginalRaw)
                {
                    cell.RawValue=row.Value;
                    cell.Display=row.Value;
                    if(cell.ValueKind=="blank")
                    {
                        cell.ValueKind=double.TryParse(row.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,out _)
                            ? "number":"text";
                    }
                    else if(cell.ValueKind=="number" &&
                        !double.TryParse(row.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,out _))
                        cell.ValueKind="text";
                }
                cell.HeaderCellAddress=row.ReferenceCell;
                if(!string.IsNullOrWhiteSpace(row.Header))
                {
                    var refCell=edited.Cells.FirstOrDefault(c=>
                        string.Equals(c.CellAddress,row.ReferenceCell,
                            StringComparison.OrdinalIgnoreCase) &&
                        c.Role=="Reference");
                    if(refCell!=null)
                    {
                        refCell.RawValue=row.Header;
                        refCell.Display=row.Header;
                    }
                    else if(row.HeaderRow>=0 && row.HeaderCol>=0)
                    {
                        var anchor=edited.Anchors.FirstOrDefault(a=>
                            a.RowOffset==row.HeaderRow &&
                            a.ColumnOffset==row.HeaderCol);
                        if(anchor!=null)anchor.Label=row.Header;
                        else if(addedHeaders.Add(row.HeaderRow+":"+row.HeaderCol))
                            edited.Anchors.Add(new DeskSpaceExcelAnchorV16{
                                RowOffset=row.HeaderRow,ColumnOffset=row.HeaderCol,
                                Label=row.Header
                            });
                    }
                }
            }
            if(edited.Cells.Count==0)
                throw new InvalidOperationException(
                    "Capture an Excel selection to add data fields before saving.");
            // Reload before updating so a second DeskSpace window cannot
            // overwrite mappings saved elsewhere since this view opened.
            var disk=DeskSpaceExcelStoreV16.Load();
            var existing=disk.FindIndex(x=>x.Id==edited.Id);
            if(existing>=0)disk[existing]=edited;
            else disk.Add(edited);
            DeskSpaceExcelStoreV16.Save(disk);
            saved.Clear();saved.AddRange(disk);
            pendingSelection=null;
            editingDraftV42=false;editingIdV42=edited.Id;
            hasUnsavedChangesV42=false;
            FilterLibraryV42();
            selectionGuardV42=true;
            try {templates.SelectedItem=saved.FirstOrDefault(x=>x.Id==edited.Id);}
            finally {selectionGuardV42=false;}
            LoadEditorV42(edited);
            status.Text="Saved '"+edited.Name+"' with "+edited.Cells.Count+
                " cell records. Original Excel workbook unchanged.";
        }
        finally{editorBusyV42=false;}
    }
}
