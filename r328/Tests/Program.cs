using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Application=System.Windows.Application;

internal static class Program
{
    static void Check(bool ok,string why)
    {
        if(!ok)throw new InvalidOperationException("R328 text comparison: "+why);
    }
    static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root)
        where T:DependencyObject
    {
        if(root is T matched)yield return matched;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var x in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return x;
    }
    [STAThread] static int Main()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Window? window=null;
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var type=asm.GetType("DeskSpace.DeskSpaceTextCompareV328",true)!;
            var compare=type.GetMethod("Compare",BindingFlags.NonPublic|BindingFlags.Static)!;
            object Diff(string a,string b)=>compare.Invoke(null,new object?[]{a,b})!;
            int Count(object result,string key)=>(int)result.GetType()
                .GetProperty(key)!.GetValue(result)!;
            var equal=Diff("one\r\ntwo","one\ntwo");
            Check(Count(equal,"Same")==2 && Count(equal,"Added")==0 &&
                Count(equal,"Removed")==0,"CRLF/LF normalization failed");
            var insert=Diff("header\nfooter","header\nnew\nfooter");
            Check(Count(insert,"Added")==1 && Count(insert,"Removed")==0 &&
                Count(insert,"Same")==2,"one new line must not mark every next line changed");
            var remove=Diff("a\nb\nc","a\nc");
            Check(Count(remove,"Removed")==1 && Count(remove,"Same")==2,
                "one removed line must preserve unchanged context");
            var edited=Diff("abc","ABC");
            Check(Count(edited,"Added")==1 && Count(edited,"Removed")==1,
                "case-sensitive edits should show one plus and one minus");
            bool oversized=false;
            try{Diff(new string('x',60001),"");}
            catch(TargetInvocationException e)when(e.InnerException is ArgumentException)
            {oversized=true;}
            Check(oversized,"text larger than 60k was accepted");
            bool tooMany=false;
            try{Diff(string.Join("\n",Enumerable.Repeat("x",501)),"");}
            catch(TargetInvocationException e)when(e.InnerException is ArgumentException)
            {tooMany=true;}
            Check(tooMany,"more than 500 lines were accepted");
            window=(Window)type.GetMethod("Create",
                BindingFlags.NonPublic|BindingFlags.Static)!
                .Invoke(null,new object?[]{null})!;
            window.Show();
            window.UpdateLayout();
            var fields=Find<TextBox>(window).ToList();
            var original=fields.Single(x=>AutomationProperties.GetName(x)=="Original text");
            var updated=fields.Single(x=>AutomationProperties.GetName(x)=="Updated text");
            var output=fields.Single(x=>AutomationProperties.GetName(x)=="Comparison results");
            var buttons=Find<Button>(window).ToList();
            var run=buttons.Single(x=>(string)x.Content=="Compare");
            var copy=buttons.Single(x=>(string)x.Content=="Copy comparison");
            var clear=buttons.Single(x=>(string)x.Content=="Clear all");
            Check(!copy.IsEnabled&&output.IsReadOnly,"copy should be disabled before manual Compare");
            original.Text="keep\nold";
            updated.Text="keep\nnew";
            run.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(output.Text.Contains("+ new")&&output.Text.Contains("- old")
                &&copy.IsEnabled,"manual compare did not show differences");
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(original.Text==""&&updated.Text==""&&output.Text==""
                &&!copy.IsEnabled,"Clear all did not erase in-memory text");
            var actions=asm.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
            var commands=(System.Collections.IEnumerable)actions.GetField("Commands",
                BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
            Check(commands.Cast<object>().Any(x=>
                string.Equals((string?)x.GetType().GetProperty("Id")!.GetValue(x),
                    "textcompare",StringComparison.Ordinal)),
                "comparison feature not exposed in existing Action Palette");
            Console.WriteLine("R328 PASS: real WPF paste/compare/clear; bounded LCS, no passive clipboard reads; Action Palette command available.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{window?.Close();app.Shutdown();}
    }
}
