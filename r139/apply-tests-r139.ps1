$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label){
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R139 $($label) expected one smoke-test anchor, got $hits"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            TestExcelTableRenderedPolicyV135();
'@ @'
            TestExcelTableRenderedPolicyV135();
            TestExcelDirectReadbackV139();
'@ 'invoke fixture'
Swap @'
    private static void TestExcelTableRenderedPolicyV135()
'@ @'
    private static void TestExcelDirectReadbackV139()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceExcelStyledCellV101",true)!;
        var validator=Product.GetType("DeskSpace.DeskSpaceExcelDirectReadbackV139",true)!;
        var compare=validator.GetMethod("Differences",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object src=Activator.CreateInstance(type)!;
        object actual=Activator.CreateInstance(type)!;
        void Set(object cell,string field,object value)=>
            type.GetProperty(field)!.SetValue(cell,value);
        string[] Problems()=>(string[])compare.Invoke(null,new[]{src,actual})!;

        // Excel automatic font colour and no-fill cells may report distinct
        // computed RGB across workbooks yet remain identical format modes.
        foreach(var cell in new[]{src,actual})
        {
            Set(cell,"DirectFontColorIndex",-4105);
            Set(cell,"DirectFontThemeColor",0);
            Set(cell,"DirectFillPattern",-4142);
            Set(cell,"DirectFillThemeColor",0);
        }
        Set(src,"FontBgr",1234);
        Set(actual,"FontBgr",5678);
        Set(src,"FillBgr",100);
        Set(actual,"FillBgr",200);
        Set(actual,"DirectFillPattern",-4105);
        Assert(Problems().Length==0,
            "Automatic font colour and no-fill are semantic formats, not fixed RGB");
        Set(actual,"DirectFillPattern",1);
        Assert(Problems().Any(x=>x.Contains("Interior.Pattern")),
            "A real nonblank fill may not pass the no-fill rule");
        Set(actual,"DirectFillPattern",-4142);

        // Theme slot equality remains required, but cross-workbook RGB
        // conversion of that theme slot is not an exact-match rule.
        Set(src,"DirectFontThemeColor",4);
        Set(actual,"DirectFontThemeColor",4);
        Assert(Problems().Length==0,"Matching theme slot must ignore derived RGB");
        Set(actual,"DirectFontThemeColor",5);
        Assert(Problems().Any(x=>x.Contains("Font.ThemeColor")),
            "Incorrect theme slot must block paste");
        Set(src,"DirectFontThemeColor",0);
        Set(actual,"DirectFontThemeColor",0);

        // Explicit colours, underline/border/format still require exact
        // results before a success message and before workbook is saved.
        Set(src,"DirectFontColorIndex",3);
        Set(actual,"DirectFontColorIndex",3);
        Assert(Problems().Any(x=>x.Contains("Font.Color (explicit RGB)")),
            "Explicit font colour mismatch must be rejected");
        Set(actual,"FontBgr",1234);
        Set(src,"DirectFillPattern",1);
        Set(actual,"DirectFillPattern",1);
        Set(src,"FillBgr",777);
        Set(actual,"FillBgr",444);
        Assert(Problems().Any(x=>x.Contains("Interior.Color (explicit RGB)")),
            "Explicit fill colour mismatch must be rejected");
        Set(actual,"FillBgr",777);
        Set(src,"NumberFormat","0.00");
        Assert(Problems().Any(x=>x.Contains("NumberFormat")),
            "Number-format mismatch must block paste");
        Set(actual,"NumberFormat","0.00");
        Set(src,"BorderBottom",true);
        Assert(Problems().Any(x=>x.Contains("BorderBottom")),
            "Border mismatch must block paste");
        Set(actual,"BorderBottom",true);
        Assert(Problems().Length==0,"Explicitly reconstructed formats must pass");
        Console.WriteLine("R139 PASS: automatic/theme Excel formats survive normalized readback; explicit colour, border and number format mismatches are still rejected.");
    }

    private static void TestExcelTableRenderedPolicyV135()
'@ 'compiled strict colour mode regression'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R139 WPF smoke: cross-workbook RGB normalization tested; explicit RGB/border/number formats remain strict.'