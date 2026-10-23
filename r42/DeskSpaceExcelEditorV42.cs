using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
        internal string OriginalShown="";
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
        public string ExcelFormat {get;set;}="";
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
        // Offline mode must preserve saved mapping viewing and editing.
        // Only actions that require a LIVE Excel process are disabled.
        var excelActions=new List<Button>();
        Button RequiresExcel(string text,Action action,bool primary=false)
        {
            var button=ButtonV42(text,()=>{
                if(!DeskSpaceExcelWheelV42.ExcelIsOpen())
                {
                    if(status!=null)status.Text=
                        "Excel is not open. Saved collections can still be viewed "+
                        "and edited, but live Excel capture and paste are unavailable.";
                    return;
                }
                action();
            },primary);
            excelActions.Add(button);
            return button;
        }
        var excelOfflineMessage=HintV42(
            "Excel is not open. Saved mappings remain available for viewing; "+
            "Excel capture, selection and paste are disabled.",11);
        var excelAvailabilityTimer=new DispatcherTimer{
            Interval=TimeSpan.FromSeconds(1)
        };
        void RefreshExcelActions()
        {
            bool online=DeskSpaceExcelWheelV42.ExcelIsOpen();
            foreach(var button in excelActions)button.IsEnabled=online;
            excelOfflineMessage.Visibility=online?Visibility.Collapsed:Visibility.Visible;
        }
        excelAvailabilityTimer.Tick+=(_,_)=>RefreshExcelActions();
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
        mainActions.Children.Add(RequiresExcel(
            "Copy and analyze selected Excel cells",()=>Run(Capture),true));
        mainActions.Children.Add(ButtonV42(
            "Screenshot",()=>OpenScreenshot()));
        mainActions.Children.Add(ButtonV42("Refresh saved mappings",()=>Run(()=>ReloadSavedV42(true))));
        head.Children.Add(mainActions);
        head.Children.Add(excelOfflineMessage);
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
        editButtons.Children.Add(RequiresExcel(
            "Capture / add fields",()=>Run(Capture),true));
        editButtons.Children.Add(ButtonV42("Save collection & data",
            ()=>Run(SaveEditorV42),true));
        editButtons.Children.Add(ButtonV42("New mapping",()=>{
            editingTemplateV42=null;editingIdV42=Guid.Empty;
            editingDraftV42=true;hasUnsavedChangesV42=false;
            mappingRowsV42.Clear();mappingGridV42!.ItemsSource=null;
            mappingNameV42?.Clear();mappingGroupV42?.Clear();
            if(status!=null)status.Text=
                "New mapping. Capture selected Excel cells to create it.";
        }));
        editButtons.Children.Add(ButtonV42("Delete selected data",
            ()=>Run(DeleteMappingV42)));
        editorHead.Children.Add(editButtons);
        DockPanel.SetDock(editorHead,Dock.Top);right.Children.Add(editorHead);

        var footer=new StackPanel{Margin=new Thickness(0,2,0,0)};
        // Excel's custom number-format codes remain editable verbatim.
        // Presets are conveniences, not a closed set of supported formats.
        var formatBar=new WrapPanel{Margin=new Thickness(0,0,0,3)};
        formatBar.Children.Add(HintV42("Format selected cell:",10));
        var preset=new ComboBox{Width=250,Height=29,IsEditable=true,
            ItemsSource=new[]{
                "General","dd-mm-yyyy","dd/mm/yyyy","yyyy-mm-dd",
                "dd-mmm-yyyy","mmm d, yyyy","dddd, dd mmmm yyyy",
                "mm/dd/yyyy","dd-mm-yyyy hh:mm","yyyy-mm-dd hh:mm:ss",
                "hh:mm","hh:mm:ss","h:mm AM/PM","0","0.00",
                "#,##0","#,##0.00","0%","0.00%","₹#,##0.00",
                "$#,##0.00","@"}};
        formatBar.Children.Add(preset);
        formatBar.Children.Add(ButtonV42("Apply format",()=>{
            CommitCellEditsV42(mappingGridV42!);
            if(mappingGridV42?.SelectedItem is MappingRowV42 row)
            {
                var code=preset.Text.Trim();
                if(code.Length==0||code.Length>160)
                    throw new InvalidOperationException(
                        "Enter an Excel format code (maximum 160 characters).");
                row.ExcelFormat=code;hasUnsavedChangesV42=true;
                mappingGridV42.Items.Refresh();
            }
            else if(status!=null)status.Text="Select a mapped cell first.";
        }));
        footer.Children.Add(formatBar);
        var pasteButtons=new WrapPanel();
        pasteButtons.Children.Add(RequiresExcel("Preview matching cells",()=>Run(Preview)));
        pasteButtons.Children.Add(RequiresExcel("Apply autofill",()=>Run(Apply),true));
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
        AddColumnV42("Excel format","ExcelFormat",160);
        mappingGridV42.Columns.Add(new DataGridComboBoxColumn{
            Header="Cell action / shortcut",
            ItemsSource=DeskSpaceExcelActionsV46.EditorKinds,
            SelectedItemBinding=new Binding("Kind"){
                Mode=BindingMode.TwoWay,
                UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},
            Width=new DataGridLength(225)
        });
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
        // Searchable Windows Excel keyboard reference. Only safe single-cell
        // date/time shortcuts may be selected for automatic Excel mappings.
        // All other listed shortcuts remain reference-only, never injected.
        var shortcutPane=new Expander{
            Header="Excel keyboard shortcuts / mapping actions",
            IsExpanded=false,Margin=new Thickness(7,1,7,2)
        };
        var shortcutArea=new StackPanel{Margin=new Thickness(6)};
        shortcutArea.Children.Add(HintV42(
            "Search Windows Excel shortcuts. Select a saved cell row, then "+
            "double-click Ctrl+; or Ctrl+Shift+; to map its paste-time action. "+
            "Other shortcuts are reference-only for safety.",11));
        var shortcutFilter=Field("Search shortcuts (date, Ctrl+B, filter...)");
        shortcutArea.Children.Add(shortcutFilter);
        var shortcutList=new ListBox{Height=130,FontSize=11};
        shortcutArea.Children.Add(shortcutList);
        void PopulateShortcutList()
        {
            var q=shortcutFilter.Text.Trim();
            shortcutList.ItemsSource=DeskSpaceExcelActionsV46.Shortcuts
                .Where(x=>q.Length==0 ||
                    x.Keys.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    x.Operation.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    x.Category.Contains(q,StringComparison.OrdinalIgnoreCase))
                .Select(x=>x.Keys+" — "+x.Operation+" ["+x.Category+"]")
                .ToArray();
        }
        shortcutFilter.TextChanged+=(_,_)=>PopulateShortcutList();
        shortcutList.MouseDoubleClick+=(_,_)=>{
            string selected=shortcutList.SelectedItem as string??"";
            if(selected.Length==0)return;
            string key=selected.Split(" — ",StringSplitOptions.None)[0];
            try
            {
                DeskSpaceExcelActionsV46.FromShortcut(key);
                if(mappingGridV42?.SelectedItem is MappingRowV42 row)
                {
                    row.Kind=DeskSpaceExcelActionsV46.EditorKinds[8];
                    row.Value=key;
                    mappingGridV42.Items.Refresh();
                    hasUnsavedChangesV42=true;
                    if(status!=null)status.Text=
                        "Shortcut "+key+" mapped. Save collection & data. "+
                        "Its current value will be generated on each paste.";
                }
                else if(status!=null)
                    status.Text="Select a data row in the table first.";
            }
            catch(InvalidOperationException ex)
            {
                if(status!=null)status.Text=ex.Message;
            }
        };
        shortcutArea.Children.Add(ButtonV42(
            "Full Microsoft Excel Windows shortcut documentation",()=>{
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://support.microsoft.com/en-us/accessibility/excel/keyboard-shortcuts-in-excel"
                ){UseShellExecute=true});
            }));
        shortcutPane.Content=shortcutArea;
        PopulateShortcutList();
        DockPanel.SetDock(shortcutPane,Dock.Bottom);
        root.Children.Add(shortcutPane);
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
        extraButtons.Children.Add(RequiresExcel("Open workbook…",()=>Run(OpenWorkbook)));
        extraButtons.Children.Add(ButtonV42("Analyze marked image region",AnalyzeScreenshot));
        extraButtons.Children.Add(RequiresExcel(
            "Verify screenshot against selected Excel range",()=>Run(LinkScreenshotToExcel)));
        extraButtons.Children.Add(RequiresExcel(
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
            if(e.NewValue is true)
            {
                RefreshExcelActions();
                excelAvailabilityTimer.Start();
            }
            else excelAvailabilityTimer.Stop();
            if(e.NewValue is true && !editorBusyV42 && !hasUnsavedChangesV42) {
                try {ReloadSavedV42(false);} catch(Exception ex) {
                    status.Text="Could not refresh encrypted mappings: "+ex.Message;
                }
            }
        };
        ReloadSavedV42(false);
        RefreshExcelActions();
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
        if(editingTemplateV42!=null && editingIdV42!=Guid.Empty &&
           !editingDraftV42)
        {
            if(hasUnsavedChangesV42)
                throw new InvalidOperationException(
                    "Save the current mapping edits before adding captured fields.");
            var existing=CloneV42(editingTemplateV42);
            if(!existing.CoordinatesVerified || !candidate.CoordinatesVerified ||
               !string.Equals(existing.SourceWorkbook,candidate.SourceWorkbook,
                    StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(existing.SourceSheet,candidate.SourceSheet,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Capture additional fields from the SAME verified workbook "+
                    "and worksheet, or choose New mapping.");
            int rowBase=Math.Min(existing.SourceRow,candidate.SourceRow);
            int colBase=Math.Min(existing.SourceColumn,candidate.SourceColumn);
            int shiftR=existing.SourceRow-rowBase;
            int shiftC=existing.SourceColumn-colBase;
            foreach(var c in existing.Cells)
            {c.RowOffset+=shiftR;c.ColumnOffset+=shiftC;}
            foreach(var a in existing.Anchors)
            {a.RowOffset+=shiftR;a.ColumnOffset+=shiftC;}
            foreach(var c in candidate.Cells)
            {
                var clone=JsonSerializer.Deserialize<DeskSpaceExcelCellV16>(
                    JsonSerializer.Serialize(c))
                    ??throw new InvalidOperationException("Invalid captured cell");
                clone.RowOffset+=candidate.SourceRow-rowBase;
                clone.ColumnOffset+=candidate.SourceColumn-colBase;
                if(!existing.Cells.Any(x=>x.RowOffset==clone.RowOffset &&
                                         x.ColumnOffset==clone.ColumnOffset))
                    existing.Cells.Add(clone);
            }
            foreach(var a in candidate.Anchors)
            {
                int row=a.RowOffset+candidate.SourceRow-rowBase;
                int col=a.ColumnOffset+candidate.SourceColumn-colBase;
                if(!existing.Anchors.Any(x=>x.RowOffset==row&&x.ColumnOffset==col))
                    existing.Anchors.Add(new DeskSpaceExcelAnchorV16{
                        RowOffset=row,ColumnOffset=col,Label=a.Label
                    });
            }
            existing.RowCount=Math.Max(existing.RowCount+shiftR,
                candidate.SourceRow+candidate.RowCount-rowBase);
            existing.ColumnCount=Math.Max(existing.ColumnCount+shiftC,
                candidate.SourceColumn+candidate.ColumnCount-colBase);
            existing.SourceRow=rowBase;existing.SourceColumn=colBase;
            editingTemplateV42=existing;
            hasUnsavedChangesV42=true;
            RenderMappingRowsV42(existing);
            editorInfoV42!.Text="Added fields to saved mapping "+
                editingIdV42+". Save will UPDATE this ID, not create another.";
            latest=null;
            return;
        }
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
    // Actual Excel dates are numeric serials. Format only their
    // DISPLAY; never convert an unchanged date to text or a new serial.
    private static bool DateFormattedV50(string code)
    {
        if(string.IsNullOrWhiteSpace(code))return false;
        // Ignore quoted text and escaped literal characters in formats.
        var bare=System.Text.RegularExpressions.Regex.Replace(code,
            "\"[^\"]*\"", "");
        return System.Text.RegularExpressions.Regex.IsMatch(bare,
            @"(?i)(d+|y+|h+|s+|m+[/\\-]d+|d+[/\\-]m+)");
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
            var shown=DeskSpaceExcelActionsV46.IsAction(cell.ValueKind)
                ? DeskSpaceExcelActionsV46.PreviewText(cell,DateTime.Now)
                : cell.ValueKind=="formula"?cell.FormulaR1C1
                : cell.ValueKind=="number" &&
                    DateFormattedV50(cell.NumberFormat) &&
                    !string.IsNullOrWhiteSpace(cell.Display) &&
                    !cell.Display.Contains("###",StringComparison.Ordinal)
                    ?cell.Display:cell.RawValue;
            mappingRowsV42.Add(new MappingRowV42{
                Cell=cell,OriginalRaw=cell.RawValue,
                OriginalShown=shown,ExcelFormat=cell.NumberFormat,
                OriginalLabel=header,
                HeaderRow=refRow,HeaderCol=refCol,
                Header=header,ReferenceCell=refAddress,
                ValueCell=cell.CellAddress.Length>0?cell.CellAddress:
                    entry.CoordinatesVerified?CellReference(
                        entry.SourceRow+cell.RowOffset,entry.SourceColumn+cell.ColumnOffset):"Unverified",
                Value=shown,
                Kind=DeskSpaceExcelActionsV46.Label(cell),
                CellType=cell.ValueKind,Formula=cell.FormulaR1C1,
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
                Value="",Kind="Reference",CellType=header.ValueKind,
                ExcelFormat=header.NumberFormat
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
                // Keep Excel's original custom format unless explicitly
                // changed in the editor. Excel applies this to the target.
                if(row.ExcelFormat.Length>0)
                {
                    if(row.ExcelFormat.Length>160)
                        throw new InvalidOperationException("Excel format exceeds 160 characters");
                    cell.NumberFormat=row.ExcelFormat;
                }
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
                string requestedAction=DeskSpaceExcelActionsV46.KindFromEditor(
                    row.Kind,row.Value??"");
                if(requestedAction.Length>0)
                {
                    // These are symbolic commands, not saved date/time values.
                    // Their actual value is computed at APPLY time in Excel.
                    cell.Role="Value";
                    bool reuseCapturedR1C1=
                        (cell.ValueKind=="formula" ||
                         cell.ValueKind==DeskSpaceExcelActionsV46.CapturedFormula) &&
                        string.Equals(row.Value.Trim(),row.Formula,
                            StringComparison.Ordinal);
                    cell.ValueKind=
                        requestedAction==DeskSpaceExcelActionsV46.CustomFormula &&
                        reuseCapturedR1C1
                            ?DeskSpaceExcelActionsV46.CapturedFormula:requestedAction;
                    if(requestedAction==DeskSpaceExcelActionsV46.CustomFormula)
                    {
                        if(!row.Value.TrimStart().StartsWith("=",StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "A custom Excel formula must begin with =.");
                        cell.FormulaR1C1=row.Value.Trim();
                    }
                    cell.RawValue="";
                    cell.Display=DeskSpaceExcelActionsV46.PreviewText(
                        cell,DateTime.Now);
                    if(string.IsNullOrWhiteSpace(row.ExcelFormat))
                        cell.NumberFormat=DeskSpaceExcelActionsV46.NumberFormat(
                            requestedAction);
                }
                else if(!DeskSpaceExcelActionsV46.IsAction(cell.ValueKind) &&
                    cell.ValueKind!="formula" && row.Value!=row.OriginalShown)
                {
                    if(cell.ValueKind=="number" &&
                        DateFormattedV50(cell.NumberFormat) &&
                        DateTime.TryParse(row.Value,
                            System.Globalization.CultureInfo.CurrentCulture,
                            System.Globalization.DateTimeStyles.None,out var date))
                    {
                        cell.RawValue=date.ToOADate().ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                        cell.Display=row.Value;
                    }
                    else
                    {
                        cell.RawValue=row.Value;
                        cell.Display=row.Value;
                    }
                    if(cell.ValueKind=="blank")
                    {
                        cell.ValueKind=double.TryParse(row.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,out _)
                            ? "number":"text";
                    }
                    else if(cell.ValueKind=="number" &&
                        !DateFormattedV50(cell.NumberFormat) &&
                        !double.TryParse(row.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,out _))
                        cell.ValueKind="text";
                }
                else if(DeskSpaceExcelActionsV46.IsAction(cell.ValueKind) &&
                    row.Kind=="Value")
                {
                    // Switching back to ordinary input is explicit.
                    cell.ValueKind="text";
                    cell.RawValue=row.Value??"";
                    cell.Display=cell.RawValue;
                    cell.FormulaR1C1="";
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
            if(!editingDraftV42)
            {
                if(editingIdV42==Guid.Empty)
                    throw new InvalidOperationException(
                        "Selected mapping has no saved ID. Reload the entry.");
                edited.Id=editingIdV42;
                var index=disk.FindIndex(x=>x.Id==editingIdV42);
                if(index<0)throw new InvalidOperationException(
                    "The original mapping ID no longer exists. "+
                    "Refresh instead of creating a duplicate.");
                disk[index]=edited;
            }
            else
            {
                if(disk.Any(x=>x.Id==edited.Id))
                    throw new InvalidOperationException(
                        "New mapping ID already belongs to another saved entry.");
                if(disk.Any(x=>x.Name.Equals(edited.Name,
                    StringComparison.OrdinalIgnoreCase) &&
                    x.GroupPath.Equals(edited.GroupPath,
                    StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        "This collection and data name already exists. "+
                        "Select its saved mapping to update it.");
                disk.Add(edited);
            }
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
