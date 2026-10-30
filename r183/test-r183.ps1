$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=Get-Content -LiteralPath "$root/DeskSpaceStartupRecoveryV183.cs" -Raw
$widget=Get-Content -LiteralPath "$root/WidgetHost.cs" -Raw
$health=Get-Content -LiteralPath "$root/DeskSpaceComponentHealthV175.cs" -Raw
foreach($gate in @(
    'internal static State NextStart(',
    'if(failures>=Threshold)sticky=true;',
    'internal static bool ResumeFullStartupNextLaunch()',
    'Interval=HealthyAfter',
    'File.Move(temporary,StateFile,true)',
    'owner.Closed+=closed'
)){
    if(!$source.Contains($gate)){throw "R183 missing recovery guard: $gate"}
}
foreach($forbidden in @(
    'Directory.Delete(', 'Application.Current.Shutdown(',
    'Process.Start(', 'File.Delete(StateFile)', 'Registry.',
    'WorkspaceStore.Root','Environment.Exit('
)){
    if($source.Contains($forbidden)){
        throw "R183 recovery must not mutate workspace or restart/kill app: $forbidden"
    }
}
if(!$widget.Contains('if(!DeskSpaceStartupRecoveryV183.SafeMode)') -or
   !$widget.Contains('DeskSpaceStartupRecoveryV183.Start(this);')){
    throw 'R183 startup gate not linked to primary widget'
}
if(!$health.Contains('new("Startup recovery",DeskSpaceStartupRecoveryV183.Status)')){
    throw 'R183 privacy-safe recovery status not linked'
}
$test=Get-Content -LiteralPath 'r36/UiSmoke/Program.cs' -Raw
if(!$test.Contains('TestStartupRecoveryV183();')){
    throw 'R183 native WPF regression not registered'
}
Write-Host 'R183 PASS: data-safe startup fallback, primary-owned monitor gate and WPF coverage.'
