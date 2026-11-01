$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFullRangeGateV107.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R242 "+$label+" expected one anchor; found "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
        int count=destination.Count;
        int filled=destination.Count(x=>x.HasData);
'@ @'
        // Validate the entire physical source/destination type metadata even
        // if the current target is empty. A blank destination is NOT permission
        // to apply formats from a broken, unknown or contradictory template.
        for(int i=0;i<source.Count;i++)
        {
            if(!ValidCellV242(source[i]) || !ValidCellV242(destination[i]))
                return new(Status.RejectMismatch,destination.Count,
                    destination.Count(x=>x is {HasData:true}),
                    source.Count(x=>x is {HasData:true}),
                    "Cell "+(i+1)+": missing, unknown or inconsistent saved "+
                    "storage type. Recapture template; no changes allowed.");
        }
        int count=destination.Count;
        int filled=destination.Count(x=>x.HasData);
'@ 'reject invalid metadata before empty target acceptance'
Swap @'
    private static string Normalize(string value)
    {
        if(value.Equals("Date",StringComparison.OrdinalIgnoreCase))
            return "Number";
        return value.ToUpperInvariant() switch {
            "FORMULA"=>"Formula","NUMBER"=>"Number",
            "BOOLEAN"=>"Boolean","TEXT"=>"Text",
            "ERROR"=>"Error","BLANK"=>"Blank",_=>value
        };
    }
'@ @'
    // Excel Value2 exposes actual dates as numeric serials. All other
    // storage classes remain distinct, especially Formula vs literal Text.
    // Unknown/unreadable types must NEVER compare equal by coincidence.
    private static string Normalize(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return "";
        return value.Trim().ToUpperInvariant() switch
        {
            "DATE" or "NUMBER"=>"Number",
            "FORMULA"=>"Formula",
            "BOOLEAN"=>"Boolean",
            "TEXT"=>"Text",
            "ERROR"=>"Error",
            "BLANK"=>"Blank",
            _=>""
        };
    }
    internal static bool ValidCellV242(Cell? cell)
    {
        if(cell==null)return false;
        var type=Normalize(cell.StoredType);
        if(type.Length==0)return false;
        // Blank means no input and no formula. Any other stored type
        // requires an actual input; a corrupt saved template is not trusted.
        return cell.HasData?type!="Blank":type=="Blank";
    }
'@ 'known-only normalized storage types and occupancy consistency'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R242: Excel full-range preflight now rejects incomplete/corrupt source types even for empty destination; no Excel COM writes added.'
