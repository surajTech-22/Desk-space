$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorSessionsV154.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($n -ne 1){throw ("R215 "+$reason+" expected one anchor, got "+$n)}
    $script:s=$script:s.Replace($old,$new)
}
Swap @'
    internal static void Attach(WidgetShellWindow widget,string device,bool primary)
'@ @'
    // No background HWND enumeration while a widget cannot be interacted with.
    // Preserve the event-driven native fence hooks (R153) at all times.
    internal static bool ShouldGuardV215(bool visible,WindowState state)=>
        visible && state!=WindowState.Minimized;

    internal static void Attach(WidgetShellWindow widget,string device,bool primary)
'@ 'pure visibility policy'
Swap @'
        EventHandler move=(_,_)=>{moveGuard.Stop();moveGuard.Start();};
        SizeChangedEventHandler size=(_,_)=>{moveGuard.Stop();moveGuard.Start();};
        DependencyPropertyChangedEventHandler visible=(_,_)=>{
            if(widget.IsVisible){moveGuard.Stop();moveGuard.Start();}
            else moveGuard.Stop();
        };
        widget.LocationChanged+=move;
        widget.SizeChanged+=size;
        widget.IsVisibleChanged+=visible;
'@ @'
        bool CanGuard()=>ShouldGuardV215(widget.IsVisible,widget.WindowState);
        void ScheduleMove()
        {
            moveGuard.Stop();
            if(CanGuard())moveGuard.Start();
        }
        // Hidden or minimized widgets do not need periodic native HWND
        // enumeration. Resume the guard when shown/restored; schedule one
        // fast coalesced containment pass to repair stale popup placement.
        void UpdateGuard()
        {
            if(CanGuard()){guard.Start();ScheduleMove();}
            else {guard.Stop();moveGuard.Stop();}
        }
        EventHandler move=(_,_)=>ScheduleMove();
        SizeChangedEventHandler size=(_,_)=>ScheduleMove();
        DependencyPropertyChangedEventHandler visible=(_,_)=>UpdateGuard();
        EventHandler stateChanged=(_,_)=>UpdateGuard();
        widget.LocationChanged+=move;
        widget.SizeChanged+=size;
        widget.IsVisibleChanged+=visible;
        widget.StateChanged+=stateChanged;
'@ 'event-driven monitor guard lifecycle'
Swap @'
        guard.Tick+=(_,_)=>{
            if(!widget.IsVisible)return;
'@ @'
        guard.Tick+=(_,_)=>{
            if(!CanGuard()){guard.Stop();return;}
'@ 'idle guard stop on race'
Swap @'
        session.Guard=guard;
        guard.Start();
        widget.Closed+=(_,_)=>{
'@ @'
        session.Guard=guard;
        UpdateGuard();
        widget.Closed+=(_,_)=>{
'@ 'only start when visible and restored'
Swap @'
            widget.IsVisibleChanged-=visible;
            Sessions.Remove(widget);
'@ @'
            widget.IsVisibleChanged-=visible;
            widget.StateChanged-=stateChanged;
            Sessions.Remove(widget);
'@ 'dispose visibility/state subscriptions'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R215: fallback HWND monitor scanner sleeps when hidden/minimized and resumes with immediate event-driven re-clamp.'
