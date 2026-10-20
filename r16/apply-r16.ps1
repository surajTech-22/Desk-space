$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($file in @('DeskSpaceExcelEngineV16.cs','DeskSpaceExcelModuleV16.cs'))
{
    Copy-Item -LiteralPath "r16/$file" -Destination "$root/$file" -Force
}
function LoadSource([string]$path){
    [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function SaveSource([string]$path,[string]$value){
    [IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))
}
# Add the workbook module to the EXISTING main DeskSpace window.
$xaml="$root/MainWindow.xaml"
$s=LoadSource $xaml
$anchor='    </TabControl>'
if(!$s.Contains($anchor)){throw "R16 main tab anchor missing"}
$tab=@'
      <TabItem Tag="Excel" Header="▦  Excel Autofill">
        <Grid x:Name="ExcelModuleSlot" Background="White"/>
      </TabItem>
'@
if($s.Contains('ExcelModuleSlot')){throw "R16 Excel module already present"}
$s=$s.Replace($anchor,$tab+$anchor)
SaveSource $xaml $s

$main="$root/MainWindow.xaml.cs"
$s=LoadSource $main
$init='InitializeComponent();'
if(!$s.Contains($init)){throw 'R16 MainWindow constructor initializer missing'}
if(($s.Split([string[]]@($init),[StringSplitOptions]::None).Count-1) -ne 1){
    throw 'R16 unexpected number of MainWindow constructors'}
$s=$s.Replace($init,$init+[Environment]::NewLine+@'
        // Excel lives in the main application tab, not another floating window.
        ExcelModuleSlot.Children.Add(new DeskSpaceExcelModuleV16().Create());
'@)
SaveSource $main $s
Write-Host 'R16: workbook-backed Excel capture, visual selection, encrypted profiles and preview-first autofill integrated into DeskSpace.'
