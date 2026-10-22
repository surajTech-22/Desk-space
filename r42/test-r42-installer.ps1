$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$patch=(Resolve-Path -LiteralPath 'r42/apply-r42-installer.ps1').Path
$base=Join-Path ([IO.Path]::GetTempPath()) ('DeskSpaceInnoR42_'+[guid]::NewGuid().ToString('N'))
$originalNative=[IO.Directory]::GetCurrentDirectory()
$originalPS=(Get-Location).Path
$lf=[string][char]10
try {
    foreach($variant in @('missing-directives','existing-directives')){
        $folder=Join-Path $base $variant
        New-Item -ItemType Directory -Path (Join-Path $folder 'installer') -Force | Out-Null
        $setup=if($variant -eq 'existing-directives') {
            @('AppVerName=DeskSpace old','CloseApplications=no')
        } else { @() }
        $lines=@(
            '[Setup]',
            'AppName=DeskSpace',
            'AppVersion=1.4.0',
            'DefaultDirName={localappdata}\Programs\DeskSpace'
        )+$setup+@(
            '[Tasks]',
            'Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked',
            '[Icons]',
            'Name: "{group}\DeskSpace"; Filename: "{app}\DeskSpace.exe"',
            '[Files]',
            'Source: "fake.exe"; DestDir: "{app}"'
        )
        $original=$lines -join $lf
        if($variant -eq 'existing-directives'){
            $original=$original.Replace($lf,[string][char]13+$lf)
        }
        $file=Join-Path $folder 'installer/DeskSpace.iss'
        [IO.File]::WriteAllText($file,$original,[Text.UTF8Encoding]::new($false))
        Set-Location -LiteralPath $folder
        [IO.Directory]::SetCurrentDirectory($folder)
        & $patch
        $result=[IO.File]::ReadAllText($file).Replace([string][char]13+$lf,$lf)
        $sections=@([regex]::Matches($result,'(?m)^\[[^\]\r\n]+\][ \t]*$') |
            ForEach-Object {$_.Value})
        $expected=@('[Setup]','[Tasks]','[Icons]','[Files]')
        if(($sections -join '|') -ne ($expected -join '|')){
            throw "R42 Inno fixture ${variant}: section headers lost"
        }
        $setupText=$result.Substring($result.IndexOf('[Setup]')+7,
            $result.IndexOf('[Tasks]')-($result.IndexOf('[Setup]')+7))
        foreach($setting in @('CloseApplications=yes',
            'RestartApplications=no','AppVerName=DeskSpace {#AppVersion}')){
            if(!$setupText.Contains($setting)){
                throw "R42 Inno fixture ${variant}: missing $setting"
            }
        }
        if($setupText.Contains('Name: "desktopicon"') -or
           $result.Contains('AppVerName=DeskSpace {#AppVersion}[Tasks]')){
            throw "R42 Inno fixture ${variant}: [Tasks] leaked into [Setup]"
        }
        foreach($unchanged in @(
            'DefaultDirName={localappdata}\Programs\DeskSpace',
            'Name: "desktopicon"; Description: "Create a desktop shortcut"',
            'Name: "{group}\DeskSpace"; Filename: "{app}\DeskSpace.exe"')){
            if(!$result.Contains($unchanged)){
                throw "R42 Inno fixture ${variant}: existing installer data was modified"
            }
        }
        Write-Host "R42 Inno fixture ${variant}: PASS (valid [Setup]/[Tasks]/[Icons]/[Files] boundaries)"
    }
}
finally {
    Set-Location -LiteralPath $originalPS
    [IO.Directory]::SetCurrentDirectory($originalNative)
    if(Test-Path $base){Remove-Item -LiteralPath $base -Recurse -Force}
}
