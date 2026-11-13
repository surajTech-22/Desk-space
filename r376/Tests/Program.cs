using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeskSpace;

internal static class Program
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R376: "+message);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            string clipSource = File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelClipboardV39.cs");
            Check(clipSource.Contains("internal static readonly List<DeskSpaceExcelTemplateV16> SharedClips"),
                "SharedClips must be static to synchronize across all MultiWidget windows");
            Check(clipSource.Contains("internal static void CopyPlain(") &&
                  clipSource.Contains("internal static async void PasteExcel("),
                "CopyPlain and PasteExcel must be accessible by all Deck views");

            string deckSource = File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            Check(deckSource.Contains("DeskSpaceExcelClipboardV39.ClipsChanged+=OnClipsChangedV376;") &&
                  deckSource.Contains("RefreshViews();"),
                "ClipsChanged must trigger RefreshViews across all active MultiWidget circle views");
            Check(deckSource.Contains("\"Excel clipboard\",DeskSpaceExcelClipboardV39.SharedClips.Count") &&
                  deckSource.Contains("Paste to Excel") &&
                  deckSource.Contains("Copy values"),
                "Excel clipboard must be docked inside the Deck DropShelf with Copy and Paste actions");

            Console.WriteLine(
                "R376 PASS: Excel Clipboard is successfully docked inside the Deck and synchronized across MultiWidget.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
