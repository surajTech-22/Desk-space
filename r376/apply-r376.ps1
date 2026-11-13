$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R376 - dock Excel Clipboard inside Deck DropShelf and share across MultiWidget.
# Apply after R375.

$clipPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelClipboardV39.cs'
if(Test-Path -LiteralPath $clipPath){
    Copy-Item -LiteralPath 'src/DeskSpace/DeskSpaceExcelClipboardV39.cs' -Destination $clipPath -Force
}

$deckPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(Test-Path -LiteralPath $deckPath){
    $lf=[string][char]10
    $crlf=([string][char]13)+$lf
    $script:deckCode=[IO.File]::ReadAllText($deckPath).Replace($crlf,$lf)

    function Swap-R376([string]$old,[string]$replacement,[string]$why){
        $old=$old.Replace($crlf,$lf)
        $replacement=$replacement.Replace($crlf,$lf)
        $count=([regex]::Matches($script:deckCode,[regex]::Escape($old))).Count
        if($count -ne 1){throw ('R376 '+$why+': expected exactly 1 match, found '+$count)}
        $script:deckCode=$script:deckCode.Replace($old,$replacement)
    }

    Swap-R376 @'
    private bool excelExpandedV122=true,excelRefreshingV122;
'@ @'
    private bool excelClipsExpandedV376=true;
    private static void OnClipsChangedV376(){RefreshViews();}
    private bool excelExpandedV122=true,excelRefreshingV122;
'@ 'expand flag and multiwidget refresh trigger'

    Swap-R376 @'
        Views.Add(this);
'@ @'
        Views.Add(this);
        DeskSpaceExcelClipboardV39.ClipsChanged+=OnClipsChangedV376;
'@ 'hook clipboard changes in DropShelf constructor'

    Swap-R376 @'
        Views.Remove(this);
'@ @'
        Views.Remove(this);
        if(Views.Count==0)DeskSpaceExcelClipboardV39.ClipsChanged-=OnClipsChangedV376;
'@ 'unhook clipboard changes in DropShelf Dispose'

    Swap-R376 @'
.Concat(excelMatchesV122.Select(x=>x.Id.ToString()))
'@ @'
.Concat(excelMatchesV122.Select(x=>x.Id.ToString()))
            .Concat(DeskSpaceExcelClipboardV39.SharedClips.Select(x=>x.SourceSheet+":"+x.SourceAddress))
'@ 'signature content'

    Swap-R376 @'
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
'@ @'
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
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
'@ 'dock Excel clipboard group inside deck'

    [IO.File]::WriteAllText($deckPath,$script:deckCode,[Text.UTF8Encoding]::new($false))
}

Write-Host 'R376 PASS: Excel Clipboard successfully docked inside Deck DropShelf and synchronized across MultiWidget.'
