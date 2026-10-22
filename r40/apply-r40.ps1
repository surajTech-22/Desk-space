$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
# R40 is applied after the existing R39 source chain. Do not reconstruct a
# second Excel mapping engine or modify stored data.
Copy-Item 'r40/DeskSpaceExcelClipboardV40.cs' "$root/DeskSpaceExcelClipboardV39.cs" -Force
function Swap([string]$file,[string]$before,[string]$after)
{
    $old=$before.Replace([string][char]13+[char]10,[string][char]10)
    $new=$after.Replace([string][char]13+[char]10,[string][char]10)
    $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
    if(!$s.Contains($old)){throw "R40 original anchor not found in $file : $($old.Substring(0,[Math]::Min(90,$old.Length)))"}
    $s=$s.Replace($old,$new)
    [IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
}
# One same reusable Excel mapping surface, but its back button shows
# "Workspace" when embedded in the main app, "Circle" in a widget.
$excel="$root/DeskSpaceExcelV16.cs"
Swap $excel '    private readonly Action back;' @'
    private readonly Action back;
    private readonly bool inWorkspace;
'@
Swap $excel @'
    public DeskSpaceExcelV16(Action exit)
    {
        back=exit;
'@ @'
    public DeskSpaceExcelV16(Action exit):this(exit,false) {}
    public DeskSpaceExcelV16(Action exit,bool inMainWorkspace)
    {
        back=exit;
        inWorkspace=inMainWorkspace;
'@
Swap $excel '        var done=ActionButton("← Circle",back);' '        var done=ActionButton(inWorkspace?"← Workspace":"← Circle",back);'

# Main WPF workspace has a real dedicated tab, not another attempt to
# open the floating widget using a named pipe.
$xaml="$root/MainWindow.xaml"
Swap $xaml '<TabItem Tag="Clock" Header="◷  Clock &amp; Timer">' @'
<TabItem x:Name="ExcelMainTab" Tag="Excel" Header="▦  Excel Mapping">
         <Grid x:Name="ExcelMainHost" Margin="0" Loaded="ExcelMainHost_Loaded"/>
       </TabItem>
       <TabItem Tag="Clock" Header="◷  Clock &amp; Timer">
'@
# Make mapping discoverable without having to know the hidden Modules page.
Swap $xaml '<Button Content="Show Live Donut Widget" Click="ShowWidget_Click" Padding="16,10" Margin="0,0,10,10"/>' @'
<Button Content="Show Live Donut Widget" Click="ShowWidget_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="▦  Excel Mapping" Click="ExcelModule_Click" Padding="16,10" Margin="0,0,10,10"/>
'@
Swap $xaml '            <Button Tag="Excel" Content="▦  Excel reusable data" Click="ExcelModule_Click"' '            <Button Tag="Excel" Content="▦  Excel Mapping" Click="ExcelModule_Click"'

$main="$root/MainWindow.xaml.cs"
Swap $main @'
    private void ExcelModule_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenExcel();
    }
'@ @'
    // The mapping UI is genuinely embedded in the main application.
    // Both the left navigation tab and the Home/Modules shortcuts
    // select the same persistent instance, not a separate floating window.
    private DeskSpaceExcelV16? workspaceExcel;
    private void ExcelMainHost_Loaded(object sender,RoutedEventArgs e)
    {
        if(workspaceExcel!=null)return;
        workspaceExcel=new DeskSpaceExcelV16(
            ()=>MainTabs.SelectedIndex=0,inMainWorkspace:true);
        ExcelMainHost.Children.Add(workspaceExcel.Create());
    }
    private void ExcelModule_Click(object sender,RoutedEventArgs e)
    {
        MainTabs.SelectedItem=ExcelMainTab;
    }
'@
Write-Host 'R40: dedicated Excel mapping tab in main DeskSpace left navigation and Home; reusable screenshot/live mapping screen, not widget launcher.'
Write-Host 'R40: separate Excel shelf has draggable title, Close, minimize, restore and preserves clipboard contents.'
