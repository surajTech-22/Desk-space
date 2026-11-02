using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static class Program
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException("R343 monitor session: "+msg);}
    static System.Collections.Generic.IEnumerable<T> Walk<T>(DependencyObject parent)
        where T:DependencyObject
    {
        if(parent is T item)yield return item;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
            foreach(var child in Walk<T>(VisualTreeHelper.GetChild(parent,i)))yield return child;
    }
    [STAThread] static int Main()
    {
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var existing=asm.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
            var entry=existing.GetNestedType("WindowEntry",F)!;
            var profile=existing.GetNestedType("Profile",F)!;
            var planner=asm.GetType("DeskSpace.DeskSpaceMonitorRestoreV343",true)!;
            var entryCtor=entry.GetConstructors(F).Single();
            var listType=typeof(System.Collections.Generic.List<>).MakeGenericType(entry);
            var entries=(IList)Activator.CreateInstance(listType)!;
            entries.Add(entryCtor.Invoke(new object[]{
                "excel","hash1",0,@"\\.\DISPLAY1",0.10,0.10,0.45,0.50}));
            entries.Add(entryCtor.Invoke(new object[]{
                "explorer","hash2",0,@"\\.\DISPLAY2",0.15,0.22,0.5,0.3}));
            entries.Add(entryCtor.Invoke(new object[]{
                "chrome","hash3",0,@"\\.\DISPLAY1",0.25,0.35,0.3,0.3}));
            var profileCtor=profile.GetConstructors(F).Single();
            object all=profileCtor.Invoke(new object[]{"Accounting",entries});
            var select=planner.GetMethod("Select",F)!;
            var summarize=planner.GetMethod("Describe",F)!;
            var options=planner.GetMethod("Options",F)!;
            object screen1=select.Invoke(null,new object?[]{all,@"\\.\DISPLAY1"})!;
            object screen2=select.Invoke(null,new object?[]{all,@"\\.\DISPLAY2"})!;
            int Count(object p)=((IEnumerable)p.GetType().GetProperty("Windows")!.GetValue(p)!).Cast<object>().Count();
            Check(Count(all)==3&&Count(screen1)==2&&Count(screen2)==1,
                "monitor filter should exclude other display's windows without mutating saved profile");
            Check(Count(select.Invoke(null,new object?[]{all,"MISSING-MONITOR"})!)==0,
                "missing physical monitor must not fall back to all monitors");
            Check(Count(select.Invoke(null,new object?[]{all,"All saved monitors"})!)==3,
                "explicit all-monitors option excluded windows");
            var names=((IEnumerable)options.Invoke(null,new object[]{all})!).Cast<string>().ToArray();
            Check(names.Length==3&&names[0]=="All saved monitors",
                "monitor options must start with all and list each saved monitor once");
            var summary=summarize.Invoke(null,new object[]{all,screen2})!;
            Check((int)summary.GetType().GetProperty("Selected")!.GetValue(summary)!==1 &&
                (int)summary.GetType().GetProperty("Excluded")!.GetValue(summary)!==2,
                "preview didn't disclose excluded windows");
            var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var owner=new Window();
            var win=(Window)existing.GetMethod("Create",F)!
                .Invoke(null,new object[]{owner})!;
            win.Show();win.UpdateLayout();
            Check(Walk<ComboBox>(win).Any(box=>(box.ToolTip as string??"")
                .Contains("saved monitor",StringComparison.OrdinalIgnoreCase)),
                "existing layout window lacks monitor-selection control");
            win.Close();application.Shutdown();
            Console.WriteLine("R343 PASS: scope 3 windows to independent displays; "+
                "missing monitor never silently remapped; selectable WPF monitor preview exists.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
