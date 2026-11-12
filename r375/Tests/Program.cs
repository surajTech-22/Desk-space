using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeskSpace;

internal static class Program
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R375: "+message);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            var actions = DeskSpaceSmartContextDeckV280.Actions("Excel");
            Check(actions.Any(a=>a.Id=="excel_paste" && a.Label=="Paste to Excel"),
                "Smart Context Deck must offer 'Paste to Excel' when Excel is active");
            string contextSource = File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceSmartContextDeckV280.cs");
            Check(contextSource.Contains("case \"excel_paste\":") &&
                  contextSource.Contains("DeskSpaceExcelClipboardV39.ShowActive();"),
                "excel_paste context action must invoke DeskSpaceExcelClipboardV39.ShowActive()");
            string deckSource = File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            Check(deckSource.Contains("DeskSpaceExcelClipboardV39.ShowActive()") ||
                  deckSource.Contains("ReviewExcelSuggestionV122"),
                "Deck must maintain Excel paste connections");
            Console.WriteLine(
                "R375 PASS: Excel Paste option is successfully connected to the Deck via Smart Context and Deck actions.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}