$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$pdf=[IO.File]::ReadAllText("$root/DeskSpacePdfPreviewV217.cs")
$preview=[IO.File]::ReadAllText("$root/DeskSpaceFilePreviewV85.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @('using Windows.Data.Pdf;',
 'PdfDocument.LoadFromFileAsync(file)',
 'selected.RenderToStreamAsync(buffer,new PdfPageRenderOptions{',
 'DestinationWidth=300',
 'document.PageCount',
 'internal const uint MaxPages=100',
 'internal const long MaxFileBytes=32L*1024*1024',
 'FileAttributes.ReparsePoint',
 'previous.IsEnabled=page>0;',
 'next.IsEnabled=(uint)(page+1)<available;',
 'panel.Loaded+=async'))
{
 if(!$pdf.Contains($needle)){throw ('R217 native PDF reader missing '+$needle)}
}
if(!$preview.Contains('DeskSpacePdfPreviewV217.Create(path)'))
{
 throw 'R217 PDF renderer not connected to the on-demand Deck preview'
}
if(!$smoke.Contains('TestNativePdfPreviewV217();')){
 throw 'R217 native UI acceptance not wired'
}
if($pdf.Contains('Process.Start(') -or $pdf.Contains('File.WriteAll') -or
   $pdf.Contains('File.Delete(') -or $pdf.Contains('Clipboard.')){
 throw 'R217 PDF preview must not modify source files or clipboard'
}
Write-Host 'R217 STATIC PASS: native Windows PDF pages, bounded on-demand preview, no writes/extraction.'
