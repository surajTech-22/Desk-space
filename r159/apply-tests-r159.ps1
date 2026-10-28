$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$lf=[string][char]10
$crlf=[string][char]13+$lf
$p='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R159 smoke $why expected one anchor found $hits"}
 $script:s=$script:s.Replace($old,$new)
}
Swap '            TestMonitorInputIsolationV158();' @'
            TestMonitorInputIsolationV158();
            TestFolderDropZipOrCopyV159();
'@ 'run real ZIP and two-widget drag-choice acceptance'

$test=@'
    private static void TestFolderDropZipOrCopyV159()
    {
        var folderType=Product.GetType("DeskSpace.DeskSpaceFolderZipV159",true)!;
        var shelfType=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var zipName=folderType.GetMethod("ZipName",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var create=folderType.GetMethod("CreateFromFolder",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var source=Path.Combine(outdir,"r159-sample-"+Guid.NewGuid().ToString("N"));
        var nested=Path.Combine(source,"Nested");
        var empty=Path.Combine(source,"Empty");
        var archive=Path.Combine(outdir,"r159-standalone-"+Guid.NewGuid()+".zip");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(source,"invoice.txt"),"zip or copy");
        File.WriteAllText(Path.Combine(nested,"data.csv"),"Name,Value");
        var root=Path.GetFileName(source);
        var hostA=new Window{Width=420,Height=420,ShowInTaskbar=false};
        var hostB=new Window{Width=420,Height=420,ShowInTaskbar=false};
        object? a=null,b=null;
        try
        {
            Assert((string)zipName.Invoke(null,new object[]{source})==root+".zip",
                "R159 ZIP should be named after the dragged folder");
            create.Invoke(null,new object[]{source,archive});
            using(var opened=System.IO.Compression.ZipFile.OpenRead(archive))
            {
                var names=opened.Entries.Select(x=>x.FullName).ToHashSet();
                Assert(names.Contains(root+"/")&&
                    names.Contains(root+"/Nested/")&&
                    names.Contains(root+"/Empty/")&&
                    names.Contains(root+"/invoice.txt")&&
                    names.Contains(root+"/Nested/data.csv"),
                    "R159 zipped folder must preserve root, nested and empty folders");
                var entry=opened.GetEntry(root+"/invoice.txt")!;
                using var stream=new StreamReader(entry.Open());
                Assert(stream.ReadToEnd()=="zip or copy",
                    "R159 ZIP file content must match the original");
            }
            Assert(File.ReadAllText(Path.Combine(source,"invoice.txt"))=="zip or copy",
                "R159 source folder must never be modified");

            hostA.Show();hostB.Show();
            a=Activator.CreateInstance(shelfType,new object[]{hostA})!;
            b=Activator.CreateInstance(shelfType,new object[]{hostB})!;
            var show=shelfType.GetMethod("ShowFolderChoice",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var choice=shelfType.GetField("folderChoice",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var first=(System.Windows.Controls.Primitives.Popup)choice.GetValue(a)!;
            var second=(System.Windows.Controls.Primitives.Popup)choice.GetValue(b)!;
            show.Invoke(a,new object[]{new[]{source}});
            Assert(first.IsOpen&&!second.IsOpen,
                "R159 folder action must open on dropped widget only");
            var buttons=Walk((DependencyObject)first.Child)
                .OfType<Button>().ToArray();
            Assert(buttons.Any(x=>x.Content?.ToString()=="ZIP")&&
                   buttons.Any(x=>x.Content?.ToString()=="Copy")&&
                   buttons.Any(x=>x.Content?.ToString()=="×"),
                "R159 circular prompt must offer ZIP, Copy and dismiss");
            var cancel=buttons.First(x=>x.Content?.ToString()=="×");
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(!first.IsOpen&&
                   ((IEnumerable<string>)shelfType.GetProperty("ActivePaths")!
                       .GetValue(null)!).Count()==0,
                "R159 cancelling a folder drop must not create a file");

            var paths=(IEnumerable<string>)shelfType.GetProperty("ActivePaths")!
                .GetValue(null)!;
            // Copy means the folder itself, not a ZIP or each child file.
            show.Invoke(a,new object[]{new[]{source}});
            buttons=Walk((DependencyObject)first.Child).OfType<Button>().ToArray();
            buttons.First(x=>x.Content?.ToString()=="Copy")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void PumpUntil(Func<bool> ready)
            {
                var until=DateTime.UtcNow.AddSeconds(12);
                while(!ready() && DateTime.UtcNow<until)
                {
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>frame.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                    System.Threading.Thread.Sleep(10);
                }
                Assert(ready(),"R159 folder staging timed out");
            }
            PumpUntil(()=>((IEnumerable<string>)shelfType
                .GetProperty("ActivePaths")!.GetValue(null)!).Count()==1);
            var copied=((IEnumerable<string>)shelfType.GetProperty("ActivePaths")!
                .GetValue(null)!).Single();
            Assert(Directory.Exists(copied) &&
                   File.Exists(Path.Combine(copied,"Nested","data.csv")),
                "R159 Copy must hold the folder and its original structure");
            var secondList=(StackPanel)shelfType.GetField("list",Any)!.GetValue(b)!;
            Assert(Walk(secondList).OfType<TextBlock>().Any(x=>
                   x.Text?.Contains(root)==true),
                "R159 copied folder must appear on the second monitor Deck");
            shelfType.GetMethod("ClearActive",BindingFlags.Public|
                BindingFlags.Static)!.Invoke(null,null);

            // ZIP must stage a FILE rather than the original directory.
            show.Invoke(a,new object[]{new[]{source}});
            buttons=Walk((DependencyObject)first.Child).OfType<Button>().ToArray();
            buttons.First(x=>x.Content?.ToString()=="ZIP")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(()=>((IEnumerable<string>)shelfType
                .GetProperty("ActivePaths")!.GetValue(null)!).Count()==1);
            var stagedZip=((IEnumerable<string>)shelfType
                .GetProperty("ActivePaths")!.GetValue(null)!).Single();
            Assert(File.Exists(stagedZip)&&
                   stagedZip.EndsWith(root+".zip",StringComparison.OrdinalIgnoreCase),
                "R159 ZIP must stage one real .zip file in shared Deck");
            using(var opened=System.IO.Compression.ZipFile.OpenRead(stagedZip))
                Assert(opened.GetEntry(root+"/Nested/data.csv")!=null,
                    "R159 staged ZIP must include the nested file");
            Assert(Walk(secondList).OfType<TextBlock>().Any(x=>
                    x.Text?.Contains(root+".zip")==true),
                "R159 staged ZIP must be visible from both widget Decks");
            shelfType.GetMethod("ClearActive",BindingFlags.Static|
                BindingFlags.Public)!.Invoke(null,null);
            Assert(File.Exists(Path.Combine(source,"invoice.txt")),
                "R159 cleaning staged ZIP must preserve the original folder");
            Console.WriteLine("R159 PASS: circular ZIP/Copy chooser, cancel, "+
                "real nested/empty folder archive, shared two-widget Deck and original safety.");
        }
        finally
        {
            if(a is IDisposable da)da.Dispose();
            if(b is IDisposable db)db.Dispose();
            hostA.Close();hostB.Close();
            if(File.Exists(archive))File.Delete(archive);
            if(Directory.Exists(source))Directory.Delete(source,true);
        }
    }

'@
Swap '    private static void TestIndependentMonitorsV154()' ($test.Replace($crlf,$lf)+
  '    private static void TestIndependentMonitorsV154()') 'native real WPF ZIP action path'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R159: real WPF ZIP/Copy chooser, cancellation, nested and empty ZIP contents, shared Deck regression installed.'
