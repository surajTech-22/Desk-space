using System.Text;

namespace DeskSpace;

/// <summary>Excel-style tab-separated grid. Quoted fields support embedded tabs, newlines and doubled quotes.</summary>
public static class MatrixCodec
{
    public static List<List<string>> Parse(string text)
    {
        var output = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (quoted) quoted = false;
                else if (cell.Length == 0) quoted = true;
                else cell.Append(ch);
                continue;
            }
            if (!quoted && ch == '\t') { row.Add(cell.ToString()); cell.Clear(); continue; }
            if (!quoted && (ch == '\n' || ch == '\r'))
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); output.Add(row); row = new List<string>();
                continue;
            }
            cell.Append(ch);
        }
        // A single trailing Excel newline is a terminator, not a new all-empty row.
        if (row.Count != 0 || cell.Length > 0 || (text.Length > 0 && text[^1] == '\t'))
        { row.Add(cell.ToString()); output.Add(row); }
        return output;
    }
    public static string Serialize(IReadOnlyList<IReadOnlyList<string>> matrix)
    {
        return string.Join("\r\n", matrix.Select(row => string.Join("\t", row.Select(Escape))));
    }
    public static string Serialize(List<List<string>> matrix) =>
        Serialize(matrix.Select(r => (IReadOnlyList<string>)r).ToList());
    private static string Escape(string? input)
    {
        var s = input ?? "";
        return s.Contains('\t') || s.Contains('\n') || s.Contains('\r') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
    public static void Paste(List<List<string>> destination, List<List<string>> cells, int rowOffset, int colOffset)
    {
        if (rowOffset < 0 || colOffset < 0) throw new ArgumentOutOfRangeException();
        int width = cells.Count == 0 ? 0 : cells.Max(r => r.Count);
        if (destination.Count + cells.Count > 100000 || colOffset + width > 10000)
            throw new ArgumentException("Grid is too large.");
        while (destination.Count < rowOffset + cells.Count) destination.Add(new List<string>());
        for (int r = 0; r < cells.Count; r++)
        {
            var target = destination[rowOffset + r];
            while (target.Count < colOffset + cells[r].Count) target.Add("");
            for (int c = 0; c < cells[r].Count; c++) target[colOffset + c] = cells[r][c];
        }
    }
    public static string ExcelColumn(int index)
    {
        index++; var name = "";
        do { index--; name = (char)('A' + index % 26) + name; index /= 26; } while (index > 0);
        return name;
    }
}
