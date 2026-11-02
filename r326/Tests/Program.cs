using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static;
    static void Assert(bool ok,string why)
    {
        if(!ok)throw new InvalidOperationException("R326 advanced search regression: "+why);
    }

    [STAThread] static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"deskspace-filter-r326-"+
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var helper=asm.GetType("DeskSpace.DeskSpaceSearchFiltersV326",true)!;
            var parse=helper.GetMethod("Parse",Static)!;
            object Criteria(string text)=>parse.Invoke(null,new object?[]{text})!;
            string Term(object c)=>(string)c.GetType().GetField("Term",
                BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(c)!;
            bool Matches(object c,DeskSpace.SearchHit h)=>(bool)c.GetType()
                .GetMethod("Matches",BindingFlags.NonPublic|BindingFlags.Instance)!
                .Invoke(c,new object[]{h})!;
            var accounting=Path.Combine(root,"Accounting Files");
            var archive=Path.Combine(root,"Archive");
            Directory.CreateDirectory(accounting);
            Directory.CreateDirectory(archive);
            string pdf=Path.Combine(accounting,"Invoice-2026.pdf");
            string xlsx=Path.Combine(accounting,"Invoice-2026.xlsx");
            string large=Path.Combine(archive,"Invoice-hidden.xlsm");
            string png=Path.Combine(archive,"Holiday.png");
            File.WriteAllText(pdf,"pdf fixture");
            File.WriteAllText(xlsx,"xlsx fixture");
            File.WriteAllBytes(large,new byte[2*1024*1024]);
            File.WriteAllBytes(png,new byte[200]);
            File.SetLastWriteTimeUtc(pdf,new DateTime(2026,05,15,12,0,0,DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(xlsx,new DateTime(2025,05,15,12,0,0,DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(large,new DateTime(2026,10,01,12,0,0,DateTimeKind.Utc));
            File.SetAttributes(large,FileAttributes.Hidden);
            DeskSpace.SearchHit Hit(string p)=>new(){
                Category="Files",Name=Path.GetFileName(p),
                Location=p,OpenTarget=p
            };
            var bad=new DeskSpace.SearchHit{
                Category="Applications",Name="Invoice-2026.pdf",
                OpenTarget=pdf,Location=pdf
            };
            var criteria=Criteria("invoice ext:pdf type:document after:2026-01-01 "+
                "before:2026-12-31 max:1mb");
            Assert(Term(criteria)=="invoice","filename term should exclude valid facets");
            Assert(Matches(criteria,Hit(pdf)),
                "matched PDF with date+size+type filter rejected");
            Assert(!Matches(criteria,Hit(xlsx))&&!Matches(criteria,Hit(large))&&
                !Matches(criteria,bad),"nonmatching files, apps should never fill result quota");
            var pathCriteria=Criteria("invoice type:sheet in:\""+accounting+"\"");
            Assert(Term(pathCriteria)=="invoice"&&Matches(pathCriteria,Hit(xlsx))&&
                !Matches(pathCriteria,Hit(large)),
                "quoted path spaces and spreadsheet group failed");
            var min=Criteria("min:1mb type:sheet");
            Assert(Matches(min,Hit(large))&&!Matches(min,Hit(xlsx)),
                "actual file size, not filename, determines min: result");
            var folder=Criteria("type:folder in:Accounting");
            Assert(Matches(folder,Hit(accounting))&&!Matches(folder,Hit(pdf)),
                "folder facet must not include files");
            var images=Criteria("type:image");
            Assert(Matches(images,Hit(png))&&!Matches(images,Hit(pdf)),
                "image facet did not isolate common file formats");
            var extensions=Criteria("ext:PDF,docx");
            Assert(Matches(extensions,Hit(pdf))&&!Matches(extensions,Hit(png)),
                "case-insensitive comma-separated extension filter failed");
            var invalid=Criteria("invoice after:invalid min:bad");
            Assert(Term(invalid).Contains("after:invalid")&&
                Term(invalid).Contains("min:bad"),"invalid filters silently erased user text");
            var plain=Criteria("invoice 2026");
            Assert(Term(plain)=="invoice 2026"&&Matches(plain,bad),
                "ordinary unfiltered application search changed");
            Assert(Term(Criteria("ext:pdf"))=="",
                "facet-only file queries must be supported");
            Console.WriteLine("R326 PASS: shared file filters, type/extension, Unicode-safe quoted directory, filesize, UTC date, hidden XLSM, invalid filter fallback, and unfiltered apps.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            try{
                var hidden=Directory.GetFiles(root,"*",SearchOption.AllDirectories);
                foreach(var f in hidden)File.SetAttributes(f,FileAttributes.Normal);
                Directory.Delete(root,true);
            }catch(Exception){}
        }
    }
}
