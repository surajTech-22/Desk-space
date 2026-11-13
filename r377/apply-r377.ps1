$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R377:
#  1) Make Deck Excel Paste truly functional (Paste latest / Capture selected / Open mappings)
#     and fix [Paste to Excel] on Excel clipboard items via explicit clipboard COM preview/apply
#     (leaving R125 strict Preview(entry,true,false) exception intact).
#  2) Add hover + eye-button spreadsheet table preview on Excel clipboard items via DeskSpaceDeckPreviewV99
#     plus inline copied-values snippet, and wake/open Deck properly for SharedClips.
#  3) Make Multi-Monitor fullscreen/video auto-hide and edge unhide strictly per-monitor aware
#     (EnumWindows per bound Screen + DWM bounds + taskbar-less maximized filter + DPI-safe edge tab).

$lf=[string][char]10
$crlf=([string][char]13)+$lf

function Begin-Patch([string]$path){
    if(!(Test-Path -LiteralPath $path)){throw ('R377 missing source: '+$path)}
    $script:code=[regex]::Replace([IO.File]::ReadAllText($path),'\r+\n?',$lf)
}
function Swap-R377([string]$old,[string]$replacement,[string]$context){
    $old=[regex]::Replace($old,'\r+\n?',$lf)
    $replacement=[regex]::Replace($replacement,'\r+\n?',$lf)
    $count=([regex]::Matches($script:code,[regex]::Escape($old))).Count
    if($count -ne 1){
        throw ('R377 '+$context+': expected one anchor match, got '+$count)
    }
    $script:code=$script:code.Replace($old,$replacement)
}
function Finish-Patch([string]$path){
    [IO.File]::WriteAllText($path,$script:code,[Text.UTF8Encoding]::new($false))
}

# --- Part A: Sync DeskSpaceExcelClipboardV39.cs ---
$clipSrc='src/DeskSpace/DeskSpaceExcelClipboardV39.cs'
$clipDst='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelClipboardV39.cs'
if(!(Test-Path -LiteralPath $clipSrc) -or !(Test-Path -LiteralPath $clipDst)){
    throw 'R377 missing DeskSpaceExcelClipboardV39 source'
}
Copy-Item -LiteralPath $clipSrc -Destination $clipDst -Force
if((Get-FileHash $clipSrc -Algorithm SHA256).Hash -ne
   (Get-FileHash $clipDst -Algorithm SHA256).Hash){
    throw 'R377 DeskSpaceExcelClipboardV39 copy hash mismatch'
}

