# R42 manual installer safety: preserve files/user data and coordinate running DeskSpace.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='installer/DeskSpace.iss'
if(!(Test-Path -LiteralPath $p)){throw 'Inno Setup script missing'}
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$matches=[regex]::Matches($s,'(?m)^\[Setup\][ \t]*$')
if($matches.Count -ne 1){throw 'Expected exactly one Inno Setup [Setup] section'}
$from=$matches[0].Index+$matches[0].Length
$next=[regex]::Match($s.Substring($from),'(?m)^\[[^\]\r\n]+\][ \t]*$')
$to=if($next.Success){$from+$next.Index}else{$s.Length}
$head=$s.Substring(0,$from)
$setup=$s.Substring($from,$to-$from)
$tail=$s.Substring($to)
function Set-Directive([string]$key,[string]$value){
    $pattern='(?mi)^'+[regex]::Escape($key)+'[ \t]*=[^\n]*$'
    $hits=[regex]::Matches($script:setup,$pattern)
    if($hits.Count -gt 1){throw "Ambiguous original Inno directive: $key"}
    if($hits.Count -eq 1){
        $script:setup=[regex]::Replace($script:setup,$pattern,($key+'='+$value))
    }else{
        $script:setup+="`n"+$key+'='+$value
    }
}
# Windows Restart Manager closes any still-running app using install files.
# Never use "force" since it might lose unsaved workspace edits.
Set-Directive 'CloseApplications' 'yes'
Set-Directive 'RestartApplications' 'no'
Set-Directive 'ForceCloseApplications' 'no'
Set-Directive 'CloseApplicationsFilter' '*.exe,*.dll,*.chm'
# Original installer displayed a stale "DeskSpace R10" title. Fix label
# only; preserve stable AppId, installation directory and all user data.
Set-Directive 'AppVerName' 'DeskSpace {#AppVersion}'
[IO.File]::WriteAllText($p,$head+$setup+$tail,[Text.UTF8Encoding]::new($false))
foreach($v in @('CloseApplications=yes','RestartApplications=no',
 'ForceCloseApplications=no','AppVerName=DeskSpace {#AppVersion}')){
    if(![IO.File]::ReadAllText($p).Contains($v)){throw "Missing guarded installer setting: $v"}
}
Write-Host 'R42 installer: automatic graceful close, no forced exit, no restart, current version label. Original AppId and data unchanged.'
