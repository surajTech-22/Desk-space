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
    static void Check(bool ok,string label)
    {if(!ok)throw new InvalidOperationException("R345 Excel preflight: "+label);}
    static System.Collections.Generic.IEnumerable<T> Walk<T>(DependencyObject obj)
        where T:DependencyObject
    {
        if(obj is T v)yield return v;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++)
            foreach(var e in Walk<T>(VisualTreeHelper.GetChild(obj,i)))yield return e;
    }
    [STAThread] static int Main()
    {
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var preflight=asm.GetType("DeskSpace.DeskSpaceExcelPreflightV345",true)!;
            var proposalType=asm.GetType("DeskSpace.DeskSpaceExcelProposalV16",true)!;
            var cellType=asm.GetType("DeskSpace.DeskSpaceExcelChangeV16",true)!;
            var proposal=Activator.CreateInstance(proposalType,true)!;
            void Set(object obj,string key,object value)=>obj.GetType()
                .GetProperty(key,F)!.SetValue(obj,value);
            Set(proposal,"Verified",true);
            Set(proposal,"StartRow",5);Set(proposal,"StartColumn",3);
            Set(proposal,"SheetName","Purchase Data");
            Set(proposal,"WorkbookFullName",@"C:\Testing\Workbooks\Invoice.xlsx");
            Set(proposal,"UsedReferenceSignatureV120",true);
            var change=Activator.CreateInstance(cellType,true)!;
            Set(change,"Row",5);Set(change,"Column",3);
            Set(change,"CellAddress","C5");
            Set(change,"Previous","");Set(change,"PreviousRaw","");
            Set(change,"NewValue","1200");
            Set(change,"Decision","Ready");
            var changes=(IList)proposalType.GetProperty("Changes",F)!.GetValue(proposal)!;
            changes.Add(change);
            var proof=preflight.GetMethod("Proof",F)!;
            string Digest()=>(string)proof.Invoke(null,new object[]{proposal})!;
            string initial=Digest();
            Check(initial.Length==64&&Digest()==initial,
                "unchanged exact Excel proposal not stable under hashing");
            string report=(string)preflight.GetMethod("Describe",F)!
                .Invoke(null,new object[]{proposal})!;
            Check(report.Contains("Purchase Data")&&
                report.Contains("WRITE")&&report.Contains("C5")&&
                report.Contains("backup",StringComparison.OrdinalIgnoreCase),
                "read-only preflight lacks target, write count or backup expectations");
            Set(change,"NewValue","9999");
            Check(Digest()!=initial,
                "changing a proposed Excel value did not invalidate the user approval");
            Set(change,"NewValue","1200");
            Set(change,"PreviousRaw","unexpected");
            Check(Digest()!=initial,
                "changing live original raw value did not invalidate approval");
            Set(change,"PreviousRaw","");
            Set(proposal,"SheetName","Other Sheet");
            Check(Digest()!=initial,
                "changing active workbook sheet did not invalidate approval");
            Set(proposal,"SheetName","Purchase Data");
            Set(proposal,"Verified",false);
            bool rejected=false;
            try{Digest();}
            catch(TargetInvocationException ex) when(ex.InnerException is InvalidOperationException)
            {rejected=true;}
            Check(rejected,"unverified Excel proposal was allowed to be approved");
            Set(proposal,"Verified",true);
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var window=(Window)preflight.GetMethod("Create",F)!
                .Invoke(null,new object?[]{null,proposal})!;
            window.Show();window.UpdateLayout();
            Check(Walk<Button>(window).Any(x=>
                string.Equals(x.Content as string,"Approve this exact preview",
                    StringComparison.Ordinal)) &&
                Walk<TextBox>(window).Any(x=>x.IsReadOnly &&
                    x.Text.Contains("Purchase Data")),
                "native WPF exact Excel preview and approve controls missing");
            window.Close();app.Shutdown();
            Console.WriteLine("R345 PASS: immutable Excel target+value digest, "+
                "unverified rejection, read-only per-cell preview, "+
                "native WPF explicit approval control; no Excel COM or workbook writes.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
