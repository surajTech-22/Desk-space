$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Synthetic WPF child only. NEVER touch a real DeskSpace process on runner.
$super='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile(
    [IO.Path]::GetFullPath($super),[ref]$tokens,[ref]$errors)
if($errors.Count -ne 0){throw 'R230 actual supervisor has syntax errors'}
$functions=@($ast.FindAll({
    param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and
       $n.Name -eq 'Request-R230NativeClose'
},$true))
if($functions.Count -ne 1){throw 'R230 actual native close helper not found'}
[void](Invoke-Expression $functions[0].Extent.Text)
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r230-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root|Out-Null
$childScript=Join-Path $root 'synthetic-wpf.ps1'
$ready=Join-Path $root 'ready.txt'
$allow=Join-Path $root 'saved-work.txt'
$closing=Join-Path $root 'closing.txt'
$closed=Join-Path $root 'closed.txt'
$child=$null
try{
    $childSource=@'
param([string]$Ready,[string]$Allow)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$window=New-Object System.Windows.Window
$window.Width=220
$window.Height=130
$window.Title='R230 isolated unsaved-work verification'
$window.ShowInTaskbar=$false
$window.WindowStyle=[System.Windows.WindowStyle]::ToolWindow
# Strong script-scope paths are important: routed WPF event scriptblocks
# can run after the original parameter binding scope has changed.
$script:CloseAllowed=$Allow
$script:AttemptFile=Join-Path (Split-Path -Parent $Ready) 'closing.txt'
$script:ClosedFile=Join-Path (Split-Path -Parent $Ready) 'closed.txt'
$window.Add_Closing({
    param($sender,$e)
    [IO.File]::AppendAllText($script:AttemptFile,'attempt;')
    if(![IO.File]::Exists($script:CloseAllowed)){$e.Cancel=$true}
})
$window.Add_Closed({[IO.File]::WriteAllText($script:ClosedFile,'closed')})
$window.Add_Loaded({[IO.File]::WriteAllText($Ready,'ready')})
[void]$window.ShowDialog()
'@
    [IO.File]::WriteAllText($childScript,$childSource,[Text.UTF8Encoding]::new($false))
    $args='-NoProfile -STA -NonInteractive -File "'+$childScript+
        '" -Ready "'+$ready+'" -Allow "'+$allow+'"'
    $child=Start-Process -FilePath 'powershell.exe' -ArgumentList $args -PassThru -WindowStyle Hidden
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while(!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline){
        Start-Sleep -Milliseconds 150
        $child.Refresh()
        if($child.HasExited){throw 'Synthetic WPF child exited before its window loaded'}
    }
    if(!(Test-Path -LiteralPath $ready)){throw 'Synthetic WPF child never loaded'}
    $sent=Request-R230NativeClose ([int[]]@($child.Id))
    if([int]$sent -lt 1){throw 'R230 real WM_CLOSE not delivered to synthetic WPF process'}
    Start-Sleep -Milliseconds 850
    $child.Refresh()
    if(!(Test-Path -LiteralPath $closing)){
        throw 'R230 first native WM_CLOSE failed to invoke WPF Closing'
    }
    if((Test-Path -LiteralPath $closed) -or $child.HasExited){
        throw 'R230 ignored WPF unsaved-work Closing cancellation'
    }
    [IO.File]::WriteAllText($allow,'saved')
    $sent=Request-R230NativeClose ([int[]]@($child.Id))
    if([int]$sent -lt 1){throw 'R230 second WM_CLOSE not delivered'}
    if(!$child.WaitForExit(12000)){throw 'R230 child did not close after saving'}
    if($child.ExitCode -ne 0){throw 'R230 child exited abnormally'}
    Write-Host 'R230 REAL WIN32 PASS: native WM_CLOSE reaches WPF, respects unsaved-work veto, then closes after save.'
}
finally{
    if($child){
        $child.Refresh()
        if(!$child.HasExited){
            # Only the test-created PowerShell process, never the DeskSpace app.
            Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
        }
        $child.Dispose()
    }
    if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Force -Recurse}
}
