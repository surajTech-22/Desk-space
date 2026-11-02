$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$dock="$root/DeskSpaceMonitorDockV190.cs"
$shelf="$root/DeskSpaceDropShelfV15.cs"
if(!(Test-Path $dock) -or !(Test-Path $shelf)){throw 'R314 shipping source missing'}
# The geometry implementation is the exact R298 version already shipped by
# R301; fail rather than silently replace unexpected future code.
$a=(Get-FileHash $dock -Algorithm SHA256).Hash
$b=(Get-FileHash 'r298/DeskSpaceMonitorDockV298.cs' -Algorithm SHA256).Hash
if($a -ne $b){throw 'R314 baseline monitor layout differs from verified shipped R298 geometry'}
Copy-Item 'r314/DeskSpaceMonitorDockV314.cs' $dock -Force
$s=[IO.File]::ReadAllText($shelf)
$nl=[string][char]10
$s=$s.Replace(([string][char]13+$nl),$nl)
function ReplaceOne([string]$old,[string]$new,[string]$name)
{
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R314 shelf '+$name+' anchor count='+$count)}
 $script:s=$script:s.Replace($old,$new)
}
ReplaceOne 'DeskSpaceMonitorDockV190.Attach(host,popup,panel);' ('DeskSpaceMonitorDockV190.RegisterMinimizedV314(panel,()=>deckMinimized);'+$nl+'        DeskSpaceMonitorDockV190.Attach(host,popup,panel);') 'monitor-local minimize callback'
ReplaceOne ('                if(deckMinimized)deckPreview?.Close();'+$nl+
             '                Render();'+$nl+
             '            });'+$nl+
             '        DockPanel.SetDock(minimise,Dock.Right);') (
             '                if(deckMinimized)deckPreview?.Close();'+$nl+
             '                Render();'+$nl+
             '                if(!deckMinimized)'+$nl+
             '                    DeskSpaceMonitorDockV190.RequestReflowV314(panel,true);'+$nl+
             '            });'+$nl+
             '        DockPanel.SetDock(minimise,Dock.Right);') 'explicit expand resets scroll'
ReplaceOne ('        popup.IsOpen=DeskSpaceSearchDeckLayoutV225.ShowDeck('+$nl+
             '            DeskSpaceDeckVisibilityV89.Visible,host.IsVisible,'+$nl+
             '            searchActiveV225,true);'+$nl+
             '    }') (
             '        bool openingV314=!popup.IsOpen;'+$nl+
             '        popup.IsOpen=DeskSpaceSearchDeckLayoutV225.ShowDeck('+$nl+
             '            DeskSpaceDeckVisibilityV89.Visible,host.IsVisible,'+$nl+
             '            searchActiveV225,true);'+$nl+
             '        DeskSpaceMonitorDockV190.RequestReflowV314(panel,openingV314);'+$nl+
             '    }') 'live render after content changes'
[IO.File]::WriteAllText($shelf,$s,[Text.UTF8Encoding]::new($false))
foreach($required in @('DeckHeaderButton(deckMinimized','title.Children.Add(close);',
    'title.Children.Add(previewMode);','title.Children.Add(minimise);',
    'RequestReflowV314(panel,openingV314)','RequestReflowV314(panel,true)',
    'DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);')){
 if(!$s.Contains($required)){throw ('R314 shipping Deck missing '+$required)}
}
Write-Host 'R314 stable source: full-height Deck remeasurement, header restored, 3 buttons retained and safe below-clock placement.'
