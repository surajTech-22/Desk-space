using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeskSpace;
using Recognizer=DeskSpace.DeskSpaceFolderAppRecognitionV363;
using Vendor=DeskSpace.DeskSpaceAccountingFileRecognitionV364;

internal static class Program
{
    private static int passed;
    static void Assert(bool condition,string name)
    {
        if(!condition)throw new Exception("R364 FAIL: "+name);
        Console.WriteLine("R364 PASS: "+name);
        passed++;
    }
    static string Folder(string root,string name,params string[] files)
    {
        var path=Path.Combine(root,name);
        Directory.CreateDirectory(path);
        foreach(var f in files)File.WriteAllText(Path.Combine(path,f),"fixture");
        return path;
    }
    static string[] Apps(string path)=>Recognizer.RecognizePath(path)
        .Select(x=>x.AppId).ToArray();
    static Recognizer.Suggestion Match(string path,string id)=>
        Recognizer.RecognizePath(path).First(x=>x.AppId==id);
    public static int Main()
    {
        var baseDir=Path.Combine(Path.GetTempPath(),"DeskSpace-R364-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        try
        {
            var prime=Folder(baseDir,"tally-modern",
                "Company.1800","Manager.1800","TranMgr.1800","TSTATE.TSF");
            Assert(Apps(prime).SequenceEqual(new[]{"TallyPrime","Explorer"}),
                "TallyPrime 3.0 database recognized by companion .1800 markers");
            Assert(Apps(Path.Combine(prime,"Company.1800")).SequenceEqual(
                new[]{"TallyPrime","Explorer"}),
                "direct Company.1800 file is recognized ONLY with companion company masters");
            var legacy=Folder(baseDir,"tally-legacy",
                "Company.900","Manager.900","LinkMgr.900");
            Assert(Apps(legacy).SequenceEqual(new[]{"TallyPrime","TallyERP","Explorer"}),
                "legacy .900 company recognized with both Tally applications");
            Assert(Apps(Path.Combine(legacy,"Company.900")).SequenceEqual(
                new[]{"TallyPrime","Explorer"}),
                "single old database shard is never shell-opened as a document");
            var broken=Folder(baseDir,"tally-unverified","Company.900");
            Assert(Apps(broken).SequenceEqual(new[]{"Explorer"}),
                "standalone Company.900 without manager companion is not assumed a complete database");
            Assert(Apps(Path.Combine(broken,"Company.900")).SequenceEqual(
                new[]{"Explorer"}),"no generic .900 file association may execute/import a database");
            var winman=Folder(baseDir,"winman-mixed",
                "Customer.tax","GST_Client.wgst","Super_Traders.7EQ");
            Assert(Apps(winman).SequenceEqual(new[]{
                "WinmanCAERP","WinmanGST","WinmanTDS","Explorer"}),
                "mixed Winman CA-ERP/GST/TDS directory suggests each specific application");
            Assert(Apps(Path.Combine(winman,"Customer.tax")).SequenceEqual(
                new[]{"WinmanCAERP","Explorer"}),".tax directly recognized as Winman CA-ERP");
            Assert(Apps(Path.Combine(winman,"GST_Client.wgst")).SequenceEqual(
                new[]{"WinmanGST","Explorer"}),".wgst directly recognized as Winman GST");
            Assert(Apps(Path.Combine(winman,"Super_Traders.7EQ")).SequenceEqual(
                new[]{"WinmanTDS","Explorer"}),".7EQ directly recognized as Winman TDS");
            var backup=Folder(baseDir,"winman-backups",
                "Client.tax.wb","Client.wgst.wb","Super_Traders.7EQ.wt");
            Assert(Apps(backup).SequenceEqual(new[]{
                "WinmanCAERP","WinmanGST","WinmanTDS","Explorer"}),
                "Winman backups are labelled for manual recovery, never restored");
            Assert(Match(Path.Combine(backup,"Client.tax.wb"),"WinmanCAERP")
                .FolderKind.Contains("BACKUP"),"Winman .tax.wb backup clearly identified");
            Assert(Match(Path.Combine(backup,"Super_Traders.7EQ.wt"),"WinmanTDS")
                .FolderKind.Contains("BACKUP"),"Winman TDS .wt backup clearly identified");
            var ordinary=Folder(baseDir,"other-types","Invoice.pdf","Invoices.csv");
            Assert(Apps(ordinary).SequenceEqual(new[]{"Explorer"}),
                "mixed ordinary files do not choose one arbitrarily");
            foreach(var ext in new[]{".xlsx",".pdf",".jpeg",".csv",".xml",".json",".zip",
                ".dwg",".blend",".mp3",".mp4",".somevendor"})
            {
                var folder=Folder(baseDir,"generic-"+ext.TrimStart('.'),"item"+ext);
                Assert(Apps(Path.Combine(folder,"item"+ext)).SequenceEqual(
                    new[]{"Default","Explorer"}),"Windows-associated file option: "+ext);
            }
            foreach(var ext in new[]{".exe",".msi",".cmd",".bat",".ps1",".js",
                ".py",".lnk",".url",".reg",".scr",".dll",".com",".hta",".jar"})
            {
                var folder=Folder(baseDir,"unsafe-"+ext.TrimStart('.'),"item"+ext);
                Assert(Apps(Path.Combine(folder,"item"+ext)).SequenceEqual(
                    new[]{"Explorer"}),"never execute potentially active file type: "+ext);
            }
            var tdl=Folder(baseDir,"tally-xml","TallyImport.xml","Settings.json");
            Assert(Apps(tdl).SequenceEqual(new[]{"Explorer"}),
                "generic Tally-named XML must never be treated as an import instruction");
            Assert(!Apps(Path.Combine(tdl,"TallyImport.xml")).Contains("TallyPrime"),
                "generic Tally XML file does not trigger Tally import");
            // Simulate installed vendors using *fake paths* and an injected
            // process launcher. Neither Tally nor Winman is ever executed.
            var vendorApps=Path.Combine(baseDir,"Vendor Install");
            Directory.CreateDirectory(vendorApps);
            var tallyExe=Path.Combine(vendorApps,"Tally.exe");
            File.WriteAllBytes(tallyExe,new byte[]{0,1,2,3});
            var tallyChoice=Match(prime,"TallyPrime");
            int launches=0;
            Assert(!Recognizer.TryOpen(tallyChoice,out var err,_=>tallyExe,
                p=>{launches++;return null;}) &&
                launches==0 && err!.Contains("cannot be opened"),
                "Tally database shard cannot pretend it was directly file-opened; no app launched");

            // Windows itself handles a registered vendor file-open action.
            // Inject the registered association and process launcher; tests
            // never execute the fake Winman program or mutate the .tax file.
            var ca=Path.Combine(vendorApps,"WinTax.exe");
            File.WriteAllBytes(ca,new byte[]{1,2,3});
            ProcessStartInfo? started=null;
            var gstFile=Match(Path.Combine(winman,"GST_Client.wgst"),"WinmanGST");
            Assert(Recognizer.TryOpen(gstFile,out err,_=>ca,
                p=>{started=p;launches++;return null;},_=>ca) &&
                err==null&&started?.UseShellExecute==true &&
                started.FileName==gstFile.Target &&
                started.ArgumentList.Count==0,
                "Winman .wgst opens the ACTUAL file using verified Windows registered handler");
            started=null;
            var taxFile=Match(Path.Combine(winman,"Customer.tax"),"WinmanCAERP");
            Assert(Recognizer.TryOpen(taxFile,out err,_=>ca,
                p=>{started=p;return null;},_=>ca) &&
                started?.FileName==taxFile.Target &&
                started.UseShellExecute,
                "Winman .tax opens selected source file, not merely Winman main window");
            Assert(!Recognizer.TryOpen(taxFile,out err,_=>ca,_=>null,_=>null) &&
                err!.Contains("verified Winman file-open association"),
                "unregistered Winman handler cannot be guessed or launched");
            var badAssociation=Path.Combine(vendorApps,"Other.exe");
            File.WriteAllBytes(badAssociation,new byte[]{1});
            Assert(!Recognizer.TryOpen(taxFile,out err,_=>ca,_=>null,
                _=>badAssociation),
                "wrong Windows association cannot launch an unrelated application");
            started=null;
            Assert(!Recognizer.TryOpen(Match(Path.Combine(backup,"Client.tax.wb"),
                "WinmanCAERP"),out err,_=>ca,
                p=>{started=p;return null;},_=>ca) &&
                started==null,
                "Winman backup cannot trigger a file-open/restore action");
            Assert(!Recognizer.TryOpen(tallyChoice,out err,_=>null,_=>null),
                "Tally company cannot silently import or launch in place of file opening");
            var wrongTds=Path.Combine(baseDir,"Unrelated Program","WinTDS.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(wrongTds)!);
            File.WriteAllBytes(wrongTds,new byte[]{1,2,3});
            Assert(!Vendor.IsValidVendorExecutable(wrongTds,"WinmanTDS"),
                "WinTDS.exe in an unrelated product folder is not assumed to be Winman TDS");
            Assert(!Recognizer.TryOpen(Match(winman,"WinmanTDS"),out err,
                _=>wrongTds,_=>null),
                "refuse to launch ambiguous unrelated WinTDS.exe");
            var validTds=Path.Combine(baseDir,"Winman TDS","WinTDS.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(validTds)!);
            File.WriteAllBytes(validTds,new byte[]{1,2,3});
            Assert(Vendor.IsValidVendorExecutable(validTds,"WinmanTDS"),
                "Winman-specific WinTDS.exe location passes installed application check");

            var csv=Path.Combine(baseDir,"generic-csv","item.csv");
            started=null;
            Assert(Recognizer.TryOpen(Match(csv,"Default"),out err,null,
                p=>{started=p;return null;},_=>ca) &&
                started?.UseShellExecute==true&&started.FileName==csv,
                "unknown but nonexecutable file passes only to Windows file association after click");

            File.Delete(Path.Combine(prime,"Manager.1800"));
            File.Delete(Path.Combine(prime,"TranMgr.1800"));
            Assert(!Recognizer.TryOpen(tallyChoice,out err,_=>tallyExe,_=>null),
                "vendor DB signature rechecked at click and fails if companion files were removed");

            Console.WriteLine($"R364 ALL {passed} TALLY/WINMAN/UNIVERSAL FILE TESTS PASSED.");
            return 0;
        }
        finally
        {
            try{Directory.Delete(baseDir,true);}catch(IOException){}
        }
    }
}
