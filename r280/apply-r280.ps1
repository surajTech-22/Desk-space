$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r280/DeskSpaceSmartContextDeckV280.cs' "$root/DeskSpaceSmartContextDeckV280.cs" -Force
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$p="$root/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R280 '+$why+' expected 1 anchor, found '+$n)}
 $script:s=$script:s.Replace($old,$next)
}
Swap @'
        Views.Add(this);
        active ??= this;
'@ @'
        Views.Add(this);
        active ??= this;
        // One opt-in session feeds both monitor-local Deck views; display
        // positions and UI state remain independent of shared context.
        DeskSpaceSmartContextDeckV280.Changed+=ContextChangedV280;
'@ 'Smart Context Deck integration'
Swap @'
    private void Render()
    {
        if(disposed)return;
'@ @'
    private void ContextChangedV280()
    {
        if(!disposed)Render();
    }

    private void Render()
    {
        if(disposed)return;
'@ 'Smart Context Deck integration'
Swap @'
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
'@ @'
        // Session-only Smart Context appears in THIS Deck and is shared
        // by the monitor widgets without another monitoring panel.
        list.Children.Add(DeskSpaceSmartContextDeckV280.CreatePanel(
            host,ContextChangedV280));
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
'@ 'Smart Context Deck integration'
Swap @'
        Views.Remove(this);
        // The media player stays inside the Deck until the FINAL widget
'@ @'
        Views.Remove(this);
        DeskSpaceSmartContextDeckV280.Changed-=ContextChangedV280;
        if(Views.Count==0)DeskSpaceSmartContextDeckV280.Stop();
        // The media player stays inside the Deck until the FINAL widget
'@ 'Smart Context Deck integration'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R280: one opt-in Smart Context session integrated into all Deck views with last-view cleanup.'