# --- Part B: Patch DeskSpaceExcelBridgeV16.cs (explicit clipboard preview/apply) ---
$bridgePath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs'
Begin-Patch $bridgePath
Swap-R377 @'
        // Don't auto-save. User may inspect the workbook and save when ready.
        return backup;
    }
}
'@ @'
        // Don't auto-save. User may inspect the workbook and save when ready.
        return backup;
    }

    public static DeskSpaceExcelProposalV16 PreviewClipboardValues(
        DeskSpaceExcelTemplateV16 entry,bool overwrite)
    {
        if(!entry.CoordinatesVerified || entry.Cells.Count==0)
            throw new InvalidOperationException(
                "No captured Excel cell values to paste.");
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        dynamic selected=app.ActiveWindow.RangeSelection;
        int r=(int)selected.Row,c=(int)selected.Column;
        bool sheetProtected=(bool)sheet.ProtectContents;
        var preview=new DeskSpaceExcelProposalV16 {
            StartRow=r,StartColumn=c,
            SheetName=Text(sheet.Name),
            WorkbookFullName=Text(app.ActiveWorkbook.FullName),
            Verified=true,
            Reason="Clipboard paste starting at active Excel cell "+Text(selected.Address)+".",
            UsedReferenceSignatureV120=false
        };
        foreach(var source in entry.Cells)
        {
            bool hasText=!string.IsNullOrEmpty(source.RawValue)||
                !string.IsNullOrWhiteSpace(source.Display);
            if(source.ValueKind=="blank" && !hasText)continue;
            int destRow=r+source.RowOffset,destCol=c+source.ColumnOffset;
            if(destRow<1||destCol<1)continue;
            dynamic cell=sheet.Cells[destRow,destCol];
            var old=(object?)cell.Value2;
            var oldText=Text(cell.Text);
            bool destinationFormula=Formula(cell);
            string decision;
            if(sheetProtected)decision="Sheet is protected";
            else if(destinationFormula)decision="Formula cell preserved";
            else if(old!=null && !overwrite)decision="Occupied (overwrite off)";
            else decision="Ready";
            if(decision=="Ready" && (bool)cell.MergeCells)
            {
                dynamic primary=cell.MergeArea.Cells[1,1];
                if((int)primary.Row!=destRow||(int)primary.Column!=destCol)
                    decision="Merged cell: not top-left";
            }
            var newText=!string.IsNullOrWhiteSpace(source.Display)
                ? source.Display : source.RawValue;
            preview.Changes.Add(new DeskSpaceExcelChangeV16 {
                Row=destRow,Column=destCol,
                CellAddress=Text(cell.Address),
                Previous=oldText,PreviousRaw=Text(old),
                NewValue=newText,
                Decision=decision,Source=source
            });
        }
        return preview;
    }

    private static object? ClipboardCellValueV377(DeskSpaceExcelCellV16 cell)
    {
        if(DeskSpaceExcelActionsV46.IsNativeShortcut(cell.ValueKind) ||
           DeskSpaceExcelActionsV46.IsAction(cell.ValueKind))
            return DeskSpaceExcelActionsV46.PreviewText(cell,DateTime.Now);
        if(cell.ValueKind=="formula")
        {
            if(!string.IsNullOrWhiteSpace(cell.RawValue) &&
               double.TryParse(cell.RawValue,NumberStyles.Any,
                   CultureInfo.InvariantCulture,out var numeric))
                return numeric;
            var text=!string.IsNullOrEmpty(cell.RawValue)?cell.RawValue:cell.Display;
            return text.StartsWith("=",StringComparison.Ordinal)?"'"+text:text;
        }
        return TypedValue(cell);
    }

    public static string ApplyClipboardValues(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)
    {
        if(!preview.Verified || !entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Clipboard preview is not ready to paste.");
        if(!preview.Changes.Any(c=>c.CanApply))
            throw new InvalidOperationException(
                "No writable destination cells at the active Excel selection.");
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        if(!string.Equals(Text(sheet.Name),preview.SheetName,StringComparison.Ordinal) ||
           !string.Equals(Text(app.ActiveWorkbook.FullName),preview.WorkbookFullName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The active Excel workbook or sheet changed. Please paste again.");
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        foreach(var change in preview.Changes.Where(x=>x.CanApply))
        {
            dynamic destination=sheet.Cells[change.Row,change.Column];
            if(Formula(destination) ||
               !string.Equals(Text((object?)destination.Value2),change.PreviousRaw,
                   StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Destination "+change.CellAddress+" changed before paste. Please paste again.");
        }
        var folder=Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelBackups");
        Directory.CreateDirectory(folder);
        var ext=Path.GetExtension(Text(app.ActiveWorkbook.Name));
        if(string.IsNullOrEmpty(ext))ext=".xlsx";
        var backup=Path.Combine(folder,"Excel_before_clipboard_"+
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+ext);
        app.ActiveWorkbook.SaveCopyAs(backup);
        if(!File.Exists(backup)||new FileInfo(backup).Length==0)
            throw new IOException("Excel backup was not created. Nothing was changed.");
        var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula)>();
        try
        {
            foreach(var change in preview.Changes.Where(x=>x.CanApply))
            {
                dynamic cell=sheet.Cells[change.Row,change.Column];
                var previous=(object?)cell.Value2;
                var hadFormula=Formula(cell);
                if(hadFormula)continue;
                if(previous!=null && !overwrite)continue;
                rollback.Add((cell,previous,hadFormula,""));
                cell.Value2=ClipboardCellValueV377(change.Source);
                if(!string.IsNullOrWhiteSpace(change.Source.NumberFormat) &&
                   change.Source.NumberFormat!="General")
                    try{cell.NumberFormat=change.Source.NumberFormat;}
                    catch(COMException){}
            }
        }
        catch
        {
            foreach(var x in rollback.AsEnumerable().Reverse())
                try{
                    if(x.HadFormula)x.Cell.FormulaR1C1=x.Formula;
                    else x.Cell.Value2=x.Old;
                }catch(COMException){}
            throw;
        }
        return backup;
    }
}
'@ 'clipboard value preview and apply'
Finish-Patch $bridgePath

# --- Part C: Patch DeskSpaceExcelWorkerV41.cs ---
$workerPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelWorkerV41.cs'
Begin-Patch $workerPath
Swap-R377 @'
    public static Task<DeskSpaceExcelProposalV16> PreviewAsync(
        DeskSpaceExcelTemplateV16 entry,bool selection,bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Preview(entry,selection,overwrite));
'@ @'
    public static Task<DeskSpaceExcelProposalV16> PreviewAsync(
        DeskSpaceExcelTemplateV16 entry,bool selection,bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Preview(entry,selection,overwrite));

    public static Task<DeskSpaceExcelProposalV16> PreviewClipboardAsync(
        DeskSpaceExcelTemplateV16 entry,bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.PreviewClipboardValues(entry,overwrite));

    public static Task<string> ApplyClipboardAsync(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.ApplyClipboardValues(preview,entry,overwrite));
'@ 'STA worker clipboard preview and apply'
Finish-Patch $workerPath

# --- Part D: Patch DeskSpaceDeckPreviewV99.cs (Excel hover + click table preview) ---
$previewPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckPreviewV99.cs'
Begin-Patch $previewPath
Swap-R377 @'
    private readonly DispatcherTimer hoverOpen=new(){
        Interval=TimeSpan.FromMilliseconds(650)
    };
'@ @'
    private readonly DispatcherTimer hoverOpen=new(){
        Interval=TimeSpan.FromMilliseconds(300)
    };
'@ 'snappy hover open interval'
Swap-R377 @'
    private string current="";
    private Border? pendingRow;
    private string pendingPath="";
'@ @'
    private string current="";
    private DeskSpaceExcelTemplateV16? currentClip;
    private Border? pendingRow;
    private string pendingPath="";
    private DeskSpaceExcelTemplateV16? pendingClip;
'@ 'excel clip hover tracking fields'
Swap-R377 @'
        hoverOpen.Tick+=(_,_)=>{
            hoverOpen.Stop();
            var row=pendingRow;
            if(row!=null && row.IsLoaded && row.IsMouseOver &&
               pendingMode?.Invoke()==true &&
               Stationary(pendingPosition,Mouse.GetPosition(row)))
                Show(pendingPath,row);
        };
'@ @'
        hoverOpen.Tick+=(_,_)=>{
            hoverOpen.Stop();
            var row=pendingRow;
            if(row!=null && row.IsLoaded && row.IsMouseOver &&
               pendingMode?.Invoke()==true &&
               Stationary(pendingPosition,Mouse.GetPosition(row)))
            {
                if(pendingClip!=null)ShowExcel(pendingClip,row);
                else if(pendingPath.Length>0)Show(pendingPath,row);
            }
        };
'@ 'dispatch hover open to Excel clip or file'
Swap-R377 @'
    internal void Close()
    {
        hoverOpen.Stop();
        pendingRow=null;
        pendingMode=null;
        manualPreview=false;
        hoverClose.Stop();
        preview.IsOpen=false;
        preview.Child=null;
        current="";
    }

    internal void CloseIfMissing()
    {
        if(current.Length>0 && !File.Exists(current) && !Directory.Exists(current))
            Close();
    }
'@ @'
    internal bool IsExcelOpen(DeskSpaceExcelTemplateV16 clip)=>
        preview.IsOpen && ReferenceEquals(currentClip,clip);

    internal void Close()
    {
        hoverOpen.Stop();
        pendingRow=null;
        pendingPath="";
        pendingClip=null;
        pendingMode=null;
        manualPreview=false;
        hoverClose.Stop();
        preview.IsOpen=false;
        preview.Child=null;
        current="";
        currentClip=null;
    }

    internal void CloseIfMissing()
    {
        if(currentClip!=null)
        {
            if(!DeskSpaceExcelClipboardV39.SharedClips.Contains(currentClip))
                Close();
            return;
        }
        if(current.Length>0 && !File.Exists(current) && !Directory.Exists(current))
            Close();
    }

    internal void ShowExcel(DeskSpaceExcelTemplateV16 clip,FrameworkElement source,
        bool explicitClick=false)
    {
        if(disposed||clip==null)return;
        hoverOpen.Stop();
        hoverClose.Stop();
        if(preview.IsOpen && ReferenceEquals(currentClip,clip))return;
        Close();
        currentClip=clip;
        manualPreview=explicitClick;
        var content=CreateExcelPreview(clip);
        content.MouseEnter+=(_,_)=>hoverClose.Stop();
        content.MouseLeave+=(_,_)=>ScheduleClose();
        preview.Child=content;
        try
        {
            var offset=source.TranslatePoint(new Point(0,0),deck).Y;
            preview.VerticalOffset=Math.Max(0,offset);
        }
        catch(InvalidOperationException){preview.VerticalOffset=0;}
        preview.IsOpen=true;
    }

    internal void BindExcel(Button button,Border row,
        DeskSpaceExcelTemplateV16 clip,Func<bool> hoverEnabled)
    {
        button.Click+=(_,e)=>{
            e.Handled=true;
            if(preview.IsOpen && ReferenceEquals(currentClip,clip))
                Close();
            else ShowExcel(clip,row,true);
        };
        row.MouseEnter+=(_,_)=>{
            hoverClose.Stop();
            if(!hoverEnabled())return;
            hoverOpen.Stop();
            pendingRow=row;
            pendingPath="";
            pendingClip=clip;
            pendingMode=hoverEnabled;
            pendingPosition=Mouse.GetPosition(row);
            hoverOpen.Start();
        };
        row.MouseMove+=(_,e)=>{
            var at=e.GetPosition(row);
            if(Stationary(pendingPosition,at))return;
            hoverOpen.Stop();
            if(preview.IsOpen && !manualPreview && !ReferenceEquals(currentClip,clip))Close();
            if(preview.IsOpen && manualPreview)return;
            if(!hoverEnabled())return;
            pendingRow=row;
            pendingPath="";
            pendingClip=clip;
            pendingMode=hoverEnabled;
            pendingPosition=at;
            hoverOpen.Start();
        };
        row.MouseLeave+=(_,_)=>{
            hoverOpen.Stop();
            if(preview.IsOpen && !manualPreview)ScheduleClose();
            else if(preview.IsOpen)ScheduleClose();
        };
        row.Unloaded+=(_,_)=>{
            if(ReferenceEquals(pendingRow,row))
            {
                hoverOpen.Stop();
                pendingRow=null;
                pendingClip=null;
                pendingMode=null;
            }
        };
    }

    private static FrameworkElement CreateExcelPreview(DeskSpaceExcelTemplateV16 clip)
    {
        var root=new StackPanel{Margin=new Thickness(11)};
        string sheetLabel=string.IsNullOrWhiteSpace(clip.SourceSheet)?"Excel clipboard":clip.SourceSheet;
        string headerTitle=string.IsNullOrWhiteSpace(clip.SourceAddress)?sheetLabel:sheetLabel+" · "+clip.SourceAddress;
        root.Children.Add(new TextBlock{
            Text=headerTitle,FontSize=13,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,MaxWidth=420,TextTrimming=TextTrimming.CharacterEllipsis
        });
        int showRows=Math.Clamp(clip.RowCount,1,18);
        int showCols=Math.Clamp(clip.ColumnCount,1,8);
        string meta=(string.IsNullOrWhiteSpace(clip.SourceWorkbook)?"":clip.SourceWorkbook+" · ")+
            clip.RowCount+" rows × "+clip.ColumnCount+" cols · "+clip.Cells.Count+" cells";
        if(clip.RowCount>showRows || clip.ColumnCount>showCols)
            meta+=" · showing top "+showRows+"×"+showCols;
        root.Children.Add(new TextBlock{
            Text=meta,FontSize=10.5,Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,2,0,7)
        });
        if(clip.Cells.Count==0)
        {
            root.Children.Add(new TextBlock{
                Text="No cell values captured in this snippet.",
                FontSize=11,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,6)
            });
        }
        else
        {
            var matrix=DeskSpaceExcelClipboardV39.BuildCellMatrix(clip,showRows,showCols);
            var grid=new Grid{SnapsToDevicePixels=true};
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            for(int c=0;c<showCols;c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            for(int r=0;r<showRows;r++)
                grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var headerBg=new SolidColorBrush(Color.FromRgb(32,45,74));
            var gridStroke=new SolidColorBrush(Color.FromRgb(58,76,118));
            var evenBg=new SolidColorBrush(Color.FromRgb(25,35,58));
            var oddBg=new SolidColorBrush(Color.FromRgb(30,42,68));
            int baseRow=Math.Max(1,clip.SourceRow);
            int baseCol=Math.Max(1,clip.SourceColumn);
            var corner=new Border{
                Background=headerBg,BorderBrush=gridStroke,BorderThickness=new Thickness(0.5),
                Padding=new Thickness(5,3,5,3)
            };
            Grid.SetRow(corner,0);Grid.SetColumn(corner,0);grid.Children.Add(corner);
            for(int c=0;c<showCols;c++)
            {
                var hCell=new Border{
                    Background=headerBg,BorderBrush=gridStroke,BorderThickness=new Thickness(0.5),
                    Padding=new Thickness(7,3,7,3),
                    Child=new TextBlock{
                        Text=DeskSpaceExcelClipboardV39.ColumnLetter(baseCol+c),
                        FontSize=10,FontWeight=FontWeights.SemiBold,
                        Foreground=new SolidColorBrush(Color.FromRgb(175,208,255)),
                        HorizontalAlignment=HorizontalAlignment.Center
                    }
                };
                Grid.SetRow(hCell,0);Grid.SetColumn(hCell,c+1);grid.Children.Add(hCell);
            }
            for(int r=0;r<showRows;r++)
            {
                var rowHdr=new Border{
                    Background=headerBg,BorderBrush=gridStroke,BorderThickness=new Thickness(0.5),
                    Padding=new Thickness(5,3,5,3),
                    Child=new TextBlock{
                        Text=(baseRow+r).ToString(),FontSize=10,
                        Foreground=new SolidColorBrush(Color.FromRgb(165,190,230)),
                        HorizontalAlignment=HorizontalAlignment.Right
                    }
                };
                Grid.SetRow(rowHdr,r+1);Grid.SetColumn(rowHdr,0);grid.Children.Add(rowHdr);
                for(int c=0;c<showCols;c++)
                {
                    var cellObj=matrix[r,c];
                    string val=cellObj!=null?DeskSpaceExcelClipboardV39.CellDisplayText(cellObj):"";
                    string? tip=null;
                    if(cellObj!=null)
                    {
                        tip=string.IsNullOrEmpty(cellObj.FormulaR1C1)
                            ? val
                            : val+"\nFormula: "+cellObj.FormulaR1C1;
                        if(string.IsNullOrWhiteSpace(tip))tip=null;
                    }
                    var cellBorder=new Border{
                        Background=(r%2==0)?evenBg:oddBg,
                        BorderBrush=gridStroke,BorderThickness=new Thickness(0.5),
                        Padding=new Thickness(7,3,7,3),
                        ToolTip=tip,
                        Child=new TextBlock{
                            Text=val,FontSize=11,
                            Foreground=new SolidColorBrush(Color.FromRgb(236,243,255)),
                            MaxWidth=145,TextTrimming=TextTrimming.CharacterEllipsis
                        }
                    };
                    Grid.SetRow(cellBorder,r+1);Grid.SetColumn(cellBorder,c+1);grid.Children.Add(cellBorder);
                }
            }
            root.Children.Add(new ScrollViewer{
                Content=grid,MaxWidth=430,MaxHeight=250,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto
            });
        }
        var bar=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};
        var copyAction=new Button{
            Content="Copy values",FontSize=11,Padding=new Thickness(7,3,7,3),
            Margin=new Thickness(0,0,5,0)
        };
        copyAction.Click+=(_,_)=>DeskSpaceExcelClipboardV39.CopyPlain(clip);
        bar.Children.Add(copyAction);
        var pasteAction=new Button{
            Content="Paste to Excel",FontSize=11,Padding=new Thickness(7,3,7,3)
        };
        pasteAction.Click+=(_,_)=>DeskSpaceExcelClipboardV39.PasteExcel(clip);
        bar.Children.Add(pasteAction);
        root.Children.Add(bar);
        return new Border{
            Child=root,
            Background=new SolidColorBrush(Color.FromRgb(24,32,52)),
            BorderBrush=Brushes.SlateBlue,
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(12)
        };
    }
'@ 'excel clip hover preview implementation'
Swap-R377 @'
            hoverOpen.Stop();
            pendingRow=row;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=Mouse.GetPosition(row);
'@ @'
            hoverOpen.Stop();
            pendingRow=row;
            pendingClip=null;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=Mouse.GetPosition(row);
'@ 'clear pendingClip on file MouseEnter'
Swap-R377 @'
            if(!hoverEnabled())return;
            pendingRow=row;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=at;
'@ @'
            if(!hoverEnabled())return;
            pendingRow=row;
            pendingClip=null;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=at;
'@ 'clear pendingClip on file MouseMove'
Finish-Patch $previewPath

# --- Part E: Patch DeskSpaceDropShelfV15.cs (useful Excel paste + Excel clipboard hover & snippet) ---
$deckPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
Begin-Patch $deckPath
Swap-R377 @'
    private bool excelClipsExpandedV376=true;
    private static void OnClipsChangedV376(){RefreshViews();}
    private bool excelExpandedV122=true,excelRefreshingV122;
'@ @'
    private bool excelClipsExpandedV376=true,excelPasteExpandedV377=true;
    private static void OnClipsChangedV376(){RefreshViews();}
    private static void OnShowDeckRequestedV377()
    {
        DeskSpaceDeckVisibilityV89.Visible=true;
        foreach(var view in Views.ToArray())
            view.WakeDeckV377();
    }
    private void WakeDeckV377()
    {
        if(disposed)return;
        if(!host.Dispatcher.CheckAccess())
        {
            host.Dispatcher.BeginInvoke(new Action(WakeDeckV377));
            return;
        }
        deckDismissed=false;
        deckMinimized=false;
        Render();
    }
    private bool excelExpandedV122=true,excelRefreshingV122;
'@ 'R377 Deck wake handler and Excel paste expand state'

Swap-R377 @'
        Views.Add(this);
        DeskSpaceExcelClipboardV39.ClipsChanged+=OnClipsChangedV376;
'@ @'
        Views.Add(this);
        DeskSpaceExcelClipboardV39.ClipsChanged+=OnClipsChangedV376;
        DeskSpaceExcelClipboardV39.ShowDeckRequested+=OnShowDeckRequestedV377;
'@ 'hook ShowDeckRequested in constructor'

Swap-R377 @'
        Views.Remove(this);
        if(Views.Count==0)DeskSpaceExcelClipboardV39.ClipsChanged-=OnClipsChangedV376;
'@ @'
        Views.Remove(this);
        if(Views.Count==0)
        {
            DeskSpaceExcelClipboardV39.ClipsChanged-=OnClipsChangedV376;
            DeskSpaceExcelClipboardV39.ShowDeckRequested-=OnShowDeckRequestedV377;
        }
'@ 'unhook ShowDeckRequested in Dispose'

Swap-R377 @'
        bool contents=held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count>0 ||
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;
'@ @'
        bool contents=held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count+DeskSpaceExcelClipboardV39.SharedClips.Count>0 ||
            DeskSpaceExcelActiveV122.ForegroundExcel() ||
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;
'@ 'include Excel clips and foreground Excel in Deck visibility'

Swap-R377 @'
            string currentNonApp=NonAppSignature();
            if(held.Count+detected.Count>0 &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
                   StringComparison.Ordinal))
                deckDismissed=false;
