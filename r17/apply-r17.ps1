$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelV16.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R17 Excel selection anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
Must '    private ListBox templates=null!,previewRows=null!;' @'
    private ListBox templates=null!,previewRows=null!,selectedCells=null!;
    private TextBlock cellSelectionInfo=null!;
    private DeskSpaceExcelTemplateV16? pendingSelection;
'@
Must @'
        body.Children.Add(Buttons(
            ActionButton("Capture selected Excel cells",()=>Run(Capture))
        ));
        body.Children.Add(Caption("2 · Saved reusable information",15,true));
'@ @'
        body.Children.Add(Buttons(
            ActionButton("Capture selected Excel cells",()=>Run(Capture))
        ));
        body.Children.Add(Caption("Choose exactly which captured values to save",12,true));
        selectedCells=new ListBox {
            MinHeight=95,Height=160,Margin=new Thickness(2,3,2,6),
            BorderBrush=new SolidColorBrush(Color.FromRgb(197,201,219))
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(selectedCells,ScrollBarVisibility.Disabled);
        body.Children.Add(selectedCells);
        cellSelectionInfo=Caption("Capture a range first, then tick only the data you need.",12);
        cellSelectionInfo.Foreground=muted;
        body.Children.Add(cellSelectionInfo);
        body.Children.Add(Buttons(
            ActionButton("Select all",()=>ToggleCaptured(true)),
            ActionButton("Select none",()=>ToggleCaptured(false)),
            ActionButton("Save checked values",()=>Run(SaveCheckedCells))
        ));
        body.Children.Add(Caption("2 · Saved reusable information",15,true));
'@
$begin=$s.IndexOf('    private void Capture()', [StringComparison]::Ordinal)
$end=$s.IndexOf('    private void Delete()', [Math]::Max(0,$begin), [StringComparison]::Ordinal)
if($begin -lt 0 -or $end -le $begin){throw 'R17 cannot locate original Capture method'}
$newMethod=@'
    private static string CellReference(int row,int column)
    {
        var letters="";
        while(column>0)
        {
            var digit=(column-1)%26;
            letters=(char)('A'+digit)+letters;
            column=(column-1)/26;
        }
        return letters+row.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    private void ToggleCaptured(bool value)
    {
        if(selectedCells==null)return;
        foreach(var checkbox in selectedCells.Items.OfType<CheckBox>())
            checkbox.IsChecked=value;
        if(pendingSelection!=null)
            cellSelectionInfo.Text=selectedCells.Items.OfType<CheckBox>()
                .Count(x=>x.IsChecked==true)+" of "+
                pendingSelection.Cells.Count+" captured cells selected.";
    }
    private void Capture()
    {
        var candidate=DeskSpaceExcelBridgeV16.Capture(name.Text,range.Text,
            formulas.IsChecked==true);
        if(screenshotBytes!=null)
        {
            candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
            candidate.ScreenshotSelection=Region();
        }
        pendingSelection=candidate;
        selectedCells.Items.Clear();
        foreach(var cell in candidate.Cells)
        {
            var address=CellReference(candidate.SourceRow+cell.RowOffset,
                candidate.SourceColumn+cell.ColumnOffset);
            var checkbox=new CheckBox{
                Tag=cell,IsChecked=true,
                Content=address+"  ·  "+cell.Display+
                    (cell.ValueKind=="formula"?"  [formula]":""),
                FontSize=12,Foreground=ink,
                Margin=new Thickness(5,4,4,4),
                ToolTip="Choose whether to store this exact Excel cell and value"
            };
            checkbox.Checked+=(_,_)=>UpdateSelectionCount();
            checkbox.Unchecked+=(_,_)=>UpdateSelectionCount();
            selectedCells.Items.Add(checkbox);
        }
        UpdateSelectionCount();
        status.Text="Captured "+candidate.Cells.Count+
            " exact cell values. Review which ones you want to save; "+
            "nothing has been added to the encrypted mapping store yet.";
    }
    private void UpdateSelectionCount()
    {
        if(pendingSelection==null||cellSelectionInfo==null)return;
        cellSelectionInfo.Text=selectedCells.Items.OfType<CheckBox>()
            .Count(x=>x.IsChecked==true)+" of "+
            pendingSelection.Cells.Count+" captured cells selected for saving.";
    }
    private void SaveCheckedCells()
    {
        if(pendingSelection==null)
            throw new InvalidOperationException(
                "Capture a selected Excel range before saving.");
        var chosen=selectedCells.Items.OfType<CheckBox>()
            .Where(x=>x.IsChecked==true)
            .Select(x=>x.Tag).OfType<DeskSpaceExcelCellV16>().ToList();
        if(chosen.Count==0)
            throw new InvalidOperationException("Select at least one cell.");
        pendingSelection.Cells=chosen;
        saved.Add(pendingSelection);
        DeskSpaceExcelStoreV16.Save(saved);
        var added=pendingSelection;
        pendingSelection=null;
        selectedCells.Items.Clear();
        cellSelectionInfo.Text="Mapping saved. Select another Excel range to capture more.";
        Refresh(added.Id);
        status.Text="Saved "+chosen.Count+" checked cells with their original "+
            "worksheet, row and column positions. Unchecked cells were excluded.";
    }
'@
$s=$s.Substring(0,$begin)+$newMethod+[Environment]::NewLine+$s.Substring($end)
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R17: per-cell review and explicit Save checked values, keeping exact Excel positions.'
