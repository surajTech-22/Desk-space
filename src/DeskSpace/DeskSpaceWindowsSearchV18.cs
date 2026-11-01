using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace DeskSpace;

// Windows Search is the fast, read-only query layer for indexed content and
// filenames. File-system crawling remains a separate fallback for unindexed
// accessible drives. This does not bypass protected locations.
internal static class DeskSpaceWindowsSearchV18
{
    public static bool LastQueryAvailable {get;private set;}
    public static List<SearchHit> Query(string query,CancellationToken cancel)
    {
        var output=new List<SearchHit>();
        LastQueryAvailable=false;
        bool content=query.StartsWith("content:",StringComparison.OrdinalIgnoreCase);
        var term=content?query.Substring(8).Trim():query.Trim();
        if(term.Length<2||term.Length>90)return output;
        // ADODB is a built-in Windows COM component; no extra NuGet runtime or
        // installer, and it uses the existing Windows Search service index.
        var type=Type.GetTypeFromProgID("ADODB.Connection");
        if(type==null)return output;
        object? conn=null,rows=null;
        try
        {
            cancel.ThrowIfCancellationRequested();
            conn=Activator.CreateInstance(type);
            if(conn==null)return output;
            dynamic db=conn;
            db.ConnectionTimeout=2;
            db.CommandTimeout=3;
            db.Open("Provider=Search.CollatorDSO.1;Extended Properties='Application=Windows';");
            var literal=term.Replace("'","''").Replace("%","[%]").Replace("_","[_]");
            var clause=content
                ? "FREETEXT(System.Search.Contents, '"+literal+"')"
                : "(System.FileName LIKE '%"+literal+"%' OR "+
                  "System.ItemPathDisplay LIKE '%"+literal+"%')";
            var sql="SELECT TOP 150 System.ItemPathDisplay FROM SYSTEMINDEX "+
                "WHERE scope='file:' AND "+clause;
            rows=db.Execute(sql);
            dynamic reader=rows;
            LastQueryAvailable=true;
            while(!(bool)reader.EOF)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    var path=Convert.ToString(reader.Fields.Item("System.ItemPathDisplay").Value);
                    if(!string.IsNullOrEmpty(path)&&(File.Exists(path)||Directory.Exists(path)))
                        output.Add(new SearchHit{
                            Name=Path.GetFileName(path),OpenTarget=path,Category="Files",
                            Location=path
                        });
                }catch(Exception){}
                reader.MoveNext();
            }
        }
        catch(OperationCanceledException){throw;}
        catch(Exception){LastQueryAvailable=false;}
        finally
        {
            if(rows!=null)
            {
                try{((dynamic)rows).Close();}catch(Exception){}
                if(Marshal.IsComObject(rows))try{Marshal.FinalReleaseComObject(rows);}catch(Exception){}
            }
            if(conn!=null)
            {
                try{((dynamic)conn).Close();}catch(Exception){}
                if(Marshal.IsComObject(conn))try{Marshal.FinalReleaseComObject(conn);}catch(Exception){}
            }
        }
        return output;
    }
}
