$ErrorActionPreference='Stop'
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs'
$s=[IO.File]::ReadAllText($path)
$old=@'
                var selected=SelectedOriginV116(app,entry);
                if(selected.HasValue)
'@
$new=@'
                // Force static call and nullable tuple type. Calling with
                // dynamic Excel COM otherwise boxes the nullable tuple into
                // ValueTuple<int,int>; that does not have HasValue.
                (int Row,int Column)? selected=
                    SelectedOriginV116((object)app,entry);
                if(selected.HasValue)
'@
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=$s.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old.Replace($crlf,$lf)))).Count
if($n -ne 1){throw "R119 expected one selection call, got $n"}
[IO.File]::WriteAllText($path,$s.Replace($old.Replace($crlf,$lf),$new.Replace($crlf,$lf)),[Text.UTF8Encoding]::new($false))
Write-Host 'R119: Excel selected-origin HasValue now uses statically bound nullable tuple.'

$smoke='r36/UiSmoke/Program.cs'
$ui=[IO.File]::ReadAllText($smoke).Replace($crlf,$lf)
$a='            TestExcelTypeAwareSheetV101();'
$b='            TestExcelTypeAwareSheetV101();'+$lf+'            TestNullableSelectedOriginV119();'
if(([regex]::Matches($ui,[regex]::Escape($a))).Count -ne 1){throw 'R119 smoke registration anchor wrong'}
$ui=$ui.Replace($a,$b)
$method=@'
    // Exercise the actual compiled Excel origin selector without Office:
    // test a selected cell, exact-size range, mismatch, and disjoint range.
    private static void TestNullableSelectedOriginV119()
    {
        var cls=Product.GetType("DeskSpace.DeskSpaceExcelBridgeV16",true)!;
        var select=cls.GetMethod("SelectedOriginV116",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var kind=Product.GetType("DeskSpace.DeskSpaceExcelTemplateV16",true)!;
        var entry=Activator.CreateInstance(kind,true)!;
        kind.GetProperty("RowCount")!.SetValue(entry,3);
        kind.GetProperty("ColumnCount")!.SetValue(entry,2);
        dynamic excel=new System.Dynamic.ExpandoObject();
        dynamic window=new System.Dynamic.ExpandoObject();
        dynamic selection=new System.Dynamic.ExpandoObject();
        dynamic areas=new System.Dynamic.ExpandoObject();
        dynamic rows=new System.Dynamic.ExpandoObject();
        dynamic cols=new System.Dynamic.ExpandoObject();
        areas.Count=1;rows.Count=1;cols.Count=1;
        selection.Areas=areas;selection.Rows=rows;selection.Columns=cols;
        selection.Row=12;selection.Column=5;
        window.RangeSelection=selection;excel.ActiveWindow=window;
        object? selected=select.Invoke(null,new object[]{(object)excel,entry});
        Assert(selected is ValueTuple<int,int> one &&
               one.Item1==12 && one.Item2==5,
            "R119 single blank target cell must yield a valid origin");
        rows.Count=3;cols.Count=2;
        selected=select.Invoke(null,new object[]{(object)excel,entry});
        Assert(selected is ValueTuple<int,int> matching &&
               matching.Item1==12 && matching.Item2==5,
            "R119 exact-size blank target range must yield origin");
        rows.Count=2;cols.Count=2;
        Assert(select.Invoke(null,new object[]{(object)excel,entry})==null,
            "R119 wrong-size destination must reject cleanly");
        areas.Count=2;rows.Count=1;cols.Count=1;
        Assert(select.Invoke(null,new object[]{(object)excel,entry})==null,
            "R119 disjoint destination must reject cleanly");
        Console.WriteLine("R119 PASS: selected-origin nullable tuple tests on production method.");
    }

'@
$anchor='    private static void TestExcelTypeAwareSheetV101()'
if(([regex]::Matches($ui,[regex]::Escape($anchor))).Count -ne 1){throw 'R119 smoke insertion anchor wrong'}
$ui=$ui.Replace($anchor,$method+$anchor)
[IO.File]::WriteAllText($smoke,$ui,[Text.UTF8Encoding]::new($false))
Write-Host 'R119 live compiled WPF test exercises original SelectedOriginV116 against fake Excel.'
