$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
function ReplaceOnce([string]$old,[string]$new,[string]$label){
    $lf=[string][char]10;$crlf=[string][char]13+$lf
    $text=[IO.File]::ReadAllText($bridge).Replace($crlf,$lf)
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $found=([regex]::Matches($text,[regex]::Escape($old))).Count
    if($found -ne 1){throw "R116 $($label) expected one source anchor, found $found"}
    [IO.File]::WriteAllText($bridge,$text.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$helper=@'
    // The old automatic-matching path demanded two source labels even when
    // the entire destination was empty. A blank worksheet can never contain
    // those labels. Inspect the whole saved rectangle BEFORE approving the
    // selected cell as a new destination anchor.
    private static DeskSpaceExcelFullRangeGateV107.Decision CheckAllCellsV116(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry,int startRow,int startColumn)
    {
        int rows=entry.RowCount,cols=entry.ColumnCount;
        if(rows<1||cols<1||rows*(long)cols>1800 ||
           startRow<1||startColumn<1||
           startRow+rows-1>1048576||startColumn+cols-1>16384)
            throw new InvalidOperationException(
                "The target selection does not fit the complete recorded Excel range.");
        var sourceByCell=entry.Cells
            .GroupBy(x=>(x.RowOffset,x.ColumnOffset))
            .ToDictionary(x=>x.Key,x=>x.First());
        var expected=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        var actual=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        for(int rr=0;rr<rows;rr++)
        for(int cc=0;cc<cols;cc++)
        {
            sourceByCell.TryGetValue((rr,cc),out var saved);
            bool occupied=saved!=null;
            string sourceType="Blank";
            if(saved!=null)
                sourceType=saved.ValueKind switch
                {
                    "formula" or "captured-formula"=>"Formula",
                    "number"=>"Number","boolean"=>"Boolean",
                    "text"=>"Text",_=>saved.FormulaR1C1.Length>0
                        ?"Formula":"Text"
                };
            expected.Add(new(occupied,sourceType));
            dynamic cell=sheet.Cells[startRow+rr,startColumn+cc];
            bool formula=Formula(cell);
            object? raw=(object?)cell.Value2;
            actual.Add(new(formula||raw!=null,
                DeskSpaceExcelInputTypeV101.Stored(raw,formula)));
        }
        return DeskSpaceExcelFullRangeGateV107.Evaluate(expected,actual);
    }
    private static (int Row,int Column)? SelectedOriginV116(
        dynamic excel,DeskSpaceExcelTemplateV16 entry)
    {
        dynamic selection=excel.ActiveWindow.RangeSelection;
        if((int)selection.Areas.Count!=1)return null;
        int countRows=(int)selection.Rows.Count;
        int countCols=(int)selection.Columns.Count;
        if(!((countRows==1&&countCols==1)||
             (countRows==entry.RowCount&&countCols==entry.ColumnCount)))
            return null;
        return ((int)selection.Row,(int)selection.Column);
    }

'@
ReplaceOnce '    public static DeskSpaceExcelProposalV16 Preview(' ($helper+'    public static DeskSpaceExcelProposalV16 Preview(') 'full-range blank/matching helper'
$old=@'
        else
        {
            var match=FindAnchorMatch((object)sheet,entry);
            r=match.Row;c=match.Col;
            int required=Math.Min(3,entry.Anchors.Count);
            verified=match.Unique&&match.Matches>=Math.Max(2,required) &&
                match.Matches*2>=entry.Anchors.Count;
            reason=verified
                ? "Matched "+match.Matches+" nearby source labels at corresponding positions."
                : "Insufficient unique matching labels. Select the target cell in Excel and preview again.";
        }
        var preview=new DeskSpaceExcelProposalV16 {
'@
$new=@'
        else
        {
            var match=FindAnchorMatch((object)sheet,entry);
            r=match.Row;c=match.Col;
            int required=Math.Min(3,entry.Anchors.Count);
            verified=match.Unique&&match.Matches>=Math.Max(2,required) &&
                match.Matches*2>=entry.Anchors.Count;
            reason=verified
                ? "Matched "+match.Matches+" nearby source labels at corresponding positions."
                : "Insufficient unique matching labels.";
            if(!verified)
            {
                var selected=SelectedOriginV116(app,entry);
                if(selected.HasValue)
                {
                    // Crucial: use the USER'S selected cell, NOT the source
                    // workbook coordinates, when pasting into a blank sheet.
                    var blank=CheckAllCellsV116((object)sheet,entry,
                        selected.Value.Row,selected.Value.Column);
                    if(blank.State==
                        DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty)
                    {
                        r=selected.Value.Row;c=selected.Value.Column;
                        verified=true;
                        reason="The entire "+entry.RowCount+"×"+entry.ColumnCount+
                            " target rectangle is empty. Using selected cell "+
                            Text(sheet.Cells[r,c].Address)+
                            " as its starting point; no reference labels needed.";
                    }
                    else
                        reason="Cannot identify a safe blank destination: "+
                            blank.Explanation+
                            " Select one start cell in a completely blank "+
                            "target area or use verified matching labels.";
                }
                else
                    reason="Reference labels cannot be matched. Select a single "+
                        "destination start cell or an exact-size empty range.";
            }
        }
        // All-or-nothing occupancy/type rule applies to the OLD value-paste
        // route as well as the newer format-only route. Do not let a partially
        // populated template enter the write queue.
        var whole=CheckAllCellsV116((object)sheet,entry,r,c);
        if(!whole.MayApply)
        {
            verified=false;
            reason="Excel paste blocked: "+whole.Explanation;
        }
        else if(verified)
            reason+=" Full target verification: "+whole.Explanation;
        var preview=new DeskSpaceExcelProposalV16 {
'@
ReplaceOnce $old $new 'automatic selected origin when blank'
$oldApply=@'
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        // Detect modifications made since the preview before touching the workbook.
'@
$newApply=@'
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        var refreshed=CheckAllCellsV116((object)sheet,entry,
            preview.StartRow,preview.StartColumn);
        if(!refreshed.MayApply)
            throw new InvalidOperationException(
                "Excel destination changed or is partially populated: "+
                refreshed.Explanation+" Nothing was written.");
        // Detect modifications made since the preview before touching the workbook.
'@
ReplaceOnce $oldApply $newApply 'write-time revalidation before backup'
Write-Host 'R116: empty destination bypasses label matching only when entire selected template footprint is blank; full/partial checks repeat before apply.'
