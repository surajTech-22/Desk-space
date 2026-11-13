using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DeskSpace;

internal static class Program
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("R377 TEST FAIL: " + message);
    }

    private static IList GetSharedClips(Assembly asm)
    {
        var clipType = asm.GetType("DeskSpace.DeskSpaceExcelClipboardV39", true)!;
        var field = clipType.GetField("SharedClips", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (IList)field.GetValue(null)!;
    }

    private static object CreateSampleClip(Assembly asm, int baseRow, int baseCol, string sheet, string addr)
    {
        var tmplType = asm.GetType("DeskSpace.DeskSpaceExcelTemplateV16", true)!;
        var cellType = asm.GetType("DeskSpace.DeskSpaceExcelCellV16", true)!;
        var tmpl = Activator.CreateInstance(tmplType)!;
        tmplType.GetProperty("Name")!.SetValue(tmpl, "Copied cells");
        tmplType.GetProperty("SourceWorkbook")!.SetValue(tmpl, "SalesBook.xlsx");
        tmplType.GetProperty("SourceSheet")!.SetValue(tmpl, sheet);
        tmplType.GetProperty("SourceAddress")!.SetValue(tmpl, addr);
        tmplType.GetProperty("SourceRow")!.SetValue(tmpl, baseRow);
        tmplType.GetProperty("SourceColumn")!.SetValue(tmpl, baseCol);
        tmplType.GetProperty("RowCount")!.SetValue(tmpl, 3);
        tmplType.GetProperty("ColumnCount")!.SetValue(tmpl, 3);
        tmplType.GetProperty("CoordinatesVerified")!.SetValue(tmpl, true);

        var cells = (IList)tmplType.GetProperty("Cells")!.GetValue(tmpl)!;
        void AddCell(int rOff, int cOff, string raw, string disp, string kind, string role, string formula = "")
        {
            var c = Activator.CreateInstance(cellType)!;
            cellType.GetProperty("RowOffset")!.SetValue(c, rOff);
            cellType.GetProperty("ColumnOffset")!.SetValue(c, cOff);
            cellType.GetProperty("RawValue")!.SetValue(c, raw);
            cellType.GetProperty("Display")!.SetValue(c, disp);
            cellType.GetProperty("ValueKind")!.SetValue(c, kind);
            cellType.GetProperty("Role")!.SetValue(c, role);
            cellType.GetProperty("FormulaR1C1")!.SetValue(c, formula);
            cellType.GetProperty("AddressVerified")!.SetValue(c, true);
            cells.Add(c);
        }
        // Notice Row 0 uses Role="Reference" just like ExcelBridge.Capture() creates for headers!
        AddCell(0, 0, "Item", "Item", "text", "Reference");
        AddCell(0, 1, "Qty", "Qty", "text", "Reference");
        AddCell(0, 2, "Total", "Total", "text", "Reference");
        AddCell(1, 0, "Widget Pro", "Widget Pro", "text", "Value");
        AddCell(1, 1, "5", "5", "number", "Value");
        AddCell(1, 2, "125.5", "$125.50", "formula", "Formula", "=RC[-1]*25.1");
        AddCell(2, 0, "Cable Pack", "Cable Pack", "text", "Value");
        AddCell(2, 1, "2", "2", "number", "Value");
        AddCell(2, 2, "40", "$40.00", "number", "Value");
        return tmpl;
    }

    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        if (root is FrameworkElement fe)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(fe))
            {
                if (child is DependencyObject dep)
                    foreach (var nested in FindVisual<T>(dep))
                        yield return nested;
            }
        }
    }

    private static void TestStrictInformationPasteV125Preserved(Assembly asm)
    {
        var bridge = asm.GetType("DeskSpace.DeskSpaceExcelBridgeV16", true)!;
        var previewMethod = bridge.GetMethod("Preview", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var sample = CreateSampleClip(asm, 1, 1, "Sheet1", "$A$1:$C$3");
        bool threw = false;
        try
        {
            previewMethod.Invoke(null, new object[] { sample, true, false });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is InvalidOperationException ioe)
        {
            threw = ioe.Message.Contains("Selected-cell fallback is disabled for data.", StringComparison.Ordinal);
        }
        Check(threw, "R125 strict header-match Preview(entry, true, false) must stay protected");
        Check(bridge.GetMethod("PreviewClipboardValues", BindingFlags.Static | BindingFlags.Public) != null &&
              bridge.GetMethod("ApplyClipboardValues", BindingFlags.Static | BindingFlags.Public) != null,
            "Explicit clipboard COM paste helpers PreviewClipboardValues/ApplyClipboardValues must exist");
    }

    private static void TestClipboardMatrixAndSnippet(Assembly asm)
    {
        var clipType = asm.GetType("DeskSpace.DeskSpaceExcelClipboardV39", true)!;
        var colLetter = clipType.GetMethod("ColumnLetter", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var snippetMethod = clipType.GetMethod("ClipSummarySnippet", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var matrixMethod = clipType.GetMethod("BuildDisplayMatrix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

        Check((string)colLetter.Invoke(null, new object[] { 1 })! == "A", "Column 1 must be A");
        Check((string)colLetter.Invoke(null, new object[] { 3 })! == "C", "Column 3 must be C");
        Check((string)colLetter.Invoke(null, new object[] { 27 })! == "AA", "Column 27 must be AA");

        var clip = CreateSampleClip(asm, 644, 3, "Sheet1", "$C$644:$E$646");
        string snippet = (string)snippetMethod.Invoke(null, new[] { clip })!;
        Check(snippet.Contains("Item") && snippet.Contains("Qty") && snippet.Contains("Total"),
            "ClipSummarySnippet must display actual copied values instead of metadata only, got: " + snippet);

        var matrix = (string[,])matrixMethod.Invoke(null, new object[] { clip, 10, 10 })!;
        Check(matrix[1, 0] == "Widget Pro" && matrix[1, 2] == "$125.50",
            "BuildDisplayMatrix must preserve row/col positions and formatted Display text");
    }

    private static void TestExcelDeckPreviewPopup(Assembly asm)
    {
        var shared = GetSharedClips(asm);
        shared.Clear();
        var clip = CreateSampleClip(asm, 644, 3, "Sheet1", "$C$644:$E$646");
        shared.Add(clip);

        var host = new Window
        {
            Width = 140, Height = 140, Left = -2000, Top = -2000,
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false
        };
        var row = new Border { Width = 240, Height = 44 };
        var deck = new Border { Child = row, Width = 260, Height = 180 };
        var btn = new Button { Content = "◉" };
        host.Content = deck;
        host.Show();
        try
        {
            var previewType = asm.GetType("DeskSpace.DeskSpaceDeckPreviewV99", true)!;
            var previewObj = Activator.CreateInstance(
                previewType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new object[] { deck },
                null)!;

            var bindExcel = previewType.GetMethod("BindExcel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var showExcel = previewType.GetMethod("ShowExcel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var isExcelOpen = previewType.GetMethod("IsExcelOpen", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var closeIfMissing = previewType.GetMethod("CloseIfMissing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var popupField = previewType.GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Func<bool> hoverTrue = () => true;
            bindExcel.Invoke(previewObj, new object[] { btn, row, clip, hoverTrue });
            showExcel.Invoke(previewObj, new object[] { clip, row, false });

            Check((bool)isExcelOpen.Invoke(previewObj, new[] { clip })!,
                "ShowExcel must open the DeskSpaceDeckPreviewV99 preview popup for Excel clips");

            var popup = (Popup)popupField.GetValue(previewObj)!;
            Check(popup.IsOpen && popup.Child is Border, "Excel preview Popup.Child must be built");
            var texts = FindVisual<TextBlock>(popup.Child!).Select(t => t.Text).ToList();
            Check(texts.Contains("C") && texts.Contains("D") && texts.Contains("E"),
                "Excel preview table must show real column letters matching $C$644:$E$646");
            Check(texts.Contains("644") && texts.Contains("645"),
                "Excel preview table must show real row numbers starting at 644");
            Check(texts.Contains("Widget Pro") && texts.Contains("$125.50") && texts.Contains("Cable Pack"),
                "Excel preview table must show copied cell values inside the popup");

            // Render cycles call CloseIfMissing(): must NOT prematurely close active Excel clip preview.
            closeIfMissing.Invoke(previewObj, null);
            Check((bool)isExcelOpen.Invoke(previewObj, new[] { clip })!,
                "CloseIfMissing must keep active Excel clip preview open while still in SharedClips");

            // Removing the clip from SharedClips and calling CloseIfMissing() must close cleanly.
            shared.Remove(clip);
            closeIfMissing.Invoke(previewObj, null);
            Check(!(bool)isExcelOpen.Invoke(previewObj, new[] { clip })! && !popup.IsOpen,
                "CloseIfMissing must close preview once clip is removed from SharedClips");

            ((IDisposable)previewObj).Dispose();
        }
        finally
        {
            shared.Clear();
            host.Close();
        }
    }

    private static void TestDeckAndSourceInvariants()
    {
        string deckSource = File.ReadAllText("DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs").Replace("\r\n", "\n");
        Check(!deckSource.Contains("Paste to Excel · Clipboard & mappings"),
            "Obsolete non-functional R375 placeholder button must be removed");
        Check(deckSource.Contains("ShowDeckRequested+=OnShowDeckRequestedV377") &&
              deckSource.Contains("DeskSpaceExcelClipboardV39.PasteExcel(latestClip)") &&
              deckSource.Contains("DeskSpaceExcelClipboardV39.CaptureSelectionNow()") &&
              deckSource.Contains("DeskSpaceFeatureNavigationV200.OpenExcelAsync(host)") &&
              deckSource.Contains("deckPreview?.BindExcel(previewClip,row,clip,()=>previewOnHover)") &&
              deckSource.Contains("ClipSummarySnippet(clip)"),
            "DropShelf must connect real Excel paste actions and hover table preview");
        Check(deckSource.Contains("held.Count+detected.Count+runningApps.Count+\n            excelMatchesV122.Count+DeskSpaceExcelClipboardV39.SharedClips.Count>0"),
            "DropShelf Visible() must account for SharedClips count");
        Check(Regex.Matches(deckSource, @"DeckHeaderButton\(").Count == 4,
            "R308 invariant broken: DeckHeaderButton( count must remain exactly 4");

        string widgetSource = File.ReadAllText("DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs");
        Check(widgetSource.Contains("previousFullscreen=FullscreenDetector.IsMonitorFullscreen(this);") &&
              !widgetSource.Contains("previousFullscreen=FullscreenDetector.IsOtherFullscreen();"),
            "WidgetHost Tick must use per-monitor IsMonitorFullscreen(this), not global GetForegroundWindow");
        Check(widgetSource.Contains("PrepareEdgeDock(") &&
              widgetSource.Contains("PlaceEdgeHandleNative("),
            "DockToEdge must use DPI-aware monitor-local edge handle placement");
    }

    private static void TestMonitorAwareFullscreenLogic(Assembly asm)
    {
        // Primary: 1920x1080 with 48px taskbar (work 0..1032)
        // Secondary: 1920..4480 x 0..1440 WITHOUT taskbar (work == monitor)
        const uint WS_CAPTION = FullscreenDetector.WS_CAPTION;
        const uint WS_MAXIMIZE = FullscreenDetector.WS_MAXIMIZE;
        const uint WS_EX_TRANSPARENT = FullscreenDetector.WS_EX_TRANSPARENT;

        // 1. Fullscreen Chrome video playing on Monitor 1 (0,0..1920,1080)
        Check(FullscreenDetector.CoversMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1080, 0, 0, 1920, 1032, 0x14000000u, 0, "Chrome_WidgetWin_1"),
            "Fullscreen video on Monitor 1 must hide Monitor 1 widget");
        Check(!FullscreenDetector.CoversMonitor(0, 0, 1920, 1080, 1920, 0, 4480, 1440, 1920, 0, 4480, 1440, 0x14000000u, 0, "Chrome_WidgetWin_1"),
            "Fullscreen video on Monitor 1 must NEVER hide Monitor 2 widget");

        // 2. Fullscreen VLC playing on Monitor 2 (1920,0..4480,1440)
        Check(FullscreenDetector.CoversMonitor(1920, 0, 4480, 1440, 1920, 0, 4480, 1440, 1920, 0, 4480, 1440, 0x14000000u, 0, "Qt652QWindowIcon"),
            "Fullscreen video on Monitor 2 must hide Monitor 2 widget");
        Check(!FullscreenDetector.CoversMonitor(1920, 0, 4480, 1440, 0, 0, 1920, 1080, 0, 0, 1920, 1032, 0x14000000u, 0, "Qt652QWindowIcon"),
            "Fullscreen video on Monitor 2 must NEVER hide Monitor 1 widget");

        // 3. Regular maximized browser/Explorer on Monitor 2 WITHOUT taskbar must NOT falsely hide Monitor 2!
        Check(!FullscreenDetector.CoversMonitor(1920, 0, 4480, 1440, 1920, 0, 4480, 1440, 1920, 0, 4480, 1440, WS_CAPTION | WS_MAXIMIZE, 0, "Chrome_WidgetWin_1"),
            "Normal maximized Chrome on taskbar-less secondary monitor must NOT be treated as fullscreen");

        // 4. Regular maximized browser on Monitor 1 WITH taskbar stops at work.Bottom (1032) -> must NOT hide Monitor 1!
        Check(!FullscreenDetector.CoversMonitor(0, 0, 1920, 1032, 0, 0, 1920, 1080, 0, 0, 1920, 1032, WS_CAPTION | WS_MAXIMIZE, 0, "Chrome_WidgetWin_1"),
            "Maximized window respecting taskbar must NOT trigger fullscreen hide");

        // 5. UWP Netflix / Movies & TV (ApplicationFrameWindow) fullscreen on Monitor 2 SHOULD trigger
        Check(FullscreenDetector.CoversMonitor(1920, 0, 4480, 1440, 1920, 0, 4480, 1440, 1920, 0, 4480, 1440, WS_CAPTION | WS_MAXIMIZE, 0, "ApplicationFrameWindow"),
            "UWP fullscreen player (ApplicationFrameWindow) must trigger fullscreen hide on its own monitor");

        // 6. Shell windows and transparent overlays must never hide widgets
        foreach (string shell in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            Check(!FullscreenDetector.CoversMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1080, 0, 0, 1920, 1032, 0, 0, shell),
                "Shell window " + shell + " must never trigger fullscreen");
        Check(!FullscreenDetector.CoversMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1080, 0, 0, 1920, 1032, 0, WS_EX_TRANSPARENT, "DiscordOverlay"),
            "Transparent click-through overlay must never trigger fullscreen");

        // 7. Verify DPI-safe edge dock placement across primary and secondary monitors
        var prepEdge = typeof(FullscreenDetector).GetMethod("PrepareEdgeDock",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var dummyEdge1 = new Window { Width = 30, Height = 80, ShowInTaskbar = false, ShowActivated = false };
        var dummyEdge2 = new Window { Width = 30, Height = 80, ShowInTaskbar = false, ShowActivated = false };
        var hostSim1 = new Window { Left = 1700, Top = 400, Width = 150, Height = 150, ShowInTaskbar = false };
        var hostSim2 = new Window { Left = 2000, Top = 300, Width = 150, Height = 150, ShowInTaskbar = false };
        var mon1Rect = new System.Drawing.Rectangle(0, 0, 1920, 1032);
        var mon2Rect = new System.Drawing.Rectangle(1920, 0, 2560, 1440);
        object?[] args1 = { IntPtr.Zero, hostSim1, mon1Rect, dummyEdge1, false, 0 };
        prepEdge.Invoke(null, args1);
        Check((bool)args1[4]! == false && dummyEdge1.Left >= 1880 && dummyEdge1.Left <= 1920,
            "Widget near right edge of Monitor 1 must dock its edge tab onto Monitor 1 right edge, got Left=" + dummyEdge1.Left);
        object?[] args2 = { IntPtr.Zero, hostSim2, mon2Rect, dummyEdge2, false, 0 };
        prepEdge.Invoke(null, args2);
        Check((bool)args2[4]! == true && Math.Abs(dummyEdge2.Left - 1920) <= 2,
            "Widget near left seam of Monitor 2 must dock its edge tab onto Monitor 2 left edge (1920), got Left=" + dummyEdge2.Left);

        // 8. Live WPF window checks for ShowDeckRequested event & live IsMonitorFullscreen
        var clipType = asm.GetType("DeskSpace.DeskSpaceExcelClipboardV39", true)!;
        var ev = clipType.GetEvent("ShowDeckRequested", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        bool wakeFired = false;
        Action wakeHandler = () => wakeFired = true;
        ev.AddEventHandler(null, wakeHandler);
        try
        {
            clipType.GetMethod("ShowActive", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null);
            Check(wakeFired, "ShowActive must raise ShowDeckRequested so hidden/minimized Decks wake cleanly");
        }
        finally
        {
            ev.RemoveEventHandler(null, wakeHandler);
        }

        // 9. Per-widget live IsMonitorFullscreen call safety on desktop runner
        var w1 = new Window { Width = 140, Height = 140, Left = -2000, Top = -2000, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
        var w2 = new Window { Width = 140, Height = 140, Left = -2000, Top = -2000, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
        w1.Show();
        w2.Show();
        try
        {
            bool fs1 = FullscreenDetector.IsMonitorFullscreen(w1);
            bool fs2 = FullscreenDetector.IsMonitorFullscreen(w2);
            Check(!fs1 && !fs2, "Live desktop runner without fullscreen video must report false");
        }
        finally
        {
            w1.Close();
            w2.Close();
        }
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            var asm = typeof(FullscreenDetector).Assembly;
            TestStrictInformationPasteV125Preserved(asm);
            TestClipboardMatrixAndSnippet(asm);
            TestExcelDeckPreviewPopup(asm);
            TestDeckAndSourceInvariants();
            TestMonitorAwareFullscreenLogic(asm);
            Console.WriteLine("R377 PASS: Excel paste works safely, Excel clipboard hover table renders values, and Multi-Monitor fullscreen detection is monitor-aware.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
