$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R377 / R378:
#  1) Revert extra Excel discovery/prefetch changes to v1.5.1141 and remove 'Excel paste' from Deck
#     while preserving [Paste to Excel], [Copy values], snippet & hover table preview on copied cells.
#  2) Keep per-monitor fullscreen detection & DPI-safe edge docking.
#  3) Make Media Player strictly monitor-aware so media controls only appear on the monitor playing it.

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
    private bool excelClipsExpandedV376=true;
    private static void OnClipsChangedV376(){RefreshViews();}
    private static void OnShowDeckRequestedV377()
    {
        DeskSpaceDeckVisibilityV89.TrySet(true,out _);
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
            DeskSpaceMediaV18.ForHostV378(host)?.DeckVisibleV123==true;
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
        var player=DeskSpaceMediaV18.ActiveV123;
'@ @'
        var player=DeskSpaceMediaV18.ForHostV378(host);
'@ 'per-monitor media state polling in RefreshMediaDeckV123'

Swap-R377 @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 &&
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123!=true && !busy)
'@ @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count+DeskSpaceExcelClipboardV39.SharedClips.Count==0 &&
            DeskSpaceMediaV18.ForHostV378(host)?.DeckVisibleV123!=true && !busy)
'@ 'keep Deck open when Excel clips or monitor-local media exist'

Swap-R377 @'
        var activeMedia=DeskSpaceMediaV18.ActiveV123;
'@ @'
        var activeMedia=DeskSpaceMediaV18.ForHostV378(host);
'@ 'render monitor-local Media Player in Deck'

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
        bool covers=winLeft<=monLeft+4 && winTop<=monTop+4 &&
            winRight>=monRight-4 && winBottom>=monBottom-4;
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
        if(widget is WidgetShellWindow shell)
            bound=DeskSpaceMonitorFenceV152.BoundScreen(shell);
        if(bound==null && widgetHwnd!=IntPtr.Zero)
            bound=Forms.Screen.FromHandle(widgetHwnd);
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
Swap-R377 @'
            excelClipboardShelf ??= new DeskSpaceExcelClipboardV39(this);
            excelClipboardShelf.Enable();
            media.VisibilityChanged+=OnMediaVisibility;
            media.Start();
'@ @'
            excelClipboardShelf ??= new DeskSpaceExcelClipboardV39(this);
            excelClipboardShelf.Enable();
            media.BindHostV378(this);
            media.VisibilityChanged+=OnMediaVisibility;
            media.Start();
'@ 'bind each monitor WidgetHost to its own DeskSpaceMediaV18'

Finish-Patch $hostPath
# --- Part G: Patch DeskSpaceMediaV18.cs (Strict Per-Monitor Media Ownership) ---
$mediaPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMediaV18.cs'
Begin-Patch $mediaPath

Swap-R377 @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
'@ @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Drawing=System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Forms=System.Windows.Forms;
'@ 'media monitor awareness usings'

Swap-R377 @'
    internal static DeskSpaceMediaV18? ActiveV123 {get;private set;}
    internal static bool DeckIntegrationV123 {get;set;}
