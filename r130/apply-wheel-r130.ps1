$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$wheel="$r/DeskSpaceExcelWheelV42.cs"
Copy-Item 'r130/DeskSpaceExcelFormatWorkerV130.cs' "$r/DeskSpaceExcelFormatWorkerV130.cs" -Force
function Swap([string]$a,[string]$b,[string]$label) {
  $lf=[string][char]10;$crlf=[string][char]13+$lf
  $s=[IO.File]::ReadAllText($script:wheel).Replace($crlf,$lf)
  $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
  $count=([regex]::Matches($s,[regex]::Escape($a))).Count
  if($count -ne 1){
    $hit=$s.IndexOf('drillTimer.Tick', [StringComparison]::Ordinal)
    if($hit -ge 0){Write-Host ('R130 diagnostic surrounding drillTimer: '+
        $s.Substring($hit,[Math]::Min(1300,$s.Length-$hit)))}
    throw "R130 $($label) expected 1 match, found $count"
  }
  [IO.File]::WriteAllText($script:wheel,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
function Section([string]$a,[string]$z,[string]$body,[string]$label){
  $lf=[string][char]10;$crlf=[string][char]13+$lf
  $s=[IO.File]::ReadAllText($script:wheel).Replace($crlf,$lf)
  $a=$a.Replace($crlf,$lf);$z=$z.Replace($crlf,$lf)
  $start=$s.IndexOf($a,[StringComparison]::Ordinal)
  if($start -lt 0 -or $s.IndexOf($a,$start+1,[StringComparison]::Ordinal) -ge 0)
    {throw "R130 $($label) ambiguous start"}
  $end=$s.IndexOf($z,$start+$a.Length,[StringComparison]::Ordinal)
  if($end -lt 0){
    $end=$s.IndexOf('    private void ExpandRing(',
       $start+$a.Length,[StringComparison]::Ordinal)
  }
  if($end -lt 0){throw "R130 $($label) no end"}
  [IO.File]::WriteAllText($script:wheel,
    $s.Substring(0,$start)+$body.Replace($crlf,$lf)+$s.Substring($end),
    [Text.UTF8Encoding]::new($false))
}
Swap @'
    private List<DeskSpaceExcelTemplateV16> formats=new();
'@ @'
    // Existing field contains ONLY information/header-value templates.
    private List<DeskSpaceExcelTemplateV16> formats=new();
    private List<DeskSpaceExcelTemplateV16> formatTemplatesV130=new();
    private const string FormatRootV130="@formats";
    private static bool IsFormatBranchV130(string group)=>
        group==FormatRootV130 ||
        group.StartsWith(FormatRootV130+"/",StringComparison.Ordinal);
    private IEnumerable<DeskSpaceExcelTemplateV16> SourceV130(string group)=>
        IsFormatBranchV130(group)?formatTemplatesV130:formats;
    private static string RelativeGroupV130(string path)=>
        path==FormatRootV130?"":path.Substring(FormatRootV130.Length+1);
'@ 'explicit donut library identity'
Swap @'
    public int SavedCount=>formats.Count;
'@ @'
    public int SavedCount=>formats.Count+formatTemplatesV130.Count;
'@ 'count both libraries'
Section @'
    private List<(string Label,string Command,string Group,Guid? Id)>
        BranchItems(string selectedGroup)
'@ @'
    private void ExpandRing(int depth,string group,double angle)
'@ @'
    private List<(string Label,string Command,string Group,Guid? Id)>
        BranchItems(string selectedGroup)
    {
        var entries=new List<(string Label,string Command,string Group,Guid? Id)>();
        bool formatBranch=IsFormatBranchV130(selectedGroup);
        string relative=formatBranch?RelativeGroupV130(selectedGroup):selectedGroup;
        var groups=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var template in SourceV130(selectedGroup))
        {
            string path=Group(template),tail;
            if(relative.Length==0)tail=path;
            else if(path.StartsWith(relative+"/",StringComparison.OrdinalIgnoreCase))
                tail=path.Substring(relative.Length+1);
            else continue;
            if(tail.Length>0)groups.Add(tail.Split('/')[0]);
        }
        foreach(var group in groups)
        {
            var path=relative.Length==0?group:relative+"/"+group;
            entries.Add(("▸ "+group,"group",
                formatBranch?FormatRootV130+"/"+path:path,null));
        }
        foreach(var template in SourceV130(selectedGroup).Where(x=>
            string.Equals(Group(x),relative,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase))
            entries.Add((template.Name,formatBranch?"format":"mapping",
                selectedGroup,template.Id));
        entries.Add(("+ Manage","manage",selectedGroup,null));
        return entries;
    }
'@ 'branch nested groups to correct saved data library'
Swap @'
            level=ExcelLevel.Functions;
            groupPath="";
'@ @'
            formatTemplatesV130=DeskSpaceExcelStoreV16.LoadFormats()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
            level=ExcelLevel.Functions;
            groupPath="";
'@ 'load saved format library in addition to existing information store'
Swap @'
            entries.Add(("Information","information","",null));
            return entries;
'@ @'
            entries.Add(("Information","information","",null));
            entries.Add(("Formats & formulas","formats","",null));
            return entries;
'@ 'separate donut branches'
Swap @'
                if(displayed[slot].Command=="information")
                    ExpandRing(1,"",RootAngle(slot),rootStep);
'@ @'
                if(displayed[slot].Command=="information" ||
                   displayed[slot].Command=="formats")
                    ExpandRing(1,displayed[slot].Command=="formats"
                        ?FormatRootV130:"",RootAngle(slot),rootStep);
'@ 'hover both donut root branches'
Swap @'
        if(item.Command=="information"||item.Command=="group")
'@ @'
        if(item.Command=="information"||item.Command=="formats"||
           item.Command=="group")
'@ 'hover any branch'
Swap @'
        if(item.Command=="information")
        {
            ExpandRing(1,"",RootAngle(slot),rootStep);
            return;
        }
'@ @'
        if(item.Command=="information" || item.Command=="formats")
        {
            ExpandRing(1,item.Command=="formats"?FormatRootV130:"",
                RootAngle(slot),rootStep);
            return;
        }
'@ 'activate root format branch'
Swap @'
        else if(entry.Command=="mapping")
        {
            var item=formats.FirstOrDefault(x=>x.Id==entry.Id);
            if(item!=null)Preview(item);
        }
'@ @'
        else if(entry.Command=="mapping" || entry.Command=="format")
        {
            var source=entry.Command=="format"?formatTemplatesV130:formats;
            var item=source.FirstOrDefault(x=>x.Id==entry.Id);
            if(item!=null)
            {
                if(entry.Command=="format")PreviewFormatV130(item);
                else Preview(item);
            }
        }
'@ 'format-only hover preview in child ring'
Swap @'
        if(item.Command!="mapping" || !item.Id.HasValue)return;
        var format=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(format==null)return;
        if(!ExcelIsOpen()){Preview(format);return;}
        HideArc();collapseParent();_=PasteAsync(format);
'@ @'
        if((item.Command!="mapping" && item.Command!="format")||
           !item.Id.HasValue)return;
        bool formatOnly=item.Command=="format";
        var template=(formatOnly?formatTemplatesV130:formats)
            .FirstOrDefault(f=>f.Id==item.Id.Value);
        if(template==null)return;
        if(!ExcelIsOpen())
        {
            if(formatOnly)PreviewFormatV130(template);else Preview(template);
            return;
        }
        HideArc();collapseParent();
        if(formatOnly)_=PasteFormatV130Async(template);
        else _=PasteAsync(template);
'@ 'format child click never calls information/value writer'
Swap @'
        if(item.Command!="mapping" || !item.Id.HasValue)return;
        var entry=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(entry==null)return;
        if(!ExcelIsOpen()){Preview(entry);return;}
        HideArc();collapseParent();
        _=PasteAsync(entry);
'@ @'
        if((item.Command!="mapping" && item.Command!="format")||
           !item.Id.HasValue)return;
        bool formatOnly=item.Command=="format";
        var entry=(formatOnly?formatTemplatesV130:formats)
            .FirstOrDefault(f=>f.Id==item.Id.Value);
        if(entry==null)return;
        if(!ExcelIsOpen())
        {
            if(formatOnly)PreviewFormatV130(entry);else Preview(entry);
            return;
        }
        HideArc();collapseParent();
        if(formatOnly)_=PasteFormatV130Async(entry);
        else _=PasteAsync(entry);
'@ 'other wheel click path library-aware'
Swap @'
    private void Preview(DeskSpaceExcelTemplateV16 entry)
'@ @'
    private void PreviewFormatV130(DeskSpaceExcelTemplateV16 entry)
    {
        shelfTitle.Text=entry.Name+" · formats & formulas";
        shelfBody.Children.Clear();
        var visual=entry.SheetVisualV101;
        shelfMessage.Text=visual==null?
            "This saved format has no complete Excel layout. Recapture in Workspace.":
            "Format mapping: "+visual.Rows+" × "+visual.Columns+
            " cells · "+(Group(entry).Length==0?"Ungrouped":Group(entry))+
            ". Original transaction data is not stored. Select the destination "+
            "in Excel before clicking this mapping to review full-range "+
            "eligibility. It will NEVER perform information/value paste.";
        Position();shelf.IsOpen=true;
    }
    private async System.Threading.Tasks.Task PasteFormatV130Async(
        DeskSpaceExcelTemplateV16 entry)
    {
        if(disposed)return;
        pasting=true;
        bool applying=false;
        try
        {
            Status("Format mapping · "+entry.Name,
                "Checking entire Excel destination; no changes made…");
            var review=await DeskSpaceExcelFormatWorkerV130.PreviewAsync(entry);
            if(disposed)return;
            if(!review.Eligible)
                throw new InvalidOperationException("Format paste blocked: "+review);
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
            applying=true;
            var result=await DeskSpaceExcelFormatWorkerV130.ApplyAsync(
                entry,review);
            if(disposed)return;
            Status("Format mapping applied",result);
            successTimer.Start();
        }
        catch(Exception ex)
        {
            if(!disposed)Status("Format mapping blocked",
                ex.Message+(applying?
                    " Inspect Excel and the safety backup.":
                    " No workbook changes were made."));
        }
        finally{pasting=false;}
    }
    private void Preview(DeskSpaceExcelTemplateV16 entry)
'@ 'separate approved format-only preview and writer'
Write-Host 'R130 Excel donut: Information and Formats branches read separate DPAPI stores, preserve grouped sub-donuts, and use distinct approved paste engines.'