'@ @'
            string currentNonApp=NonAppSignature();
            if(held.Count+detected.Count+DeskSpaceExcelClipboardV39.SharedClips.Count>0 &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
                   StringComparison.Ordinal))
                deckDismissed=false;
'@ 'allow newly copied Excel cells to reopen a dismissed Deck'

Swap-R377 @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 &&
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123!=true && !busy)
'@ @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count+DeskSpaceExcelClipboardV39.SharedClips.Count==0 &&
            !DeskSpaceExcelActiveV122.ForegroundExcel() &&
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123!=true && !busy)
'@ 'keep Deck open when Excel clips or active Excel actions exist'

Swap-R377 @'
        if(DeskSpaceExcelActiveV122.ForegroundExcel() && excelMatchesV122.Count==0)
        {
            var pasteGroup=DeskSpaceShelfGroupsV88.Add(list,"Excel paste",1,true,()=>{});
            var pasteBtn=new Button{
                Content="Paste to Excel · Clipboard & mappings",
                ToolTip="Connect to active Microsoft Excel workbook to paste copied values or mappings",
                FontSize=11,Padding=new Thickness(6,5,6,5),
                Margin=new Thickness(1,2,1,2),
                HorizontalContentAlignment=HorizontalAlignment.Left
            };
            pasteBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.ShowActive();
            pasteGroup.Children.Add(pasteBtn);
        }
        var excelClipGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel clipboard",DeskSpaceExcelClipboardV39.SharedClips.Count,
            excelClipsExpandedV376,()=>{
                excelClipsExpandedV376=!excelClipsExpandedV376;Render();
            });
        if(excelClipsExpandedV376)
            foreach(var clip in DeskSpaceExcelClipboardV39.SharedClips.ToArray())
            {
                var row=new Border{
                    Background=new SolidColorBrush(Color.FromArgb(145,39,50,84)),
                    CornerRadius=new CornerRadius(6),
                    Padding=new Thickness(6,4,6,4),
                    Margin=new Thickness(1,2,1,3)
                };
                var rowContent=new StackPanel();
                var header=new DockPanel();
                var drop=new Button{
                    Content="×",FontSize=12,MinWidth=20,Padding=new Thickness(1),
                    ToolTip="Remove from Excel clipboard"
                };
                drop.Click+=(_,_)=>{
                    DeskSpaceExcelClipboardV39.SharedClips.Remove(clip);
                    DeskSpaceExcelClipboardV39.NotifyChanged();
                };
                DockPanel.SetDock(drop,Dock.Right);
                header.Children.Add(drop);
                header.Children.Add(new TextBlock{
                    Text=clip.SourceSheet+" · "+clip.SourceAddress,
                    FontWeight=FontWeights.SemiBold,FontSize=11,Foreground=Brushes.White
                });
                rowContent.Children.Add(header);
                rowContent.Children.Add(new TextBlock{
                    Text=clip.RowCount+" rows × "+clip.ColumnCount+" cols · "+clip.Cells.Count+" copied cells",
                    FontSize=10,Foreground=Brushes.LightSteelBlue,
                    Margin=new Thickness(0,2,0,4)
                });
                var actions=new WrapPanel();
                var copyBtn=new Button{
                    Content="Copy values",FontSize=11,
                    Padding=new Thickness(6,3,6,3),Margin=new Thickness(0,0,4,0),
                    ToolTip="Copy cell values to Windows clipboard"
                };
                copyBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.CopyPlain(clip);
                actions.Children.Add(copyBtn);

                var pasteBtn=new Button{
                    Content="Paste to Excel",FontSize=11,
                    Padding=new Thickness(6,3,6,3),
                    ToolTip="Paste values directly to active Excel workbook with backup"
                };
                pasteBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.PasteExcel(clip);
                actions.Children.Add(pasteBtn);

                rowContent.Children.Add(actions);
                row.Child=rowContent;
                excelClipGroup.Children.Add(row);
            }
