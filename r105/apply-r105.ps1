$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r105/DeskSpaceExcelSheetViewV105.cs' -Destination "$r/DeskSpaceExcelSheetViewV101.cs" -Force
Copy-Item -LiteralPath 'r105/DeskSpaceStableHoverV105.cs' -Destination "$r/DeskSpaceStableHoverV93.cs" -Force
Copy-Item -LiteralPath 'r105/DeskSpaceDualChannelUpdaterV105.cs' -Destination "$r/DeskSpacePublicUpdaterV44.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$tag)
{
  $nl=[string][char]13+[string][char]10
  $s=[IO.File]::ReadAllText($path).Replace($nl,[string][char]10)
  $before=$before.Replace($nl,[string][char]10)
  $after=$after.Replace($nl,[string][char]10)
  $count=([regex]::Matches($s,[regex]::Escape($before))).Count
  if($count -ne 1){throw "R105 $($tag): expected 1 anchor, found $count"}
  [IO.File]::WriteAllText($path,$s.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
        Assert(windowPopup!=null &&
            windowPopup.Placement==
                System.Windows.Controls.Primitives.PlacementMode.Right &&
            ReferenceEquals(windowPopup.PlacementTarget,iconCard),
'@ @'
        Assert(windowPopup!=null &&
            windowPopup.Placement==
                System.Windows.Controls.Primitives.PlacementMode.Custom &&
            ReferenceEquals(windowPopup.PlacementTarget,iconCard),
'@ 'custom side popup'
Swap $smoke @'
        Assert(windowPopup.PopupAnimation==
            System.Windows.Controls.Primitives.PopupAnimation.None,
'@ @'
        var flyoutX=Product.GetType("DeskSpace.DeskSpaceStableHoverV93",true)!.
            GetMethod("WindowFlyoutX",BindingFlags.Static|BindingFlags.NonPublic)!;
        double X(bool side,double width,double pop,double gap)=>
            (double)flyoutX.Invoke(null,new object[]{side,width,pop,gap})!;
        Assert(X(true,220,260,4)>=224 && X(false,220,260,4)<=-264,
            "R105 running-app window list must be fully outside the app icon grid");
        Assert(windowPopup.PopupAnimation==
            System.Windows.Controls.Primitives.PopupAnimation.None,
'@ 'pure left/right placement test'
Swap $smoke @'
            TestExpandedExcelStyleViewerV103();
'@ @'
            TestExpandedExcelStyleViewerV103();
            TestPerCellFormattingV105();
'@ 'add per-cell visual regression'
Swap $smoke @'
    private static void TestCompactShelfGroupsV88()
'@ @'
    private static void TestPerCellFormattingV105()
    {
        var modelType=Product.GetType("DeskSpace.DeskSpaceExcelTemplateV16",true)!;
        var visualType=Product.GetType("DeskSpace.DeskSpaceExcelSheetVisualV101",true)!;
        var cellType=Product.GetType("DeskSpace.DeskSpaceExcelStyledCellV101",true)!;
        var viewer=Product.GetType("DeskSpace.DeskSpaceExcelSheetViewV101",true)!;
        var model=Activator.CreateInstance(modelType)!;
        modelType.GetProperty("SourceSheet")!.SetValue(model,"Per-cell test");
        modelType.GetProperty("SourceAddress")!.SetValue(model,"$A$1:$B$2");
        modelType.GetProperty("SourceRow")!.SetValue(model,1);
        modelType.GetProperty("SourceColumn")!.SetValue(model,1);
        var visual=Activator.CreateInstance(visualType)!;
        visualType.GetProperty("Rows")!.SetValue(visual,2);
        visualType.GetProperty("Columns")!.SetValue(visual,2);
        visualType.GetProperty("RowHeights")!.SetValue(visual,new List<double>{32,36});
        visualType.GetProperty("ColumnWidths")!.SetValue(visual,new List<double>{140,165});
        var colors=new[]{0x00FFFF,0xFFCC99,0x99FF99,0xFFFFFF};
        var formats=new[]{"0.00","dd/mm/yyyy","@","0%"};
        var all=(System.Collections.IList)visualType.GetProperty("Cells")!.GetValue(visual)!;
        for(int i=0;i<4;i++)
        {
            var cell=Activator.CreateInstance(cellType)!;
            cellType.GetProperty("Row")!.SetValue(cell,i/2);
            cellType.GetProperty("Column")!.SetValue(cell,i%2);
            cellType.GetProperty("Address")!.SetValue(cell,
                (i%2==0?"A":"B")+(i/2+1));
            cellType.GetProperty("FillBgr")!.SetValue(cell,colors[i]);
            cellType.GetProperty("FontBgr")!.SetValue(cell,i==2?255:0);
            cellType.GetProperty("NumberFormat")!.SetValue(cell,formats[i]);
            cellType.GetProperty("BorderTop")!.SetValue(cell,i==0);
            cellType.GetProperty("BorderBottom")!.SetValue(cell,i==1);
            cellType.GetProperty("BorderLeft")!.SetValue(cell,i==2);
            cellType.GetProperty("BorderRight")!.SetValue(cell,i==3);
            cellType.GetProperty("StoredType")!.SetValue(cell,"Number");
            cellType.GetProperty("ExpectedType")!.SetValue(cell,i==1?"Date":"Number");
            all.Add(cell);
        }
        Assert(all.Count==4 &&
            Enumerable.Range(0,4).Select(i=>
                (int)cellType.GetProperty("FillBgr")!.GetValue(all[i]!)!).Distinct().Count()==4,
            "R105 four individual Excel cells must keep distinct style data");
        modelType.GetProperty("SheetVisualV101")!.SetValue(model,visual);
        var rendered=(FrameworkElement)viewer.GetMethod("Create",
            BindingFlags.Static|BindingFlags.NonPublic)!.
            Invoke(null,new[]{model})!;
        Render(rendered,1000,500,"r105-four-independent-excel-cell-styles");
        var displayed=Walk(rendered).OfType<Border>().Where(b=>
            b.Background is SolidColorBrush bg && colors.Contains(
                bg.Color.B<<16 | bg.Color.G<<8 | bg.Color.R)).Count();
        Assert(displayed>=4,
            "R105 four cell-specific fills must appear independently in WPF");
        var merge=viewer.GetMethod("MergeCapturedVisuals",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var merged=merge.Invoke(null,new object?[]{visual,0,0,null,0,0,2,2})!;
        var persisted=(System.Collections.IList)visualType.GetProperty("Cells")!.
            GetValue(merged)!;
        Assert(persisted.Count==4 && Enumerable.Range(0,4).All(i=>
            formats.Contains((string)cellType.GetProperty("NumberFormat")!.
                GetValue(persisted[i]!)!)),
            "R105 saving and merging must not collapse distinct cell formats");
        Console.WriteLine("R105 PASS: per-cell styles, visual fill, border, merge, side-only flyout.");
    }
    private static void TestCompactShelfGroupsV88()
'@ 'real WPF 4-cell screenshot'
Write-Host 'R105 applied: independent selected-cell Excel formatting and side-only Running Apps window popup.'
