using System.Diagnostics;
using System.Globalization;

namespace DeskSpace;

/// <summary>
/// Optional local Tesseract OCR. The output is an UNVERIFIED draft, not an exact spreadsheet.
/// Keeps word coordinates, clusters lines by vertical position and detects gaps as possible cells.
/// Requires Tesseract OCR executable on PATH or TESSERACT_EXE.
/// </summary>
public static class ImageOcr
{
    private sealed record Word(string Value, int Left, int Top, int Width, int Height);
    public static async Task<List<List<string>>> RecognizeDraftAsync(string imagePath)
    {
        var executable = Environment.GetEnvironmentVariable("TESSERACT_EXE");
        if (string.IsNullOrWhiteSpace(executable)) executable = "tesseract";
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        info.ArgumentList.Add(imagePath);
        info.ArgumentList.Add("stdout");
        info.ArgumentList.Add("tsv");
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Tesseract OCR could not start.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("OCR exceeded 60 seconds."); }
        string result = await stdoutTask;
        string errors = await stderrTask;
        if (process.ExitCode != 0) throw new InvalidOperationException("OCR failed: " + errors);
        var words = new List<Word>();
        foreach (string line in result.Replace("\r", "").Split('\n').Skip(1))
        {
            var fields = line.Split('\t', 12);
            if (fields.Length < 12 || fields[0] != "5" || string.IsNullOrWhiteSpace(fields[11])) continue;
            if (int.TryParse(fields[6], out var x) && int.TryParse(fields[7], out var y)
                && int.TryParse(fields[8], out var w) && int.TryParse(fields[9], out var h)
                && double.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf)
                && conf > 20)
                words.Add(new Word(fields[11].Trim(), x, y, w, h));
        }
        if (words.Count == 0) throw new InvalidOperationException("No recognizable text. The original image is preserved.");
        // Row grouping: approximate and deliberately marked as unverified for manual correction.
        int medianHeight = words.Select(x => x.Height).Order().ElementAt(words.Count / 2);
        int tolerance = Math.Max(5, medianHeight / 2);
        var groups = new List<List<Word>>();
        foreach (var w in words.OrderBy(x => x.Top + x.Height / 2).ThenBy(x => x.Left))
        {
            int center = w.Top + w.Height / 2;
            List<Word>? found = groups.FirstOrDefault(group => Math.Abs((int)group.Average(x => x.Top + x.Height / 2) - center) <= tolerance);
            if (found is null) groups.Add(new List<Word> { w }); else found.Add(w);
        }
        var grid = new List<List<string>>();
        foreach (var line in groups.OrderBy(group => group.Min(x => x.Top)))
        {
            var ordered = line.OrderBy(x => x.Left).ToArray();
            var cells = new List<string>();
            string current = "";
            int right = -1;
            foreach (var w in ordered)
            {
                int gap = w.Left - right;
                if (current.Length > 0 && gap > Math.Max(18, medianHeight * 2))
                { cells.Add(current); current = w.Value; }
                else current += (current.Length > 0 ? " " : "") + w.Value;
                right = Math.Max(right, w.Left + w.Width);
            }
            if (current.Length > 0) cells.Add(current);
            grid.Add(cells);
        }
        // We cannot infer reliable blank columns or merged cells from OCR alone.
        return grid;
    }
}
