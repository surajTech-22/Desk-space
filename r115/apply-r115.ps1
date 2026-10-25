$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r115/DeskSpaceStableHoverV115.cs' -Destination "$r/DeskSpaceStableHoverV93.cs" -Force
Copy-Item -LiteralPath 'r115/DeskSpaceExcelSheetViewV115.cs' -Destination "$r/DeskSpaceExcelSheetViewV101.cs" -Force
function Change([string]$path,[string]$old,[string]$new,[string]$reason){
 $crlf=[string][char]13+[string][char]10
 $lf=[string][char]10
 $content=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf)
 $new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($content,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R115 $reason expected exactly once; found $count"}
 [IO.File]::WriteAllText($path,$content.Replace($old,$new),
   [Text.UTF8Encoding]::new($false))
}
$ed="$r/DeskSpaceExcelEditorV42.cs"
Change $ed @'
        Padding=new Thickness(12),Margin=new Thickness(0,0,10,0),Child=child
'@ @'
        Padding=new Thickness(9),Margin=new Thickness(0,0,8,0),Child=child
'@ 'compact panels without removing controls'
Change $ed @'
        var head=new StackPanel{Margin=new Thickness(8,5,8,3)};
        head.Children.Add(new TextBlock{
            Text="Excel Mapping",FontSize=15,
            FontWeight=FontWeights.SemiBold,Foreground=ink});
'@ @'
        // Workspace title already identifies Excel Mapping. Reclaim room
        // vertically for the captured Excel grid instead of duplicating it.
        var head=new StackPanel{Margin=new Thickness(8,2,8,1)};
'@ 'remove redundant page title and free height'
Change $ed @'
        var content=new Grid{Margin=new Thickness(6,0,6,4),MinHeight=180};
        content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(198)});
'@ @'
        var content=new Grid{Margin=new Thickness(4,0,4,3),MinHeight=220};
        content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(168)});
