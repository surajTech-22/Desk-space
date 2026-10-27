$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$format="$r/DeskSpaceExcelFormatApplyV109.cs"
$editor="$r/DeskSpaceExcelEditorV42.cs"
$unified="$r/DeskSpaceExcelUnifiedV126.cs"
$wheel="$r/DeskSpaceExcelWheelV42.cs"
$worker="$r/DeskSpaceExcelFormatWorkerV130.cs"
Copy-Item 'r147/DeskSpaceExcelCombinedFormulaV147.cs' "$r/DeskSpaceExcelCombinedFormulaV147.cs" -Force
$lf=[string][char]10; $crlf=[string][char]13+$lf
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $src=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($src,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R147 ${label}: expected exactly one anchor, found $n"}
 [IO.File]::WriteAllText($file,$src.Replace($a,$b),
    [Text.UTF8Encoding]::new($false))
}
# PRE-FLIGHT: reject any destination formula cell that has pre-existing
# data or a DIFFERENT formula. This happens BEFORE any workbook mutation.
# The same location/address mapping is reused in the final write phase.
Swap $format @'
        if((bool)sheet.ProtectContents)problems.Add("Worksheet is protected.");
'@ @'
        // R147 shared formatting/formula paste preflight. A saved formula is
        // not optional data: it belongs in this same mapping operation.
        var formulaSourcesV147=order.Where(c=>c.StoredType=="Formula")
            .Select(c=>new DeskSpaceExcelCombinedFormulaV147.Cell(
                c.Row,c.Column,c.FormulaA1,c.FormulaR1C1)).ToArray();
        if(formulaSourcesV147.Length>0)
        {
            try
            {
                var currentV147=new List<DeskSpaceExcelCombinedFormulaV147.Target>();
                foreach(var formulaV147 in formulaSourcesV147)
                {
                    dynamic dest=target.Cells[formulaV147.Row+1,formulaV147.Column+1];
                    bool exists=IsFormula(dest);
                    currentV147.Add(new(formulaV147.Row,formulaV147.Column,
                        exists,!exists && (object?)dest.Value2!=null,
                        exists?Text(dest.FormulaR1C1):""));
                }
                DeskSpaceExcelCombinedFormulaV147.Inspect(
                    formulaSourcesV147,currentV147);
            }
            catch(InvalidDataException error)
            {
                problems.Add("Combined formats/formulas blocked: "+error.Message);
            }
        }
        if((bool)sheet.ProtectContents)problems.Add("Worksheet is protected.");
'@ 'combine formula conflict preflight with formatting preview'

# Existing formatting transaction already has a SaveCopyAs and a full
# verification/rollback handler. Write formulas INSIDE that transaction,
# AFTER independent formatting/style/table readback, BEFORE reporting success.
# Therefore the same rollback undoes partially inserted formulas.
Swap $format @'
            // Do not save the open workbook behind the user's back.
