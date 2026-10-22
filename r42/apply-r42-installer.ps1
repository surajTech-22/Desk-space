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
Set-Directive 'CloseApplicationsFilter' '*.exe,*.dll,*.chm'
# Original installer displayed a stale "DeskSpace R10" title. Fix label
# only; preserve stable AppId, installation directory and all user data.
Set-Directive 'AppVerName' 'DeskSpace {#AppVersion}'
# Inno Setup sections MUST begin on their own line. When directives are
# appended to [Setup], the previous version joined the final directive to
# the following [Tasks] header (AppVerName=... [Tasks]), so Inno interpreted
# "Name: desktopicon; ..." as a [Setup] directive and aborted.
# Preserve the exact original text of every non-[Setup] section.
if($tail.Length -gt 0 -and !$setup.EndsWith("`n")){$setup+="`n"}
$built=$head+$setup+$tail
$beforeSections=[regex]::Matches($s,'(?m)^\\[[^\\]\\r\\n]+\\][ \\t]*
Write-Host 'R42 installer: automatic graceful close, no forced exit, no restart, current version label. Original AppId and data unchanged.'
)
$afterSections=[regex]::Matches($built,'(?m)^\\[[^\\]\\r\\n]+\\][ \\t]*
Write-Host 'R42 installer: automatic graceful close, no forced exit, no restart, current version label. Original AppId and data unchanged.'
)
if($beforeSections.Count -ne $afterSections.Count){
    throw 'Installer patch changed the number of Inno Setup sections'
}
for($i=0;$i -lt $beforeSections.Count;$i++){
    if($beforeSections[$i].Value -ne $afterSections[$i].Value){
        throw "Installer section header changed: $($beforeSections[$i].Value)"
    }
}
if($setup -match '(?m)^[ \\t]*Name[ \\t]*:'){
    throw 'Inno Tasks/Icons directive leaked into [Setup] section'
}
if($tail.Length -gt 0 -and !$built.Contains("`n"+$tail)){
    throw 'Installer subsection boundary was not preserved'
}
[IO.File]::WriteAllText($p,$built,[Text.UTF8Encoding]::new($false))
foreach($v in @('CloseApplications=yes','RestartApplications=no',
    'AppVerName=DeskSpace {#AppVersion}')){
    if(!$built.Contains($v)){throw "Missing guarded installer setting: $v"}
}
Write-Host 'R42 installer: automatic graceful close, no forced exit, no restart, current version label. Original AppId and data unchanged.'
