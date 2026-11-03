using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace DeskSpace;

// R364: known vendor data is NOT a generic document. Only open the vendor
// application, never pass the raw accounting database/backup to an unverified
// CLI flag, import records, restore a backup or modify a live data directory.
internal static class DeskSpaceAccountingFileRecognitionV364
{
    internal static readonly HashSet<string> WinmanTdsExtensions=
        new(StringComparer.OrdinalIgnoreCase){
            ".24q",".26q",".27q",".27eq",".7eq"
        };
    private static readonly HashSet<string> DangerousExtensions=
        new(StringComparer.OrdinalIgnoreCase){
            ".exe",".com",".scr",".msi",".msp",".msix",".appx",".appref-ms",
            ".bat",".cmd",".ps1",".psm1",".psd1",".vbs",".vbe",".wsf",
            ".wsh",".hta",".js",".jse",".jar",".sh",".py",".rb",".pl",
            ".php",".lnk",".url",".scf",".pif",".cpl",".dll",".reg",
            ".sct",".inf",".application",".gadget",".workflow"
        };
    internal static bool SafeAssociation(string file)
    {
        var extension=Path.GetExtension(file);
        return extension.Length>=2 && extension.Length<=15 &&
            !DangerousExtensions.Contains(extension) &&
            !string.Equals(extension,".900",StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension,".1800",StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension,".wb",StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension,".wt",StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsVendor(string appId)=>appId is
        "TallyPrime" or "TallyERP" or "WinmanCAERP" or "WinmanGST" or "WinmanTDS";

    private static readonly string[] PrimeExe={"Tally.exe"};
    private static readonly string[] ErpExe={"Tally.exe"};
    private static readonly string[] WinmanExe={"WinTax.exe"};
    private static readonly string[] TdsExe={"WinTDS.exe"};

    internal static string[] AllowedExecutableNames(string appId)=>appId switch
    {
        "TallyPrime"=>PrimeExe,
        "TallyERP"=>ErpExe,
        "WinmanCAERP" or "WinmanGST"=>WinmanExe,
        "WinmanTDS"=>TdsExe,
        _=>Array.Empty<string>()
    };

    private static bool ValidVendorExe(string path,string appId)
    {
        try
        {
            if(!AllowedExecutableNames(appId).Contains(Path.GetFileName(path),
                    StringComparer.OrdinalIgnoreCase) || !File.Exists(path) ||
                (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                return false;
            // TDS.exe is a generic filename used by unrelated software.
            // For Winman TDS, the application must also live in a Winman
            // install directory, never the dropped folder or an arbitrary
            // unrelated product installation.
            if(appId=="WinmanTDS" &&
                !Path.GetFullPath(path).Split(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
                    .Any(x=>x.Contains("winman",StringComparison.OrdinalIgnoreCase)))
                return false;
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) {return false;}
    }
    internal static bool IsValidVendorExecutable(string path,string appId)=>
        ValidVendorExe(path,appId);

    private static IEnumerable<string> Candidates(string appId)
    {
        var system=Path.GetPathRoot(Environment.SystemDirectory)??"C:\\";
        var pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        switch(appId)
        {
            case "TallyPrime":
                yield return Path.Combine(pf,"TallyPrime","Tally.exe");
                yield return Path.Combine(pf86,"TallyPrime","Tally.exe");
                yield return Path.Combine(system,"TallyPrime","Tally.exe");
                yield return Path.Combine(system,"Tally","Tally.exe");
                break;
            case "TallyERP":
                yield return Path.Combine(system,"Tally.ERP9","Tally.exe");
                yield return Path.Combine(system,"Tally","Tally.exe");
                yield return Path.Combine(pf86,"Tally.ERP9","Tally.exe");
                break;
            case "WinmanCAERP":
            case "WinmanGST":
                foreach(var dir in new[]{
                    Path.Combine(system,"Winman","Winman CA-ERP & GST"),
                    Path.Combine(system,"Winman","Winman CA ERP"),
                    Path.Combine(system,"Winman","Winman CA ERP & GST"),
                    Path.Combine(system,"Winman"),
                    Path.Combine(pf,"Winman","Winman CA-ERP & GST"),
                    Path.Combine(pf86,"Winman","Winman CA-ERP & GST")
                })
                    yield return Path.Combine(dir,"WinTax.exe");
                break;
            case "WinmanTDS":
                foreach(var dir in new[]{
                    Path.Combine(system,"Winman","Winman TDS"),
                    Path.Combine(system,"Winman","TDS"),
                    Path.Combine(pf,"Winman","Winman TDS"),
                    Path.Combine(pf86,"Winman","Winman TDS")
                })
                    yield return Path.Combine(dir,"WinTDS.exe");
                break;
        }
    }
    internal static string? FindInstalledVendor(string appId)
    {
        if(!IsVendor(appId))return null;
        var name=AllowedExecutableNames(appId)[0];
        const string appPaths=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";
        foreach(var registry in new[]{Registry.CurrentUser,Registry.LocalMachine})
        {
            try
            {
                using var key=registry.OpenSubKey(appPaths+name);
                var registered=(key?.GetValue(null) as string)?.Trim().Trim('"');
                if(registered!=null && ValidVendorExe(registered,appId))
                    return Path.GetFullPath(registered);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException){}
        }
        return Candidates(appId).FirstOrDefault(path=>ValidVendorExe(path,appId));
    }

    internal static bool ContainsTallyCompany(IReadOnlyCollection<string> files,
        out bool modern)
    {
        modern=files.Contains("Company.1800",StringComparer.OrdinalIgnoreCase) &&
            (files.Contains("Manager.1800",StringComparer.OrdinalIgnoreCase) ||
             files.Contains("TranMgr.1800",StringComparer.OrdinalIgnoreCase));
        bool older=files.Contains("Company.900",StringComparer.OrdinalIgnoreCase) &&
            (files.Contains("Manager.900",StringComparer.OrdinalIgnoreCase) ||
             files.Contains("TranMgr.900",StringComparer.OrdinalIgnoreCase));
        return modern||older;
    }

    private static bool IsTax(string name)=>name.EndsWith(".tax",
        StringComparison.OrdinalIgnoreCase);
    private static bool IsGst(string name)=>name.EndsWith(".wgst",
        StringComparison.OrdinalIgnoreCase);
    private static bool IsTds(string name)=>WinmanTdsExtensions.Contains(
        Path.GetExtension(name));

    internal static IReadOnlyList<DeskSpaceFolderAppRecognitionV363.Suggestion>
        RecognizeVendorFolder(string full,IReadOnlyCollection<string> files)
    {
        var results=new List<DeskSpaceFolderAppRecognitionV363.Suggestion>();
        void Add(string id,string name,string kind,string evidence)=>
            results.Add(new(full,full,id,name,kind,evidence));
        if(ContainsTallyCompany(files,out bool modern))
        {
            Add("TallyPrime","TallyPrime",modern?
                "TallyPrime company database":"Tally company database",
                modern?"Company.1800 + Manager/TranMgr.1800":
                       "Company.900 + Manager/TranMgr.900");
            if(!modern)Add("TallyERP","Tally.ERP 9","Tally company database",
                "Legacy .900 company format");
        }
        bool tax=files.Any(IsTax),gst=files.Any(IsGst),tds=files.Any(IsTds);
        if(tax)Add("WinmanCAERP","Winman CA-ERP","Winman income-tax data",
            ".tax (select file within Winman)");
        if(gst)Add("WinmanGST","Winman GST","Winman GST data",
            ".wgst (select file within Winman)");
        if(tds)Add("WinmanTDS","Winman TDS","Winman TDS data",
            ".24Q/.26Q/.27Q/.7EQ (select file within Winman)");
        if(!tax && files.Any(x=>x.EndsWith(".tax.wb",
            StringComparison.OrdinalIgnoreCase)))
            Add("WinmanCAERP","Winman CA-ERP","Winman income-tax BACKUP",
                ".tax.wb backup — restore within Winman only");
        if(!gst && files.Any(x=>x.EndsWith(".wgst.wb",
            StringComparison.OrdinalIgnoreCase)))
            Add("WinmanGST","Winman GST","Winman GST BACKUP",
                ".wgst.wb backup — restore within Winman only");
        if(!tds && files.Any(x=>x.EndsWith(".wt",
            StringComparison.OrdinalIgnoreCase)))
            Add("WinmanTDS","Winman TDS","Winman TDS BACKUP",
                ".wt backup — restore within Winman only");
        return results;
    }

    internal static IReadOnlyList<DeskSpaceFolderAppRecognitionV363.Suggestion>
        RecognizeVendorFile(string full)
    {
        var directory=Path.GetDirectoryName(full);
        if(directory==null||!File.Exists(full))return Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
        string name=Path.GetFileName(full);
        string id,app,kind;
        if(IsTax(name)){id="WinmanCAERP";app="Winman CA-ERP";kind="Winman income-tax data";}
        else if(IsGst(name)){id="WinmanGST";app="Winman GST";kind="Winman GST data";}
        else if(IsTds(name)){id="WinmanTDS";app="Winman TDS";kind="Winman TDS data";}
        else if(name.EndsWith(".tax.wb",StringComparison.OrdinalIgnoreCase)){
            id="WinmanCAERP";app="Winman CA-ERP";kind="Winman income-tax BACKUP — restore within application";
        }
        else if(name.EndsWith(".wgst.wb",StringComparison.OrdinalIgnoreCase)){
            id="WinmanGST";app="Winman GST";kind="Winman GST BACKUP — restore within application";
        }
        else if(name.EndsWith(".wt",StringComparison.OrdinalIgnoreCase)){
            id="WinmanTDS";app="Winman TDS";kind="Winman TDS BACKUP — restore within application";
        }
        else if(name.Equals("Company.900",StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Company.1800",StringComparison.OrdinalIgnoreCase))
        {
            // An individual Tally database shard must not be opened as a
            // Windows document. Require a companion company master file.
            string suffix=name.EndsWith(".1800",StringComparison.OrdinalIgnoreCase)?
                ".1800":".900";
            if(!File.Exists(Path.Combine(directory,"Manager"+suffix)) &&
               !File.Exists(Path.Combine(directory,"TranMgr"+suffix)))
                return Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
            id="TallyPrime";app="TallyPrime";kind="Tally company data — select company inside Tally";
        }
        else return Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
        return new[]{new DeskSpaceFolderAppRecognitionV363.Suggestion(
            directory,full,id,app,kind,name)};
    }

    internal static bool IsRegisteredDocument(string path)=>
        SafeAssociation(path);

    // Application opening is explicit. We start the product without
    // passing a live company database shard or proprietary Winman file as a
    // command-line argument: users select/import from inside the product.
    internal static bool TryBuildVendorStart(
        DeskSpaceFolderAppRecognitionV363.Suggestion suggestion,
        Func<string,string?> resolver,out ProcessStartInfo? info)
    {
        info=null;
        if(!IsVendor(suggestion.AppId))return false;
        var exe=resolver(suggestion.AppId);
        if(exe==null||!ValidVendorExe(exe,suggestion.AppId))return false;
        info=new ProcessStartInfo(Path.GetFullPath(exe)){
            UseShellExecute=false,
            WorkingDirectory=Path.GetDirectoryName(exe)??suggestion.Folder
        };
        return true;
    }
}
