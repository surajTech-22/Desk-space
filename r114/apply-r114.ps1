$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
foreach($pair in @(
  @('r114/DeskSpaceExcelSheetViewV114.cs',"$r/DeskSpaceExcelSheetViewV101.cs"),
  @('r114/DeskSpaceExcelSemanticV114.cs',"$r/DeskSpaceExcelSemanticV106.cs"),
  @('r114/DeskSpaceStableHoverV114.cs',"$r/DeskSpaceStableHoverV93.cs")
)){
 if(!(Test-Path -LiteralPath $pair[0])){throw "Missing R114 source $($pair[0])"}
 Copy-Item -LiteralPath $pair[0] -Destination $pair[1] -Force
}
function Patch([string]$path,[string]$before,[string]$after,[string]$label){
 $crlf=[string][char]13+[string][char]10
 $lf=[string][char]10
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf)
 $b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R114 $($label): expected one match, got $n"}
 [IO.File]::WriteAllText($path,$s.Replace($a,$b),
     [Text.UTF8Encoding]::new($false))
}
$bridge="$r/DeskSpaceExcelBridgeV16.cs"
Patch $bridge @'
                dynamic cell=range.Cells[r,c];
                var raw=(object?)cell.Value2;
                bool hasFormula=Formula(cell);
'@ @'
                dynamic cell=range.Cells[r,c];
                object? raw;
                try {raw=(object?)cell.Value2;}
                catch(Exception excelError) when(
                    excelError is COMException or ArgumentException or
                        Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){
                    throw new InvalidOperationException(
                        "Excel selection capture failed reading the value at "+
                        "row "+(row+r-1)+", column "+(column+c-1)+
                        " ("+excelError.GetType().Name+", 0x"+
                        excelError.HResult.ToString("X8")+"). "+
                        excelError.Message+". No workbook cells were edited.",
                        excelError);
                }
                bool hasFormula=Formula(cell);
'@ 'cell value read failure must never silently convert a value to blank'
$worker="$r/DeskSpaceExcelWorkerV41.cs"
Patch $worker @'
            catch(Exception ex){result.TrySetException(ex);}
'@ @'
            catch(Exception ex)
            {
                // Log exception type/HRESULT/call location; never log captured
                // cell data. Continue to surface exact original error to UI.
                try
                {
                    var downloads=System.IO.Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile),"Downloads");
                    System.IO.Directory.CreateDirectory(downloads);
                    var path=System.IO.Path.Combine(downloads,
                        "DeskSpace_Excel_COM_Error.log");
                    var details=DateTime.Now.ToString("o")+" | "+
                        ex.GetType().FullName+" | 0x"+
                        ex.HResult.ToString("X8")+" | "+
                        ex.Message+Environment.NewLine+
                        ex.StackTrace+Environment.NewLine+
                        "------------------------------"+Environment.NewLine;
                    System.IO.File.AppendAllText(path,details);
                }
                catch(System.IO.IOException){}
                catch(UnauthorizedAccessException){}
                result.TrySetException(ex);
            }
'@ 'record real Excel capture failure details without cell input values'
Write-Host 'R114: contextual Excel selection errors and logging; grouped Running Apps list immediately on hover/click.'
