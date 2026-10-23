$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r46/DeskSpaceExcelActionsV46.cs' (Join-Path $root 'DeskSpaceExcelActionsV46.cs') -Force
$bridge=Join-Path $root 'DeskSpaceExcelBridgeV16.cs'
$s=[IO.File]::ReadAllText($bridge).Replace([string][char]13+[char]10,[string][char]10)
function Replace-Once([string]$before,[string]$after) {
    if(!$script:s.Contains($before)){throw "R46 missing Excel bridge anchor: $before"}
    $script:s=$script:s.Replace($before,$after)
}
Replace-Once '    private static string Text(object? o)' @'
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    private static string Text(object? o)
'@
Replace-Once 'NewValue=source.Display,' 'NewValue=DeskSpaceExcelActionsV46.PreviewText(source,DateTime.Now),'
Replace-Once '        if(cell.ValueKind=="boolean")' @'
        if(DeskSpaceExcelActionsV46.IsNativeShortcut(cell.ValueKind))
            throw new InvalidOperationException(
                "Excel shortcuts must execute in Excel, never be converted to values.");
        if(cell.ValueKind=="boolean")
'@
Replace-Once 'var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula)>();' 'var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula,string Format)>();'
Replace-Once 'rollback.Add((cell,previous,hadFormula,hadFormula?Text(cell.FormulaR1C1):""));' @'
rollback.Add((cell,previous,hadFormula,
                    hadFormula?Text(cell.FormulaR1C1):"",
                    Text(cell.NumberFormat)));
'@
# R42 intentionally removed replay of CAPTURED Excel formulas. Keep that
# protection; only explicit NEW user-selected formula actions are executable.
Replace-Once 'cell.Value2=TypedValue(change.Source);' @'
if(DeskSpaceExcelActionsV46.IsFormulaAction(change.Source.ValueKind))
                {
                    // The formula itself goes to Excel, not its calculated
                    // result. Formula accepts native A1 references.
                    if(change.Source.ValueKind==DeskSpaceExcelActionsV46.CapturedFormula)
                        cell.FormulaR1C1=DeskSpaceExcelActionsV46.Formula(change.Source);
                    else cell.Formula=DeskSpaceExcelActionsV46.Formula(change.Source);
                }
                else if(DeskSpaceExcelActionsV46.IsNativeShortcut(change.Source.ValueKind))
                {
                    // Excel executes its OWN keyboard command. Verify that
                    // Windows foreground is Excel, never send to another app.
                    dynamic excelWindow=app.ActiveWindow;
                    excelWindow.Activate();
                    IntPtr excelHwnd=new IntPtr((int)app.Hwnd);
                    SetForegroundWindow(excelHwnd);
                    if(GetForegroundWindow()!=excelHwnd)
                        throw new InvalidOperationException(
                            "Activate Excel on this PC before using keyboard "+
                            "mappings. No keys were sent elsewhere.");
                    cell.Select();
                    if(!string.Equals(Text(app.ActiveCell.Address),
                          change.CellAddress,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Excel active cell differs from verified target.");
                    app.SendKeys(DeskSpaceExcelActionsV46.NativeSendKeys(
                        change.Source.ValueKind),true);
                }
                else cell.Value2=TypedValue(change.Source);
                if(!string.IsNullOrWhiteSpace(change.Source.NumberFormat) &&
                   (DeskSpaceExcelActionsV46.IsAction(change.Source.ValueKind) ||
                    change.Source.ValueKind=="number"))
                    cell.NumberFormat=change.Source.NumberFormat;
'@
Replace-Once 'else x.Cell.Value2=x.Old;' @'
else x.Cell.Value2=x.Old;
                    if(x.Format.Length>0)x.Cell.NumberFormat=x.Format;
'@
[IO.File]::WriteAllText($bridge,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R51: Excel executes keyboard shortcuts directly, formulas enter as formulas, workbook backup/rollback and custom display format retained.'
