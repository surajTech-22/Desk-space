$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs'
function Swap([string]$from,[string]$to,[string]$label){
 $nl=[string][char]10;$crlf=[string][char]13+$nl
 $s=[IO.File]::ReadAllText($script:file).Replace($crlf,$nl)
 $a=$from.Replace($crlf,$nl);$b=$to.Replace($crlf,$nl)
 $hits=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R131 $label missing/ambiguous ($hits)"}
 [IO.File]::WriteAllText($script:file,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap @'
    private static Checked Inspect(DeskSpaceExcelTemplateV16 template)
'@ @'
    // Excel can reject a TableStyle setter even after a template is saved.
    // Verify that all requested styles exist in THIS workbook before any
    // SaveCopyAs, table creation or other mutation. Fail closed when Office
    // cannot verify style availability instead of risking a partial write.
    private static void PreflightTableStylesV131(
        dynamic workbook,IEnumerable<DeskSpaceExcelTableV106> tables,
        List<string> problems)
    {
        foreach(var name in tables.Select(t=>t.TableStyle)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(string.IsNullOrWhiteSpace(name) ||
                name.StartsWith("System._ComObject",StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("Saved table style is invalid or missing. Recapture this format from Excel; existing saved mappings are preserved.");
                continue;
            }
            try
            {
                dynamic available=workbook.TableStyles[name];
                string found=Text((object?)available.Name);
                if(!string.Equals(found,name,StringComparison.OrdinalIgnoreCase))
                    problems.Add("Table style '"+name+
                        "' does not resolve in the destination workbook.");
            }
            catch(Exception error) when(error is COMException ||
                error is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ||
                error is System.Reflection.TargetInvocationException ||
                error is ArgumentException)
            {
                problems.Add("Table style '"+name+
                    "' is unavailable or cannot be verified in the destination workbook: "+
                    error.Message+". No workbook changes will be made.");
            }
        }
    }
    private static void SetTableStyleV131(dynamic table,string required)
    {
        try
        {
            var current=Text((object?)table.TableStyle);
            // Avoid redundant Excel COM setter calls. On some versions of
            // Office this setter throws even though the style is unchanged.
            if(string.Equals(current,required,StringComparison.OrdinalIgnoreCase))
                return;
            table.TableStyle=required;
            var observed=Text((object?)table.TableStyle);
            if(!string.Equals(observed,required,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Excel TableStyle readback differs from the requested style.");
        }
        catch(Exception error) when(error is COMException ||
            error is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ||
            error is System.Reflection.TargetInvocationException)
        {
            throw new InvalidOperationException(
                "Excel could not set or read table style '"+required+
                "'. The style must be supported by the destination workbook.",error);
        }
    }
    private static Checked Inspect(DeskSpaceExcelTemplateV16 template)
'@ 'preflight and guarded style writer'
Swap @'
        if(features.Tables.Any(x=>x.TableStyle.Length==0))
            problems.Add("Excel Table style was not captured.");
'@ @'
        if(features.Tables.Any(x=>x.TableStyle.Length==0))
            problems.Add("Excel Table style was not captured.");
        PreflightTableStylesV131(app.ActiveWorkbook,features.Tables,problems);
'@ 'validate target workbook table styles without mutation'
Swap @'
            created.Name=table.Name;
            created.TableStyle=table.TableStyle;
'@ @'
            created.Name=table.Name;
            SetTableStyleV131(created,table.TableStyle);
'@ 'new-table style call'
Swap @'
                    table.TableStyle=metadata.TableStyle;
'@ @'
                    SetTableStyleV131(table,metadata.TableStyle);
'@ 'avoid redundant TableStyle invocation in existing table'
Write-Host 'R131 Excel: read-only workbook style preflight, no redundant TableStyle COM invocation, unchanged full-range gate and verified workbook backup/rollback.'
