$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$smoke='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($smoke).Replace($crlf,$lf)
function ReplaceOnce([string]$before,[string]$after)
{
 $script:s=$script:s.Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 if(([regex]::Matches($script:s,[regex]::Escape($before))).Count -ne 1)
   {throw 'R121 WPF test registration anchor count mismatch'}
 $script:s=$script:s.Replace($before,$after)
}
ReplaceOnce @'
            TestHeaderValueSignatureV120();
'@ @'
            TestHeaderValueSignatureV120();
            TestIndependentFormatAndRecentV121();
'@
$test=@'
    private static void TestIndependentFormatAndRecentV121()
    {
        var templateType=Product.GetType(
            "DeskSpace.DeskSpaceExcelTemplateV16",true)!;
        var styleType=Product.GetType(
            "DeskSpace.DeskSpaceExcelSheetVisualV101",true)!;
        var styledCellType=Product.GetType(
            "DeskSpace.DeskSpaceExcelStyledCellV101",true)!;
        var savedCellType=Product.GetType(
            "DeskSpace.DeskSpaceExcelCellV16",true)!;
        var source=Activator.CreateInstance(templateType,true)!;
        var visual=Activator.CreateInstance(styleType,true)!;
        styleType.GetProperty("Rows")!.SetValue(visual,1);
        styleType.GetProperty("Columns")!.SetValue(visual,1);
        var cell=Activator.CreateInstance(styledCellType,true)!;
        styledCellType.GetProperty("Row")!.SetValue(cell,0);
        styledCellType.GetProperty("Column")!.SetValue(cell,0);
        styledCellType.GetProperty("FormulaR1C1")!.
            SetValue(cell,"=SUM(R[-1]C:R[-2]C)");
        var visualCells=(System.Collections.IList)
            styleType.GetProperty("Cells")!.GetValue(visual)!;
        visualCells.Add(cell);
        templateType.GetProperty("SheetVisualV101")!.SetValue(source,visual);
        var savedCell=Activator.CreateInstance(savedCellType,true)!;
        savedCellType.GetProperty("RawValue")!.SetValue(
            savedCell,"PRIVATE_SOURCE_TRANSACTION");
        var savedCells=(System.Collections.IList)
            templateType.GetProperty("Cells")!.GetValue(source)!;
        savedCells.Add(savedCell);
        var formatStore=Product.GetType(
            "DeskSpace.DeskSpaceExcelFormatStoreV121",true)!;
        var only=formatStore.GetMethod("OnlyFormatting",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var result=only.Invoke(null,new object[]{source,"My formula layout"})!;
        var clean=(System.Collections.IList)
            templateType.GetProperty("Cells")!.GetValue(result)!;
        Assert(clean.Count==0 && savedCells.Count==1,
            "R121 format copy must strip source values but preserve source");
        var style=templateType.GetProperty("SheetVisualV101")!.GetValue(result)!;
        var preserved=(System.Collections.IList)
            styleType.GetProperty("Cells")!.GetValue(style)!;
        Assert(preserved.Count==1 &&
            (string)styledCellType.GetProperty("FormulaR1C1")!
                .GetValue(preserved[0])!=="=SUM(R[-1]C:R[-2]C)",
            "R121 exact source formula must survive in format-only snapshot");
        var appRecents=Product.GetType(
            "DeskSpace.DeskSpacePinnedRecentV122",true)!;
        var safe=appRecents.GetMethod("IsSafeDocument",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string temporary=System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "deskspace-r121-"+Guid.NewGuid().ToString("N")+".txt");
        try
        {
            System.IO.File.WriteAllText(temporary,"data");
            Assert((bool)safe.Invoke(null,new object[]{temporary})!,
                "R121 ordinary existing document must be accepted");
            Assert(!(bool)safe.Invoke(null,new object[]{temporary+".exe"})!,
                "R121 executable must never be offered as recent document");
        }
        finally{if(System.IO.File.Exists(temporary))
            System.IO.File.Delete(temporary);}
        var presenter=Product.GetType(
            "DeskSpace.DeskSpaceExcelDeckSuggestionsV123",true)!;
        Assert(presenter.GetMethod("Bind",BindingFlags.NonPublic|
                BindingFlags.Instance)!=null &&
               presenter.GetMethod("ApplyClicked",
                    BindingFlags.NonPublic|BindingFlags.Instance)!=null,
            "R121 suggestions must be in the existing Deck with click-to-apply");
        Console.WriteLine(
            "R121 PASS: separate format store retains R1C1 without values; safe recent files; Deck approval actions.");
    }

'@
ReplaceOnce @'
    private static void TestExcelTypeAwareSheetV101()
'@ ($test+'    private static void TestExcelTypeAwareSheetV101()')
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R121 WPF tests registered: no value leakage, saved formulas, safe recent files and click-only deck suggestions.'
