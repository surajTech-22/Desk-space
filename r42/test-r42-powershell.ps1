# DeskSpace R42 -- preflight all local PowerShell scripts.
# Read-only validation: never installs or modifies application data.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$files=@(Get-ChildItem -LiteralPath 'r42' -Filter '*.ps1' -File -Recurse)
if($files.Count -lt 6){throw 'R42 PowerShell sources not found'}
foreach($file in $files){
    $tokens=$null
    $errors=$null
    $ast=[System.Management.Automation.Language.Parser]::ParseFile(
        $file.FullName,[ref]$tokens,[ref]$errors)
    if($null -ne $errors -and $errors.Length -gt 0){
        $message=(@($errors) | ForEach-Object {
            $_.Extent.File+':'+$_.Extent.StartLineNumber+': '+$_.Message
        }) -join [Environment]::NewLine
        throw ('R42 PowerShell parser failed in '+$file.Name+': '+$message)
    }
    # The host variable is an automatic, read-only PowerShell variable.
    # A valid script may read $Host but must not assign to it.
    $assignments=$ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.AssignmentStatementAst]
    },$true)
    foreach($assignment in $assignments){
        $lhs=$assignment.Left.Extent.Text.Trim()
        if($lhs.Equals('$Host',[StringComparison]::OrdinalIgnoreCase)){
            throw ('Reserved PowerShell Host assignment: '+
                $file.Name+' line '+$assignment.Extent.StartLineNumber)
        }
    }
}
Write-Host ('R42 PowerShell preflight PASS: '+$files.Count+' scripts parsed; no reserved Host assignments.')

# Protect the manual Windows builder from malformed or duplicated workflow
# stages, not only from parser errors in individual .ps1 files.
$workflowPath='.github/workflows/build-installer.yml'
if(!(Test-Path -LiteralPath $workflowPath)){throw 'Windows build workflow missing'}
$workflow=[IO.File]::ReadAllText($workflowPath)
$stepNames=@(foreach($stepMatch in [regex]::Matches(
    $workflow,'(?m)^      - name: ([^\r\n]+)
$duplicates=@($stepNames | Group-Object | Where-Object {$_.Count -gt 1})
if($duplicates.Count -gt 0){
    throw ('Duplicated Windows build steps: '+
        (($duplicates | ForEach-Object {$_.Name}) -join ', '))
}
$required=@(
    'R42 editable saved Excel mapping and simplified review',
    'R42 PowerShell syntax and automatic-variable preflight',
    'R42 user-approved current location and worldwide weather tests',
    'R42 verified installed-app closure and corrected Inno Setup version label',
    'Assign monotonic build version to installer and app',
    'Compile, test and package self-contained app',
    'R41 WPF test Excel, scroll and weather scenes',
    'Confirm installer and checksum exist'
)
$previous=-1
foreach($name in $required){
    $index=-1
    for($i=0;$i -lt $stepNames.Count;$i++){
        if([string]::Equals([string]$stepNames[$i],[string]$name,
            [StringComparison]::Ordinal)){$index=$i;break}
    }
    if($index -le $previous){
        throw ('Missing or out-of-order build stage: '+$name+
            '; position='+$index+' after='+$previous+
            '; available='+($stepNames -join ' | '))
    }
    $previous=$index
}
Write-Host ('R42 workflow preflight PASS: '+$stepNames.Count+
    ' unique ordered build steps; no malformed duplicate tail.')
)){
    $stepMatch.Groups[1].Value.Trim()
})
if($stepNames.Count -lt 40){throw "Workflow stage parser found only $($stepNames.Count) steps"}
$duplicates=@($stepNames | Group-Object | Where-Object {$_.Count -gt 1})
if($duplicates.Count -gt 0){
    throw ('Duplicated Windows build steps: '+
        (($duplicates | ForEach-Object {$_.Name}) -join ', '))
}
$required=@(
    'R42 editable saved Excel mapping and simplified review',
    'R42 PowerShell syntax and automatic-variable preflight',
    'R42 user-approved current location and worldwide weather tests',
    'R42 verified installed-app closure and corrected Inno Setup version label',
    'Assign monotonic build version to installer and app',
    'Compile, test and package self-contained app',
    'R41 WPF test Excel, scroll and weather scenes',
    'Confirm installer and checksum exist'
)
$previous=-1
foreach($name in $required){
    $index=-1
    for($i=0;$i -lt $stepNames.Count;$i++){
        if([string]::Equals([string]$stepNames[$i],[string]$name,
            [StringComparison]::Ordinal)){$index=$i;break}
    }
    if($index -le $previous){
        throw ('Missing or out-of-order build stage: '+$name+
            '; position='+$index+' after='+$previous+
            '; available='+($stepNames -join ' | '))
    }
    $previous=$index
}
Write-Host ('R42 workflow preflight PASS: '+$stepNames.Count+
    ' unique ordered build steps; no malformed duplicate tail.')
