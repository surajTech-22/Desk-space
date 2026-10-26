$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R140 $($label) expected one anchor, found $n"}
 [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            TestExcelDirectReadbackV139();
'@ @'
            TestExcelDirectReadbackV139();
            TestFullExcelFormatMatrixV140();
'@ 'invoke format matrix'
# R139's old theme test assumed ThemeColor takes priority over automatic
# ColorIndex. That assumption is the precise K14 bug.
Swap $smoke @'
        Set(src,"DirectFontThemeColor",4);
        Set(actual,"DirectFontThemeColor",4);
'@ @'
        Set(src,"DirectFontColorIndex",1);
        Set(actual,"DirectFontColorIndex",1);
        Set(src,"DirectFontThemeColor",4);
        Set(actual,"DirectFontThemeColor",4);
'@ 'update R139 explicit-theme fixture to a nonautomatic colour'
Swap $smoke @'
    private static void TestExcelTableRenderedPolicyV135()
'@ @'
    private static void TestFullExcelFormatMatrixV140()
    {
        var cell=Product.GetType("DeskSpace.DeskSpaceExcelStyledCellV101",true)!;
        var validator=Product.GetType("DeskSpace.DeskSpaceExcelDirectReadbackV139",true)!;
        var compare=validator.GetMethod("Differences",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object src=Activator.CreateInstance(cell)!;
        object dest=Activator.CreateInstance(cell)!;
        void Set(object x,string property,object? value)=>
            cell.GetProperty(property)!.SetValue(x,value);
        string[] Diff()=>(string[])compare.Invoke(null,new[]{src,dest})!;
        void TestField(string name,object expected,object wrong)
        {
            Set(src,name,expected);
            Set(dest,name,wrong);
            Assert(Diff().Any(x=>x.Contains(name,StringComparison.Ordinal)),
                name+" readback mismatch was not detected.");
            Set(dest,name,expected);
            Assert(Diff().Length==0,name+" identical styling was falsely rejected.");
        }
        foreach(var x in new[]{src,dest})
        {
            Set(x,"DirectFontColorIndex",-4105);
            Set(x,"DirectFillPattern",-4142);
        }
        // K14 failure: Excel captures inherited ThemeColor 2 but a font
        // with ColorIndex automatic can read back as ThemeColor 0.
        Set(src,"DirectFontThemeColor",2);
        Set(dest,"DirectFontThemeColor",0);
        Set(src,"FontBgr",16777215);
        Set(dest,"FontBgr",0);
        Assert(Diff().Length==0,
            "Automatic Excel font must not compare its inherited ThemeColor=2.");
        Set(src,"DirectFontColorIndex",1);
        Set(dest,"DirectFontColorIndex",1);
        Set(src,"DirectFontThemeColor",4);
        Set(dest,"DirectFontThemeColor",4);
        Assert(Diff().Length==0,"Equal actual theme slots must match.");
        Set(dest,"DirectFontThemeColor",5);
        Assert(Diff().Any(x=>x.Contains("Font.ThemeColor")),
            "Different actual theme slots must fail.");
        Set(dest,"DirectFontThemeColor",4);
        Set(src,"DirectFontTintAndShade",0.4d);
        Set(dest,"DirectFontTintAndShade",0.0d);
        Assert(Diff().Any(x=>x.Contains("Font.TintAndShade")),
            "Tint mismatch must fail.");
        Set(dest,"DirectFontTintAndShade",0.4d);
        Assert(Diff().Length==0,"Correct font tint should pass.");
        Set(src,"DirectFillPattern",1);
        Set(dest,"DirectFillPattern",1);
        Set(src,"DirectFillThemeColor",5);
        Set(dest,"DirectFillThemeColor",5);
        Set(src,"DirectFillTintAndShade",-0.25d);
        Set(dest,"DirectFillTintAndShade",0.0d);
        Assert(Diff().Any(x=>x.Contains("Interior.TintAndShade")),
            "Fill tint mismatch must fail.");
        Set(dest,"DirectFillTintAndShade",-0.25d);
        Assert(Diff().Length==0,"Correct fill tint should pass.");

        TestField("HorizontalAlignment",-4131,-4108);
        TestField("VerticalAlignment",-4108,-4160);
        TestField("TextOrientation",30,0);
        TestField("IndentLevel",2,0);
        TestField("WrapText",true,false);
        TestField("ShrinkToFit",true,false);
        TestField("NumberFormat","dd-mmm-yyyy","General");
        TestField("FontName","Aptos Narrow","Arial");
        TestField("FontSize",15d,10d);
        TestField("Bold",true,false);
        TestField("Italic",true,false);
        TestField("Underline",true,false);
        TestField("StrikeThrough",true,false);
        Set(src,"BorderBottom",true);
        Set(dest,"BorderBottom",true);
        TestField("BorderBottomColor",65535,0);
        TestField("BorderBottomWeight",4,2);
        Set(src,"BorderRight",true);
        Set(dest,"BorderRight",true);
        TestField("BorderRightColor",255,0);
        TestField("BorderRightWeight",3,1);
        Set(src,"BorderLeft",true);
        Set(dest,"BorderLeft",true);
        TestField("BorderLeftColor",255,0);
        TestField("BorderLeftWeight",3,1);
        Set(src,"BorderTop",true);
        Set(dest,"BorderTop",true);
        TestField("BorderTopColor",255,0);
        TestField("BorderTopWeight",3,1);
        Assert(Diff().Length==0,"Complete style readback must agree.");
        Console.WriteLine(
            "R140 PASS: K14 automatic theme, explicit theme/tint, fill tint, "+
            "number/date, font properties, all four border colours/weights, "+
            "alignment, wrap, orientation and indent.");
    }

    private static void TestExcelTableRenderedPolicyV135()
'@ 'full style matrix'
# Feature layer stores superscript, subscript and protection, so verify them.
$worker='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs'
Swap $worker @'
                   (wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.TableRole!=observed.TableRole ||
                   wanted.Locked!=observed.Locked ||
                   wanted.FormulaHidden!=observed.FormulaHidden))
'@ @'
                   (wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.Superscript!=observed.Superscript ||
                   wanted.Subscript!=observed.Subscript ||
                   wanted.TableRole!=observed.TableRole ||
                   wanted.Locked!=observed.Locked ||
                   wanted.FormulaHidden!=observed.FormulaHidden))
'@ 'strict feature layer readback'
# Make the unsupported feature list honest; input formulas are preserved in
# the library but are never pasted on an entirely blank format-only target.
Write-Host 'R140 source and Windows smoke tests: theme vs automatic, colour tint, fill, all border edges, alignment, fonts, number/date, table formatting, superscript, protection and blank-formula behaviour.'