'@ @'
        string clipStatusV377=DeskSpaceExcelClipboardV39.CurrentStatus();
        if(DeskSpaceExcelActiveV122.ForegroundExcel() && excelMatchesV122.Count==0)
        {
            int pasteCountV377=DeskSpaceExcelClipboardV39.SharedClips.Count>0?3:2;
            var pasteGroup=DeskSpaceShelfGroupsV88.Add(list,"Excel paste",pasteCountV377,
                excelPasteExpandedV377,()=>{
                    excelPasteExpandedV377=!excelPasteExpandedV377;Render();
                });
            if(excelPasteExpandedV377)
            {
                if(DeskSpaceExcelClipboardV39.SharedClips.Count>0)
                {
                    var latestClip=DeskSpaceExcelClipboardV39.SharedClips[0];
                    var pasteLatestBtn=new Button{
                        Content="Paste latest · "+latestClip.SourceSheet+" ("+
                            latestClip.RowCount+"×"+latestClip.ColumnCount+")",
                        ToolTip="Paste the newest copied Excel cells at the active Excel selection (with workbook backup)",
                        FontSize=11,Padding=new Thickness(6,4,6,4),
                        Margin=new Thickness(1,2,1,2),
                        HorizontalContentAlignment=HorizontalAlignment.Left
                    };
                    pasteLatestBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.PasteExcel(latestClip);
                    pasteGroup.Children.Add(pasteLatestBtn);
                }
                var captureBtn=new Button{
                    Content="Capture selected Excel cells",
                    ToolTip="Copy selected cells from the open Microsoft Excel worksheet into Excel clipboard",
                    FontSize=11,Padding=new Thickness(6,4,6,4),
                    Margin=new Thickness(1,2,1,2),
                    HorizontalContentAlignment=HorizontalAlignment.Left
                };
                captureBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.CaptureSelectionNow();
                pasteGroup.Children.Add(captureBtn);

                var mapBtn=new Button{
                    Content="Open Excel mappings",
                    ToolTip="Open saved Excel templates, header rules and backups",
                    FontSize=11,Padding=new Thickness(6,4,6,4),
                    Margin=new Thickness(1,2,1,2),
                    HorizontalContentAlignment=HorizontalAlignment.Left
                };
                mapBtn.Click+=(_,_)=>{_=DeskSpaceFeatureNavigationV200.OpenExcelAsync(host);};
                pasteGroup.Children.Add(mapBtn);

                if(DeskSpaceExcelClipboardV39.SharedClips.Count==0 && clipStatusV377.Length>0)
                    pasteGroup.Children.Add(new TextBlock{
                        Text=clipStatusV377,FontSize=10,
                        Foreground=new SolidColorBrush(Color.FromRgb(161,232,188)),
                        Margin=new Thickness(3,2,3,2),TextWrapping=TextWrapping.Wrap
                    });
            }
        }
        var excelClipGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel clipboard",DeskSpaceExcelClipboardV39.SharedClips.Count,
            excelClipsExpandedV376,()=>{
                excelClipsExpandedV376=!excelClipsExpandedV376;Render();
            });
        if(excelClipsExpandedV376)
        {
            if(DeskSpaceExcelClipboardV39.SharedClips.Count>0 && clipStatusV377.Length>0)
                excelClipGroup.Children.Add(new TextBlock{
                    Text=clipStatusV377,FontSize=10,
                    Foreground=new SolidColorBrush(Color.FromRgb(161,232,188)),
                    Margin=new Thickness(2,1,2,3),TextWrapping=TextWrapping.Wrap
                });
            foreach(var clip in DeskSpaceExcelClipboardV39.SharedClips.ToArray())
            {
                string snippetV377=DeskSpaceExcelClipboardV39.ClipSummarySnippet(clip);
                var row=new Border{
                    Background=new SolidColorBrush(Color.FromArgb(145,39,50,84)),
                    CornerRadius=new CornerRadius(6),
                    Padding=new Thickness(6,4,6,4),
                    Margin=new Thickness(1,2,1,3),
                    ToolTip=previewOnHover?null:(snippetV377.Length>0?snippetV377:"Copied Excel cells")
                };
                row.MouseEnter+=(_,_)=>row.Background=new SolidColorBrush(
                    Color.FromArgb(205,58,74,122));
                row.MouseLeave+=(_,_)=>row.Background=new SolidColorBrush(
                    Color.FromArgb(145,39,50,84));
                var rowContent=new StackPanel();
                var header=new DockPanel{LastChildFill=true};
                var drop=new Button{
                    Content="×",FontSize=12,MinWidth=20,Padding=new Thickness(1),
                    ToolTip="Remove from Excel clipboard"
                };
                drop.Click+=(_,_)=>{
                    deckPreview?.CloseIfMissing();
                    DeskSpaceExcelClipboardV39.SharedClips.Remove(clip);
                    DeskSpaceExcelClipboardV39.NotifyChanged();
                };
                DockPanel.SetDock(drop,Dock.Right);
                header.Children.Add(drop);
                var previewClip=new Button{
                    Content="◉",FontSize=12,MinWidth=24,Padding=new Thickness(1),
                    Foreground=Brushes.LightSteelBlue,
                    Background=Brushes.Transparent,
                    BorderThickness=new Thickness(0),
                    ToolTip="Preview copied cell table"
                };
                deckPreview?.BindExcel(previewClip,row,clip,()=>previewOnHover);
                DockPanel.SetDock(previewClip,Dock.Right);
                header.Children.Add(previewClip);
                header.Children.Add(new TextBlock{
                    Text=clip.SourceSheet+" · "+clip.SourceAddress,
                    FontWeight=FontWeights.SemiBold,FontSize=11,Foreground=Brushes.White,
                    VerticalAlignment=VerticalAlignment.Center,
                    TextTrimming=TextTrimming.CharacterEllipsis
                });
                rowContent.Children.Add(header);
                rowContent.Children.Add(new TextBlock{
                    Text=clip.RowCount+" rows × "+clip.ColumnCount+" cols · "+clip.Cells.Count+" copied cells",
                    FontSize=10,Foreground=Brushes.LightSteelBlue,
                    Margin=new Thickness(0,2,0,snippetV377.Length>0?1:4)
                });
                if(snippetV377.Length>0)
                    rowContent.Children.Add(new TextBlock{
                        Text=snippetV377,FontSize=10.5,
                        Foreground=new SolidColorBrush(Color.FromRgb(207,228,255)),
                        TextTrimming=TextTrimming.CharacterEllipsis,
                        Margin=new Thickness(0,0,0,4)
                    });
                var actions=new WrapPanel();
                var copyBtn=new Button{
                    Content="Copy values",FontSize=11,
                    Padding=new Thickness(6,3,6,3),Margin=new Thickness(0,0,4,0),
                    ToolTip="Copy cell values to Windows clipboard"
                };
                copyBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.CopyPlain(clip);
                actions.Children.Add(copyBtn);

                var pasteBtn=new Button{
                    Content="Paste to Excel",FontSize=11,
                    Padding=new Thickness(6,3,6,3),
                    ToolTip="Paste values directly to active Excel workbook with backup"
                };
                pasteBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.PasteExcel(clip);
                actions.Children.Add(pasteBtn);

                rowContent.Children.Add(actions);
                row.Child=rowContent;
                excelClipGroup.Children.Add(row);
            }
        }
