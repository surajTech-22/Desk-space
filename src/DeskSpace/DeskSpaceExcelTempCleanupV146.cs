using System;
using System.IO;

namespace DeskSpace;

// Deletes ONLY abandoned, app-owned encrypted mapping transaction sidecars.
// Never deletes Excel workbooks, *.xlsx files, recovery snapshots, or
// *.dpapi.previous data. A live save writes a fresh .tmp and therefore does
// not meet the age threshold.
internal static class DeskSpaceExcelTempCleanupV146
{
    private static readonly object Sync=new();
    private static DateTime nextCheckUtc=DateTime.MinValue;
    private static readonly string[] Sidecars={
        "templates.dpapi.tmp","formats.dpapi.tmp"
    };
    internal static int Clean(string directory,DateTime utcNow)
    {
        if(!Directory.Exists(directory))return 0;
        int deleted=0;
        foreach(string name in Sidecars)
        {
            string file=Path.Combine(directory,name);
            string canonical=file.Substring(0,file.Length-4);
            if(!File.Exists(file) || !File.Exists(canonical))continue;
            try
            {
                var age=utcNow-File.GetLastWriteTimeUtc(file);
                if(age<TimeSpan.FromHours(24))continue;
                // Exclusive open prevents removal while another DeskSpace
                // process has the pending transaction file open.
                using(var handle=new FileStream(file,FileMode.Open,
                    FileAccess.ReadWrite,FileShare.None))
                {
                    if(utcNow-File.GetLastWriteTimeUtc(file)<
                       TimeSpan.FromHours(24))continue;
                }
                File.Delete(file);
                deleted++;
            }
            catch(IOException){} // save is active, or locked
            catch(UnauthorizedAccessException){} // never force a deletion
        }
        return deleted;
    }

    internal static void CleanIfDue()
    {
        var now=DateTime.UtcNow;
        lock(Sync)
        {
            if(now<nextCheckUtc)return;
            nextCheckUtc=now.AddHours(1);
        }
        string mappingFolder=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelMappings");
        try{Clean(mappingFolder,now);}
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
}
