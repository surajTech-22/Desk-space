using System;
using System.IO;
using DeskSpace;
static class Program
{
  static int assertions;
  static void Check(bool yes,string label)
  {
    assertions++;
    if(!yes)throw new Exception("R146 test FAILED: "+label);
  }
  static int Main()
  {
    var now=DateTime.UtcNow;
    var dir=Path.Combine(Path.GetTempPath(),"DeskSpace_R146_Test_"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
      string path(string name)=>Path.Combine(dir,name);
      File.WriteAllText(path("templates.dpapi"),"ENCRYPTED DATA KEEP");
      File.WriteAllText(path("formats.dpapi"),"ENCRYPTED FORMATS KEEP");
      File.WriteAllText(path("templates.dpapi.previous"),"RECOVERY KEEP");
      File.WriteAllText(path("formats.dpapi.previous"),"RECOVERY KEEP");
      File.WriteAllText(path("report.xlsx"),"WORKBOOK MUST NEVER DELETE");
      File.WriteAllText(path("Before_Excel_FormulaPaste_a.xlsx"),"BACKUP KEEP");
      File.WriteAllText(path("templates.dpapi.tmp"),"ABANDONED");
      File.SetLastWriteTimeUtc(path("templates.dpapi.tmp"),now.AddHours(-26));
      File.WriteAllText(path("formats.dpapi.tmp"),"CURRENT SAVE");
      File.SetLastWriteTimeUtc(path("formats.dpapi.tmp"),now.AddMinutes(-3));
      File.WriteAllText(path("other.dpapi.tmp"),"UNKNOWN SIDE CAR KEEP");
      File.SetLastWriteTimeUtc(path("other.dpapi.tmp"),now.AddDays(-3));
      File.WriteAllText(path("orphan.dpapi.tmp"),"NO CANONICAL FILE KEEP");
      File.SetLastWriteTimeUtc(path("orphan.dpapi.tmp"),now.AddDays(-3));
      int removed=DeskSpaceExcelTempCleanupV146.Clean(dir,now);
      Check(removed==1,"one recognized stale transaction sidecar removed");
      Check(!File.Exists(path("templates.dpapi.tmp")),"stale canonical-associated sidecar removed");
      foreach(var keep in new[]{"templates.dpapi","formats.dpapi",
        "templates.dpapi.previous","formats.dpapi.previous",
        "formats.dpapi.tmp","other.dpapi.tmp","orphan.dpapi.tmp",
        "report.xlsx","Before_Excel_FormulaPaste_a.xlsx"})
        Check(File.Exists(path(keep)),"must retain "+keep);
      Check(DeskSpaceExcelTempCleanupV146.Clean(dir,now)==0,
        "idempotent housekeeping no repeat deletion");
      File.SetLastWriteTimeUtc(path("formats.dpapi.tmp"),now.AddHours(-36));
      Check(DeskSpaceExcelTempCleanupV146.Clean(dir,now)==1,
        "old format sidecar safely reclaimed");
      Check(DeskSpaceExcelTempCleanupV146.Clean(
        Path.Combine(dir,"not-existing"),now)==0,
        "absent directory is safe");
      Console.WriteLine("R146 temp cleanup PASS: "+assertions+
          " safety and retention assertions.");
      return 0;
    }
    catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    finally{Directory.Delete(dir,true);}
  }
}
