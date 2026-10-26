$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker="$r/DeskSpaceExcelFormatApplyV109.cs"
$wheel="$r/DeskSpaceExcelWheelV42.cs"
$editor="$r/DeskSpaceExcelEditorV42.cs"
$smoke='r36/UiSmoke/Program.cs'
Copy-Item 'r135/DeskSpaceExcelRenderedPolicyV135.cs' "$r/DeskSpaceExcelRenderedPolicyV135.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$label) {
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R135 $($label) expected one anchor; found $hits"}
 [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap $wheel @'
            var approval=MessageBox.Show(owner,
                "Apply saved format/formulas '"+entry.Name+"'?\n\n"+
                review.ToString()+"\n\nA workbook backup is created first. "+
                "Existing transaction values and formulas are protected.",
                "DeskSpace · confirm FORMAT-only paste",
                MessageBoxButton.YesNo,MessageBoxImage.Warning);
            if(approval!=MessageBoxResult.Yes)
            {
                Status("Format mapping cancelled","No workbook changes made.");
                return;
            }
'@ @'
            // User already requested FORMAT-only paste by selecting its donut
            // entry. Preview confirmed the whole destination as eligible;
            // do not demand an additional modal confirmation. Apply() repeats
            // the read-only preflight and takes the mandatory verified backup.
'@ 'remove redundant format donut confirmation'
Swap $editor @'
            if(MessageBox.Show(
                "Apply cell-specific formatting to "+approved.Sheet+
                " "+approved.Destination+"?\n"+
                "Only if the ENTIRE destination is empty or its FULL data layout matches.\n"+
                approved.PlannedActions+"\n"+
                "Existing input values/formulas will not be overwritten.\n"+
                "The workbook will be backed up, and formatting read back before success.",
                "DeskSpace — confirm Excel formatting",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
'@ @'
            // Explicit Apply click is consent. An additional Yes/No prompt
            // must not interrupt the formatting flow. The verified full-range
            // preview is repeated in the worker before any workbook changes.
'@ 'remove redundant format Workspace confirmation'
Swap $worker @'
            var source=live.Visual.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
'@ @'
            // Only true ListObject cells may use structural/table-style
            // verification instead of comparing Excel-generated DisplayFormat
            // pixel colours, which may vary between workbooks and neutral
            // destination table headers. ALL direct cell-format checks remain.
            var sourceTablesV135=live.Visual.FeaturesV106!.Tables;
            var source=live.Visual.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
'@ 'capture table regions before strict readback'
Swap $worker @'
                if(!mergeChildren.Contains((s.Row,s.Column)) &&
                   s.DisplayStyleAvailable &&
                   (!a.DisplayStyleAvailable ||
'@ @'
                if(!mergeChildren.Contains((s.Row,s.Column)) &&
                   DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(
                       s.Row,s.Column,sourceTablesV135) &&
                   s.DisplayStyleAvailable &&
                   (!a.DisplayStyleAvailable ||
'@ 'avoid comparing computed Excel table display to nonidentical source theme'
Swap $worker @'
                        "Rendered Excel Table/cell appearance differs at "+
'@ @'
                        "Rendered non-table cell appearance differs at "+
'@ 'distinguish genuine non-table display discrepancy'
# WPF verification: compiled reflection regression for table-vs-normal cell
Swap $smoke @'
            TestExcelTypeAwareSheetV101();
'@ @'
            TestExcelTypeAwareSheetV101();
            TestExcelTableRenderedPolicyV135();
'@ 'add policy unit regressions to WPF smoke'
Swap $smoke @'
    private static void TestExcelTypeAwareSheetV101()
'@ @'
    private static void TestExcelTableRenderedPolicyV135()
    {
        var policy=Product.GetType("DeskSpace.DeskSpaceExcelRenderedPolicyV135",true)!;
        var tableType=Product.GetType("DeskSpace.DeskSpaceExcelTableV106",true)!;
        var check=policy.GetMethod("RequiresExactRenderedMatch",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var table=Activator.CreateInstance(tableType)!;
        void Set(string name,object value)=>
            tableType.GetProperty(name)!.SetValue(table,value);
        Set("FullySelected",true);
        Set("TableStyle","TableStyleMedium2");
        Set("Row",0);Set("Column",1);Set("RowCount",5);Set("ColumnCount",6);
        var tables=Array.CreateInstance(tableType,1);
        tables.SetValue(table,0);
        bool MustMatch(int row,int col)=>(bool)check.Invoke(
            null,new object[]{row,col,tables})!;
        Assert(!MustMatch(2,3),
            "Real Excel Table display should use verified ListObject style instead of raw pixel equality");
        Assert(MustMatch(6,3)&&MustMatch(2,0),
            "Non-table cell rendering must remain strictly verified");
        Set("TableStyle","");
        Assert(MustMatch(2,3),
            "An invalid saved table style must not bypass rendered appearance verification");
        Set("TableStyle","System._ComObject");
        Assert(MustMatch(2,3),
            "Corrupt legacy table style must never bypass verification");
        Set("TableStyle","TableStyleMedium2");
        Set("FullySelected",false);
        Assert(MustMatch(2,3),
            "Partially captured tables may not relax rendered appearance checking");
        Console.WriteLine("R135 PASS: legitimate table displayed style is read via table identity; all other rendered cells remain strict.");
    }

    private static void TestExcelTypeAwareSheetV101()
'@ 'compiled strict non-table and invalid-style checks'
Write-Host 'R135: no duplicate Yes/No dialog on format paste; direct formatting, formulas, table structural verification and rollback remain intact; computed display compare is strict only outside an authenticated Excel Table.'
