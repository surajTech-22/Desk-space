$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$p="$r/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
    $old=$before.Replace($crlf,$lf)
    $new=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R155 $why expected one anchor, got $count"}
    $script:s=$script:s.Replace($old,$new)
}

# The previous shelf was INSTANCE-OWNED; constructing a widget for monitor 2
# set 'active=this' and hid monitor 1's held files from clipboard routes.
# Share only held FILE CONTENT across widget views. Keep individual Deck
# minimize/close/hover/position and Running Apps/recent discovery independent.
Swap '    private readonly List<Held> held=new();' @'
    private static readonly List<Held> held=new();
    private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();
    private static void RefreshViews()
    {
        foreach(var view in Views.ToArray())
            if(!view.disposed)view.Render();
    }
'@ 'one shared held-file queue and view notification'

Swap @'
    private readonly string staging=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","TemporaryShelf",Guid.NewGuid().ToString("N"));
    private bool busy, disposed;
    private static DeskSpaceDropShelfV15? active;
    public static bool IsActive=>active!=null;
    public static IReadOnlyList<string> ActivePaths=>
        active?.held.Select(x=>x.StagedPath).ToArray()??Array.Empty<string>();
    public static void ImportClipboardFiles()=>active?.ImportFromClipboard();
    public static void ClearActive()=>active?.ClearAll();
'@ @'
    private static readonly string staging=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","TemporaryShelf",Guid.NewGuid().ToString("N"));
    private static bool busy;
    private bool disposed;
    static DeskSpaceDropShelfV15()
    {
        // A monitor disconnect must not delete the files used by the other
        // widget. Remove our temporary copies only on final process exit.
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{
            try{if(Directory.Exists(staging))Directory.Delete(staging,true);}
            catch(IOException){}catch(UnauthorizedAccessException){}
        };
    }
    public static bool IsActive=>Views.Count>0;
    public static IReadOnlyList<string> ActivePaths=>
        held.Select(x=>x.StagedPath).ToArray();
    public static void ImportClipboardFiles()=>Views.FirstOrDefault()?.ImportFromClipboard();
    public static void ClearActive()=>Views.FirstOrDefault()?.ClearAll();
'@ 'static shared staging/clipboard routes and process-exit cleanup'

Swap '        active=this;' '        Views.Add(this);' 'register widget view without replacing primary'
Swap @'
        busy=true;
        Render();
'@ @'
        busy=true;
        RefreshViews();
'@ 'notify both views on copy start'
Swap '                if(disposed||held.Count>=25)break;' '                if(held.Count>=25)break;' 'monitor close cannot cancel shared queue'
Swap '                if(disposed){DeletePath(destination);break;}' @'
                // Detached monitor views do not own these shared copies.
'@ 'no staged-file deletion on widget detach'
Swap @'
                held.Add(new Held(Path.GetFileName(destination),destination));
                Render();
'@ @'
                held.Add(new Held(Path.GetFileName(destination),destination));
                RefreshViews();
'@ 'notify both decks when file arrives'
Swap '        finally{busy=false;if(!disposed)Render();}' '        finally{busy=false;RefreshViews();}' 'all decks leave busy state'
Swap @'
        held.Remove(item);
        DeletePath(item.StagedPath);
        Render();
'@ @'
        if(!held.Remove(item))return;
        DeletePath(item.StagedPath);
        RefreshViews();
'@ 'remove file globally and update both decks'
Swap '        if(ReferenceEquals(active,this))active=null;' '        Views.Remove(this);' 'disconnect only the closing deck view'
Swap @'
        try { if(Directory.Exists(staging))Directory.Delete(staging,true); }
        catch(IOException){}catch(UnauthorizedAccessException){}
'@ @'
        // Shared copies outlive any single monitor view and are removed when
        // explicitly cleared, dragged successfully or DeskSpace exits.
'@ 'never delete shared files on a monitor disconnect'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))

$smoke='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($smoke).Replace($crlf,$lf)
function SmokeSwap([string]$before,[string]$after,[string]$why)
{
    $old=$before.Replace($crlf,$lf)
    $new=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R155 smoke $why anchor count $count"}
    $script:s=$script:s.Replace($old,$new)
}
SmokeSwap '            TestIndependentMonitorsV154();' @'
            TestIndependentMonitorsV154();
            TestSharedHeldFilesV155();
'@ 'test independent widgets AND shared held files'

$test=@'
    private static void TestSharedHeldFilesV155()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var heldType=type.GetNestedType("Held",
            BindingFlags.NonPublic)!;
        var shared=(System.Collections.IList)type.GetField("held",
            BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var refresh=type.GetMethod("RefreshViews",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var active=type.GetProperty("ActivePaths",
            BindingFlags.Static|BindingFlags.Public)!;
        var firstHost=new Window{Width=140,Height=140};
        var secondHost=new Window{Width=140,Height=140};
        var first=Activator.CreateInstance(type,new object[]{firstHost})!;
        var second=Activator.CreateInstance(type,new object[]{secondHost})!;
        var path=Path.Combine(outdir,"r155-shared-held-file.txt");
        File.WriteAllText(path,"R155 two-monitor shared files");
        try
        {
            Assert(shared.Count==0,"R155 test requires an initially empty shelf");
            var item=Activator.CreateInstance(heldType,
                new object[]{"r155-shared-held-file.txt",path})!;
            shared.Add(item);
            refresh.Invoke(null,null);
            var paths=((IEnumerable<string>)active.GetValue(null)!).ToArray();
            Assert(paths.Length==1 && paths[0]==path &&
                DeskSpaceClipboardV10.Shelf.Count==1,
                "R155 clipboard and both views must access one held-file queue");
            var view=(StackPanel)type.GetField("list",Any)!.GetValue(second)!;
            Assert(Walk(view).OfType<TextBlock>().Any(x=>
                x.Text?.Contains("r155-shared-held-file.txt")==true),
                "R155 secondary deck must display file held on first monitor");
            ((IDisposable)first).Dispose();
            Assert(File.Exists(path)&&
                ((IEnumerable<string>)active.GetValue(null)!).Count()==1,
                "R155 closing primary widget must not delete secondary's file");
            var remove=type.GetMethod("Remove",
                BindingFlags.Instance|BindingFlags.NonPublic)!;
            remove.Invoke(second,new[]{item});
            Assert(shared.Count==0 && !File.Exists(path),
                "R155 removing a file on either monitor clears shared queue");
            Console.WriteLine("R155 PASS: both independent real WPF Deck views "+
                "share held files, clipboard routes, remove and cleanup.");
        }
        finally
        {
            ((IDisposable)first).Dispose();
            ((IDisposable)second).Dispose();
            firstHost.Close();
            secondHost.Close();
            if(File.Exists(path))File.Delete(path);
            shared.Clear();
        }
    }

'@
SmokeSwap '    private static void TestIndependentMonitorsV154()' (
    $test.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'test two real Deck controls with one held queue'
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R155: shared held-file queue and staged paths across independently positioned widget Deck views.'
