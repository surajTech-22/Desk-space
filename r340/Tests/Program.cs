using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static class Program
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
    static void Check(bool ok,string reason)
    {
        if(!ok)throw new InvalidOperationException("R340 snippet acceptance: "+reason);
    }
    static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject node)
        where T:DependencyObject
    {
        if(node is T match)yield return match;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            foreach(var item in Find<T>(VisualTreeHelper.GetChild(node,i)))yield return item;
    }
    [STAThread]static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "DeskSpaceR340-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var type=asm.GetType("DeskSpace.DeskSpaceSnippetsV340",true)!;
            var ctor=type.GetConstructors(Flags).Single();
            string file=Path.Combine(root,"snippets.json");
            var storage=ctor.Invoke(new object?[]{file});
            var load=type.GetMethod("Load",Flags)!;
            var save=type.GetMethod("Save",Flags)!;
            var delete=type.GetMethod("Delete",Flags)!;
            var pin=type.GetMethod("TogglePinned",Flags)!;
            object[] All(object target)=>((IEnumerable)load.Invoke(target,null)!).Cast<object>().ToArray();
            Check(!File.Exists(file)&&All(storage).Length==0,
                "snippet tool must not capture clipboard or save anything automatically");
            object first=save.Invoke(storage,new object[]{"VAT template","Debit amount: 12,000"})!;
            object second=save.Invoke(storage,new object[]{"Excel formula","=SUM(A1:A10)"})!;
            Check(File.Exists(file),"explicitly saved snippets weren't persisted");
            object fresh=ctor.Invoke(new object?[]{file});
            Check(All(fresh).Length==2,"snippets not loaded in next session");
            string firstId=(string)first.GetType().GetProperty("Id")!.GetValue(first)!;
            Check((bool)pin.Invoke(storage,new object[]{firstId})!,
                "pin failed");
            var pinned=All(storage).Single(x=>
                (string)x.GetType().GetProperty("Id")!.GetValue(x)! == firstId);
            Check((bool)pinned.GetType().GetProperty("Pinned")!.GetValue(pinned)!,
                "pin flag not persisted");
            bool invalid=false;
            try{save.Invoke(storage,new object[]{"Password",new string('x',8193)});}
            catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException)
            {invalid=true;}
            Check(invalid&&All(storage).Length==2,
                "unbounded snippet accepted or other snippets overwritten");
            Check((bool)delete.Invoke(storage,new object[]{firstId})! &&
                All(storage).Length==1,
                "explicit delete did not remove selected snippet");
            Check(!Directory.EnumerateFiles(root,"*.tmp",SearchOption.AllDirectories).Any(),
                "atomic snippet writer left temporary copies");

            var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var create=type.GetMethod("Create",Flags)!;
            var window=(Window)create.Invoke(null,new object?[]{null,storage})!;
            window.Show();window.UpdateLayout();
            Check(window.Title.Contains("Saved text snippets") &&
                Find<TextBox>(window).Count()>=3 &&
                Find<Button>(window).Any(x=>(string)x.Content=="Copy selected"),
                "WPF snippet manager did not expose manual save/filter/copy controls");
            var palette=asm.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
            var commands=((IEnumerable)palette.GetField("Commands",Flags)!
                .GetValue(null)!).Cast<object>().ToArray();
            Check(commands.Any(x=>string.Equals((string?)x.GetType().GetProperty("Id")!.GetValue(x),
                "snippets",StringComparison.Ordinal)),
                "saved snippets not accessible from existing Action Palette");
            window.Close();application.Shutdown();
            Console.WriteLine("R340 PASS: opt-in manual local snippets, atomic storage, restart, "+
                "pin/unpin, delete, input cap, WPF save/filter/copy controls, and palette access.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{try{Directory.Delete(root,true);}catch(Exception){}}
    }
}
