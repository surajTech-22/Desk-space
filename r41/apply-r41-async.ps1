$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Edit([string]$file,[string]$before,[string]$after)
{
 $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 if(!$s.Contains($before)){throw "R41 missing Excel worker anchor in $file : $($before.Substring(0,[Math]::Min(110,$before.Length)))"}
 $s=$s.Replace($before,$after)
 [IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
}
$clip="$root/DeskSpaceExcelClipboardV39.cs"
Edit $clip '    private bool disposed,ownClipboardWrite,manualOpen,userHidden,collapsed,dragging,customPosition;' '    private bool disposed,ownClipboardWrite,manualOpen,userHidden,collapsed,dragging,customPosition,captureBusy;'
Edit $clip @'
    private void CaptureSelection(bool clipboardEvent)
    {
        if(disposed)return;
        try
        {
            // Only pick up genuine copied Excel cell ranges, never normal
            // text, files, images or passwords from the Windows clipboard.
            if(clipboardEvent && !DeskSpaceExcelBridgeV16.ExcelCopyActive())return;
            var entry=DeskSpaceExcelBridgeV16.Capture(
                "Cells · "+DateTime.Now.ToString("HH:mm:ss"),"",false);
            if(entry.Cells.Count==0)return;
'@ @'
    private async void CaptureSelection(bool clipboardEvent)
    {
        if(disposed || captureBusy)return;
        captureBusy=true;
        try
        {
            // Read native Excel data on a separate STA thread. Clipboard
            // notifications never block scrolling or WPF repainting.
            var entry=clipboardEvent
                ? await DeskSpaceExcelWorkerV41.CopyEventAsync()
                : await DeskSpaceExcelWorkerV41.CaptureAsync(
                    "Cells · "+DateTime.Now.ToString("HH:mm:ss"),"",true);
            if(disposed || entry==null || entry.Cells.Count==0)return;
'@
Edit $clip @'
        catch(Exception){/* Excel may be busy or clipboard owned by another process. */}
    }
'@ @'
        catch(Exception ex)
        {
            if(!clipboardEvent&&!disposed)
                MessageBox.Show(ex.Message,"DeskSpace Excel clipboard",
                    MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally{captureBusy=false;}
    }
'@
Edit $clip @'
    private void PasteExcel(DeskSpaceExcelTemplateV16 clip)
    {
        try
        {
            // Explicit user action and preview. Paste values into selected
            // origin, leave formulas/formatting unchanged and make a backup.
            var preview=DeskSpaceExcelBridgeV16.Preview(clip,true,true);
'@ @'
    private async void PasteExcel(DeskSpaceExcelTemplateV16 clip)
    {
        try
        {
            // Preview safely off UI dispatcher.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(clip,true,true);
'@
Edit $clip '            var backup=DeskSpaceExcelBridgeV16.Apply(preview,clip,true);' '            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(preview,clip,true);'
$ui="$root/DeskSpaceExcelV16.cs"
Edit $ui @'
    private void Preview()
    {
        var entry=Selected;
        latest=DeskSpaceExcelBridgeV16.Preview(entry,
            manual.IsChecked==true,overwrite.IsChecked==true);
'@ @'
    private async void Preview()
    {
        try
        {
        var entry=Selected;
        status.Text="Comparing workbook labels and destination formulas…";
        latest=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
            manual.IsChecked==true,overwrite.IsChecked==true);
'@
Edit $ui @'
            "No safe automatic position found. Select the first target cell in Excel, tick manual target, and preview.";
    }
    private void Apply()
'@ @'
            "No safe automatic position found. Select the first target cell in Excel, tick manual target, and preview.";
        }
        catch(Exception ex)
        {
            status.Text="Excel preview unavailable: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel Preview",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private async void Apply()
'@
Edit $ui @'
    private async void Apply()
    {
        var entry=Selected;
'@ @'
    private async void Apply()
    {
        try
        {
        var entry=Selected;
'@
Edit $ui @'
        var backup=DeskSpaceExcelBridgeV16.Apply(latest,entry,overwrite.IsChecked==true);
'@ @'
        status.Text="Creating workbook backup and pasting values into verified cells…";
        var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
            latest,entry,overwrite.IsChecked==true);
'@
Edit $ui @'
        latest=null;
    }
    public void Dispose()
'@ @'
        latest=null;
        }
        catch(Exception ex)
        {
            status.Text="Excel autofill stopped: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel autofill",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    public void Dispose()
'@
Write-Host 'R41: COM cell capture, clipboard events, destination preview, backup and formula-safe paste run on STA workers, not DeskSpace UI.'
