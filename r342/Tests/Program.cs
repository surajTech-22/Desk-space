using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using DeskSpace;

internal static class Program
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    static void Check(bool ok,string why)
    {
        if(!ok)throw new InvalidOperationException("R342 unified Search result menu: "+why);
    }
    [STAThread]static int Main()
    {
        var folder=Path.Combine(Path.GetTempPath(),
            "DeskSpaceR342-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"invoice2026.pdf");
            File.WriteAllText(file,"read-only fixture");
            var dll=typeof(WeatherReading).Assembly;
            var type=dll.GetType("DeskSpace.DeskSpaceUnifiedResultMenuV342",true)!;
            var available=type.GetMethod("Available",Flags)!;
            var create=type.GetMethod("Create",Flags)!;
            string[] Choices(SearchHit hit,bool pinMode=false)
            {
                var r=(IEnumerable)available.Invoke(null,new object?[]{hit,pinMode})!;
                return r.Cast<object>().Select(x=>(string)x.GetType()
                    .GetProperty("Id")!.GetValue(x)!).ToArray();
            }
            var hit=new SearchHit{Category="Files",Name="invoice2026.pdf",
                OpenTarget=file,Location=folder};
            Check(Choices(hit).SequenceEqual(new[]{"open","folder","path","file"}),
                "file menu must provide only safe actions");
            Check(Choices(hit,true).Length==0,
                "app-pin mode offered pin for a file");
            var folderHit=new SearchHit{Category="Files",Name="test folder",
                OpenTarget=folder};
            Check(Choices(folderHit).SequenceEqual(new[]{"open","folder","path"}),
                "folder menu offered invalid copy-file or pin");
            var appHit=new SearchHit{Category="Applications",Name="App",
                OpenTarget=file};
            Check(Choices(appHit).Contains("pin") &&
                Choices(appHit,true).SequenceEqual(new[]{"pin"}),
                "application pin mode missing or non-pin operations shown");
            var note=new SearchHit{Category="Notes",Name="note",
                OpenTarget=""};
            Check(Choices(note).SequenceEqual(new[]{"open"}),
                "notes must not expose raw file actions");
            var stale=new SearchHit{Category="Files",Name="missing.pdf",
                OpenTarget=Path.Combine(folder,"missing.pdf")};
            Check(Choices(stale).Length==0,
                "missing file should not offer actions");

            int calls=0;
            Action<string,SearchHit> invoke=(operation,result)=>{
                Check(operation=="path" && ReferenceEquals(result,hit),
                    "context menu dispatched wrong command or hit");
                calls++;
            };
            var menu=(ContextMenu)create.Invoke(null,new object?[]{hit,invoke,false})!;
            Check(menu.Items.Count==4,"real WPF context menu omitted actions");
            var item=menu.Items.Cast<MenuItem>().Single(x=>(string)x.Header=="Copy path");
            item.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Check(calls==1,"menu did not invoke original search hit");
            File.Delete(file);
            item.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Check(calls==1,
                "menu performed operation after file was removed");
            Check(Choices(hit).Length==0,
                "deleted files must be revalidated when opened");
            Check(File.Exists(Path.Combine(folder,"nothing"))==false,
                "search context menu caused filesystem mutation");
            Console.WriteLine("R342 PASS: same non-destructive menu policy for both Search UIs, "+
                "real WPF menu action, folder/app/note restrictions, missing files and click-time revalidation.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{try{Directory.Delete(folder,true);}catch(Exception){}}
    }
}