'@ 'useful Excel paste actions + Excel clipboard hover table & snippet'
Finish-Patch $deckPath

# --- Part F: Patch WidgetHost.cs (Monitor-Aware Fullscreen Detection + DPI-safe Edge Dock) ---
$hostPath='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
Begin-Patch $hostPath
Swap-R377 @'
        if ((DateTime.UtcNow-lastFullscreenPoll).TotalSeconds>=2)
        {
            previousFullscreen=FullscreenDetector.IsOtherFullscreen();
            lastFullscreenPoll=DateTime.UtcNow;
        }
'@ @'
        if ((DateTime.UtcNow-lastFullscreenPoll).TotalSeconds>=1)
        {
            previousFullscreen=FullscreenDetector.IsMonitorFullscreen(this);
            lastFullscreenPoll=DateTime.UtcNow;
        }
'@ 'per-monitor fullscreen polling in WidgetHost Tick'

Swap-R377 @'
    private void DockToEdge(bool manual = false)
    {
        if (hiddenAtEdge) return;
        dockedManually = manual;
        SavePosition();
        // WPF Left/Top are DIPs, not Windows screen pixels. Using FromPoint
        // with DIPs assigned a mixed-DPI widget to the wrong monitor.
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var monitor=(DeskSpaceMonitorFenceV152.BoundScreen(this) ??
            Forms.Screen.FromHandle(hwnd)).WorkingArea;
        var area=new Rect(monitor.Left, monitor.Top, monitor.Width, monitor.Height);
        double leftDist=Math.Abs(Left-area.Left),rightDist=Math.Abs(area.Right-(Left+Width));
        edgeHandle.Left=leftDist<=rightDist?area.Left:area.Right-edgeHandle.Width;
        edgeHandle.Top=Math.Clamp(Top + Height / 2 - edgeHandle.Height / 2,area.Top,area.Bottom-edgeHandle.Height);
        SuspendWeatherMotion();
        hiddenAtEdge=true;Hide();edgeHandle.Show();edgeHandle.Topmost=true;
    }
