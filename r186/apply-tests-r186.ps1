$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$replacement,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$replacement=$replacement.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R186 '+$label+' expected one anchor, got '+$count)}
 $script:s=$script:s.Replace($old,$replacement)
}
One '            TestPostInstallReceiptV185();' @'
            TestPostInstallReceiptV185();
            TestStagedContentDuplicatesV186();
'@ 'invoke native WPF and filesystem regression'
$method=@'
    private static void TestStagedContentDuplicatesV186()
    {
        var t=Product.GetType("DeskSpace.DeskSpaceContentDuplicatesV186",true)!;
        var check=t.GetMethod("CheckTestFixtures",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var checkProduction=t.GetMethod("Check",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var root=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),"Downloads",
            "DeskSpace_R186_DuplicateSmoke_"+Guid.NewGuid().ToString("N"));
        var shelf=Path.Combine(root,"TemporaryShelf");
        Directory.CreateDirectory(shelf);
        try
        {
            string Add(string name,string content)
            {
                var path=Path.Combine(shelf,name);
                File.WriteAllText(path,content);
                return path;
            }
            var a=Add("alpha.txt","same UTF8 contents 123");
            var b=Add("bravo.txt","same UTF8 contents 123");
            var changed=Add("changed.txt","different contents 123");
            string Folder(string name)
            {
                string dir=Path.Combine(shelf,name);
                Directory.CreateDirectory(Path.Combine(dir,"Nested"));
                File.WriteAllText(Path.Combine(dir,"Nested","inside.txt"),
                    "identical nested contents");
                return dir;
            }
            var folder1=Folder("folder-one");
            var folder2=Folder("folder-two");
            var outside=Path.Combine(root,"original-must-not-be-read.txt");
            File.WriteAllText(outside,"source should not be accessed");
            var items=new[]{a,b,changed,folder1,folder2,outside};
            var report=check.Invoke(null,new object[]{
                items,shelf,CancellationToken.None})!;
            var groups=(Array)report.GetType().GetProperty("Groups")!.
                GetValue(report)!;
            Assert((int)report.GetType().GetProperty("Checked")!.GetValue(report)!==5 &&
                (int)report.GetType().GetProperty("Skipped")!.GetValue(report)!==1 &&
                groups.Length==2,
                "R186 must match two identical files and two identical folders, "+
                "but reject other content and source files outside staging");
            var kinds=groups.Cast<object>().Select(g=>
                (string)g.GetType().GetProperty("Kind")!.GetValue(g)!).ToHashSet();
            Assert(kinds.Contains("File")&&kinds.Contains("Folder"),
                "R186 content fingerprints must distinguish files and folder trees");
            var onlyOutside=checkProduction.Invoke(null,new object[]{
                new[]{outside},CancellationToken.None})!;
            Assert((int)onlyOutside.GetType().GetProperty("Checked")!.
                    GetValue(onlyOutside)!==0,
                "R186 production scan must refuse files outside the private DeskSpace shelf");
            var cancelled=new CancellationTokenSource();
            cancelled.Cancel();
            bool aborts=false;
            try{check.Invoke(null,new object[]{
                items,shelf,cancelled.Token});}
            catch(TargetInvocationException e) when(e.InnerException
                is OperationCanceledException){aborts=true;}
            Assert(aborts,"R186 cancelled duplicate scans must abort without changes");
            Assert(File.ReadAllText(a)=="same UTF8 contents 123" &&
                File.ReadAllText(b)=="same UTF8 contents 123" &&
                File.ReadAllText(outside)=="source should not be accessed",
                "R186 scan may never modify source or staged items");
            var health=Product.GetType(
                "DeskSpace.DeskSpaceComponentHealthV175",true)!;
            var create=health.GetMethod("Create",
                BindingFlags.NonPublic|BindingFlags.Static)!;
            var owner=new Window{Width=130,Height=130,ShowInTaskbar=false};
            Window? status=null;
            try
            {
                owner.Show();
                status=(Window)create.Invoke(null,new object[]{owner,true})!;
                status.Show();status.UpdateLayout();
                Assert(Walk(status).OfType<Button>().Any(x=>
                        x.Content?.ToString()=="Find duplicate held files (SHA-256)") &&
                    Walk(status).OfType<Button>().Any(x=>
                        x.Content?.ToString()=="Cancel scan"),
                    "R186 on-demand duplicate and cancel buttons must render in native WPF status");
            }
            finally{status?.Close();owner.Close();}
            Console.WriteLine("R186 PASS: local SHA256 file/tree groups, "+
                "source restriction, cancellation, non-destructive files and WPF UI.");
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'WPF duplicate button and filesystem test insertion'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R186 native WPF and Downloads-only content-fingerprint regression installed.'
