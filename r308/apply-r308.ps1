$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(!(Test-Path $p)){throw 'R308 shipping Deck source missing'}
$s=[IO.File]::ReadAllText($p)
$nl=[string][char]10
$s=$s.Replace(([string][char]13+$nl),$nl)
function ReplaceExactlyOnce([string]$old,[string]$replacement,[string]$label){
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R308 '+$label+' expected once, got '+$n)}
 $script:s=$script:s.Replace($old,$replacement)
}
ReplaceExactlyOnce '    private bool deckDismissed,appRenderPending;' (
  '    // Default expanded. Minimize only on explicit user click.'+$nl+
  '    private bool deckMinimized,deckDismissed,appRenderPending;') 'state'
ReplaceExactlyOnce ('        // R306 permanently expanded: no header-only collapse control.'+$nl) (
  '        // Three original actions: minimize/expand, preview, dismiss.'+$nl+
  '        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",'+$nl+
  '            deckMinimized?"Expand deck":"Minimise deck to header",()=>{'+$nl+
  '                deckMinimized=!deckMinimized;'+$nl+
  '                if(deckMinimized)deckPreview?.Close();'+$nl+
  '                Render();'+$nl+
  '            });'+$nl+
  '        DockPanel.SetDock(minimise,Dock.Right);'+$nl+
  '        title.Children.Add(minimise);'+$nl) 'header actions'
ReplaceExactlyOnce 'heading.MaxWidth=Math.Max(70,panel.MaxWidth-79);' 'heading.MaxWidth=Math.Max(70,panel.MaxWidth-103);' 'spacing'
ReplaceExactlyOnce ('        // Always include Running Apps and all Deck sections when visible.'+$nl) (
  '        // Collapse only on explicit minimize, never on refresh/hover.'+$nl+
  '        if(deckMinimized)'+$nl+
  '        {'+$nl+
  '            deckPreview?.Close();'+$nl+
  '            popup.IsOpen=host.IsVisible;'+$nl+
  '            return;'+$nl+
  '        }'+$nl) 'manual minimize'
ReplaceExactlyOnce ('        // Slim header is part of the existing floating deck. No second panel.'+$nl) (
  '        // Every closed->open transition defaults to expanded.'+$nl+
  '        if(!popup.IsOpen)deckMinimized=false;'+$nl+
  '        // Slim header is part of the existing floating deck. No second panel.'+$nl) 'reopen'
ReplaceExactlyOnce ('            deckDismissed=false;'+$nl+'            runningAppsTimer.Start();') (
  '            deckMinimized=false;'+$nl+
  '            deckDismissed=false;'+$nl+
  '            runningAppsTimer.Start();') 'show action'
ReplaceExactlyOnce ('        popup.IsOpen=!deckDismissed &&'+$nl+'            DeskSpaceSearchDeckLayoutV225.ShowDeck(') (
  '        if((bool)e.NewValue)deckMinimized=false;'+$nl+
  '        popup.IsOpen=!deckDismissed &&'+$nl+
  '            DeskSpaceSearchDeckLayoutV225.ShowDeck(') 'visibility'
if(!$s.Contains('deckMinimized=!deckMinimized;') -or
   !$s.Contains('DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);') -or
   !$s.Contains('DeskSpaceMonitorDockV190.ConstrainBeforePaintV298(')){
 throw 'R308 original buttons or monitor-visibility protection missing'
}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R308 full Deck by default; explicit minimize/expand, preview and close retained.'
