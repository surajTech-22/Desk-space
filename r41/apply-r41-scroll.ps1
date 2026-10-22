$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r41/DeskSpaceSmoothScrollV41.cs' "$root/DeskSpaceSmoothScrollV41.cs" -Force
function Swap([string]$path,[string]$before,[string]$after)
{
 $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 if(!$s.Contains($before)){throw "R41 smooth scroll app integration missing: $before"}
 $s=$s.Replace($before,$after)
 [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
Swap "$root/MainWindow.xaml.cs" @'
    public MainWindow()
    {
        InitializeComponent();
'@ @'
    public MainWindow()
    {
        DeskSpaceSmoothScrollV41.Enable();
        InitializeComponent();
'@
Swap "$root/WidgetHost.cs" @'
    public WidgetShellWindow()
    {
        Title = "DeskSpace Live Donut";
'@ @'
    public WidgetShellWindow()
    {
        // One shared mouse-wheel routing implementation covers nested
        // panels in the main Workspace AND the independent floating circle.
        DeskSpaceSmoothScrollV41.Enable();
        Title = "DeskSpace Live Donut";
'@
Write-Host 'R41 global smooth scroll routing enabled for Workspace, Excel mapping, Information wheel, lists, notes, clipboard and donut.'
