$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$path="$root/DeskSpaceDropShelfV15.cs"
if(!(Test-Path $path)){throw 'Expected canonical Stable Deck file missing'}
$nl=[string][char]10
$s=[IO.File]::ReadAllText($path).Replace(([string][char]13+$nl),$nl)
$old='        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",'
$buttonStart=$s.IndexOf($old,[StringComparison]::Ordinal)
if($buttonStart -lt 0){throw 'R306 expected previous minimize button not found'}
$buttonEnd=$s.IndexOf('        var heading=Text(',$buttonStart,[StringComparison]::Ordinal)
if($buttonEnd -le $buttonStart){throw 'R306 existing heading not found'}
$legacy=$s.Substring($buttonStart,$buttonEnd-$buttonStart)
if(!$legacy.Contains('title.Children.Add(minimise);')){throw 'R306 unexpected minimise handler'}
$s=$s.Remove($buttonStart,$buttonEnd-$buttonStart)
$s=$s.Insert($buttonStart,'        // R306 permanently expanded: no header-only collapse control.'+$nl)
$oldField='    private bool deckMinimized,deckDismissed,appRenderPending;'
if(!$s.Contains($oldField)){throw 'R306 legacy minimized state not found'}
$s=$s.Replace($oldField,'    // Always expanded while visible; user may still explicitly dismiss.'+$nl+'    private bool deckDismissed,appRenderPending;')
$begin=$s.IndexOf('        if(deckMinimized)'+$nl,[StringComparison]::Ordinal)
if($begin -lt 0){throw 'R306 minimize branch missing'}
$end=$s.IndexOf('        DeskSpaceRunningAppsV88.AddHoverGroupLive(',$begin,[StringComparison]::Ordinal)
if($end -le $begin){throw 'R306 app group render not found'}
$s=$s.Remove($begin,$end-$begin)
$s=$s.Insert($begin,'        // Always include Running Apps and all Deck sections when visible.'+$nl)
$s=$s.Replace('heading.MaxWidth=Math.Max(70,panel.MaxWidth-103);','heading.MaxWidth=Math.Max(70,panel.MaxWidth-79);')
if($s.Contains('deckMinimized') -or !$s.Contains('DeskSpaceRunningAppsV88.AddHoverGroupLive(') -or !$s.Contains('DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);')){
 throw 'R306 failed: collapsed Deck state remains or key R304 feature lost'
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R306 DONE: Deck expanded by default, after refresh and after restart; no header-only collapse.'
