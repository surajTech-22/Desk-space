# R42 Inno safety: preserve all sections, user data, and install identity.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='installer/DeskSpace.iss'
if(!(Test-Path -LiteralPath $p)){throw 'Inno Setup script missing'}
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$lf=[string][char]10
$sectionPattern='(?m)^\[[^\]\r\n]+\][ \t]*$'
$headers=[regex]::Matches($s,$sectionPattern)
$setupHeaders=@($headers | Where-Object {$_.Value.Trim() -eq '[Setup]'})
if($setupHeaders.Count -ne 1){throw 'Expected exactly one [Setup] section'}
$setupHeader=$setupHeaders[0]
$from=$setupHeader.Index+$setupHeader.Length
$next=$headers | Where-Object {$_.Index -gt $setupHeader.Index} | Select-Object -First 1
$to=if($null -ne $next){$next.Index}else{$s.Length}
$head=$s.Substring(0,$from)
$setup=$s.Substring($from,$to-$from)
$tail=$s.Substring($to)
function Set-Directive([string]$key,[string]$value){
    $pattern='(?mi)^'+[regex]::Escape($key)+'[ \t]*=[^\n]*$'
    $hits=[regex]::Matches($script:setup,$pattern)
    if($hits.Count -gt 1){throw "Ambiguous Inno directive: $key"}
    if($hits.Count -eq 1){
        $script:setup=[regex]::Replace($script:setup,$pattern,
            [System.Text.RegularExpressions.MatchEvaluator]{
                param($m)
                return $key+'='+$value
            })
    }else{
        # Always terminate appended directive with a newline before the
        # following [Tasks] or [Icons] section.
        if(!$script:setup.EndsWith($script:lf)){$script:setup+=$script:lf}
        $script:setup+=($key+'='+$value+$script:lf)
    }
}
# Ask Windows Restart Manager to shut down DeskSpace gracefully.
# Never force-close processes and lose unsaved workspace edits.
Set-Directive 'CloseApplications' 'yes'
Set-Directive 'RestartApplications' 'no'
Set-Directive 'CloseApplicationsFilter' '*.exe,*.dll,*.chm'
Set-Directive 'AppVerName' 'DeskSpace {#AppVersion}'
if($tail.Length -gt 0 -and !$setup.EndsWith($lf)){$setup+=$lf}
$built=$head+$setup+$tail
# Verify every original Inno section header stays intact.
$afterHeaders=[regex]::Matches($built,$sectionPattern)
if($headers.Count -ne $afterHeaders.Count){
    throw 'R42 installer patch changed section count'
}
for($i=0;$i -lt $headers.Count;$i++){
    if($headers[$i].Value -ne $afterHeaders[$i].Value){
        throw 'R42 installer patch damaged a section header'
    }
}
if($setup -match '(?m)^[ \t]*Name[ \t]*:'){
    throw 'R42 installer [Tasks]/[Icons] entries leaked into [Setup]'
}
if($tail.Length -gt 0 -and !$built.Contains($lf+$tail)){
    throw 'R42 installer did not preserve [Tasks]/[Icons] section boundary'
}
if($tail.Length -gt 0 -and !$built.EndsWith($tail)){
    throw 'R42 installer changed original non-Setup sections'
}
foreach($value in @('CloseApplications=yes','RestartApplications=no',
    'AppVerName=DeskSpace {#AppVersion}')){
    if(!$setup.Contains($value)){throw "R42 missing installer setting: $value"}
}
[IO.File]::WriteAllText($p,$built,[Text.UTF8Encoding]::new($false))
Write-Host 'R42 Inno: graceful app close, version label, original section boundaries verified.'
