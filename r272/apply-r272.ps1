$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $text=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $count=([regex]::Matches($text,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R272 ${why}: $count anchors"}
 [IO.File]::WriteAllText($path,$text.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
}
Swap 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceMediaV18.cs' @'
    internal bool DeckVisibleV123=>visible&&!dismissedV123;
    internal string DeckTitleV123=>title;
'@ @'
    internal bool DeckVisibleV123=>visible&&!dismissedV123&&!disposed;
    internal bool DeckDismissedV272=>visible&&dismissedV123&&!disposed;
    // The Deck close button hides controls, not the underlying media.
    // A deliberate Restore action can re-open them for the SAME track,
    // without waiting for a new track or starting playback.
    internal bool RestoreDeckV272()
    {
        if(disposed||!visible||!dismissedV123)return false;
        dismissedV123=false;
        VisibilityChanged?.Invoke(true);
        return true;
    }
    internal string DeckTitleV123=>title;
'@ 'explicit restore state'
Swap 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceMediaV18.cs' @'
    internal void DeckCommandV123(string command)
    {
        if(command=="mute")Volume(0xAD);
        else _=Control(command);
    }
    internal void DismissPanelV123()
    {
        dismissedV123=true;
        CloseBelow();
        VisibilityChanged?.Invoke(false);
    }
'@ @'
    internal void DeckCommandV123(string command)
    {
        // A stale Deck button may still receive a queued WPF click after
        // widget teardown. Reject it without controlling an old session.
        if(disposed||!visible||dismissedV123)return;
        if(command=="mute")Volume(0xAD);
        else if(command is "previous" or "toggle" or "next" or "stop")
            _=Control(command);
    }
    internal void DismissPanelV123()
    {
        if(disposed||dismissedV123)return;
        dismissedV123=true;
        CloseBelow();
        VisibilityChanged?.Invoke(false);
    }
'@ 'stale command guard'
Swap 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs' @'
        var activeMedia=DeskSpaceMediaV18.ActiveV123;
        if(activeMedia?.DeckVisibleV123==true)
        {
'@ @'
        var activeMedia=DeskSpaceMediaV18.ActiveV123;
        if(activeMedia?.DeckDismissedV272==true)
        {
            // Close removes transport buttons; only an explicit click
            // restores them for the same track. Keep a compact affordance
            // instead of forcing the user to wait for a new song to begin.
            var restore=new Button{
                Content="♫  Show media controls",
                ToolTip="Restore the closed Media Player controls without playing music",
                FontSize=11,Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(4,3,4,3)
            };
            System.Windows.Automation.AutomationProperties.SetName(
                restore,"Restore media Deck controls");
            restore.Click+=(_,_)=>{
                if(activeMedia.RestoreDeckV272())Render();
            };
            list.Children.Add(restore);
        }
        if(activeMedia?.DeckVisibleV123==true)
        {
'@ 'compact Deck media restore'
Write-Host 'R272: user can restore media controls after close, without restarting playback; stale Deck buttons safe.'
