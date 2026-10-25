$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function ReplaceOnce([string]$path,[string]$before,[string]$after,[string]$label)
{
    $crlf=[string][char]13+[string][char]10
    $lf=[string][char]10
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $n=([regex]::Matches($s,[regex]::Escape($before))).Count
    if($n -ne 1){throw "R117 $($label) expected once but found $n"}
    [IO.File]::WriteAllText($path,$s.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
$previous=@'
        if(entry.Cells.Count==0 &&
           !entry.SheetVisualV101.Cells.Any(x=>
               x.NumberFormat!="General" || x.Bold || x.Italic ||
               x.FillBgr!=0xFFFFFF || x.FontBgr!=0 ||
               x.BorderTop || x.BorderBottom ||
               x.BorderLeft || x.BorderRight ||
               x.MergeRows>1 || x.MergeColumns>1))
            throw new InvalidOperationException(
                "The selected range is completely empty and unformatted. "+
                "Choose a formatted template range.");
        return entry;
'@
$corrected=@'
        // A formatting template stores the SELECTED PHYSICAL CELLS, their
        // geometry, dimensions and direct styles, even if their values are
        // all blank. The previous heuristic rejected legitimate empty
        // formats (alignment, underline, row height, or default styles).
        // Never require source values or artificial placeholder records.
        if(entry.SheetVisualV101.Cells.Count!=rows*cols ||
           entry.SheetVisualV101.Rows!=rows ||
           entry.SheetVisualV101.Columns!=cols)
            throw new InvalidDataException(
                "Incomplete formatting-only Excel capture; nothing saved.");
        return entry;
'@
ReplaceOnce $bridge $previous $corrected 'allow empty data-free format capture'
$hover="$root/DeskSpaceStableHoverV93.cs"
ReplaceOnce $hover @'
        var iconRow=new WrapPanel{Margin=new Thickness(3)};
'@ @'
        // Explicit THREE-column coordinates, never a wrapping row.
        // The final single-window app belongs in the middle of row two,
        // not the left side as it was in the old WrapPanel.
        var iconRow=new Grid{Margin=new Thickness(3),Width=234};
        for(int col=0;col<3;col++)
            iconRow.ColumnDefinitions.Add(new ColumnDefinition{
                Width=new GridLength(78)});
'@ 'replace wrap layout with fixed three-column grid'
$helper=@'
    // Real two-dimensional layout: grouped multi-window apps occupy the
    // outside columns, and remaining one-window apps fill central slots.
    // Only if ALL apps have one window do we use simple alphabetical rows.
    internal readonly record struct AppGridSlotV117(
        string Name,int Row,int Column);

    internal static AppGridSlotV117[] PlanAppGridV117(
        IEnumerable<(string Name,int Count)> groups,bool outwardRight)
    {
        var all=groups.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(all.Length==0)return Array.Empty<AppGridSlotV117>();
        int rows=(all.Length+2)/3;
        var multi=all.Where(x=>x.Count>1)
            .OrderByDescending(x=>x.Count)
            .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        var single=all.Where(x=>x.Count<=1).ToArray();
        var placements=new List<AppGridSlotV117>(all.Length);
        if(multi.Length==0)
        {
            for(int i=0;i<single.Length;i++)
                placements.Add(new(single[i].Name,i/3,i%3));
            return placements.ToArray();
        }
        var occupied=new HashSet<(int Row,int Column)>();
        var edgeSlots=new List<(int Row,int Column)>();
        for(int r=0;r<rows;r++)
        {
            edgeSlots.Add((r,outwardRight?2:0));
            edgeSlots.Add((r,outwardRight?0:2));
        }
        for(int i=0;i<multi.Length;i++)
        {
            var slot=i<edgeSlots.Count?edgeSlots[i]:
                (Row:i-edgeSlots.Count,Column:1);
            occupied.Add(slot);
            placements.Add(new(multi[i].Name,slot.Row,slot.Column));
        }
        var middle=Enumerable.Range(0,rows).Select(r=>(Row:r,Column:1))
            .Where(slot=>!occupied.Contains(slot));
        var edges=Enumerable.Range(0,rows)
            .SelectMany(r=>new[]{(Row:r,Column:0),(Row:r,Column:2)})
            .Where(slot=>!occupied.Contains(slot));
        var emptySlots=middle.Concat(edges).ToArray();
        if(emptySlots.Length<single.Length)
            throw new InvalidOperationException("Running Apps grid capacity mismatch.");
        for(int i=0;i<single.Length;i++)
            placements.Add(new(single[i].Name,emptySlots[i].Row,
                emptySlots[i].Column));
        return placements.ToArray();
    }

'@
ReplaceOnce $hover '    internal static Button MakeCloseButton(' ($helper+'    internal static Button MakeCloseButton(') 'add row/column placement planner'
$old=@'
        // Updating order never recreates tiles, HWNDs, or the menu.
        void OrderIconTiles(bool outwardRight,
            IReadOnlyList<DeskSpaceRunningAppsV88.Entry> current)
        {
            var appOrder=OrderAppNamesV115(appTiles.Keys.Select(name=>
                (Name:name,Count:current.Count(w=>
                    string.Equals(w.Name,name,StringComparison.OrdinalIgnoreCase)))),
                outwardRight);
            int index=0;
            foreach(var name in appOrder)
                if(appTiles.TryGetValue(name,out var tile))
                {
                    int at=iconRow.Children.IndexOf(tile);
                    if(at>=0 && at!=index)
                    {
                        iconRow.Children.RemoveAt(at);
                        iconRow.Children.Insert(index,tile);
                    }
                    index++;
                }
        }
'@
$new=@'
        // Assign true row+column locations in-place; do NOT detach/reinsert
        // hovered elements or shift their hit targets while a flyout is open.
        void OrderIconTiles(bool outwardRight,
            IReadOnlyList<DeskSpaceRunningAppsV88.Entry> current)
        {
            var planned=PlanAppGridV117(appTiles.Keys.Select(name=>
                (Name:name,Count:current.Count(w=>
                    string.Equals(w.Name,name,StringComparison.OrdinalIgnoreCase)))),
                outwardRight);
            int rows=planned.Length==0?0:planned.Max(x=>x.Row)+1;
            while(iconRow.RowDefinitions.Count<rows)
                iconRow.RowDefinitions.Add(new RowDefinition{
                    Height=GridLength.Auto});
            while(iconRow.RowDefinitions.Count>rows)
                iconRow.RowDefinitions.RemoveAt(
                    iconRow.RowDefinitions.Count-1);
            foreach(var slot in planned)
                if(appTiles.TryGetValue(slot.Name,out var tile))
                {
                    Grid.SetRow(tile,slot.Row);
                    Grid.SetColumn(tile,slot.Column);
                }
        }
'@
ReplaceOnce $hover $old $new 'replace flat sort with stable grid coordinates'
$smoke='r36/UiSmoke/Program.cs'
ReplaceOnce $smoke @'
            TestRunningAppsFourWayV115();
'@ @'
            TestRunningAppsFourWayV115();
            TestRunningAppsGridV117();
'@ 'add actual grid-position regression test'
$test=@'
    private static void TestRunningAppsGridV117()
    {
        var cls=Product.GetType("DeskSpace.DeskSpaceStableHoverV93",true)!;
        var plan=cls.GetMethod("PlanAppGridV117",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object[] Slots(bool outward,params (string Name,int Count)[] groups)=>
            ((System.Collections.IEnumerable)plan.Invoke(null,
                new object[]{groups,outward})!).Cast<object>().ToArray();
        string Name(object slot)=>(string)slot.GetType().
            GetProperty("Name")!.GetValue(slot)!;
        int Row(object slot)=>(int)slot.GetType().
            GetProperty("Row")!.GetValue(slot)!;
        int Col(object slot)=>(int)slot.GetType().
            GetProperty("Column")!.GetValue(slot)!;
        var actual=Slots(false,("File Explorer",3),("Accounting",1),
            ("Excel",2),("Chrome",1));
        object At(string name)=>actual.Single(x=>Name(x)==name);
        Assert(Row(At("File Explorer"))==0 && Col(At("File Explorer"))==0,
            "R117 Explorer (3 windows) must occupy left edge");
        Assert(Row(At("Excel"))==0 && Col(At("Excel"))==2,
            "R117 Excel (2 windows) must occupy right edge");
        Assert(Row(At("Accounting"))==0 && Col(At("Accounting"))==1,
            "R117 one-window Accounting must occupy first center");
        Assert(Row(At("Chrome"))==1 && Col(At("Chrome"))==1,
            "R117 overflow Chrome must occupy SECOND-ROW CENTER, never left");
        var reverse=Slots(true,("File Explorer",3),("Accounting",1),
            ("Excel",2),("Chrome",1));
        Assert(Col(reverse.Single(x=>Name(x)=="File Explorer"))==2 &&
               Col(reverse.Single(x=>Name(x)=="Excel"))==0,
            "R117 outward-right placement mirrors multi-window edges");
        var singles=Slots(false,("File Explorer",1),("Accounting",1),
            ("Excel",1),("Chrome",1)).
            OrderBy(Row).ThenBy(Col).Select(Name).ToArray();
        Assert(singles.SequenceEqual(new[]{"Accounting","Chrome","Excel",
            "File Explorer"}),"R117 all singles remain alphabetically ordered");
        Console.WriteLine("R117 PASS: edge multis, centered overflow, mirrored grid and alphabetical singles.");
    }

'@
ReplaceOnce $smoke '    private static void TestRunningAppsFourWayV115()' ($test+'    private static void TestRunningAppsFourWayV115()') 'WPF grid geometry regression'
Write-Host 'R117: data-free Excel format snapshots saved; live deck now uses stable three-column cell placements.'
