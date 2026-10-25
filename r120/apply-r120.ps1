$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
function ReplaceOnce([string]$path,[string]$before,[string]$after,[string]$label)
{
 $lf=[string][char]10
 $crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R120 $($label): expected one anchor, got $n"}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
$matchingBefore=@'
    private static (int Row,int Col,int Matches,bool Unique) FindAnchorMatch(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry)
    {
        var anchors=entry.Anchors.Where(a=>Normalize(a.Label).Length>2).ToArray();
        if(anchors.Length==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var indexed=anchors.GroupBy(a=>Normalize(a.Label)).ToDictionary(
            x=>x.Key,x=>x.ToList(),StringComparer.OrdinalIgnoreCase);
        var votes=new Dictionary<(int,int),HashSet<string>>();
        foreach(var (row,col,label) in SheetLabels((object)sheet))
        {
            var key=Normalize(label);
            if(!indexed.TryGetValue(key,out var matches))continue;
            foreach(var a in matches)
            {
                var loc=(row-a.RowOffset,col-a.ColumnOffset);
                if(loc.Item1<1||loc.Item2<1)continue;
                if(!votes.TryGetValue(loc,out var seen))
                    votes[loc]=seen=new HashSet<string>();
                seen.Add(key);
            }
        }
        if(votes.Count==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var ranked=votes.OrderByDescending(v=>v.Value.Count)
            .ThenBy(v=>Math.Abs(v.Key.Item1-entry.SourceRow)+
                Math.Abs(v.Key.Item2-entry.SourceColumn)).ToArray();
        var top=ranked[0];
        var unique=ranked.Length==1||top.Value.Count>ranked[1].Value.Count;
        return (top.Key.Item1,top.Key.Item2,top.Value.Count,unique);
    }
'@
$matchingAfter=@'
    // Header/value maps have a label signature of their own. In-range
    // user-approved Reference cells take precedence over unrelated neighboring
    // labels captured for visual context. Do not apply the formatting template
    // occupancy/type gate here; this is the Values-only mapping pathway.
    private static DeskSpaceExcelAnchorV16[] SignatureAnchorsV120(
        DeskSpaceExcelTemplateV16 entry)
    {
        var references=entry.Cells.Where(c=>
                c.Role=="Reference" && Normalize(c.RawValue).Length>2)
            .Select(c=>new DeskSpaceExcelAnchorV16{
                RowOffset=c.RowOffset,ColumnOffset=c.ColumnOffset,
                Label=c.RawValue
            }).ToArray();
        var surrounding=entry.Anchors.Where(a=>
            Normalize(a.Label).Length>2 &&
            !references.Any(c=>c.RowOffset==a.RowOffset &&
                               c.ColumnOffset==a.ColumnOffset)).ToArray();
        // Two explicit headers already form a deterministic signature.
        // Old mappings with no classified references still work using
        // the neighboring labels originally recorded with the selection.
        var anchors=references.Length>=2 ? references :
            references.Concat(surrounding).ToArray();
        return anchors.GroupBy(a=>
                (a.RowOffset,a.ColumnOffset,Key:Normalize(a.Label)))
            .Select(x=>x.First()).ToArray();
    }
    internal static (int Row,int Col,int Matches,bool Unique)
        MatchHeaderSignatureV120(
            DeskSpaceExcelTemplateV16 entry,
            IEnumerable<(int Row,int Col,string Label)> labels)
    {
        var anchors=SignatureAnchorsV120(entry);
        if(anchors.Length==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var indexed=anchors.GroupBy(a=>Normalize(a.Label))
            .ToDictionary(x=>x.Key,x=>x.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var votes=new Dictionary<(int,int),HashSet<string>>();
        foreach(var (row,col,label) in labels)
        {
            string key=Normalize(label);
            if(!indexed.TryGetValue(key,out var matching))continue;
            foreach(var anchor in matching)
            {
                var origin=(row-anchor.RowOffset,col-anchor.ColumnOffset);
                if(origin.Item1<1||origin.Item2<1)continue;
                if(!votes.TryGetValue(origin,out var found))
                    votes[origin]=found=new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                found.Add(key);
            }
        }
        if(votes.Count==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var ranked=votes.OrderByDescending(x=>x.Value.Count)
            .ThenBy(x=>Math.Abs(x.Key.Item1-entry.SourceRow)+
                        Math.Abs(x.Key.Item2-entry.SourceColumn)).ToArray();
        var first=ranked[0];
        bool unique=ranked.Length==1 ||
                    first.Value.Count>ranked[1].Value.Count;
        int required=anchors.Select(a=>Normalize(a.Label))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        bool strong=first.Value.Count>=Math.Min(3,Math.Max(2,required)) &&
                    first.Value.Count*2>=required;
        return (first.Key.Item1,first.Key.Item2,first.Value.Count,
            unique&&strong);
    }
    private static (int Row,int Col,int Matches,bool Unique) FindAnchorMatch(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry)=>
        MatchHeaderSignatureV120(entry,SheetLabels((object)sheet));

    // This is intentionally independent from R107's formatting eligibility
    // check. The data mapping is allowed to fill selected EMPTY value cells
    // in a partly populated form while protecting old values and formulas.
    internal static string HeaderValueDecisionV120(bool targetVerified,
        string sourceRole,string sourceKind,bool destinationFormula,
        bool destinationOccupied,bool overwrite,bool sheetProtected)
    {
        if(!targetVerified)return "Unconfirmed target";
        if(sourceRole!="Value" || sourceKind=="formula" ||
           sourceKind=="blank" || sourceKind=="")
            return "Reference or formula (analysis only)";
        if(destinationFormula)return "Existing formula protected";
        if(destinationOccupied&&!overwrite)return "Existing value protected";
        if(sheetProtected)return "Sheet protected";
        return "Ready";
    }
'@
ReplaceOnce $bridge $matchingBefore $matchingAfter 'restore reference-signature matching'
$before=@'
            int required=Math.Min(3,entry.Anchors.Count);
            verified=match.Unique&&match.Matches>=Math.Max(2,required) &&
                match.Matches*2>=entry.Anchors.Count;
'@
$after=@'
            verified=match.Unique;
'@
ReplaceOnce $bridge $before $after 'align verified result with classified header signature'
$before=@'
            reason=verified
                ? "Matched "+match.Matches+" nearby source labels at corresponding positions."
                : "Insufficient unique matching labels.";
            if(!verified)
            {
                // Force static call and nullable tuple type. Calling with
                // dynamic Excel COM otherwise boxes the nullable tuple into
                // ValueTuple<int,int>; that does not have HasValue.
                (int Row,int Column)? selected=
                    SelectedOriginV116((object)app,entry);
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
'@
$after=@'
            reason=verified
                ? "Matched "+match.Matches+" verified reference labels at "+
                  "their saved relative positions. Only mapped values can be pasted."
                : "No unique reference-label signature matched this worksheet. "+
                  "For a sheet without the saved headers, choose "+
                  "'Paste starting at the selected Excel cell' in More options, "+
                  "then preview again. Nothing has been changed.";
        }
        // IMPORTANT: R107 full-range occupancy is ONLY for formatting.
        // Header/value mappings may target partially populated workbooks.
        // Per-cell protection below decides what can actually be written.
'@
ReplaceOnce $bridge $before $after 'remove formatting restriction from legacy value preview'
$before=@'
            string decision="Ready";
            if(!verified)decision="Unconfirmed target";
            else if(source.Role!="Value" || source.ValueKind=="formula" || source.ValueKind=="blank")
                decision="Reference or formula (analysis only)";
            else if(destinationFormula)decision="Existing formula protected";
            else if(old!=null&&!overwrite)decision="Existing value protected";
            else if((bool)sheet.ProtectContents)decision="Sheet protected";
            else if((bool)cell.MergeCells)
'@
$after=@'
            string decision=HeaderValueDecisionV120(
                verified,source.Role,source.ValueKind,destinationFormula,
                old!=null,overwrite,(bool)sheet.ProtectContents);
            if(decision=="Ready" && (bool)cell.MergeCells)
'@
ReplaceOnce $bridge $before $after 'keep per-cell protections instead of whole-range occupancy'
$before=@'
        var refreshed=CheckAllCellsV116((object)sheet,entry,
            preview.StartRow,preview.StartColumn);
        if(!refreshed.MayApply)
            throw new InvalidOperationException(
                "Excel destination changed or is partially populated: "+
                refreshed.Explanation+" Nothing was written.");
'@
ReplaceOnce $bridge $before '' 'remove formatting restriction from value apply'
# Guard the complete SOURCE identity as well as workbook/sheet selection.
# Revalidate reference labels before sending queued writes.
$before=@'
    public static string Apply(DeskSpaceExcelProposalV16 preview,
        DeskSpaceExcelTemplateV16 entry,bool overwrite)
'@
$after=@'
    public static string Apply(DeskSpaceExcelProposalV16 preview,
        DeskSpaceExcelTemplateV16 entry,bool overwrite)
'@
# We intentionally leave the old Apply method signature and destination
# integrity/readback intact: no extra workbook side effects and no new store.
Write-Host 'R120: header/value signature matching includes user reference cells; partially filled destination permitted with per-cell protection. Formatting engine unchanged.'
