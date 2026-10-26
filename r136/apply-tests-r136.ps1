$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$smoke='r36/UiSmoke/Program.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($smoke).Replace($crlf,$nl)
function Swap([string]$before,[string]$after,[string]$label){
 $a=$before.Replace($crlf,$nl);$b=$after.Replace($crlf,$nl)
 $hits=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R136 $label smoke insertion expected one anchor, got $hits"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            TestExcelTableRenderedPolicyV135();
'@ @'
            TestExcelTableRenderedPolicyV135();
            TestBlankExcelFormatPolicyV136();
'@ 'invoke pure semantic rule regression'
Swap @'
    private static void TestExcelTableRenderedPolicyV135()
'@ @'
    private static void TestBlankExcelFormatPolicyV136()
    {
        var policy=Product.GetType("DeskSpace.DeskSpaceExcelBlankFormatPolicyV136",true)!;
        var tableType=Product.GetType("DeskSpace.DeskSpaceExcelTableV106",true)!;
        var read=policy.GetMethod("AllowedStructuralHeader",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var table=Activator.CreateInstance(tableType)!;
        void Set(string key,object value)=>
            tableType.GetProperty(key)!.SetValue(table,value);
        Set("HasHeaders",true);
        Set("Row",1);Set("Column",2);Set("RowCount",4);Set("ColumnCount",2);
        var columnNames=(List<string>)tableType.GetProperty("ColumnNames")!.
            GetValue(table)!;
        columnNames.Add("Column1");columnNames.Add("Column2");
        var tables=Array.CreateInstance(tableType,1);
        tables.SetValue(table,0);
        string Expected(int r,int c)=>(string)read.Invoke(null,
            new object[]{r,c,tables})!;
        Assert(Expected(1,2)=="Column1" && Expected(1,3)=="Column2",
            "A genuine Excel Table requires structural column headers");
        Assert(Expected(2,2)=="" && Expected(4,3)=="" &&
               Expected(1,1)=="" && Expected(0,3)=="",
            "Format-only pastes must never copy source formula or value "+
            "content into the blank destination");
        Console.WriteLine("R136 PASS: only genuine table headers may be populated; blank body values/formulas remain absent.");
    }
    private static void TestExcelTableRenderedPolicyV135()
'@ 'WPF regression of structural-only table values'
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R136 compiled regression added: blank target has only required ListObject column headers; no source body values or formulas permitted.'