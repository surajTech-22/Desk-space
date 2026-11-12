using System;
using System.IO;
using System.Reflection;

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
            string deckSource = File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            Check(deckSource.Contains("Paste to Excel · Clipboard & mappings") &&
                  deckSource.Contains("DeskSpaceExcelClipboardV39.ShowActive();"),
                "shipping Deck must contain Excel paste launcher button");
            string contextSource = File.ReadAllText(
                "src/DeskSpace/DeskSpaceSmartContextDeckV280.cs");
            Check(contextSource.Contains("case \"excel_paste\":") &&
                  contextSource.Contains("new Suggestion(\"excel_paste\",\"Paste to Excel\")"),
                "Smart Context Deck must contain excel_paste action");
            Console.WriteLine(
                "R375 PASS: Excel Paste option is successfully connected to the Deck via DropShelf and Smart Context.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}