$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$start=$s.IndexOf('    private static void TestFolderDropZipOrCopyV159()')
$end=if($start -ge 0){$s.IndexOf('    private static void TestIndependentMonitorsV154()',$start)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R160 could not locate R159 ZIP/Copy WPF test to upgrade'}
$test=@'
    private static void TestFolderDropZipOrCopyV159()
    {
        var folderType=Product.GetType("DeskSpace.DeskSpaceFolderZipV159",true)!;
        var shelfType=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var adornerType=Product.GetType("DeskSpace.DeskSpaceFolderDragAdornerV160",true)!;
        var zipName=folderType.GetMethod("ZipName",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var create=folderType.GetMethod("CreateFromFolder",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var source=Path.Combine(outdir,"r160-sample-"+Guid.NewGuid().ToString("N"));
        var nested=Path.Combine(source,"Nested");
        var empty=Path.Combine(source,"Empty");
        var archive=Path.Combine(outdir,"r160-standalone-"+Guid.NewGuid()+".zip");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(source,"invoice.txt"),"zip or copy");
        File.WriteAllText(Path.Combine(nested,"data.csv"),"Name,Value");
        var root=Path.GetFileName(source);
        var hostA=new Window{Width=116,Height=116,Left=70,Top=70,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false,
            Content=new Grid()};
        var hostB=new Window{Width=116,Height=116,Left=270,Top=70,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false,
            Content=new Grid()};
        object? a=null,b=null;
        try
        {
            Assert((string)zipName.Invoke(null,new object[]{source})==root+".zip",
                "R160 ZIP must have the original folder name");
            create.Invoke(null,new object[]{source,archive});
            using(var opened=System.IO.Compression.ZipFile.OpenRead(archive))
            {
                var entries=opened.Entries.Select(x=>x.FullName).ToHashSet();
                Assert(entries.Contains(root+"/")&&
                    entries.Contains(root+"/Nested/")&&
                    entries.Contains(root+"/Empty/")&&
                    entries.Contains(root+"/invoice.txt")&&
                    entries.Contains(root+"/Nested/data.csv"),
                    "R160 ZIP must preserve nested and empty folders");
                using var reader=new StreamReader(opened.GetEntry(root+"/invoice.txt")!.Open());
                Assert(reader.ReadToEnd()=="zip or copy",
                    "R160 ZIP contents must match original");
            }
            hostA.Show();hostB.Show();
            a=Activator.CreateInstance(shelfType,new object[]{hostA})!;
            b=Activator.CreateInstance(shelfType,new object[]{hostB})!;
            var show=shelfType.GetMethod("ShowFolderChoice",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var hide=shelfType.GetMethod("HideFolderChoice",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var commit=shelfType.GetMethod("CommitFolderDrop",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var overlay=shelfType.GetField("folderAdorner",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var paths=(System.Collections.Generic.IEnumerable<string>)
                shelfType.GetProperty("ActivePaths")!.GetValue(null)!;
            var choice=adornerType.GetMethod("ChoiceAt",
                BindingFlags.NonPublic|BindingFlags.Static)!;
            var diameter=new Size(116,116);
            Assert((bool?)choice.Invoke(null,new object[]{
                    diameter,new Point(30,58)})==true,
                "R160 left half of circle must select ZIP");
            Assert((bool?)choice.Invoke(null,new object[]{
                    diameter,new Point(86,58)})==false,
                "R160 right half of circle must select Copy");
            Assert((bool?)choice.Invoke(null,new object[]{
                    diameter,new Point(2,2)})==null,
                "R160 outside circular menu must not select an action");

            // Simulate DragOver BEFORE any drop. No Popup HWND is allowed.
            show.Invoke(a,new object[]{new[]{source}});
            Assert(overlay.GetValue(a)!=null&&overlay.GetValue(b)==null,
                "R160 ZIP/Copy must appear inside only the dragged widget");
            Assert(shelfType.GetField("folderChoice",
                   BindingFlags.NonPublic|BindingFlags.Instance)==null,
                "R160 must not contain the detached R159 Popup");
            // Render the actual adorner after WPF Window layout/Dispatcher
            // has processed the AdornerLayer invalidation.
            var live=(System.Windows.Documents.Adorner)overlay.GetValue(a)!;
            hostA.UpdateLayout();
            var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(
                live.AdornedElement);
            layer?.Update(live.AdornedElement);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                new Action(()=>{}),System.Windows.Threading.DispatcherPriority.Render);
            hostA.UpdateLayout();
            var bitmap=new RenderTargetBitmap(116,116,96,96,
                PixelFormats.Pbgra32);
            bitmap.Render(live);
            var raster=new byte[116*116*4];
            bitmap.CopyPixels(raster,116*4,0);
            int leftPixel=(58*116+31)*4;
            int rightPixel=(58*116+85)*4;
            Console.WriteLine("R160 visual sizes: adorner="+live.RenderSize+
                " adorned="+live.AdornedElement.RenderSize+
                " host="+hostA.RenderSize+" L="+raster[leftPixel+3]+
                " R="+raster[rightPixel+3]);
            Assert(raster[leftPixel+3]>160&&raster[rightPixel+3]>160,
                "R160 ZIP and Copy halves must both be painted inside the circle");
            var encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var picture=Path.Combine(outdir,"r160-folder-drag-in-circle.png");
            using(var file=File.Create(picture))encoder.Save(file);
            Assert(File.Exists(picture),
                "R160 screenshot of actual drag-over circular choice must exist");
            Assert(!paths.Any(),
                "R160 drag-hover must never stage a file before mouse release");
            hide.Invoke(a,null);
            Assert(overlay.GetValue(a)==null&&!paths.Any(),
                "R160 drag cancel must restore the donut without staging files");

            void PumpUntil(Func<bool> ready)
            {
                var until=DateTime.UtcNow.AddSeconds(15);
                while(!ready()&&DateTime.UtcNow<until)
                {
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>frame.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                    System.Threading.Thread.Sleep(10);
                }
                Assert(ready(),"R160 held-folder operation timed out");
            }
            // Simulate drop on right half: Copy without ZIP.
            show.Invoke(a,new object[]{new[]{source}});
            commit.Invoke(a,new object[]{false});
            PumpUntil(()=>paths.Count()==1);
            var copied=paths.Single();
            Assert(Directory.Exists(copied)&&
                   File.Exists(Path.Combine(copied,"Nested","data.csv")),
                "R160 Copy must keep folder intact and uncompressed");
            var view=(StackPanel)shelfType.GetField("list",Any)!.GetValue(b)!;
            Assert(Walk(view).OfType<TextBlock>().Any(x=>
                   x.Text?.Contains(root)==true),
                "R160 copied folder must be shared to other monitor Deck");
            shelfType.GetMethod("ClearActive",BindingFlags.Public|
                BindingFlags.Static)!.Invoke(null,null);

            // Simulate drop on left half: ZIP and hold a single .zip file.
            show.Invoke(a,new object[]{new[]{source}});
            commit.Invoke(a,new object[]{true});
            PumpUntil(()=>paths.Count()==1);
            var staged=paths.Single();
            Assert(File.Exists(staged)&&
                   staged.EndsWith(root+".zip",StringComparison.OrdinalIgnoreCase),
                "R160 ZIP must stage one archive, not the folder");
            using(var verified=System.IO.Compression.ZipFile.OpenRead(staged))
                Assert(verified.GetEntry(root+"/Nested/data.csv")!=null,
                    "R160 staged ZIP must contain nested files");
            Assert(Walk(view).OfType<TextBlock>().Any(x=>
                   x.Text?.Contains(root+".zip")==true),
                "R160 zipped folder must show in second monitor Deck");
            shelfType.GetMethod("ClearActive",BindingFlags.Public|
                BindingFlags.Static)!.Invoke(null,null);
            Assert(File.Exists(Path.Combine(source,"invoice.txt")),
                "R160 dropping/copying/zipping must leave source untouched");
            Console.WriteLine("R160 PASS: options inside 116px donut on DragOver, "+
                "no popup/after-drop prompt, drop left ZIP, drop right Copy, "+
                "cancel, real ZIP contents and two-monitor Deck sharing.");
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
$s=$s.Substring(0,$start)+$test.Replace($crlf,$lf)+$s.Substring($end)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R160: upgraded WPF smoke to require in-donut drag-hover ZIP/Copy with no Popup HWND.'