'@ 'wider Excel preview and narrower saved-mapping sidebar'
Change $ed @'
        var editorHead=new StackPanel();
        editorHead.Children.Add(new TextBlock{
            Text="COLLECTION & DATA",FontSize=13,
            FontWeight=FontWeights.Bold,Foreground=ink,
            Margin=new Thickness(2,3,2,7)});
        editorHead.Children.Add(new TextBlock{
            Text="Collection name (groups related data together)",
'@ @'
        var editorHead=new StackPanel();
        editorHead.Children.Add(new TextBlock{
            Text="COLLECTION & DATA",FontSize=13,
            FontWeight=FontWeights.Bold,Foreground=ink,
            Margin=new Thickness(2,2,2,3)});
        var collectionAndData=new Grid{Margin=new Thickness(0,0,2,3)};
        collectionAndData.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        collectionAndData.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        var collectionColumn=new StackPanel{Margin=new Thickness(0,0,6,0)};
        var dataColumn=new StackPanel{Margin=new Thickness(6,0,0,0)};
        collectionColumn.Children.Add(new TextBlock{
            Text="Collection name",
'@ 'collection & data horizontal grid'
Change $ed @'
        editorHead.Children.Add(mappingGroupV42);
        editorHead.Children.Add(new TextBlock{
            Text="Data name (saved Excel mapping)",
'@ @'
        collectionColumn.Children.Add(mappingGroupV42);
        dataColumn.Children.Add(new TextBlock{
            Text="Data name",
'@ 'collection field in left column'
Change $ed @'
        editorHead.Children.Add(mappingNameV42);
        editorInfoV42=HintV42(
            "Enter a collection and data name, select cells in Excel, then click Capture. "+
            "Edit Data field / Data value rows below and save. "+
            "Collections appear in the wheel after their first data mapping is saved.",11);
'@ @'
        dataColumn.Children.Add(mappingNameV42);
        Grid.SetColumn(collectionColumn,0);
        Grid.SetColumn(dataColumn,1);
        collectionAndData.Children.Add(collectionColumn);
        collectionAndData.Children.Add(dataColumn);
        editorHead.Children.Add(collectionAndData);
        editorInfoV42=HintV42(
            "Capture selected Excel cells and save the mapping. Cell details are available in the preview.",10);
'@ 'data field in right column, compact hint'
Change $ed @'
        var tabs=new TabControl{Margin=new Thickness(0,4,0,0)};
'@ @'
        var tabs=new TabControl{Margin=new Thickness(0,2,0,0)};
'@ 'compact tab margin'
# R103 already puts the paste/format footer INSIDE the mapping-only tab.
# Do not duplicate or move it: the sheet tab is already full-height.
$smoke='r36/UiSmoke/Program.cs'
Change $smoke @'
        Assert(output is DockPanel &&
               Walk(output).OfType<ScrollViewer>().Any() &&
               Walk(output).OfType<ComboBox>().Any(),
            "R104 preview must contain a scrollable Excel grid and zoom selector");
'@ @'
        Assert(output is DockPanel &&
               Walk(output).OfType<ScrollViewer>().Any() &&
               !Walk(output).OfType<ComboBox>().Any(),
            "R115 preview must be scrollable at fixed 100% without a zoom selector");
'@ 'replace obsolete R104 zoom UI assertion with fixed-scale R115 test'
Change $smoke @'
            TestRunningAppsSideOnlyV113();
'@ @'
            TestRunningAppsSideOnlyV113();
            TestRunningAppsFourWayV115();
'@ 'invoke four-way and ordering tests'
$method=@'
    private static void TestRunningAppsFourWayV115()
    {
        var cls=Product.GetType("DeskSpace.DeskSpaceStableHoverV93",true)!;
        var order=cls.GetMethod("OrderAppNamesV115",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string[] Sort(bool outward,params (string Name,int Count)[] groups)=>
            (string[])order.Invoke(null,new object[]{groups,outward})!;
        var left=Sort(false,("Chrome",1),("Explorer",4),
            ("Excel",3),("Calculator",1));
        Assert(left.SequenceEqual(new[]{"Explorer","Calculator","Chrome","Excel"}),
            "R115 multiwindow apps must be on both edges; single apps alphabetical");
        var right=Sort(true,("Chrome",1),("Explorer",4),
            ("Excel",3),("Calculator",1));
        Assert(right.SequenceEqual(new[]{"Excel","Calculator","Chrome","Explorer"}),
            "R115 outward-right must place largest multiple-window group at right edge");
        var singles=Sort(false,("Word",1),("Excel",1),("Chrome",1));
        Assert(singles.SequenceEqual(new[]{"Chrome","Excel","Word"}),
            "R115 all single-window apps must be alphabetical");
        var choose=cls.GetMethod("ChooseFourWayPlacement",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object Place(double l,double r,double t,double b,double w,
            double dpi,int count,bool outward)=>
            choose.Invoke(null,new object[]{l,r,t,b,w,dpi,count,outward})!;
        string Dir(object value)=>value.GetType().GetProperty("Direction")!.
            GetValue(value)!.ToString()!;
        bool Allowed(object value)=>(bool)value.GetType().GetProperty("CanOpen")!.
            GetValue(value)!;
        Assert(Dir(Place(800,900,400,400,1920,1,4,true))=="Right",
            "R115 enough side room should use outward right side");
        Assert(Dir(Place(110,105,500,20,1920,1,4,false))=="Up",
            "R115 above grid should be available when both sides are narrow");
        Assert(Dir(Place(110,105,20,500,1920,1,4,true))=="Down",
            "R115 below grid should be available when both sides are narrow");
        Assert(!Allowed(Place(40,50,40,40,1920,1,4,false)),
            "R115 no supported direction must block rather than cover app grid");
        Assert(Dir(Place(100,90,720,10,1440,1.5,3,false))=="Up",
            "R115 four-way choice respects scaled monitor working area");
        Console.WriteLine("R115 PASS: alphabetical singles, edge-grouped multis, four directions, and monitor limits.");
    }

'@
$methodAnchor=@'
    private static void TestRunningAppsSideOnlyV113()
'@
Change $smoke $methodAnchor ($method+$methodAnchor) 'inject R115 WPF regression'
Write-Host 'R115 applied: four-way Running Apps, multiple-window edge sorting, wider 100% Excel sheet, horizontal names.'