'@ @'
            // R147: combined paste. Native R1C1 formula writes use the exact
            // saved relative/absolute expressions, independent of old source
            // data. All destination cells were inspected before formatting.
            var wantedV147=source.Where(c=>c.StoredType=="Formula")
                .Select(c=>new DeskSpaceExcelCombinedFormulaV147.Cell(
                    c.Row,c.Column,c.FormulaA1,c.FormulaR1C1)).ToArray();
            int writtenV147=0,matchingV147=0;
            if(wantedV147.Length>0)
            {
                var currentV147=new List<DeskSpaceExcelCombinedFormulaV147.Target>();
                foreach(var itemV147 in wantedV147)
                {
                    dynamic formulaCell=live.Range.Cells[
                        itemV147.Row+1,itemV147.Column+1];
                    bool exists=IsFormula(formulaCell);
                    currentV147.Add(new(itemV147.Row,itemV147.Column,
                        exists,!exists && (object?)formulaCell.Value2!=null,
                        exists?Text(formulaCell.FormulaR1C1):""));
                }
                var planV147=DeskSpaceExcelCombinedFormulaV147.Inspect(
                    wantedV147,currentV147);
                // Preserve Excel's calculated-column setting, previously
                // disabled by R145 until this transaction's finally block.
                foreach(var put in planV147.Writes)
                {
                    dynamic cell=live.Range.Cells[put.Row+1,put.Column+1];
                    if(IsFormula(cell) || (object?)cell.Value2!=null)
                        throw new InvalidDataException(
                            "Formula destination changed before native Excel write.");
                    cell.FormulaR1C1=put.FormulaR1C1;
                    writtenV147++;
                }
                matchingV147=planV147.ExistingMatches;
                foreach(var wantedFormulaV147 in wantedV147)
                {
                    dynamic cell=live.Range.Cells[
                        wantedFormulaV147.Row+1,wantedFormulaV147.Column+1];
                    if(!IsFormula(cell) ||
                       !string.Equals(Text(cell.FormulaR1C1),
                           wantedFormulaV147.FormulaR1C1,
                           StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "Combined formula readback differs from saved formula at "+
                            Text(cell.Address));
                }
            }
            // Do not save the open workbook behind the user's back.
'@ 'write exact formulas after styles and tables inside same backup/rollback'

# One visible mapping action. Keep underlying legacy formula-only library
# method private for backwards compatibility, but DO NOT expose a second
# Paste Special button which creates a conflicting workflow.
Swap $editor @'
        formatControls.Children.Add(RequiresExcel(
            "Paste Special · Formulas",PasteSpecialFormulasV144,true));
        formatFoot.Children.Add(formatControls);
'@ @'
        // R147: Apply formats & formulas now also pastes captured formulas.
        // No second public Paste Special command or extra module.
        formatFoot.Children.Add(formatControls);
'@ 'remove redundant Workspace formula-only paste command'

Swap $unified @'
        if(status!=null)
                status.Text="Checking selected Excel range for Paste Special · Formulas…";
'@ @'
        if(status!=null)
                status.Text="Checking selected Excel range for saved formulas…";
'@ 'retain private legacy path with honest internal status'

Swap $editor @'
            if(status!=null)status.Text="Applying verified Excel cell formats…";
'@ @'
            if(status!=null)
                status.Text="Applying verified Excel cell formats AND formulas…";
'@ 'single Workspace action status'

# R146 introduced a shelf button requiring users to separately paste formulas.
# Remove it: a click of the saved mapping now applies BOTH in one transaction.
Swap $wheel @'
        shelfMessage.Text=visual==null?
            "This saved format has no complete Excel layout. Recapture in Workspace.":
            "Format mapping: "+visual.Rows+" × "+visual.Columns+
            " cells · "+(Group(entry).Length==0?"Ungrouped":Group(entry))+
            ". Select the destination in Excel. Donut click applies FORMATS "+
            "ONLY (blank input cells stay blank). To paste actual formulas, "+
            "use the separate Paste formulas button below. No source data copied.";
        if(visual!=null && visual.Cells.Any(c=>c.StoredType=="Formula"))
        {
            var pasteFormulasV146=new Button{
                Content="ƒx  Paste saved formulas only",
                ToolTip="Excel Paste Special: formulas only, no formats or values. "+
                    "Works into blank formula cells and never overwrites user data.",
                Height=29,
                Padding=new Thickness(8,3,8,3),
                Margin=new Thickness(3,5,3,3),
                HorizontalAlignment=HorizontalAlignment.Stretch,
                Background=new SolidColorBrush(Color.FromRgb(60,90,151)),
                Foreground=Brushes.White,
                BorderThickness=new Thickness(0),
                FontWeight=FontWeights.SemiBold
            };
            pasteFormulasV146.Click+=(_,e)=>{
                e.Handled=true;
                if(!ExcelIsOpen())
                    Status("Formula paste blocked",
                        "Open the destination workbook in Microsoft Excel first.");
                else
                    _=PasteFormulasV146Async(entry);
            };
            shelfBody.Children.Add(pasteFormulasV146);
        }
        Position();shelf.IsOpen=true;
'@ @'
        shelfMessage.Text=visual==null?
            "This saved format has no complete Excel layout. Recapture in Workspace.":
            "Format mapping: "+visual.Rows+" × "+visual.Columns+
            " cells · "+(Group(entry).Length==0?"Ungrouped":Group(entry))+
            ". Select the destination in Excel and click the saved mapping "+
            "to apply BOTH its formatting and "+
            visual.Cells.Count(c=>c.StoredType=="Formula")+
            " saved formulas in one verified operation. Existing input values "+
            "and different formulas are never overwritten. No separate paste needed.";
        Position();shelf.IsOpen=true;
'@ 'remove separate donut formula button and tell user about combined action'

Swap $wheel @'
                "Apply saved format/formulas '"+entry.Name+"'?\n\n"+
'@ @'
                "Apply saved formats AND formulas '"+entry.Name+"'?\n\n"+
'@ 'correct single donut confirmation'

Swap $wheel @'
            Status("Format mapping applied",result);
'@ @'
            Status("Formats & formulas applied",result);
'@ 'report actual combined result'

Write-Host 'R147: default Formats & Formulas mapping applies actual Excel formulas in same backed-up formatting transaction. Removed redundant separate formula-only button. Saved inputs protected; formula conflicts blocked before writes.'
