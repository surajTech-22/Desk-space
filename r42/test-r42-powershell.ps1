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
    if(@($errors).Count -gt 0){
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