'@ @'
    internal static DeskSpaceMediaV18? ActiveV123 {get;private set;}
    internal static bool DeckIntegrationV123 {get;set;}

    private static readonly object SyncV378=new();
    private static readonly List<DeskSpaceMediaV18> InstancesV378=new();
    private static readonly Dictionary<string,string> PreferredMonitorBySourceV378=new(StringComparer.OrdinalIgnoreCase);
    private Window? ownerHostV378;
    private string currentSourceIdV378="";

    internal void BindHostV378(Window owner)
    {
        ownerHostV378=owner;
    }

    internal static DeskSpaceMediaV18? ForHostV378(Window? owner)
    {
        lock(SyncV378)
        {
            if(owner!=null)
            {
                var match=InstancesV378.LastOrDefault(x=>!x.disposed && ReferenceEquals(x.ownerHostV378,owner));
                if(match!=null)return match;
                if(InstancesV378.Any(x=>!x.disposed && x.ownerHostV378!=null))
                    return null;
            }
            return ActiveV123;
        }
    }

    internal sealed record MediaWindowInfoV378(
        IntPtr Handle,
        string Title,
        string ClassName,
        string ProcessName,
        string AppUserModelId,
        IReadOnlyList<string> ChildProcesses,
        IReadOnlyList<string> ChildAppUserModelIds,
        bool Minimized,
        bool Fullscreen,
        string MonitorDevice);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectV378 { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePointV378 { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePlacementV378
    {
        public int Length,Flags,ShowCmd;
        public NativePointV378 MinPosition,MaxPosition;
        public NativeRectV378 NormalPosition;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    private struct NativeMonitorInfoV378
    {
        public uint cbSize;
        public NativeRectV378 rcMonitor;
        public NativeRectV378 rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]
        public string szDevice;
    }

    private delegate bool EnumWindowsProcV378(IntPtr hwnd,IntPtr lParam);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProcV378 callback,IntPtr lParam);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr parent,EnumWindowsProcV378 callback,IntPtr lParam);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd,StringBuilder output,int maxCount);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd,StringBuilder output,int maxCount);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRectV378 rect);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hwnd,ref NativePlacementV378 placement);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64V378(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong32V378(IntPtr hwnd,int index);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect([In] ref NativeRectV378 rect,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor,ref NativeMonitorInfoV378 info);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern IntPtr OpenProcess(uint desiredAccess,bool inheritHandle,uint processId);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess,uint flags,StringBuilder exeName,ref uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr hProcess,ref uint length,StringBuilder? appUserModelId);

    private static uint GetWindowLongUIntV378(IntPtr hwnd,int index)
    {
        var raw=IntPtr.Size==8?GetWindowLongPtr64V378(hwnd,index):new IntPtr(GetWindowLong32V378(hwnd,index));
        return unchecked((uint)raw.ToInt64());
    }

    private static string ResolveWindowDeviceV378(Window owner)
    {
        try
        {
            if(owner is WidgetShellWindow shell)
            {
                var bound=DeskSpaceMonitorFenceV152.BoundScreen(shell);
                if(bound!=null && !string.IsNullOrWhiteSpace(bound.DeviceName))
                    return bound.DeviceName;
            }
            var hwnd=new System.Windows.Interop.WindowInteropHelper(owner).Handle;
            if(hwnd!=IntPtr.Zero)
            {
                var mon=MonitorFromWindow(hwnd,2);
                var info=new NativeMonitorInfoV378{cbSize=(uint)Marshal.SizeOf<NativeMonitorInfoV378>(),szDevice=""};
                if(mon!=IntPtr.Zero && GetMonitorInfo(mon,ref info) && !string.IsNullOrWhiteSpace(info.szDevice))
                    return info.szDevice;
                var scr=Forms.Screen.FromHandle(hwnd);
                if(!string.IsNullOrWhiteSpace(scr.DeviceName))return scr.DeviceName;
            }
            return Forms.Screen.PrimaryScreen?.DeviceName??"";
        }
        catch{return "";}
    }

    private static (string ProcessName,string AppUserModelId) QueryProcessMetaV378(uint pid,Dictionary<uint,(string,string)> cache)
    {
        if(cache.TryGetValue(pid,out var cached))return cached;
        string proc="",aumid="";
        IntPtr h=OpenProcess(0x1000u,false,pid);
        if(h!=IntPtr.Zero)
        {
            try
            {
                uint cap=512;
                var sb=new StringBuilder((int)cap);
                if(QueryFullProcessImageName(h,0,sb,ref cap))
                    proc=Path.GetFileNameWithoutExtension(sb.ToString());
                uint len=260;
                var ab=new StringBuilder((int)len);
                int rc=GetApplicationUserModelId(h,ref len,ab);
                if(rc==0)aumid=ab.ToString();
                else if(rc==122 && len>0 && len<=2048)
                {
                    ab=new StringBuilder((int)len);
                    if(GetApplicationUserModelId(h,ref len,ab)==0)
                        aumid=ab.ToString();
                }
            }
            catch{}
            finally{CloseHandle(h);}
        }
        if(string.IsNullOrWhiteSpace(proc))
        {
            try{proc=Process.GetProcessById((int)pid).ProcessName;}
            catch{}
        }
        var result=(proc??"",aumid??"");
        cache[pid]=result;
        return result;
    }

    private static IReadOnlyList<MediaWindowInfoV378> SnapshotMediaWindowsV378()
    {
        var result=new List<MediaWindowInfoV378>();
        var procCache=new Dictionary<uint,(string,string)>();
        uint selfPid=(uint)Environment.ProcessId;
        var clsSb=new StringBuilder(128);
        EnumWindows((hwnd,_)=>{
            if(!IsWindowVisible(hwnd))return true;
            if(DwmGetWindowAttribute(hwnd,14,out int cloaked,sizeof(int))==0 && cloaked!=0)
                return true;
            GetWindowThreadProcessId(hwnd,out uint pid);
            if(pid==0 || pid==selfPid)return true;

            clsSb.Clear();
            int cl=GetClassName(hwnd,clsSb,clsSb.Capacity);
            string cls=cl>0?clsSb.ToString():"";
            if(string.Equals(cls,"Progman",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"WorkerW",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"Shell_TrayWnd",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"Shell_SecondaryTrayWnd",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"Windows.UI.Core.CoreWindow",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"DummyDwmListenerWindow",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cls,"EdgeUiInputTopWndClass",StringComparison.OrdinalIgnoreCase))
                return true;

            int tlen=GetWindowTextLength(hwnd);
            string winTitle="";
            if(tlen>0)
            {
                var tb=new StringBuilder(Math.Min(tlen+2,1024));
                if(GetWindowText(hwnd,tb,tb.Capacity)>0)
                    winTitle=tb.ToString();
            }

            bool minimized=IsIconic(hwnd);
            uint style=GetWindowLongUIntV378(hwnd,-16);
            uint exStyle=GetWindowLongUIntV378(hwnd,-20);
            if((exStyle & 0x80u)!=0 && string.IsNullOrWhiteSpace(winTitle) && !IsPipTitleV378(winTitle))
                return true;

            if(!minimized)
            {
                if(!GetWindowRect(hwnd,out var rect))return true;
                int w=rect.Right-rect.Left, h=rect.Bottom-rect.Top;
                if(w<60 || h<40)return true;
                IntPtr mon=MonitorFromWindow(hwnd,2);
                var mi=new NativeMonitorInfoV378{cbSize=(uint)Marshal.SizeOf<NativeMonitorInfoV378>(),szDevice=""};
                if(mon==IntPtr.Zero || !GetMonitorInfo(mon,ref mi) || string.IsNullOrWhiteSpace(mi.szDevice))
                    return true;
                bool fullscreen=FullscreenDetector.CoversMonitor(
                    rect.Left,rect.Top,rect.Right,rect.Bottom,
                    mi.rcMonitor.Left,mi.rcMonitor.Top,mi.rcMonitor.Right,mi.rcMonitor.Bottom,
                    mi.rcWork.Left,mi.rcWork.Top,mi.rcWork.Right,mi.rcWork.Bottom,
                    style,exStyle,cls);
                var (procName,aumid)=QueryProcessMetaV378(pid,procCache);
                var childProcs=new List<string>();
                var childAumids=new List<string>();
                if(string.Equals(cls,"ApplicationFrameWindow",StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName,"ApplicationFrameHost",StringComparison.OrdinalIgnoreCase))
                {
                    EnumChildWindows(hwnd,(child,__)=>{
                        GetWindowThreadProcessId(child,out uint cpid);
                        if(cpid!=0 && cpid!=pid && cpid!=selfPid)
                        {
                            var (cp,ca)=QueryProcessMetaV378(cpid,procCache);
                            if(!string.IsNullOrWhiteSpace(cp) && !childProcs.Contains(cp,StringComparer.OrdinalIgnoreCase))
                                childProcs.Add(cp);
                            if(!string.IsNullOrWhiteSpace(ca) && !childAumids.Contains(ca,StringComparer.OrdinalIgnoreCase))
                                childAumids.Add(ca);
                        }
                        return true;
                    },IntPtr.Zero);
                }
                result.Add(new MediaWindowInfoV378(
                    hwnd,winTitle,cls,procName,aumid,childProcs,childAumids,false,fullscreen,mi.szDevice));
            }
            else
            {
                var wp=new NativePlacementV378{Length=Marshal.SizeOf<NativePlacementV378>()};
                IntPtr mon=IntPtr.Zero;
                if(GetWindowPlacement(hwnd,ref wp))
                {
                    var nr=wp.NormalPosition;
                    mon=MonitorFromRect(ref nr,2);
                }
                if(mon==IntPtr.Zero)mon=MonitorFromWindow(hwnd,2);
                var mi=new NativeMonitorInfoV378{cbSize=(uint)Marshal.SizeOf<NativeMonitorInfoV378>(),szDevice=""};
                if(mon==IntPtr.Zero || !GetMonitorInfo(mon,ref mi) || string.IsNullOrWhiteSpace(mi.szDevice))
                    return true;
                var (procName,aumid)=QueryProcessMetaV378(pid,procCache);
                result.Add(new MediaWindowInfoV378(
                    hwnd,winTitle,cls,procName,aumid,Array.Empty<string>(),Array.Empty<string>(),true,false,mi.szDevice));
            }
            return true;
        },IntPtr.Zero);
        return result;
    }

    private static string? GetPreferredMonitorV378(string? sourceAppId)
    {
        if(string.IsNullOrWhiteSpace(sourceAppId))return null;
        lock(SyncV378)
            return PreferredMonitorBySourceV378.TryGetValue(sourceAppId,out var dev)?dev:null;
    }

    private static void SetPreferredMonitorV378(string? sourceAppId,string monitorDevice)
    {
        if(string.IsNullOrWhiteSpace(sourceAppId)||string.IsNullOrWhiteSpace(monitorDevice))return;
        lock(SyncV378)
            PreferredMonitorBySourceV378[sourceAppId]=monitorDevice;
    }

    private static string NormalizeTokenV378(string s)
    {
        if(string.IsNullOrWhiteSpace(s))return "";
        var sb=new StringBuilder(s.Length);
        foreach(char ch in s)
        {
            if(char.IsLetterOrDigit(ch))sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static bool MatchAppIdentityV378(string? procOrAumid,string sourceLower)
    {
        if(string.IsNullOrWhiteSpace(procOrAumid)||string.IsNullOrWhiteSpace(sourceLower))return false;
        string raw=procOrAumid.Trim().ToLowerInvariant();
        if(raw.EndsWith(".exe"))raw=raw[..^4];
        if(sourceLower.Equals(raw,StringComparison.OrdinalIgnoreCase))return true;
        string sourceNoExe=sourceLower.EndsWith(".exe")?sourceLower[..^4]:sourceLower;
        if(sourceNoExe.Equals(raw,StringComparison.OrdinalIgnoreCase))return true;

        // Well-known browser & media player SourceAppUserModelId aliases on Windows 10/11
        bool sourceIsChrome=sourceLower=="chrome"||sourceLower.StartsWith("chrome.")||sourceLower.Contains("google.chrome");
        if(sourceIsChrome && raw=="chrome")return true;

        bool sourceIsEdge=sourceLower=="msedge"||sourceLower.StartsWith("msedge.")||sourceLower.Contains("microsoft.microsoftedge");
        if(sourceIsEdge && raw=="msedge")return true;

        bool sourceIsFirefox=sourceLower is "308046b0af4a39cb" or "e7cf176e110c211b" or "6f193ccc56814779" ||
            sourceLower.Contains("firefox") || sourceLower.Contains("mozilla");
        if(sourceIsFirefox && raw is "firefox" or "zen" or "floorp" or "waterfox" or "librewolf")return true;

        bool sourceIsBrave=sourceLower=="brave"||sourceLower.StartsWith("brave.")||sourceLower.Contains("brave");
        if(sourceIsBrave && raw=="brave")return true;

        bool sourceIsOpera=sourceLower.Contains("opera");
        if(sourceIsOpera && raw is "opera" or "operagx")return true;

        bool sourceIsVivaldi=sourceLower.Contains("vivaldi");
        if(sourceIsVivaldi && raw=="vivaldi")return true;

        bool sourceIsSpotify=sourceLower.Contains("spotify");
        if(sourceIsSpotify && raw=="spotify")return true;

        bool sourceIsVlc=sourceLower.Contains("vlc");
        if(sourceIsVlc && raw=="vlc")return true;

        bool sourceIsZuneVideo=sourceLower.Contains("zunevideo");
        if(sourceIsZuneVideo && raw is "video.ui" or "zunevideo")return true;

        bool sourceIsZuneMusic=sourceLower.Contains("zunemusic")||sourceLower.Contains("microsoft.media.player");
        if(sourceIsZuneMusic && raw is "microsoft.media.player" or "music.ui" or "zunemusic")return true;

        // UWP / MSIX PackageFamilyName / exclamation suffix match
        int bang=raw.IndexOf('!');
        string rawPkg=bang>0?raw[..bang]:raw;
        int srcBang=sourceLower.IndexOf('!');
        string srcPkg=srcBang>0?sourceLower[..srcBang]:sourceLower;
        if(rawPkg.Length>=5 && srcPkg.Length>=5 &&
           (rawPkg.Equals(srcPkg,StringComparison.OrdinalIgnoreCase) ||
            rawPkg.Contains(srcPkg,StringComparison.OrdinalIgnoreCase) ||
            srcPkg.Contains(rawPkg,StringComparison.OrdinalIgnoreCase)))
            return true;

        // Generic stem match for PotPlayer, MPC-HC, MPV, MusicBee, iTunes, etc.
        string normRaw=NormalizeTokenV378(raw);
        if(normRaw.Length>=3)
        {
            if(normRaw is "app" or "host" or "applicationframehost" or "explorer" or "microsoft" or "windows" or "google")
                return false;
            var parts=sourceLower.Split(new[]{'\\','/','.', '_', '!', '-', ' '},StringSplitOptions.RemoveEmptyEntries);
            foreach(var p in parts)
            {
                string np=NormalizeTokenV378(p);
                if(np.Length>=3 &&
                   (np==normRaw || (np.Length>=5 && normRaw.Contains(np)) || (normRaw.Length>=5 && np.Contains(normRaw))))
                    return true;
            }
        }
        return false;
    }

    internal static bool WindowMatchesSourceV378(MediaWindowInfoV378 win,string sourceAppId)
    {
        if(string.IsNullOrWhiteSpace(sourceAppId))return false;
        string sl=sourceAppId.Trim().ToLowerInvariant();
        if(MatchAppIdentityV378(win.AppUserModelId,sl))return true;
        foreach(var ca in win.ChildAppUserModelIds)
            if(MatchAppIdentityV378(ca,sl))return true;
        if(MatchAppIdentityV378(win.ProcessName,sl))return true;
        foreach(var cp in win.ChildProcesses)
            if(MatchAppIdentityV378(cp,sl))return true;
        return false;
    }

    internal static bool IsPipTitleV378(string title)
    {
        if(string.IsNullOrWhiteSpace(title))return false;
        return title.Equals("Picture in Picture",StringComparison.OrdinalIgnoreCase) ||
               title.Equals("Picture-in-Picture",StringComparison.OrdinalIgnoreCase) ||
               title.IndexOf("Picture-in-Picture",StringComparison.OrdinalIgnoreCase)>=0 ||
               title.IndexOf("Picture in Picture",StringComparison.OrdinalIgnoreCase)>=0;
    }

    private static string StripBrowserTitleSuffixV378(string winTitle)
    {
        string s=(winTitle??"").Replace("\u200B","").Trim();
        foreach(var suffix in new[]{
            " - Google Chrome"," - Chromium"," - Brave"," - Vivaldi"," - Opera"," - Opera GX",
            " — Mozilla Firefox"," - Mozilla Firefox"," - Zen Browser",
            " - Microsoft Edge"," - Personal - Microsoft Edge"," - Work - Microsoft Edge",
            " - VLC media player"})
        {
            if(s.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))
            {
                s=s[..^suffix.Length].Trim();
                break;
            }
        }
        return s;
    }

    internal static bool TitleMatchesMediaV378(string winTitle,string mediaTitle,string mediaArtist,bool strictShort=false)
    {
        string cleanedWin=StripBrowserTitleSuffixV378(winTitle);
        string mt=(mediaTitle??"").Replace("\u200B","").Trim();
        string ma=(mediaArtist??"").Replace("\u200B","").Trim();
        if(string.IsNullOrWhiteSpace(cleanedWin) || string.IsNullOrWhiteSpace(mt))return false;

        if(mt.Length>=5 && (cleanedWin.IndexOf(mt,StringComparison.OrdinalIgnoreCase)>=0 ||
           (!strictShort && cleanedWin.Length>=6 && mt.IndexOf(cleanedWin,StringComparison.OrdinalIgnoreCase)>=0)))
            return true;

        if(cleanedWin.Equals(mt,StringComparison.OrdinalIgnoreCase))return true;
        if(ma.Length>=3 && cleanedWin.IndexOf(ma,StringComparison.OrdinalIgnoreCase)>=0 &&
           cleanedWin.IndexOf(mt,StringComparison.OrdinalIgnoreCase)>=0)
            return true;

        var winSegs=cleanedWin.Split(new[]{" | "," — "," – "," - "},StringSplitOptions.RemoveEmptyEntries);
        var mediaSegs=mt.Split(new[]{" | "," — "," – "," - "},StringSplitOptions.RemoveEmptyEntries);
        foreach(var ws in winSegs)
        {
            string w=ws.Trim();
            if(w.Equals(mt,StringComparison.OrdinalIgnoreCase))return true;
            if(!strictShort && mt.Length>=6 && w.Length>=6 &&
               (w.IndexOf(mt,StringComparison.OrdinalIgnoreCase)>=0 || mt.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0))
                return true;
            foreach(var ms in mediaSegs)
            {
                string m=ms.Trim();
                if(m.Length>=5 && w.IndexOf(m,StringComparison.OrdinalIgnoreCase)>=0)
                    return true;
            }
        }
        return false;
    }

    // Pure monitor ownership resolver: guarantees a session NEVER broadcasts
    // across all monitors when it is playing in a specific window/tab/monitor.
    internal static HashSet<string> SelectMonitorsForSession(
        string sourceAppId,
        string mediaTitle,
        string mediaArtist,
        bool isVideo,
        IReadOnlyList<MediaWindowInfoV378> windows,
        IReadOnlyList<string> connectedMonitors,
        string primaryMonitor,
        string? preferredMonitor=null)
    {
        var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates=windows
            .Where(w=>!string.IsNullOrWhiteSpace(w.MonitorDevice) && WindowMatchesSourceV378(w,sourceAppId))
            .ToList();

        if(candidates.Count==0 && !string.IsNullOrWhiteSpace(mediaTitle))
        {
            candidates=windows
                .Where(w=>!w.Minimized && !string.IsNullOrWhiteSpace(w.MonitorDevice) &&
                    TitleMatchesMediaV378(w.Title,mediaTitle,mediaArtist,strictShort:true))
                .ToList();
        }

        if(candidates.Count==0)
        {
            if(isVideo && connectedMonitors.Count>1)
                return result;
            if(!string.IsNullOrWhiteSpace(primaryMonitor))
                result.Add(primaryMonitor);
            else if(connectedMonitors.Count>0)
                result.Add(connectedMonitors[0]);
            return result;
        }

        var nonMinimized=candidates.Where(w=>!w.Minimized).ToList();
        if(nonMinimized.Count>0)
        {
            var pips=nonMinimized.Where(w=>IsPipTitleV378(w.Title)).ToList();
            if(pips.Count>0)
            {
                foreach(var p in pips)result.Add(p.MonitorDevice);
                return result;
            }

            var titleMatches=nonMinimized
                .Where(w=>TitleMatchesMediaV378(w.Title,mediaTitle,mediaArtist))
                .ToList();
            if(titleMatches.Count>0)
            {
                foreach(var m in titleMatches)result.Add(m.MonitorDevice);
                return result;
            }

            var fullscreens=nonMinimized.Where(w=>w.Fullscreen).ToList();
            if(fullscreens.Count>0)
            {
                foreach(var fs in fullscreens)result.Add(fs.MonitorDevice);
                return result;
            }

            var distinctMonitors=nonMinimized
                .Select(w=>w.MonitorDevice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if(distinctMonitors.Count==1)
            {
                result.Add(distinctMonitors[0]);
                return result;
            }

            if(!string.IsNullOrWhiteSpace(preferredMonitor) &&
               nonMinimized.Any(w=>string.Equals(w.MonitorDevice,preferredMonitor,StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(preferredMonitor);
                return result;
            }

            result.Add(nonMinimized[0].MonitorDevice);
            return result;
        }

        var minTitleMatches=candidates
            .Where(w=>TitleMatchesMediaV378(w.Title,mediaTitle,mediaArtist))
            .ToList();
        if(minTitleMatches.Count>0)
        {
            result.Add(minTitleMatches[0].MonitorDevice);
            return result;
        }

        if(!string.IsNullOrWhiteSpace(preferredMonitor) &&
           candidates.Any(w=>string.Equals(w.MonitorDevice,preferredMonitor,StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(preferredMonitor);
            return result;
        }

        result.Add(candidates[0].MonitorDevice);
        return result;
    }
'@ 'monitor-aware media window matcher and per-host lookup'

Swap-R377 @'
    public DeskSpaceMediaV18()
    {
        ActiveV123=this;
'@ @'
    public DeskSpaceMediaV18()
    {
        lock(SyncV378)
        {
            InstancesV378.Add(this);
            ActiveV123=this;
        }
'@ 'register DeskSpaceMediaV18 instance for multi-monitor lookup'

Swap-R377 @'
        try
        {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session=manager.GetSessions().FirstOrDefault(x=>
                x.GetPlaybackInfo()?.PlaybackStatus==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? manager.GetCurrentSession();
            bool active=session?.GetPlaybackInfo()?.PlaybackStatus==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if(active)lastPlaying=DateTime.UtcNow;
            bool show=session!=null&&(active||
                visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35));
            if(!show)
            {
                current=null;
                if(visible){visible=false;VisibilityChanged?.Invoke(false);}
                return;
            }
            current=session;
            playing=active;
            source=Short(session!.SourceAppUserModelId,42);
            var info=session.GetPlaybackInfo();
            capabilities=info?.Controls;
            shuffle=info?.IsShuffleActive;
            repeat=info?.AutoRepeatMode??MediaPlaybackAutoRepeatMode.None;
            speed=info?.PlaybackRate??1;
            string newTitle="";
            string priorIdentity=source+"|"+title;
            try
            {
                var meta=await session.TryGetMediaPropertiesAsync();
                newTitle=Short(meta.Title,92);
                artist=Short(meta.Artist,72);
                mediaKind=meta.PlaybackType?.ToString()??"Media";
                string identity=source+"|"+newTitle;
                if(artIdentity!=identity)
                {
                    artIdentity=identity;
                    _=LoadArtAsync(meta.Thumbnail,identity);
                }
            }
            catch(Exception){artist="";mediaKind="Media";}
'@ @'
        try
        {
            Window? attachedHost=ownerHostV378;
            string hostDevice=attachedHost!=null?ResolveWindowDeviceV378(attachedHost):"";
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var rawSessions=manager.GetSessions().ToList();
            var currentGlobal=manager.GetCurrentSession();
            GlobalSystemMediaTransportControlsSession? session=null;
            GlobalSystemMediaTransportControlsSessionMediaProperties? prefetchedMeta=null;
            if(string.IsNullOrWhiteSpace(hostDevice))
            {
                session=rawSessions.FirstOrDefault(x=>
                    x.GetPlaybackInfo()?.PlaybackStatus==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    ?? currentGlobal;
            }
            else if(rawSessions.Count>0)
            {
                var allScreens=Forms.Screen.AllScreens;
                var connected=allScreens.Select(s=>s.DeviceName).ToArray();
                var primary=(Forms.Screen.PrimaryScreen??allScreens.FirstOrDefault())?.DeviceName??hostDevice;
                var windowsSnap=await Task.Run(SnapshotMediaWindowsV378);
                if(disposed)return;
                bool withinPauseGrace=visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35);
                var ordered=rawSessions
                    .Select(s=>new{
                        Session=s,
                        Playing=s.GetPlaybackInfo()?.PlaybackStatus==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        IsCurrentGlobal=currentGlobal!=null&&
                            string.Equals(s.SourceAppUserModelId,currentGlobal.SourceAppUserModelId,StringComparison.OrdinalIgnoreCase)
                    })
                    .OrderByDescending(x=>x.Playing)
                    .ThenByDescending(x=>x.IsCurrentGlobal)
                    .ToList();
                foreach(var item in ordered)
                {
                    bool matchesPrevPaused=!item.Playing&&withinPauseGrace&&
                        string.Equals(item.Session.SourceAppUserModelId,currentSourceIdV378,StringComparison.OrdinalIgnoreCase);
                    if(!item.Playing&&!matchesPrevPaused)continue;
                    GlobalSystemMediaTransportControlsSessionMediaProperties? meta=null;
                    string rawTitle="",rawArtist="";
                    bool isVideo=false;
                    try
                    {
                        meta=await item.Session.TryGetMediaPropertiesAsync();
                        rawTitle=meta?.Title??"";
                        rawArtist=meta?.Artist??"";
                        isVideo=meta?.PlaybackType==MediaPlaybackType.Video;
                    }
                    catch(Exception){}
                    string? preferred=GetPreferredMonitorV378(item.Session.SourceAppUserModelId);
                    var owners=SelectMonitorsForSession(
                        item.Session.SourceAppUserModelId,rawTitle,rawArtist,isVideo,
                        windowsSnap,connected,primary,preferred);
                    if(owners.Count==1)
                        SetPreferredMonitorV378(item.Session.SourceAppUserModelId,owners.First());
                    if(owners.Contains(hostDevice))
                    {
                        session=item.Session;
                        prefetchedMeta=meta;
                        break;
                    }
                }
            }
            bool active=session?.GetPlaybackInfo()?.PlaybackStatus==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if(active)lastPlaying=DateTime.UtcNow;
            bool show=session!=null&&(active||
                visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35));
            if(!show)
            {
                current=null;playing=false;currentSourceIdV378="";
                if(!string.IsNullOrWhiteSpace(hostDevice)){title="";artist="";}
                if(visible){visible=false;VisibilityChanged?.Invoke(false);}
                return;
            }
            current=session;
            currentSourceIdV378=session!.SourceAppUserModelId??"";
            playing=active;
            source=Short(session!.SourceAppUserModelId,42);
            var info=session.GetPlaybackInfo();
            capabilities=info?.Controls;
            shuffle=info?.IsShuffleActive;
            repeat=info?.AutoRepeatMode??MediaPlaybackAutoRepeatMode.None;
            speed=info?.PlaybackRate??1;
            string newTitle="";
            string priorIdentity=source+"|"+title;
            try
            {
                var meta=prefetchedMeta??await session.TryGetMediaPropertiesAsync();
                if(meta!=null)
                {
                    newTitle=Short(meta.Title,92);
                    artist=Short(meta.Artist,72);
                    mediaKind=meta.PlaybackType?.ToString()??"Media";
                    string identity=source+"|"+newTitle;
                    if(artIdentity!=identity)
                    {
                        artIdentity=identity;
                        _=LoadArtAsync(meta.Thumbnail,identity);
                    }
                }
            }
            catch(Exception){artist="";mediaKind="Media";}
'@ 'filter GSMTC playback sessions per monitor in PollAsync'

Swap-R377 @'
    private void KeepVisible()
    {
        if(host==null||popup==null)return;
        var work=SystemParameters.WorkArea;
        double width=frame?.Width??256;
        double height=view==Layout.Pill?76:view==Layout.Advanced?240:156;
        offsetX=Math.Clamp(offsetX,work.Left-host.Left+7,
            Math.Max(work.Left-host.Left+7,work.Right-host.Left-width-7));
        offsetY=Math.Clamp(offsetY,work.Top-host.Top+7,
            Math.Max(work.Top-host.Top+7,work.Bottom-host.Top-height-7));
        popup.HorizontalOffset=offsetX;popup.VerticalOffset=offsetY;
    }
'@ @'
    private void KeepVisible()
    {
        if(host==null||popup==null)return;
        Rect work=SystemParameters.WorkArea;
        try
        {
            var src=PresentationSource.FromVisual(host);
            double sx=src?.CompositionTarget?.TransformToDevice.M11??1;
            double sy=src?.CompositionTarget?.TransformToDevice.M22??1;
            if(sx<=0.1||double.IsNaN(sx))sx=1;
            if(sy<=0.1||double.IsNaN(sy))sy=1;
            Drawing.Rectangle area;
            if(host is WidgetShellWindow shell && DeskSpaceMonitorFenceV152.BoundScreen(shell) is Forms.Screen bound)
                area=bound.WorkingArea;
            else
            {
                var hwnd=new System.Windows.Interop.WindowInteropHelper(host).Handle;
                area=(hwnd!=IntPtr.Zero?Forms.Screen.FromHandle(hwnd):Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0]).WorkingArea;
            }
            work=new Rect(area.Left/sx,area.Top/sy,area.Width/sx,area.Height/sy);
        }
        catch{}
        double width=frame?.Width??256;
        double height=view==Layout.Pill?76:view==Layout.Advanced?240:156;
        offsetX=Math.Clamp(offsetX,work.Left-host.Left+7,
            Math.Max(work.Left-host.Left+7,work.Right-host.Left-width-7));
        offsetY=Math.Clamp(offsetY,work.Top-host.Top+7,
            Math.Max(work.Top-host.Top+7,work.Bottom-host.Top-height-7));
        popup.HorizontalOffset=offsetX;popup.VerticalOffset=offsetY;
    }
'@ 'clamp standalone media popup onto own physical monitor work area'

Swap-R377 @'
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!visible||dismissedV123)return;
'@ @'
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!visible||dismissedV123)return;
        if(ownerHostV378!=null && !ReferenceEquals(ownerHostV378,window))return;
'@ 'prevent cross-host floating ShowBelow'

Swap-R377 @'
    public void Dispose()
    {
        disposed=true;poller.Stop();CloseBelow();
        if(ReferenceEquals(ActiveV123,this))ActiveV123=null;
        manager=null;current=null;VisibilityChanged=null;
    }
'@ @'
    public void Dispose()
    {
        disposed=true;poller.Stop();CloseBelow();
        lock(SyncV378)
        {
            InstancesV378.Remove(this);
            if(ReferenceEquals(ActiveV123,this))
                ActiveV123=InstancesV378.LastOrDefault(x=>!x.disposed);
        }
        manager=null;current=null;VisibilityChanged=null;
    }
'@ 'remove instance from multi-monitor list on Dispose'

Finish-Patch $mediaPath


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
if($deckFinal.Contains('"Excel paste"') -or
   $deckFinal.Contains('Paste to Excel · Clipboard & mappings') -or
   !$deckFinal.Contains('DeskSpaceMediaV18.ForHostV378(host)') -or
   !$deckFinal.Contains('BindExcel(previewClip,row,clip')){
    throw 'R377 Deck Excel paste removal, clipboard hover binding, or monitor media wiring missing'
}
$activeFinal=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelActiveV122.cs')
if($activeFinal.Contains('AnyExcelOpen') -or $bridgeFinal.Contains('AccessibleObjectFromWindow')){
    throw 'R377 extra 1142 Excel discovery code should remain reverted to v1.5.1141'
}
if(([regex]::Matches($deckFinal,'DeckHeaderButton[(]')).Count -ne 4){
    throw 'R377 broke R308 header button count invariant (must equal 4)'
}
if(!$hostFinal.Contains('FullscreenDetector.IsMonitorFullscreen(this)') -or
   !$hostFinal.Contains('CoversMonitor(') -or
   !$hostFinal.Contains('media.BindHostV378(this);') -or
   !$hostFinal.Contains('PlaceEdgeHandleNative(')){
    throw 'R377 per-monitor fullscreen detector, media host bind, or edge dock placement missing'
}
$mediaFinal=[IO.File]::ReadAllText($mediaPath)
if(!$mediaFinal.Contains('ForHostV378(') -or !$mediaFinal.Contains('SelectMonitorsForSession(')){
    throw 'R377 monitor-aware Media Player logic missing from DeskSpaceMediaV18'
}

Write-Host 'R377 PASS: Excel reverted cleanly, Excel paste removed from Deck, and Media Player + widgets are monitor-aware.'
