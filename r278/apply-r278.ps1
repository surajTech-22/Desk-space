$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r278/DeskSpaceMonitorPositionStoreV278.cs' "$root/DeskSpaceMonitorPositionStoreV278.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$p="$root/DeskSpaceMonitorSessionsV154.cs"
$source=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:source,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R278 '+$label+': '+$n+' patch anchors')}
 $script:source=$script:source.Replace($old,$next)
}
Swap @'
    internal static bool SavePosition(WidgetShellWindow widget,bool expanded)
    {
        if(!Sessions.TryGetValue(widget,out var s))return false;
        if(!expanded)
        {
            var hwnd=new WindowInteropHelper(widget).Handle;
            if(hwnd!=IntPtr.Zero&&GetWindowRect(hwnd,out var rect))
            {
                var position=new SavedPosition{Device=s.Device,X=rect.L,Y=rect.T};
                lock(FileGate)
                {
                    try
                    {
                        var all=ReadAll();
                        all.RemoveAll(p=>p.Device.Equals(s.Device,
                            StringComparison.OrdinalIgnoreCase));
                        all.Add(position);
                        Directory.CreateDirectory(WorkspaceStore.Root);
                        var tmp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try
                        {
                            File.WriteAllText(tmp,JsonSerializer.Serialize(all));
                            File.Move(tmp,Filename,true);
                        }
                        finally{if(File.Exists(tmp))File.Delete(tmp);}
                    }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                        or JsonException)
                    {System.Diagnostics.Debug.WriteLine("DeskSpace monitor position: "+ex);}
                }
            }
        }
        // Secondary must never write the legacy primary widget position.
        return !s.Primary;
    }

    internal static void Restore(WidgetShellWindow widget)
    {
        if(!Sessions.TryGetValue(widget,out var s))return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName.Equals(
            s.Device,StringComparison.OrdinalIgnoreCase));
        if(screen==null)return;
        SavedPosition? saved;
        lock(FileGate){saved=ReadAll().FirstOrDefault(x=>x.Device.Equals(
            s.Device,StringComparison.OrdinalIgnoreCase));}
        if(saved==null)return;
        var hwnd=new WindowInteropHelper(widget).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return;
        var bounds=new Rectangle(saved.X,saved.Y,rect.R-rect.L,rect.B-rect.T);
        var fitted=DeskSpaceMonitorFenceV153.Constrain(bounds,screen.WorkingArea);
        SetWindowPos(hwnd,IntPtr.Zero,fitted.X,fitted.Y,0,0,
            NoSize|NoZOrder|NoActivate);
        DeskSpaceMonitorFenceV152.ClampNow(widget);
    }

    private static List<SavedPosition> ReadAll()
    {
        try
        {
            return File.Exists(Filename)?
                JsonSerializer.Deserialize<List<SavedPosition>>(
                    File.ReadAllText(Filename))??new():new();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or JsonException)
        {
            System.Diagnostics.Debug.WriteLine("DeskSpace display layouts: "+ex);
            return new();
        }
    }

'@ @'
    internal static bool SavePosition(WidgetShellWindow widget,bool expanded)
    {
        if(!Sessions.TryGetValue(widget,out var session))return false;
        if(!expanded)
        {
            var hwnd=new WindowInteropHelper(widget).Handle;
            if(hwnd!=IntPtr.Zero && GetWindowRect(hwnd,out var rect))
            {
                // Persist this display only. A cross-process named mutex
                // serializes saves from independently hosted widgets, and
                // corrupt layouts are preserved rather than overwritten.
                try
                {
                    DeskSpaceMonitorPositionStoreV278.Save(
                        session.Device,rect.L,rect.T);
                }
                catch(Exception ex) when(ex is IOException or
                    UnauthorizedAccessException or InvalidDataException or
                    ArgumentException or NotSupportedException or
                    System.Security.SecurityException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "DeskSpace monitor position not overwritten: "+
                        ex.GetType().Name);
                }
            }
        }
        // Secondary must never write the primary's legacy widget-v3.json.
        return !session.Primary;
    }

    internal static void Restore(WidgetShellWindow widget)
    {
        if(!Sessions.TryGetValue(widget,out var session))return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName.Equals(
            session.Device,StringComparison.OrdinalIgnoreCase));
        if(screen==null)return;
        // A missing/corrupt saved entry must NOT fall back to another screen.
        var saved=DeskSpaceMonitorPositionStoreV278.Read(session.Device);
        if(saved==null)return;
        var hwnd=new WindowInteropHelper(widget).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return;
        var bounds=new Rectangle(saved.X,saved.Y,rect.R-rect.L,rect.B-rect.T);
        var fitted=DeskSpaceMonitorFenceV153.Constrain(bounds,screen.WorkingArea);
        SetWindowPos(hwnd,IntPtr.Zero,fitted.X,fitted.Y,0,0,
            NoSize|NoZOrder|NoActivate);
        DeskSpaceMonitorFenceV152.ClampNow(widget);
    }

'@ 'monitor position write+restore'
Swap @'
    private sealed class SavedPosition
    {
        public string Device {get;set;}="";
        public int X {get;set;}
        public int Y {get;set;}
    }
'@ @'

'@ 'retire legacy JSON class'
Swap @'
    private static readonly object FileGate=new();
    private static string Filename=>Path.Combine(WorkspaceStore.Root,
        "widget-independent-displays-v154.json");
'@ @'

'@ 'retire unsafe local-only locks'
[IO.File]::WriteAllText($p,$source,[Text.UTF8Encoding]::new($false))
Write-Host 'R278 P0: display positions are monitor keyed, cross-process serialized, atomic and fail-closed.'