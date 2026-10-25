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
# Retain the matched header signature as a volatile preview credential;
# never persist it or mix it with stored format templates.
ReplaceOnce $bridge @'
    public bool Verified {get;set;}
    public List<DeskSpaceExcelChangeV16> Changes {get;set;}=new();
'@ @'
    public bool Verified {get;set;}
    public bool UsedReferenceSignatureV120 {get;set;}
    public List<DeskSpaceExcelChangeV16> Changes {get;set;}=new();
'@ 'tag reference-verified preview'
ReplaceOnce $bridge @'
            Verified=verified,Reason=reason
'@ @'
            Verified=verified,Reason=reason,
            UsedReferenceSignatureV120=!useSelection
'@ 'record whether target came from signature or manual selection'
ReplaceOnce $bridge @'
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        // Detect modifications made since the preview before touching the workbook.
'@ @'
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        if(preview.UsedReferenceSignatureV120)
        {
            var matched=FindAnchorMatch((object)sheet,entry);
            if(!matched.Unique || matched.Row!=preview.StartRow ||
               matched.Col!=preview.StartColumn)
                throw new InvalidOperationException(
                    "Header/reference signature changed since preview. "+
                    "No Excel cells changed. Preview the value mapping again.");
        }
        // Detect modifications made since the preview before touching the workbook.
'@ 'prevent stale header signature before writes'

$smoke='r36/UiSmoke/Program.cs'
ReplaceOnce $smoke @'
            TestNullableSelectedOriginV119();
'@ @'
            TestNullableSelectedOriginV119();
            TestHeaderValueSignatureV120();
'@ 'run header signature and partially filled target tests'

$method=@'
    // Values-only mapping regression, including the reported 8/30-filled
    // worksheet. This tests production signature and write-decision helpers.
    private static void TestHeaderValueSignatureV120()
    {
        var engine=Product.GetType("DeskSpace.DeskSpaceExcelBridgeV16",true)!;
        var template=Product.GetType("DeskSpace.DeskSpaceExcelTemplateV16",true)!;
        var cell=Product.GetType("DeskSpace.DeskSpaceExcelCellV16",true)!;
        var entry=Activator.CreateInstance(template,true)!;
        var cells=(System.Collections.IList)template.GetProperty("Cells")!.
            GetValue(entry)!;
        void Add(string role,string label,int row,int col)
        {
            var saved=Activator.CreateInstance(cell,true)!;
            cell.GetProperty("Role")!.SetValue(saved,role);
            cell.GetProperty("RawValue")!.SetValue(saved,label);
            cell.GetProperty("RowOffset")!.SetValue(saved,row);
            cell.GetProperty("ColumnOffset")!.SetValue(saved,col);
            cells.Add(saved);
        }
        Add("Reference","Signature",0,0);
        Add("Reference","Authorised signatory",1,0);
        Add("Value","ACME",0,1);
        var method=engine.GetMethod("MatchHeaderSignatureV120",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object Match(params (int Row,int Col,string Label)[] labels)=>
            method.Invoke(null,new object[]{entry,labels})!;
        var correct=(ValueTuple<int,int,int,bool>)Match(
            (22,7,"SIGNATURE"),(23,7,"Authorised Signatory"),
            (44,7,"Signature"));
        Assert(correct.Item1==22&&correct.Item2==7&&correct.Item3==2&&
               correct.Item4,
            "R120 two explicit reference labels must match exact relative signature");
        var missing=(ValueTuple<int,int,int,bool>)Match(
            (22,7,"Signature"),(44,7,"Unrelated"));
        Assert(!missing.Item4,"R120 one label cannot authorize automatic paste");
        var duplicate=(ValueTuple<int,int,int,bool>)Match(
            (22,7,"Signature"),(23,7,"Authorised signatory"),
            (44,7,"Signature"),(45,7,"Authorised signatory"));
        Assert(!duplicate.Item4,"R120 ambiguous repeated signature must block paste");
        var decision=engine.GetMethod("HeaderValueDecisionV120",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string Decide(bool verified,string role,string kind,
            bool formula,bool occupied,bool overwrite=false,
            bool protectedSheet=false)=>
            (string)decision.Invoke(null,new object[]{
                verified,role,kind,formula,occupied,overwrite,protectedSheet})!;
        // 8 occupied cells (with existing values), 22 empty cells:
        // Values-only autofill is allowed to fill the blank cells, but may
        // NEVER erase the 8 occupied cells under the default safety option.
        int ready=0,protectedValues=0;
        for(int i=0;i<30;i++)
        {
            var result=Decide(true,"Value","number",false,i<8);
            if(result=="Ready")ready++;
            if(result=="Existing value protected")protectedValues++;
        }
        Assert(ready==22&&protectedValues==8,
            "R120 populated 8/30 destination must allow 22 safe value writes");
        Assert(Decide(true,"Reference","text",false,false)!= "Ready",
            "R120 reference/signature cells must never be pasted");
        Assert(Decide(true,"Value","number",true,false)!="Ready",
            "R120 existing Excel formula must never be overwritten");
        Assert(Decide(true,"Value","number",false,true,true)=="Ready",
            "R120 explicit replace permits non-formula occupied value");
        Assert(Decide(false,"Value","number",false,false)!="Ready",
            "R120 unmatched or ambiguous headers cannot authorize paste");
        Assert(Decide(true,"Value","blank",false,false)!="Ready",
            "R120 blank captured cells cannot clear target values");
        Assert(Decide(true,"Value","text",false,false,false,true)!="Ready",
            "R120 protected worksheets cannot receive mapped values");
        Console.WriteLine(
            "R120 PASS: header signatures, duplicate/missing labels, 8/30 partial values, source references, formulas and overwrite protections.");
    }

'@
ReplaceOnce $smoke @'
    private static void TestExcelTypeAwareSheetV101()
'@ ($method+'    private static void TestExcelTypeAwareSheetV101()') 'WPF runtime header/value regression'
Write-Host 'R120: values-only header/value signatures and per-cell protections are independent from format-template occupancy rules.'
