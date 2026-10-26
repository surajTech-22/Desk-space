$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$file="$r/DeskSpaceExcelFormatApplyV109.cs"
Copy-Item 'r139/DeskSpaceExcelDirectReadbackV139.cs' "$r/DeskSpaceExcelDirectReadbackV139.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
$old=@'
                if(!mergeChildren.Contains((s.Row,s.Column)) &&
                   (s.NumberFormat!=a.NumberFormat ||
                   s.FontName!=a.FontName ||
                   Math.Abs(s.FontSize-a.FontSize)>0.5 ||
                   s.Bold!=a.Bold || s.Italic!=a.Italic ||
                   s.Underline!=a.Underline || s.StrikeThrough!=a.StrikeThrough ||
                   s.FillBgr!=a.FillBgr || s.FontBgr!=a.FontBgr ||
                   s.DirectFillPattern!=a.DirectFillPattern ||
                   s.BorderLeft!=a.BorderLeft||s.BorderRight!=a.BorderRight ||
                   s.BorderTop!=a.BorderTop||s.BorderBottom!=a.BorderBottom))
                    throw new InvalidDataException(
                        "Formatting readback differs at "+
                        Text(live.Range.Cells[s.Row+1,s.Column+1].Address));
'@
$new=@'
                if(!mergeChildren.Contains((s.Row,s.Column)))
                {
                    var directDifferences=
                        DeskSpaceExcelDirectReadbackV139.Differences(s,a);
                    if(directDifferences.Length>0)
                        throw new InvalidDataException(
                            "Formatting readback differs at "+
                            Text(live.Range.Cells[s.Row+1,s.Column+1].Address)+
                            ": "+string.Join("; ",directDifferences)+
                            ". Excel's automatic/theme colours are compared "+
                            "by their actual colour mode, not cross-workbook "+
                            "computed RGB. Existing changes will be rolled back.");
                }
'@
$old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R139 direct readback anchor count $n"}
$s=$s.Replace($old,$new)
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R139: direct Excel readback now differentiates theme/automatic no-fill from explicit RGB. Still verifies number format/font properties/underline/borders; exact TableStyle and non-table display checks remain unchanged. Field-level mismatch is shown before safe rollback.'