'@ @'
    private void DockToEdge(bool manual = false)
    {
        if (hiddenAtEdge) return;
        dockedManually = manual;
        SavePosition();
        // WPF Left/Top are DIPs, not Windows screen pixels. Compute edge placement
        // cleanly on this widget's own physical monitor for mixed-DPI multi-monitor setups.
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var monitor=(DeskSpaceMonitorFenceV152.BoundScreen(this) ??
            Forms.Screen.FromHandle(hwnd)).WorkingArea;
        FullscreenDetector.PrepareEdgeDock(hwnd,this,monitor,edgeHandle,
            out bool snapLeft,out int centerY);
        if(edgeHandle.Content is Button edgeButton)
            edgeButton.Content=snapLeft?"❯":"❮";
        SuspendWeatherMotion();
        hiddenAtEdge=true;Hide();edgeHandle.Show();edgeHandle.Topmost=true;
        FullscreenDetector.PlaceEdgeHandleNative(edgeHandle,monitor,snapLeft,centerY);
    }
'@ 'monitor-local DPI-safe edge tab docking'

Swap-R377 @'
public static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)] private struct MONITORINFO { public uint cbSize;public RECT rcMonitor;public RECT rcWork;public uint dwFlags; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RECT r);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    public static bool IsOtherFullscreen()
    {
        try
        {
            var w=GetForegroundWindow(); if(w==IntPtr.Zero) return false;
            GetWindowThreadProcessId(w,out var pid);if(pid==Environment.ProcessId)return false;
            if(!GetWindowRect(w,out var rect))return false;
            var info=new MONITORINFO {cbSize=(uint)Marshal.SizeOf<MONITORINFO>()};
            if(!GetMonitorInfo(MonitorFromWindow(w,2),ref info))return false;
            return rect.Left<=info.rcMonitor.Left+2&&rect.Top<=info.rcMonitor.Top+2&&
                rect.Right>=info.rcMonitor.Right-2&&rect.Bottom>=info.rcMonitor.Bottom-2;
        }
        catch{return false;}
    }
}
'@ @'
public static class FullscreenDetector
{
    public const uint WS_CAPTION=0x00C00000;
    public const uint WS_MAXIMIZE=0x01000000;
    public const uint WS_EX_TRANSPARENT=0x00000020;
    private const int GWL_STYLE=-16;
    private const int GWL_EXSTYLE=-20;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS=9;
    private const int DWMWA_CLOAKED=14;
    private static readonly IntPtr HWND_TOPMOST=new(-1);
    private const uint SWP_NOSIZE=0x0001;
    private const uint SWP_NOACTIVATE=0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]
        public string szDevice;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd,IntPtr lParam);
    private delegate bool EnumMonitorsProc(IntPtr hMonitor,IntPtr hdc,ref RECT rect,IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RECT r);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc,IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc,IntPtr lprcClip,EnumMonitorsProc proc,IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFOEX info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd,int nIndex);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd,int nIndex);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr insertAfter,int x,int y,int cx,int cy,uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out RECT value,int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out int value,int size);

    private static uint GetStyleLong(IntPtr hwnd,int index)
    {
        try
        {
            return IntPtr.Size==8
                ? unchecked((uint)GetWindowLongPtr64(hwnd,index).ToInt64())
                : unchecked((uint)GetWindowLong32(hwnd,index));
        }
        catch{return 0;}
    }

    private static RECT GetAccurateWindowRect(IntPtr hwnd)
    {
        try
        {
            if(DwmGetWindowAttribute(hwnd,DWMWA_EXTENDED_FRAME_BOUNDS,
                out RECT dwm,Marshal.SizeOf<RECT>())==0 &&
               dwm.Right>dwm.Left && dwm.Bottom>dwm.Top)
                return dwm;
        }
        catch{}
        return GetWindowRect(hwnd,out var wr)?wr:default;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            return DwmGetWindowAttribute(hwnd,DWMWA_CLOAKED,out int cloaked,sizeof(int))==0 && cloaked!=0;
        }
        catch{return false;}
    }

    public static bool CoversMonitor(
        int winLeft,int winTop,int winRight,int winBottom,
        int monLeft,int monTop,int monRight,int monBottom,
        int workLeft,int workTop,int workRight,int workBottom,
        uint style,uint exStyle,string className)
    {
        if(monRight<=monLeft || monBottom<=monTop)return false;
        if(winRight<=winLeft || winBottom<=winTop)return false;
        if((exStyle & WS_EX_TRANSPARENT)!=0)return false;
        if(string.Equals(className,"Progman",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(className,"WorkerW",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(className,"Shell_TrayWnd",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(className,"Shell_SecondaryTrayWnd",StringComparison.OrdinalIgnoreCase))
            return false;
        bool covers=winLeft<=monLeft+2 && winTop<=monTop+2 &&
            winRight>=monRight-2 && winBottom>=monBottom-2;
        if(!covers)return false;
        bool hasWorkInset=workLeft>monLeft || workTop>monTop ||
            workRight<monRight || workBottom<monBottom;
        bool hasCaption=(style & WS_CAPTION)==WS_CAPTION;
        bool isMaximized=(style & WS_MAXIMIZE)!=0;
        if(!hasWorkInset && hasCaption && isMaximized &&
           !string.Equals(className,"ApplicationFrameWindow",StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool TryResolveMonitor(Window? widget,out MONITORINFOEX target)
    {
        target=default;
        IntPtr widgetHwnd=widget!=null
            ? new System.Windows.Interop.WindowInteropHelper(widget).Handle
            : IntPtr.Zero;
        Forms.Screen? bound=null;
        if(widget!=null)
            bound=DeskSpaceMonitorFenceV152.BoundScreen(widget) ??
                (widgetHwnd!=IntPtr.Zero?Forms.Screen.FromHandle(widgetHwnd):null);
        string? wantedDevice=bound?.DeviceName;
        if(!string.IsNullOrWhiteSpace(wantedDevice))
        {
            MONITORINFOEX foundInfo=default;
            bool matched=false;
            EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr hMon,IntPtr _,ref RECT _,IntPtr _)=>{
                var mi=new MONITORINFOEX{cbSize=(uint)Marshal.SizeOf<MONITORINFOEX>()};
                if(GetMonitorInfo(hMon,ref mi) &&
                   string.Equals(mi.szDevice,wantedDevice,StringComparison.OrdinalIgnoreCase))
                {
                    foundInfo=mi;
                    matched=true;
                    return false;
                }
                return true;
            },IntPtr.Zero);
            if(matched)
            {
                target=foundInfo;
                return true;
            }
        }
        IntPtr fallbackMon=widgetHwnd!=IntPtr.Zero
            ? MonitorFromWindow(widgetHwnd,2)
            : MonitorFromWindow(GetForegroundWindow(),2);
        if(fallbackMon==IntPtr.Zero)return false;
        var info=new MONITORINFOEX{cbSize=(uint)Marshal.SizeOf<MONITORINFOEX>()};
        if(!GetMonitorInfo(fallbackMon,ref info))return false;
        target=info;
        return true;
    }

    public static bool IsMonitorFullscreen(Window? widget)
    {
        try
        {
            if(!TryResolveMonitor(widget,out var mon))return false;
            int myPid=Environment.ProcessId;
            bool detected=false;
            var sb=new System.Text.StringBuilder(128);
            EnumWindows((hwnd,_)=>{
                if(!IsWindowVisible(hwnd) || IsIconic(hwnd))return true;
                GetWindowThreadProcessId(hwnd,out uint pid);
                if(pid==myPid)return true;
                if(IsCloaked(hwnd))return true;
                RECT rect=GetAccurateWindowRect(hwnd);
                if(rect.Right<=rect.Left || rect.Bottom<=rect.Top)return true;
                sb.Clear();
                GetClassName(hwnd,sb,sb.Capacity);
                uint style=GetStyleLong(hwnd,GWL_STYLE);
                uint exStyle=GetStyleLong(hwnd,GWL_EXSTYLE);
                if(CoversMonitor(rect.Left,rect.Top,rect.Right,rect.Bottom,
                    mon.rcMonitor.Left,mon.rcMonitor.Top,mon.rcMonitor.Right,mon.rcMonitor.Bottom,
                    mon.rcWork.Left,mon.rcWork.Top,mon.rcWork.Right,mon.rcWork.Bottom,
                    style,exStyle,sb.ToString()))
                {
                    detected=true;
                    return false;
                }
                return true;
            },IntPtr.Zero);
            return detected;
        }
        catch{return false;}
    }

    internal static void PrepareEdgeDock(IntPtr hwnd,Window widget,
        System.Drawing.Rectangle monitor,Window edgeHandle,
        out bool snapLeft,out int centerY)
    {
        double scale=1.0;
        try
        {
            uint dpi=hwnd!=IntPtr.Zero?GetDpiForWindow(hwnd):0;
            if(dpi>=96)scale=dpi/96.0;
        }
        catch{}
        RECT selfRect;
        if(hwnd!=IntPtr.Zero && GetWindowRect(hwnd,out selfRect) &&
           selfRect.Right>selfRect.Left)
        {
            int leftDist=Math.Abs(selfRect.Left-monitor.Left);
            int rightDist=Math.Abs(monitor.Right-selfRect.Right);
            snapLeft=leftDist<=rightDist;
            centerY=(selfRect.Top+selfRect.Bottom)/2;
        }
        else
        {
            int pxLeft=(int)Math.Round(widget.Left*scale);
            int pxTop=(int)Math.Round(widget.Top*scale);
            int pxW=(int)Math.Round(widget.Width*scale);
            int pxH=(int)Math.Round(widget.Height*scale);
            int leftDist=Math.Abs(pxLeft-monitor.Left);
            int rightDist=Math.Abs(monitor.Right-(pxLeft+pxW));
            snapLeft=leftDist<=rightDist;
            centerY=pxTop+pxH/2;
        }
        int pxEdgeW=(int)Math.Round(edgeHandle.Width*scale);
        int pxEdgeH=(int)Math.Round(edgeHandle.Height*scale);
        int targetX=snapLeft?monitor.Left:monitor.Right-pxEdgeW;
        int targetY=Math.Clamp(centerY-pxEdgeH/2,monitor.Top,
            Math.Max(monitor.Top,monitor.Bottom-pxEdgeH));
        edgeHandle.Left=targetX/scale;
        edgeHandle.Top=targetY/scale;
    }

    internal static void PlaceEdgeHandleNative(Window edgeHandle,
        System.Drawing.Rectangle monitor,bool snapLeft,int centerY)
    {
        try
        {
            var edgeHwnd=new System.Windows.Interop.WindowInteropHelper(edgeHandle).Handle;
            if(edgeHwnd==IntPtr.Zero)return;
            double scale=1.0;
            try
            {
                uint dpi=GetDpiForWindow(edgeHwnd);
                if(dpi>=96)scale=dpi/96.0;
            }
            catch{}
            int pxW=(int)Math.Round(edgeHandle.Width*scale);
            int pxH=(int)Math.Round(edgeHandle.Height*scale);
            if(GetWindowRect(edgeHwnd,out var cur) && cur.Right>cur.Left && cur.Bottom>cur.Top)
            {
                pxW=cur.Right-cur.Left;
                pxH=cur.Bottom-cur.Top;
            }
            int x=snapLeft?monitor.Left:monitor.Right-pxW;
            int y=Math.Clamp(centerY-pxH/2,monitor.Top,Math.Max(monitor.Top,monitor.Bottom-pxH));
            SetWindowPos(edgeHwnd,HWND_TOPMOST,x,y,0,0,SWP_NOSIZE|SWP_NOACTIVATE);
        }
        catch{}
    }

    public static bool IsOtherFullscreen()
    {
        try
        {
            var w=GetForegroundWindow();
            if(w==IntPtr.Zero)return false;
            GetWindowThreadProcessId(w,out var pid);
            if(pid==Environment.ProcessId)return false;
            var rect=GetAccurateWindowRect(w);
            var info=new MONITORINFOEX{cbSize=(uint)Marshal.SizeOf<MONITORINFOEX>()};
            if(!GetMonitorInfo(MonitorFromWindow(w,2),ref info))return false;
            var sb=new System.Text.StringBuilder(128);
            GetClassName(w,sb,sb.Capacity);
            return CoversMonitor(rect.Left,rect.Top,rect.Right,rect.Bottom,
                info.rcMonitor.Left,info.rcMonitor.Top,info.rcMonitor.Right,info.rcMonitor.Bottom,
                info.rcWork.Left,info.rcWork.Top,info.rcWork.Right,info.rcWork.Bottom,
                GetStyleLong(w,GWL_STYLE),GetStyleLong(w,GWL_EXSTYLE),sb.ToString());
        }
        catch{return false;}
    }
}
'@ 'per-monitor fullscreen detector and DPI-safe edge placement'
Finish-Patch $hostPath

# --- R377 Static verification guards ---
$bridgeFinal=Get-Content $bridgePath -Raw
$workerFinal=Get-Content $workerPath -Raw
$previewFinal=Get-Content $previewPath -Raw
$deckFinal=Get-Content $deckPath -Raw
$hostFinal=Get-Content $hostPath -Raw

if(!$bridgeFinal.Contains('Selected-cell fallback is disabled for data.') -or
   !$bridgeFinal.Contains('PreviewClipboardValues(') -or
   !$bridgeFinal.Contains('ApplyClipboardValues(')){
    throw 'R377 bridge clipboard or R125 strict information guard missing'
}
if(!$workerFinal.Contains('PreviewClipboardAsync(') -or
   !$workerFinal.Contains('ApplyClipboardAsync(')){
    throw 'R377 worker clipboard helpers missing'
}
if(!$previewFinal.Contains('ShowExcel(') -or !$previewFinal.Contains('BindExcel(') -or
   !$previewFinal.Contains('CreateExcelPreview(')){
    throw 'R377 Deck preview Excel table helpers missing'
}
if($deckFinal.Contains('Paste to Excel · Clipboard & mappings') -or
   !$deckFinal.Contains('Capture selected Excel cells') -or
   !$deckFinal.Contains('Open Excel mappings') -or
   !$deckFinal.Contains('BindExcel(previewClip,row,clip')){
    throw 'R377 Deck Excel paste actions or clipboard hover binding missing'
}
if(([regex]::Matches($deckFinal,'DeckHeaderButton[(]')).Count -ne 4){
    throw 'R377 broke R308 header button count invariant (must equal 4)'
}
if(!$hostFinal.Contains('FullscreenDetector.IsMonitorFullscreen(this)') -or
   !$hostFinal.Contains('CoversMonitor(') -or
   !$hostFinal.Contains('PlaceEdgeHandleNative(')){
    throw 'R377 per-monitor fullscreen detector or edge dock placement missing'
}

Write-Host 'R377 PASS: Excel paste actions fixed, Excel clipboard hover table preview wired, Multi-Monitor fullscreen behavior isolated per monitor.'
