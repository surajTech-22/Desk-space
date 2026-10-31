$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r246/DeskSpaceKeyboardHelpV246.cs' -Destination "$root/DeskSpaceKeyboardHelpV246.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R246 "+$label+": expected one anchor; got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
        Add("Excel mappings","Open the existing Excel mappings tab in Workspace.",
'@ @'
        Add("Keyboard shortcuts guide",
            "Press F1 in Workspace to view verified DeskSpace shortcuts and their scope.",
            ()=>DeskSpaceKeyboardHelpV246.Show(this));
        Add("Excel mappings","Open the existing Excel mappings tab in Workspace.",
'@ 'Features link'
Swap @'
        PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.K &&
'@ @'
        PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.F1 && Keyboard.Modifiers==ModifierKeys.None)
            {
                DeskSpaceKeyboardHelpV246.Show(this);
                e.Handled=true;
                return;
            }
            if(e.Key==Key.K &&
'@ 'Workspace-scoped F1 help, without affecting existing Ctrl+K'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R246: Workspace F1 and Features entry open accessible verified keyboard shortcut guide.'
