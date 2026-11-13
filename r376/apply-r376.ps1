$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R376 - dock Excel Clipboard inside Deck DropShelf and share across MultiWidget.
# Apply after R375.

$clipPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelClipboardV39.cs'
if(Test-Path -LiteralPath $clipPath){
    $nl=[string][char]10
    $c=[IO.File]::ReadAllText($clipPath).Replace([string][char]13+$nl, $nl)

    $od='    private readonly StackPanel list;
    private readonly List<DeskSpaceExcelTemplateV16> clips=new();
    private HwndSource? source;
    private IntPtr hwnd;
    private bool disposed,ownClipboardWrite,manualOpen,userHidden,collapsed,dragging,customPosition,captureBusy;'
    $nd='    private readonly StackPanel list;
    internal static readonly List<DeskSpaceExcelTemplateV16> SharedClips=new();
    internal static event Action? ClipsChanged;
    internal static void NotifyChanged()=>ClipsChanged?.Invoke();
    private HwndSource? source;
    private IntPtr hwnd;
    private static bool ownClipboardWrite;
    private bool disposed,manualOpen,userHidden,collapsed,dragging,customPosition,captureBusy;'
    if($c.Contains($od)){ $c=$c.Replace($od,$nd) }

    $oc='            if(clips.Count>0 && clips[0].SourceWorkbook==entry.SourceWorkbook &&
               clips[0].SourceSheet==entry.SourceSheet &&
               clips[0].SourceAddress==entry.SourceAddress &&
               clips[0].Cells.Count==entry.Cells.Count &&
               clips[0].Cells.Zip(entry.Cells,(x,y)=>x.RawValue==y.RawValue).All(x=>x))
                return;
            clips.Insert(0,entry);
            if(clips.Count>8)clips.RemoveAt(clips.Count-1);
            Render();'
    $nc='            if(SharedClips.Count>0 && SharedClips[0].SourceWorkbook==entry.SourceWorkbook &&
               SharedClips[0].SourceSheet==entry.SourceSheet &&
               SharedClips[0].SourceAddress==entry.SourceAddress &&
               SharedClips[0].Cells.Count==entry.Cells.Count &&
               SharedClips[0].Cells.Zip(entry.Cells,(x,y)=>x.RawValue==y.RawValue).All(x=>x))
                return;
            SharedClips.Insert(0,entry);
            if(SharedClips.Count>8)SharedClips.RemoveAt(SharedClips.Count-1);
            NotifyChanged();
            Render();'
    if($c.Contains($oc)){ $c=$c.Replace($oc,$nc) }

    $ov='    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(!disposed)popup.IsOpen=!userHidden && (bool)e.NewValue && (clips.Count>0||manualOpen);}'
    $nv='    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(!disposed)popup.IsOpen=false;}'
    if($c.Contains($ov)){ $c=$c.Replace($ov,$nv) }

    $c=$c.Replace('    private static string Tsv(','    internal static string Tsv(')
    $c=$c.Replace('    private void CopyPlain(','    internal static void CopyPlain(')
    $c=$c.Replace('    private async void PasteExcel(','    internal static async void PasteExcel(')

    $or='        Reposition(this,EventArgs.Empty);
        popup.IsOpen=!userHidden && host.IsVisible;
    }'
    $nr='        Reposition(this,EventArgs.Empty);
        popup.IsOpen=false;
    }'
    if($c.Contains($or)){ $c=$c.Replace($or,$nr) }
    $c=$c.Replace('disposed=true;clips.Clear();','disposed=true;')

    [IO.File]::WriteAllText($clipPath,$c,[Text.UTF8Encoding]::new($false))
}

$deckPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(Test-Path -LiteralPath $deckPath){
    $nl=[string][char]10
    $d=[IO.File]::ReadAllText($deckPath).Replace([string][char]13+$nl, $nl)

    $of='    private bool excelExpandedV122=true,excelRefreshingV122;'
    $nf='    private bool excelClipsExpandedV376=true;
    private static void OnClipsChangedV376(){RefreshViews();}
    private bool excelExpandedV122=true,excelRefreshingV122;'
    if($d.Contains($of)){ $d=$d.Replace($of,$nf) }

    $oc='        DeskSpaceSmartContextDeckV280.Changed+=ContextChangedV280;'
    $nc='        DeskSpaceSmartContextDeckV280.Changed+=ContextChangedV280;
        DeskSpaceExcelClipboardV39.ClipsChanged+=OnClipsChangedV376;'
    if($d.Contains($oc)){ $d=$d.Replace($oc,$nc) }

    $od='        DeskSpaceSmartContextDeckV280.Changed-=ContextChangedV280;'
    $nd='        DeskSpaceSmartContextDeckV280.Changed-=ContextChangedV280;
        if(Views.Count==0)DeskSpaceExcelClipboardV39.ClipsChanged-=OnClipsChangedV376;'
    if($d.Contains($od)){ $d=$d.Replace($od,$nd) }

    $os='.Concat(excelMatchesV122.Select(x=>x.Id.ToString()))'
    $ns='.Concat(excelMatchesV122.Select(x=>x.Id.ToString()))
            .Concat(DeskSpaceExcelClipboardV39.SharedClips.Select(x=>x.SourceSheet+":"+x.SourceAddress))'
    if($d.Contains($os)){ $d=$d.Replace($os,$ns) }

    $oct='held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count>0 ||'
    $nct='held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count+DeskSpaceExcelClipboardV39.SharedClips.Count>0 ||'
    if($d.Contains($oct)){ $d=$d.Replace($oct,$nct) }

    $ora='        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,'
    $nra='        var excelClipGroup=DeskSpaceShelfGroupsV88.Add(list,
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
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,'

    if($d.Contains($ora)){ $d=$d.Replace($ora,$nra) }

    [IO.File]::WriteAllText($deckPath,$d,[Text.UTF8Encoding]::new($false))
}
Write-Host 'R376 PASS: Excel Clipboard successfully docked inside Deck DropShelf and synchronized across MultiWidget.'
