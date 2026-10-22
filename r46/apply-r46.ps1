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
Replace-Once 'NewValue=source.Display,' 'NewValue=DeskSpaceExcelActionsV46.PreviewText(source,DateTime.Now),'
Replace-Once '        if(cell.ValueKind=="boolean")' @'
        if(DeskSpaceExcelActionsV46.IsStaticDateTime(cell.ValueKind))
            return DeskSpaceExcelActionsV46.ValueAtPaste(cell,DateTime.Now);
        if(cell.ValueKind=="boolean")
'@
Replace-Once 'var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula)>();' 'var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula,string Format)>();'
Replace-Once 'rollback.Add((cell,previous,hadFormula,hadFormula?Text(cell.FormulaR1C1):""));' @'
rollback.Add((cell,previous,hadFormula,
                    hadFormula?Text(cell.FormulaR1C1):"",
                    Text(cell.NumberFormat)));
'@
Replace-Once 'if(change.Source.ValueKind=="formula")' @'
if(DeskSpaceExcelActionsV46.IsFormulaAction(change.Source.ValueKind))
                    cell.FormulaR1C1=DeskSpaceExcelActionsV46.Formula(change.Source);
                else if(change.Source.ValueKind=="formula")
'@
Replace-Once 'else cell.Value2=TypedValue(change.Source);' @'
else cell.Value2=TypedValue(change.Source);
                if(DeskSpaceExcelActionsV46.IsAction(change.Source.ValueKind))
                    cell.NumberFormat=DeskSpaceExcelActionsV46.NumberFormat(
                        change.Source.ValueKind);
'@
Replace-Once 'else x.Cell.Value2=x.Old;' @'
else x.Cell.Value2=x.Old;
                    if(x.Format.Length>0)x.Cell.NumberFormat=x.Format;
'@
[IO.File]::WriteAllText($bridge,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R46 Excel value engine wired: capture-time date is never frozen; live formulas explicit; rollback includes number format.'
