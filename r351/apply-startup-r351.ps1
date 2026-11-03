$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=Join-Path $root 'App.xaml.cs'
$project=Join-Path $root 'DeskSpace.csproj'
$worker=Join-Path $root 'DeskSpaceStorageMaintenanceV351.cs'
$scriptFile=Join-Path $root 'DeskSpacePostLaunchCleanupV351.ps1'
foreach($file in @($app,$project)){
    if(!(Test-Path -LiteralPath $file)){throw ('R351 shipping source missing '+$file)}
}
$original=[IO.File]::ReadAllText('r351/DeskSpacePostLaunchCleanupV351.ps1')
$marker='# R351_GUARDED_HELPERS'
if(([regex]::Matches($original,[regex]::Escape($marker))).Count -ne 1){
    throw 'R351 one guarded helper marker required'
}
$helper=([IO.File]::ReadAllText('r348/DeskSpaceSafeStorageCleanupV348.ps1'))+
    [Environment]::NewLine+
    ([IO.File]::ReadAllText('r351/DeskSpaceStagedInstallerRetentionV351.ps1'))
$joined=$original.Replace($marker,$helper)
[IO.File]::WriteAllText($scriptFile,$joined,[Text.UTF8Encoding]::new($false))
$tokens=$null;$errors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile($scriptFile,[ref]$tokens,[ref]$errors)
if($errors.Count -ne 0){throw ('R351 post-launch script syntax: '+($errors|Out-String))}
Copy-Item 'r351/DeskSpaceStorageMaintenanceV351.cs' $worker -Force
function One([string]$path,[string]$old,[string]$replacement,[string]$label){
    $text=[IO.File]::ReadAllText($path)
    $hits=([regex]::Matches($text,[regex]::Escape($old))).Count
    if($hits -ne 1){throw "R351 $label expected one shipping anchor; got $hits"}
    $text=$text.Replace($old,$replacement)
    [IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
}
One $project '<EmbeddedResource Include="DeskSpaceUpdateSupervisor.ps1" />' @'
<EmbeddedResource Include="DeskSpaceUpdateSupervisor.ps1" />
    <EmbeddedResource Include="DeskSpacePostLaunchCleanupV351.ps1" />
'@ 'embed post-launch cleanup in DeskSpace.exe (manual & auto updates)'
One $app 'var main=new MainWindow();MainWindow=main;main.Show();' @'
var main=new MainWindow();MainWindow=main;main.Show();
        DeskSpaceStorageMaintenanceV351.Begin();
'@ 'normal Workspace startup'
# Widget-only automatic updater restarts must trigger cleanup too, without
# opening or changing the user's full Workspace.
One $app @'
                window.Show();
                widgetStarted=true;
'@ @'
                window.Show();
                widgetStarted=true;
                DeskSpaceStorageMaintenanceV351.Begin();
'@ 'widget-host startup'
Write-Host 'R351: postlaunch cleanup embedded into real DeskSpace.exe and triggered by both Workspace and widget host.'
