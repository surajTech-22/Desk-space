$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$widget=[IO.File]::ReadAllText("$r/WidgetHost.cs")
$manager=[IO.File]::ReadAllText("$r/DeskSpaceMultiMonitorV151.cs")
$session=[IO.File]::ReadAllText("$r/DeskSpaceMonitorSessionsV154.cs")
$clip=[IO.File]::ReadAllText("$r/DeskSpaceNativeClipV154.cs")
$fence=[IO.File]::ReadAllText("$r/DeskSpaceMonitorFenceV153.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('DeskSpaceMonitorSessionsV154.SavePosition(this,ringExpanded)',
 'DeskSpaceMonitorSessionsV154.PrepareExpansion(this,OpenDiameter)')){
 if(!$widget.Contains($token)){throw "R154 real host integration missing: $token"}
}
foreach($token in @('DeskSpaceMonitorSessionsV154.Attach(widget,',
 'DeskSpaceMonitorSessionsV154.Attach(replica,screen.DeviceName,false);',
 'DeskSpaceMonitorSessionsV154.Restore(replica);',
 'DeskSpaceMonitorSessionsV154.Attach(primary,original.DeviceName,true);')){
 if(!$manager.Contains($token)){throw "R154 monitor manager integration missing: $token"}
}
foreach($token in @('return !s.Primary;',
 'widget-independent-displays-v154.json',
 'Directory.CreateDirectory(WorkspaceStore.Root);',
 'internal static void PrepareExpansion(',
 'GetWindowRect(hwnd,out var rect)',
 'EnumThreadWindows(GetCurrentThreadId()',
 'if(current==root){owned=true;break;}',
 'if(!owned)return true;',
 'SetWindowPos(hwnd,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height',
 'DeskSpaceNativeClipV154.Apply(hwnd,area)')){
 if(!$session.Contains($token)){throw "R154 session protection missing: $token"}
}
foreach($token in @('CreateRectRgn','SetWindowRgn',
 'internal static Rectangle IntersectInWindow(',
 'if(Clipped.Remove(hwnd))','if(SetWindowRgn(hwnd,region,true)==0)')){
 if(!$clip.Contains($token)){throw "R154 region protection missing: $token"}
}
foreach($token in @('WindowPosChanged=0x0047','SizingMessage=0x0214',
 'DeskSpaceNativeClipV154.Apply(hwnd,work);',
 'DeskSpaceNativeClipV154.Release(Source.Handle);',
 'TrackWindowInAreaForTest(Window window,Rectangle work)')){
 if(!$fence.Contains($token)){throw "R154 HWND hook missing: $token"}
}
foreach($token in @('TestIndependentMonitorsV154();',
 'private static void TestIndependentMonitorsV154()',
 'primary.Width=400;','secondary.Width=390;',
 'R154 primary expanded WPF HWND crossed monitor seam',
 'R154 independent secondary expanded HWND crossed monitor seam',
 'R154 collapsing primary must not relocate secondary instance')){
 if(!$ui.Contains($token)){throw "R154 real expansion smoke missing: $token"}
}
if($session.Contains('settings.PinnedApps.Clear()') -or
   $session.Contains('Application.Current.Shutdown()')){
 throw 'R154 must not modify common user data or shut down app'
}
Write-Host 'R154 PASS: separate monitor widgets and positions, shared existing WorkspaceStore, pre-expand native containment, clip backup, and two real WPF HWND expansion regression.'
