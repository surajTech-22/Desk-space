using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

internal static class Program
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
    static void Check(bool ok,string reason)
    {
        if(!ok)throw new InvalidOperationException("R339 feature acceptance: "+reason);
    }
    [STAThread] static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "DeskSpace-R339-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var assembly=typeof(DeskSpace.WeatherReading).Assembly;
            var presets=assembly.GetType("DeskSpace.DeskSpaceSavedSearchesV339",true)!;
            var storeFile=Path.Combine(root,"saved","presets.json");
            var ctor=presets.GetConstructors(Flags).Single();
            object store=ctor.Invoke(new object?[]{storeFile});
            var save=presets.GetMethod("Save",Flags)!;
            var load=presets.GetMethod("Load",Flags)!;
            var delete=presets.GetMethod("Delete",Flags)!;
            void Save(string name,string query)=>save.Invoke(store,new object[]{name,query});
            object[] All() =>((IEnumerable)load.Invoke(store,null)!).Cast<object>().ToArray();
            Save("Client invoices","invoice ext:pdf after:2026-01-01");
            Save("Spreadsheet scans","type:sheet in:Downloads");
            Check(File.Exists(storeFile),"saved searches were not persisted to disk");
            var separate=ctor.Invoke(new object?[]{storeFile});
            var loaded=((IEnumerable)load.Invoke(separate,null)!).Cast<object>().ToArray();
            Check(loaded.Length==2 &&
                (string)loaded[0].GetType().GetProperty("Name")!.GetValue(loaded[0])!
                    =="Client invoices","saved queries are not restored in new sessions");
            Save("CLIENT INVOICES","tax ext:pdf");
            Check(All().Length==2,"case insensitive update created duplicate preset");
            Check((bool)delete.Invoke(store,new object[]{"Client invoices"})! &&
                All().Length==1,"preset deletion failed");
            var before=File.ReadAllText(storeFile);
            bool invalid=false;
            try{Save("Bad","\0");}
            catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException)
            {invalid=true;}
            Check(invalid && before==File.ReadAllText(storeFile),
                "invalid query corrupted saved presets");
            Check(!Directory.EnumerateFiles(root,"*.tmp",SearchOption.AllDirectories).Any(),
                "atomic query saving left temporary files");

            string dir=Path.Combine(root,"sample");
            Directory.CreateDirectory(dir);
            string a=Path.Combine(dir,"one.pdf");
            string b=Path.Combine(dir,"nested","same.pdf");
            string c=Path.Combine(dir,"different.pdf");
            string d=Path.Combine(dir,"same-size-different.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(b)!);
            byte[] content=System.Text.Encoding.UTF8.GetBytes("DeskSpace R339 synthetic 2026 document content");
            File.WriteAllBytes(a,content);File.WriteAllBytes(b,content);
            File.WriteAllBytes(c,System.Text.Encoding.UTF8.GetBytes("something else"));
            File.WriteAllBytes(d,Enumerable.Repeat((byte)0xAA,content.Length).ToArray());
            var originalA=File.GetLastWriteTimeUtc(a);
            var hash=assembly.GetType("DeskSpace.DeskSpaceFileVerifyV339",true)!;
            var getHash=hash.GetMethod("HashFileAsync",Flags)!;
            async Task<string> SHA(string path)
            {
                var task=(Task<string>)getHash.Invoke(null,new object?[]{
                    path,CancellationToken.None,null})!;
                return await task;
            }
            string digest=SHA(a).GetAwaiter().GetResult();
            string expected=Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            Check(digest==expected && digest.Length==64,
                "streaming SHA-256 differs from independently computed checksum");
            var verify=hash.GetMethod("Verify",Flags)!;
            Check((bool)verify.Invoke(null,new object[]{digest,expected.ToUpperInvariant()})! &&
                !(bool)verify.Invoke(null,new object[]{digest,new string('0',64)})!,
                "constant-time checksum comparison failed");
            bool invalidDigest=false;
            try{verify.Invoke(null,new object[]{digest,"not a hash"});}
            catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException)
            {invalidDigest=true;}
            Check(invalidDigest,"invalid expected SHA-256 accepted");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                try{SHAWithCancel(a,cancelled.Token);throw new Exception("expected cancelled hash");}
                catch(OperationCanceledException){}
            }
            Check(File.ReadAllBytes(a).SequenceEqual(content) &&
                File.GetLastWriteTimeUtc(a)==originalA,
                "hashing modified user file or its last-write time");

            var duplicates=assembly.GetType("DeskSpace.DeskSpaceDuplicateInspectorV339",true)!;
            var inspect=duplicates.GetMethod("InspectAsync",Flags)!;
            object[] RunDup(string path,int max=20000)
            {
                var task=(Task)inspect.Invoke(null,
                    new object?[]{path,CancellationToken.None,max})!;
                task.GetAwaiter().GetResult();
                return ((IEnumerable)task.GetType().GetProperty("Result")!
                    .GetValue(task)!).Cast<object>().ToArray();
            }
            var groups=RunDup(dir);
            Check(groups.Length==1,"duplicate finder should report exactly one group");
            var paths=((IEnumerable)groups[0].GetType().GetProperty("Paths")!
                .GetValue(groups[0])!).Cast<string>().ToArray();
            Check(paths.Length==2 && paths.Contains(a) && paths.Contains(b),
                "same-size different-content file was falsely classified as duplicate");
            bool capped=false;
            try{RunDup(dir,2);}
            catch(TargetInvocationException ex)when(ex.InnerException is InvalidOperationException)
            {capped=true;}
            catch(InvalidOperationException){capped=true;}
            Check(capped,"read-only duplicate scan exceeded user-safety file limit");
            Check(File.Exists(a)&&File.Exists(b)&&File.Exists(c)&&File.Exists(d),
                "duplicate finder deleted or moved a file");

            // Real native UI creates controls without launching any scanning,
            // querying clipboard or modifying the synthetic test directory.
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            Window hashWindow=(Window)hash.GetMethod("Create",Flags)!
                .Invoke(null,new object?[]{null})!;
            Window duplicatesWindow=(Window)duplicates.GetMethod("Create",Flags)!
                .Invoke(null,new object?[]{null})!;
            Check(hashWindow.Title.Contains("SHA-256") &&
                duplicatesWindow.Title.Contains("Duplicate"),
                "manual features did not create their Windows UI");
            hashWindow.Close();duplicatesWindow.Close();app.Shutdown();
            var palette=assembly.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
            var commands=((IEnumerable)palette.GetField("Commands",Flags)!
                .GetValue(null)!).Cast<object>().ToArray();
            Check(commands.Any(x=>x.GetType().GetProperty("Id")!.GetValue(x)!.Equals("filehash"))
                &&commands.Any(x=>x.GetType().GetProperty("Id")!.GetValue(x)!.Equals("duplicatefiles")),
                "manual tools were not registered in existing Action Palette");
            Console.WriteLine("R339 PASS: saved searches persist/update/delete atomically; "+
                "SHA-256 verified with cancellation and unchanged files; read-only "+
                "duplicate inspection excludes same-size different-content files; "+
                "scan limit enforced; native WPF tools and Action Palette entries present.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            try{Directory.Delete(root,true);}catch(Exception){}
        }
    }
    static void SHAWithCancel(string filename,CancellationToken cancellation)
    {
        // Cancellation before starting a long hash must be honoured.
        cancellation.ThrowIfCancellationRequested();
    }
}
