$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Launch ONLY test-created WPF executable; no real DeskSpace process touched.
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r232-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$exe='r232/NativeCloseProbe/bin/Release/net8.0-windows/NativeCloseProbe.exe'
$super='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile(
    [IO.Path]::GetFullPath($super),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -ne 0){throw 'R232 supervisor fails PowerShell parsing'}
$func=@($ast.FindAll({
    param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and
      $n.Name -eq 'Request-R230NativeClose'
},$true))
if($func.Count -ne 1){throw 'R232 native close helper unavailable'}
[void](Invoke-Expression $func[0].Extent.Text)
$child=$null
try{
    dotnet build r232/NativeCloseProbe/NativeCloseProbe.csproj -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'R232 compiled WPF probe failed to build'}
    if(!(Test-Path -LiteralPath $exe)){throw 'R232 probe EXE unavailable'}
    $child=Start-Process -FilePath ([IO.Path]::GetFullPath($exe)) -ArgumentList @($root) -PassThru
    $ready=Join-Path $root 'ready.txt'
    $closed=Join-Path $root 'closed.txt'
    $attempt=Join-Path $root 'closing.txt'
    $allow=Join-Path $root 'allow-close.txt'
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    while(!(Test-Path $ready) -and [DateTime]::UtcNow -lt $deadline){
        Start-Sleep -Milliseconds 140
        $child.Refresh()
        if($child.HasExited){throw 'R232 isolated WPF exited before Loaded'}
    }
    if(!(Test-Path $ready)){throw 'R232 isolated WPF probe not Loaded'}
    $sent=Request-R230NativeClose ([int[]]@($child.Id))
    if([int]$sent -lt 1){throw 'R232 did not send WM_CLOSE to WPF test HWND'}
    $deadline=[DateTime]::UtcNow.AddSeconds(8)
    while(!(Test-Path $attempt) -and [DateTime]::UtcNow -lt $deadline){
        Start-Sleep -Milliseconds 140
        $child.Refresh()
        if($child.HasExited){break}
    }
    if(!(Test-Path $attempt)){throw 'R232 WPF Closing handler did not run'}
    $child.Refresh()
    if($child.HasExited -or (Test-Path $closed)){
        throw 'R232 failed: WM_CLOSE terminated the app despite WPF Closing cancellation'
    }
    [IO.File]::WriteAllText($allow,'saved')
    $sent=Request-R230NativeClose ([int[]]@($child.Id))
    if([int]$sent -lt 1){throw 'R232 second WM_CLOSE not delivered'}
    if(!$child.WaitForExit(15000)){throw 'R232 allowed close did not exit'}
    if(!(Test-Path $closed) -or $child.ExitCode -ne 0){
        throw ('R232 allowed WPF Close abnormal exit: '+$child.ExitCode)
    }
    Write-Host 'R232 PASS: real WPF app refused native close with unsaved work, exited normally after save.'
}
finally{
    if($child){
        $child.Refresh()
        if(!$child.HasExited){
            # Only isolated TEST process is cleaned up.
            Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
        }
        $child.Dispose()
    }
    Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction SilentlyContinue
}
