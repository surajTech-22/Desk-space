$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r39/DeskSpaceExcelScreenshotV39.cs' "$root/DeskSpaceExcelScreenshotV39.cs" -Force
Copy-Item 'r39/DeskSpaceExcelClipboardV39.cs' "$root/DeskSpaceExcelClipboardV39.cs" -Force
function ReplaceIn([string]$path,[string]$old,[string]$new)
{
 $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$s.Contains($old)){throw "R39 host anchor missing: $($old.Substring(0,[Math]::Min(110,$old.Length)))"}
 $s=$s.Replace($old,$new)
 [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
$hostFile="$root/WidgetHost.cs"
ReplaceIn $hostFile '    private DeskSpaceDropShelfV15? attachedShelf;' @'
    private DeskSpaceDropShelfV15? attachedShelf;
    private DeskSpaceExcelClipboardV39? excelClipboardShelf;
'@
ReplaceIn $hostFile @'
            attachedShelf ??= new DeskSpaceDropShelfV15(this);
            attachedShelf.Enable();
'@ @'
            attachedShelf ??= new DeskSpaceDropShelfV15(this);
            attachedShelf.Enable();
            // Independent Excel-only clipboard, not part of the Files shelf.
            excelClipboardShelf ??= new DeskSpaceExcelClipboardV39(this);
            excelClipboardShelf.Enable();
'@
ReplaceIn $hostFile '            attachedShelf?.Dispose();' @'
            attachedShelf?.Dispose();
            excelClipboardShelf?.Dispose();
'@
ReplaceIn $hostFile @'
        menu.Items.Add("Excel · reusable data",null,(_,_)=>Dispatcher.Invoke(
            ()=>OpenInWidget("Excel")));
'@ @'
        menu.Items.Add("Excel · reusable data",null,(_,_)=>Dispatcher.Invoke(
            ()=>OpenInWidget("Excel")));
        menu.Items.Add("Excel Clipboard shelf",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceExcelClipboardV39.ShowActive));
'@
ReplaceIn $hostFile @'
    private void ClickSegment(string name)
    {
        if(name=="Information")
'@ @'
    private void ClickSegment(string name)
    {
        if(name=="Excel Clipboard")
        {
            CollapseRing();
            DeskSpaceExcelClipboardV39.ShowActive();
            return;
        }
        if(name=="Information")
'@
ReplaceIn $hostFile 'if(name is "Files" or "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Information" or "Settings")' 'if(name is "Files" or "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Information" or "Settings" or "Excel")'
Write-Host 'R39 Excel clipboard follows circle, auto-collects copied Excel cells, has independent menu and module routing.'
