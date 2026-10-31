using System.Globalization;
using System.Runtime.InteropServices;

namespace DeskSpace;

/// <summary>
/// Optional Excel Desktop COM bridge. Operates only on an existing running Excel instance.
/// Never changes source workbook on capture; paste modifies the user-approved selected range.
/// Requires Microsoft Excel desktop installed and opened in the same user session.
/// </summary>
public static class ExcelInterop
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object ppunk);

    private static dynamic RunningExcel()
    {
        Type? excelType = Type.GetTypeFromProgID("Excel.Application");
        if (excelType is null) throw new InvalidOperationException("Microsoft Excel desktop is not installed.");
        Guid clsid = excelType.GUID;
        try { GetActiveObject(ref clsid, IntPtr.Zero, out object obj); return obj; }
        catch (COMException) { throw new InvalidOperationException("Open the Excel desktop application and select the cells first."); }
    }
    public sealed record ExcelPosition(string Address, int Row, int Col);
    public static ExcelPosition ActivePosition()
    {
        dynamic excel = RunningExcel();
        dynamic cell = excel.ActiveCell ?? throw new InvalidOperationException("Select a cell in Excel first.");
        return new ExcelPosition((string)cell.Address, (int)cell.Row, (int)cell.Column);
    }
    public static (List<List<string>> Cells, int Row, int Column, int Rows, int Columns) Capture(string path)
    {
        dynamic excel = RunningExcel();
        dynamic source = excel.Selection;
        if (source is null) throw new InvalidOperationException("Select a rectangular Excel range first.");
        int rows = (int)source.Rows.Count;
        int columns = (int)source.Columns.Count;
        int rowStart = (int)source.Row;
        int colStart = (int)source.Column;
        if (rows < 1 || columns < 1 || (long)rows * columns > 20000)
            throw new InvalidOperationException("Select at most 20,000 cells, not whole worksheet rows or columns.");
        var cells = new List<List<string>>();
        for (int r = 1; r <= rows; r++)
        {
            var values = new List<string>();
            for (int c = 1; c <= columns; c++)
            {
                // Value2 is used for raw grid content; formulas/styles saved separately in the Excel snapshot.
                object? raw = source.Cells[r, c].Value2;
                values.Add(Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "");
            }
            cells.Add(values);
        }
        dynamic? book = null;
        bool prevAlerts = (bool)excel.DisplayAlerts;
        try
        {
            excel.DisplayAlerts = false;
            book = excel.Workbooks.Add();
            dynamic sheet = book.Worksheets[1];
            dynamic target = sheet.Cells[rowStart, colStart];
            source.Copy(target);
            // Copy dimensions to preserve the selected range's Excel geometry where feasible.
            for (int c = 0; c < columns; c++)
                sheet.Columns[colStart + c].ColumnWidth = source.Columns[c + 1].ColumnWidth;
            for (int r = 0; r < rows; r++)
                sheet.Rows[rowStart + r].RowHeight = source.Rows[r + 1].RowHeight;
            book.SaveAs(path, 51); // xlOpenXMLWorkbook
        }
        finally
        {
            try { if (book is not null) book.Close(false); } catch { }
            try { excel.DisplayAlerts = prevAlerts; } catch { }
            try { excel.CutCopyMode = false; } catch { }
        }
        return (cells, rowStart, colStart, rows, columns);
    }
    public static void PasteCellsAsText(List<List<string>> cells)
    {
        if (cells.Count == 0) throw new InvalidOperationException("The grid is empty.");
        int width = cells.Max(r => r.Count), height = cells.Count;
        if ((long)width * height > 20000) throw new InvalidOperationException("Limit is 20,000 cells per paste.");
        dynamic excel = RunningExcel();
        dynamic destination = excel.ActiveCell ?? throw new InvalidOperationException("Select the target top-left cell in Excel first.");
        dynamic range = destination.Resize(height, width);
        var values = new object[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                values[r, c] = c < cells[r].Count ? cells[r][c] : "";
        // This is a TEXT-only grid paste: preserve leading zeros, =-prefixed strings,
        // and identifiers without evaluating imported OCR/clipboard content as formulas.
        range.NumberFormat = "@";
        range.Value2 = values;
    }

    public static void PasteRich(DataTab tab, string fullPath)
    {
        if (tab.ExcelSnapshot is null || tab.ExcelRows < 1 || tab.ExcelColumns < 1)
            throw new InvalidOperationException("This tab does not contain a captured Excel range.");
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Excel snapshot missing", fullPath);
        dynamic excel = RunningExcel();
        dynamic destination = excel.ActiveCell;
        if (destination is null) throw new InvalidOperationException("Select the destination top-left cell in Excel.");
        dynamic? sourceBook = null;
        try
        {
            sourceBook = excel.Workbooks.Open(fullPath, 0, true); // Read only
            dynamic sourceSheet = sourceBook.Worksheets[1];
            dynamic first = sourceSheet.Cells[tab.ExcelSourceRow, tab.ExcelSourceCol];
            dynamic last = sourceSheet.Cells[tab.ExcelSourceRow + tab.ExcelRows - 1, tab.ExcelSourceCol + tab.ExcelColumns - 1];
            dynamic source = sourceSheet.Range[first, last];
            source.Copy(destination); // Excel-native copy: content/formulas/formats/merges where supported.
            dynamic targetSheet = destination.Worksheet;
            int row = (int)destination.Row, col = (int)destination.Column;
            for (int c = 0; c < tab.ExcelColumns; c++)
                targetSheet.Columns[col + c].ColumnWidth = sourceSheet.Columns[tab.ExcelSourceCol + c].ColumnWidth;
            for (int r = 0; r < tab.ExcelRows; r++)
                targetSheet.Rows[row + r].RowHeight = sourceSheet.Rows[tab.ExcelSourceRow + r].RowHeight;
        }
        finally
        {
            try { if (sourceBook is not null) sourceBook.Close(false); } catch { }
            try { excel.CutCopyMode = false; } catch { }
        }
    }
}
