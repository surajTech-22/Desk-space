$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorSessionsV154.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$purpose)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R167 ${purpose}: expected 1 anchor, found $count"}
 $script:s=$script:s.Replace($before,$after)
}

Swap @'
        var guard=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher)
            {Interval=TimeSpan.FromMilliseconds(250)};
'@ @'
        // Previously every widget enumerated ALL native UI-thread HWNDs
        // four times a second, even when stationary. Keep the original
        // per-owner fallback guard but run it less frequently. Location,
        // size and visibility changes now schedule a fast coalesced pass.
        var guard=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher)
            {Interval=TimeSpan.FromMilliseconds(1500)};
        var moveGuard=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher)
            {Interval=TimeSpan.FromMilliseconds(60)};
        moveGuard.Tick+=(_,_)=>{
            moveGuard.Stop();
            if(!widget.IsVisible)return;
            try{GuardOwnedWindows(widget);}
            catch(Exception ex){System.Diagnostics.Debug.WriteLine(
                "DeskSpace monitor move guard: "+ex.GetType().Name);}
        };
        EventHandler move=(_,_)=>{moveGuard.Stop();moveGuard.Start();};
        SizeChangedEventHandler size=(_,_)=>{moveGuard.Stop();moveGuard.Start();};
        DependencyPropertyChangedEventHandler visible=(_,_)=>{
            if(widget.IsVisible){moveGuard.Stop();moveGuard.Start();}
            else moveGuard.Stop();
        };
        widget.LocationChanged+=move;
        widget.SizeChanged+=size;
        widget.IsVisibleChanged+=visible;
'@ 'throttled per-window monitor guard plus immediate coalesced events'

Swap @'
        widget.Closed+=(_,_)=>{
            guard.Stop();
            Sessions.Remove(widget);
        };
'@ @'
        widget.Closed+=(_,_)=>{
            guard.Stop();
            moveGuard.Stop();
            widget.LocationChanged-=move;
            widget.SizeChanged-=size;
            widget.IsVisibleChanged-=visible;
            Sessions.Remove(widget);
        };
'@ 'all timers/handlers removed on independent widget close'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R167: coalesce owner window movement/resize guard, use 1.5s fallback and remove handlers on close.'
