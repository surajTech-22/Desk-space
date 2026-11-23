using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace DeskSpace;

internal sealed record RollbackCellV380(
    int Row,
    int Column,
    string CellAddress,
    string PreviousValue,
    string NewValue);

internal sealed record ExcelRollbackEntryV380(
    Guid Id,
    DateTime TimestampUtc,
    string WorkbookName,
    string SheetName,
    List<RollbackCellV380> Cells);

internal static class AccountingNormalizerV380
{
    private const string GstinAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly Regex GstinPattern = new(
        @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PanPattern = new(
        @"^[A-Z]{5}[0-9]{4}[A-Z]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TrailingDrCrRegex = new(
        @"(?<=[\d\.\)\s])\s*(Dr|Cr)\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CurrencyPrefixRegex = new(
        @"^(?:₹|Rs\.?|INR)\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CurrencySuffixRegex = new(
        @"\s*(?:₹|Rs\.?|INR)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] SupportedDateFormats =
    {
        "dd/MM/yyyy", "d/M/yyyy",
        "dd-MM-yyyy", "d-M-yyyy",
        "dd.MM.yyyy", "d.M.yyyy",
        "dd-MMM-yyyy", "d-MMM-yyyy",
        "dd-MMM-yy", "d-MMM-yy",
        "yyyy-MM-dd", "yyyy/MM/dd",
        "dd/MM/yy", "d/M/yy",
        "dd-MM-yy", "d-M-yy",
        "dd MMM yyyy", "d MMM yyyy"
    };

    internal static string NormalizeIndianNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string original = raw.Trim();
        string work = TrailingDrCrRegex.Replace(original, "").Trim();
        bool negative = false;

        if (work.StartsWith('(') && work.EndsWith(')') && work.Length >= 3)
        {
            negative = true;
            work = work[1..^1].Trim();
            work = TrailingDrCrRegex.Replace(work, "").Trim();
        }

        if (work.EndsWith("/-", StringComparison.Ordinal))
            work = work[..^2].Trim();

        bool hadOuterSign = false;
        if (work.StartsWith('-'))
        {
            if (negative)
                return original;
            negative = true;
            hadOuterSign = true;
            work = work[1..].Trim();
        }
        else if (work.StartsWith('+'))
        {
            hadOuterSign = true;
            work = work[1..].Trim();
        }

        work = CurrencyPrefixRegex.Replace(work, "").Trim();
        work = CurrencySuffixRegex.Replace(work, "").Trim();

        if (work.StartsWith('-'))
        {
            if (negative || hadOuterSign)
                return original;
            negative = true;
            work = work[1..].Trim();
        }
        else if (work.StartsWith('+'))
        {
            if (hadOuterSign)
                return original;
            work = work[1..].Trim();
        }

        if (work.Length == 0 || work.Any(char.IsWhiteSpace))
            return original;

        string stripped = work.Replace(",", "", StringComparison.Ordinal);
        if (stripped.Length == 0)
            return original;

        bool seenDot = false;
        bool seenDigit = false;
        foreach (char ch in stripped)
        {
            if (ch is >= '0' and <= '9')
            {
                seenDigit = true;
            }
            else if (ch == '.' && !seenDot)
            {
                seenDot = true;
            }
            else
            {
                return original;
            }
        }

        if (!seenDigit)
            return original;

        if (!decimal.TryParse(stripped, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
            return original;

        if (stripped.StartsWith('.'))
            stripped = "0" + stripped;

        return negative ? "-" + stripped : stripped;
    }

    internal static string NormalizeDateDdMmYyyy(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string trimmed = raw.Trim();
        if (DateTime.TryParseExact(
                trimmed,
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    internal static bool ValidateGstin(string? gstin, out string reason)
    {
        string code = (gstin ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            reason = "GSTIN is empty.";
            return false;
        }

        if (code.Length != 15)
        {
            reason = $"GSTIN must be 15 characters (got {code.Length}).";
            return false;
        }

        if (!GstinPattern.IsMatch(code))
        {
            reason = "Invalid GSTIN structure (expected 2-digit state + 10-char PAN + entity + 'Z' + check char).";
            return false;
        }

        int total = 0;
        for (int i = 0; i < 14; i++)
        {
            int cp = GstinAlphabet.IndexOf(code[i]);
            if (cp < 0)
            {
                reason = $"Unexpected character '{code[i]}' at position {i + 1}.";
                return false;
            }

            int factor = (i % 2 == 0) ? 1 : 2;
            int product = cp * factor;
            int addend = (product / 36) + (product % 36);
            total += addend;
        }

        int checkIndex = (36 - (total % 36)) % 36;
        char expected = GstinAlphabet[checkIndex];
        char actual = code[14];
        if (actual != expected)
        {
            reason = $"GSTIN checksum mismatch: expected '{expected}' at position 15, got '{actual}'.";
            return false;
        }

        reason = "Valid GSTIN (checksum verified)";
        return true;
    }

    internal static bool ValidatePan(string? pan)
    {
        string code = (pan ?? "").Trim().ToUpperInvariant();
        return code.Length == 10 && PanPattern.IsMatch(code);
    }

    internal static IReadOnlyList<(string InvoiceNo, List<int> RowNumbers)> FindDuplicateInvoices(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int invoiceColIndex,
        bool firstRowIsHeader = true)
    {
        if (rows == null || rows.Count == 0 || invoiceColIndex < 0)
            return Array.Empty<(string InvoiceNo, List<int> RowNumbers)>();

        var seen = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        int startRow = firstRowIsHeader ? 1 : 0;

        for (int r = startRow; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null || invoiceColIndex >= row.Count)
                continue;

            string normalized = (row[invoiceColIndex] ?? "").Trim().ToUpperInvariant();
            if (normalized.Length == 0)
                continue;

            if (!seen.TryGetValue(normalized, out var list))
            {
                list = new List<int>();
                seen[normalized] = list;
                order.Add(normalized);
            }
            list.Add(r + 1);
        }

        var duplicates = new List<(string InvoiceNo, List<int> RowNumbers)>();
        foreach (string key in order)
        {
            var rowNums = seen[key];
            if (rowNums.Count > 1)
                duplicates.Add((key, rowNums));
        }

        return duplicates;
    }
}

internal static class ExcelRollbackStoreV380
{
    private const int MaxEntries = 10;
    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    internal static string DefaultStoragePath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "excel-rollback-v380.json");

    internal static ExcelRollbackEntryV380 Record(DeskSpaceExcelProposalV16 proposal, string? storagePath = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var cells = proposal.Changes
            .Where(c => c != null && c.CanApply)
            .Select(c => new RollbackCellV380(
                c.Row,
                c.Column,
                c.CellAddress ?? "",
                !string.IsNullOrEmpty(c.Previous) ? c.Previous : (c.PreviousRaw ?? ""),
                c.NewValue ?? ""))
            .ToList();

        string workbookName = string.IsNullOrWhiteSpace(proposal.WorkbookFullName)
            ? "Workbook"
            : System.IO.Path.GetFileName(proposal.WorkbookFullName);

        var entry = new ExcelRollbackEntryV380(
            Guid.NewGuid(),
            DateTime.UtcNow,
            workbookName,
            proposal.SheetName ?? "",
            cells);

        lock (FileLock)
        {
            var history = LoadHistory(storagePath).ToList();
            history.Insert(0, entry);
            if (history.Count > MaxEntries)
                history = history.Take(MaxEntries).ToList();
            SaveHistory(history, storagePath);
        }

        return entry;
    }

    internal static IReadOnlyList<ExcelRollbackEntryV380> LoadHistory(string? storagePath = null)
    {
        string path = storagePath ?? DefaultStoragePath;
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(path))
                    return Array.Empty<ExcelRollbackEntryV380>();

                var info = new FileInfo(path);
                if (info.Length > 4 * 1024 * 1024)
                    return Array.Empty<ExcelRollbackEntryV380>();

                var items = JsonSerializer.Deserialize<List<ExcelRollbackEntryV380>>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);

                return items?
                    .Where(x => x != null && x.Cells != null)
                    .Take(MaxEntries)
                    .ToList() ?? new List<ExcelRollbackEntryV380>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return Array.Empty<ExcelRollbackEntryV380>();
            }
        }
    }

    internal static void SaveHistory(IReadOnlyList<ExcelRollbackEntryV380> entries, string? storagePath = null)
    {
        string path = storagePath ?? DefaultStoragePath;
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var bounded = (entries ?? Array.Empty<ExcelRollbackEntryV380>())
            .Where(x => x != null)
            .Take(MaxEntries)
            .ToList();

        lock (FileLock)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(bounded, JsonOptions), new UTF8Encoding(false));
                File.Move(tmp, path, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (IOException) { }
            }
        }
    }

    internal static string BuildRestoreTsv(ExcelRollbackEntryV380 entry)
    {
        if (entry?.Cells == null || entry.Cells.Count == 0)
            return "";

        int minRow = entry.Cells.Min(c => c.Row);
        int maxRow = entry.Cells.Max(c => c.Row);
        int minCol = entry.Cells.Min(c => c.Column);
        int maxCol = entry.Cells.Max(c => c.Column);

        int rowCount = Math.Max(1, maxRow - minRow + 1);
        int colCount = Math.Max(1, maxCol - minCol + 1);

        var grid = new List<List<string>>(rowCount);
        for (int r = 0; r < rowCount; r++)
        {
            var row = new List<string>(colCount);
            for (int c = 0; c < colCount; c++)
                row.Add("");
            grid.Add(row);
        }

        foreach (var cell in entry.Cells)
        {
            int rr = cell.Row - minRow;
            int cc = cell.Column - minCol;
            if (rr >= 0 && rr < rowCount && cc >= 0 && cc < colCount)
                grid[rr][cc] = cell.PreviousValue ?? "";
        }

        return MatrixCodec.Serialize(grid);
    }
}

internal static class DeskSpaceExcelProV380
{
    internal static void ShowPrePasteDiff(
        Window? owner,
        DeskSpaceExcelProposalV16 proposal,
        Func<Task>? onConfirmApply = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Pre-Paste Diff Preview",
            860,
            600,
            owner,
            transient: true);
        win.MinWidth = 680;
        win.MinHeight = 440;

        int writableCount = proposal.Changes.Count(c => c.CanApply);
        int formulaCount = proposal.Changes.Count(c =>
            !c.CanApply && (c.Decision?.Contains("formula", StringComparison.OrdinalIgnoreCase) ?? false));
        int blockedCount = proposal.Changes.Count - writableCount - formulaCount;

        var root = new DockPanel { Margin = new Thickness(18) };

        // Header card
        var headerStack = new StackPanel();
        string wbName = string.IsNullOrWhiteSpace(proposal.WorkbookFullName)
            ? "(Unsaved Workbook)"
            : System.IO.Path.GetFileName(proposal.WorkbookFullName);
        headerStack.Children.Add(new TextBlock
        {
            Text = $"{wbName}  ·  Sheet: {proposal.SheetName}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text)
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(proposal.Reason) ? "Review cell changes before writing to Excel." : proposal.Reason,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            Margin = new Thickness(0, 4, 0, 10)
        });

        var badges = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        badges.Children.Add(CreateBadge($"{writableCount} Writable", WpfColor.FromRgb(22, 163, 74), p));
        badges.Children.Add(CreateBadge($"{formulaCount} Formula preserved", WpfColor.FromRgb(37, 99, 235), p));
        badges.Children.Add(CreateBadge($"{blockedCount} Protected / Skipped", WpfColor.FromRgb(217, 119, 6), p));
        headerStack.Children.Add(badges);

        var filterCheck = new WpfCheckBox
        {
            Content = "Show writable cells only",
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            Margin = new Thickness(0, 10, 0, 0),
            IsChecked = false
        };
        headerStack.Children.Add(filterCheck);

        var headerCard = DeskSpaceFluentBackdropV380.CreateCard(headerStack, p);
        headerCard.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(headerCard, Dock.Top);
        root.Children.Add(headerCard);

        // Footer buttons
        var statusText = new TextBlock
        {
            Text = writableCount > 0
                ? $"Ready to write {writableCount} cell(s). Existing formulas remain untouched."
                : "No writable cells matched this selection.",
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        var footerDock = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var buttonRow = new StackPanel { Orientation = WpfOrientation.Horizontal };
        var cancelBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Cancel", () => win.Close(), false, "Close without modifying Excel");
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);

        WpfButton? confirmBtn = null;
        confirmBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            $"Confirm Paste ({writableCount})",
            async () =>
            {
                if (confirmBtn != null)
                    confirmBtn.IsEnabled = false;
                try
                {
                    if (onConfirmApply != null)
                    {
                        await onConfirmApply();
                        ExcelRollbackStoreV380.Record(proposal);
                    }
                    win.Close();
                }
                catch (Exception ex)
                {
                    statusText.Text = ex.Message;
                    if (confirmBtn != null)
                        confirmBtn.IsEnabled = writableCount > 0;
                }
            },
            true,
            "Apply writable changes and save rollback snapshot");
        confirmBtn.IsEnabled = writableCount > 0 && onConfirmApply != null;

        buttonRow.Children.Add(cancelBtn);
        buttonRow.Children.Add(confirmBtn);
        DockPanel.SetDock(buttonRow, Dock.Right);
        footerDock.Children.Add(buttonRow);
        footerDock.Children.Add(statusText);
        DockPanel.SetDock(footerDock, Dock.Bottom);
        root.Children.Add(footerDock);

        // Diff rows container
        var rowsHost = new StackPanel();
        void RebuildRows()
        {
            rowsHost.Children.Clear();
            rowsHost.Children.Add(BuildDiffRow(p, "Cell", "Current Value", "→", "New Value", "Decision", isHeader: true, isWritable: false, isFormula: false));
            var items = filterCheck.IsChecked == true
                ? proposal.Changes.Where(c => c.CanApply)
                : proposal.Changes;

            foreach (var c in items)
            {
                bool isFormula = !c.CanApply && (c.Decision?.Contains("formula", StringComparison.OrdinalIgnoreCase) ?? false);
                string prevDisplay = string.IsNullOrEmpty(c.Previous) ? "(empty)" : c.Previous;
                rowsHost.Children.Add(BuildDiffRow(
                    p,
                    c.CellAddress,
                    prevDisplay,
                    c.CanApply ? "→" : "·",
                    c.NewValue,
                    c.Decision ?? "",
                    isHeader: false,
                    isWritable: c.CanApply,
                    isFormula: isFormula));
            }
        }

        filterCheck.Checked += (_, _) => RebuildRows();
        filterCheck.Unchecked += (_, _) => RebuildRows();
        RebuildRows();

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rowsHost
        };
        var tableCard = DeskSpaceFluentBackdropV380.CreateCard(scroll, p);
        root.Children.Add(tableCard);

        win.Content = root;
        win.Show();
        win.Activate();
    }

    internal static void ShowStudio(
        Window? owner,
        Func<Workspace>? getWorkspace = null,
        Action? saveWorkspace = null)
    {
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Excel Pro & Accounting Studio",
            960,
            660,
            owner,
            transient: false);
        win.MinWidth = 760;
        win.MinHeight = 520;

        var tabs = new TabControl
        {
            Margin = new Thickness(14),
            Background = DeskSpaceDesignV283.Solid(p.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            ItemContainerStyle = BuildTabItemStyle(p)
        };

        tabs.Items.Add(BuildAccountingPrepTab(p, getWorkspace, saveWorkspace));
        tabs.Items.Add(BuildGstinCheckerTab(p));
        tabs.Items.Add(BuildRollbackHistoryTab(p));

        win.Content = tabs;
        win.Show();
        win.Activate();
    }

    private static TabItem BuildAccountingPrepTab(
        DeskSpaceDesignV283.Palette p,
        Func<Workspace>? getWorkspace,
        Action? saveWorkspace)
    {
        var tab = new TabItem { Header = "Accounting & GSTIN Prep" };
        var root = new DockPanel { Margin = new Thickness(10) };

        var status = new TextBlock
        {
            Text = "Paste TSV/CSV or load a saved DataTab, then normalize Indian numbers, dates, GSTINs/PANs, or find duplicate invoices.",
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        // Top controls
        var topPanel = new StackPanel();
        var dataTabCombo = new WpfComboBox { MinWidth = 220, Margin = new Thickness(0, 0, 8, 0) };
        var colBox = new WpfTextBox { Width = 60, Text = "1", VerticalContentAlignment = VerticalAlignment.Center };
        DeskSpaceDesignV283.Input(colBox, p);
        var hasHeaderCheck = new WpfCheckBox
        {
            Content = "First row is header",
            IsChecked = true,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var inputBox = new WpfTextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 200,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12
        };
        DeskSpaceDesignV283.Input(inputBox, p);

        var reportBox = new WpfTextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 140,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12
        };
        DeskSpaceDesignV283.Input(reportBox, p);

        Workspace ResolveWorkspace()
        {
            if (getWorkspace != null)
                return getWorkspace();
            return new WorkspaceStore().Load();
        }

        void RefreshTabsCombo()
        {
            try
            {
                var ws = ResolveWorkspace();
                dataTabCombo.ItemsSource = ws.DataTabs;
                if (ws.DataTabs.Count > 0 && dataTabCombo.SelectedIndex < 0)
                    dataTabCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                status.Text = "Could not read DataTabs: " + ex.Message;
            }
        }

        RefreshTabsCombo();

        int SelectedColZeroBased() => int.TryParse(colBox.Text.Trim(), out int oneBased) && oneBased >= 1 ? oneBased - 1 : 0;

        List<List<string>> ParseGrid() => ParseTsvOrCsv(inputBox.Text);

        void WriteGrid(List<List<string>> grid) => inputBox.Text = MatrixCodec.Serialize(grid);

        var row1 = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row1.Children.Add(dataTabCombo);
        var loadTabBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Load DataTab", () =>
        {
            if (dataTabCombo.SelectedItem is DataTab dt)
            {
                inputBox.Text = MatrixCodec.Serialize(dt.Cells);
                status.Text = $"Loaded '{dt.Name}' ({dt.Cells.Count} rows).";
            }
        }, false, "Load selected DataTab into the grid");
        loadTabBtn.Margin = new Thickness(0, 0, 8, 0);
        row1.Children.Add(loadTabBtn);

        row1.Children.Add(new TextBlock
        {
            Text = "Target col (1-based):",
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 6, 0)
        });
        row1.Children.Add(colBox);
        row1.Children.Add(hasHeaderCheck);
        topPanel.Children.Add(row1);

        var row2 = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        void AddActionBtn(string label, Action action, bool primary = false, string tip = "")
        {
            var b = DeskSpaceFluentBackdropV380.CreatePillButton(label, action, primary, tip);
            b.Margin = new Thickness(0, 0, 6, 6);
            row2.Children.Add(b);
        }

        AddActionBtn("Normalize Indian Numbers", () =>
        {
            var grid = ParseGrid();
            int start = hasHeaderCheck.IsChecked == true ? 1 : 0;
            int changed = 0;
            for (int r = start; r < grid.Count; r++)
            {
                for (int c = 0; c < grid[r].Count; c++)
                {
                    string norm = AccountingNormalizerV380.NormalizeIndianNumber(grid[r][c]);
                    if (!string.Equals(norm, grid[r][c], StringComparison.Ordinal))
                    {
                        grid[r][c] = norm;
                        changed++;
                    }
                }
            }
            WriteGrid(grid);
            status.Text = $"Normalized {changed} currency/numeric cell(s).";
        }, true, "Convert ₹/Rs./(parentheses)/Dr/Cr into plain signed numbers");

        AddActionBtn("Normalize Dates (dd/MM/yyyy)", () =>
        {
            var grid = ParseGrid();
            int start = hasHeaderCheck.IsChecked == true ? 1 : 0;
            int col = SelectedColZeroBased();
            int changed = 0;
            for (int r = start; r < grid.Count; r++)
            {
                if (col < grid[r].Count)
                {
                    string norm = AccountingNormalizerV380.NormalizeDateDdMmYyyy(grid[r][col]);
                    if (!string.Equals(norm, grid[r][col], StringComparison.Ordinal))
                    {
                        grid[r][col] = norm;
                        changed++;
                    }
                }
            }
            WriteGrid(grid);
            status.Text = $"Normalized {changed} date cell(s) in column {col + 1} to dd/MM/yyyy.";
        });

        AddActionBtn("Validate GSTIN / PAN Column", () =>
        {
            var grid = ParseGrid();
            int start = hasHeaderCheck.IsChecked == true ? 1 : 0;
            int col = SelectedColZeroBased();
            var sb = new StringBuilder();
            int valid = 0, invalid = 0;
            for (int r = start; r < grid.Count; r++)
            {
                if (col >= grid[r].Count)
                    continue;
                string raw = (grid[r][col] ?? "").Trim();
                if (raw.Length == 0)
                    continue;
                if (raw.Length == 10)
                {
                    if (AccountingNormalizerV380.ValidatePan(raw))
                    {
                        valid++;
                    }
                    else
                    {
                        invalid++;
                        sb.AppendLine($"Row {r + 1}: Invalid PAN '{raw}'");
                    }
                }
                else
                {
                    if (AccountingNormalizerV380.ValidateGstin(raw, out string reason))
                    {
                        valid++;
                    }
                    else
                    {
                        invalid++;
                        sb.AppendLine($"Row {r + 1}: '{raw}' — {reason}");
                    }
                }
            }
            reportBox.Text = sb.Length == 0
                ? $"All {valid} GSTIN/PAN entries in column {col + 1} passed validation."
                : sb.ToString();
            status.Text = $"Column {col + 1}: {valid} valid, {invalid} invalid.";
        });

        AddActionBtn("Find Duplicate Invoices", () =>
        {
            var grid = ParseGrid();
            int col = SelectedColZeroBased();
            var dupes = AccountingNormalizerV380.FindDuplicateInvoices(
                grid.Select(r => (IReadOnlyList<string>)r).ToList(),
                col,
                hasHeaderCheck.IsChecked == true);
            if (dupes.Count == 0)
            {
                reportBox.Text = $"No duplicate invoice numbers found in column {col + 1}.";
            }
            else
            {
                reportBox.Text = string.Join(
                    Environment.NewLine,
                    dupes.Select(d => $"{d.InvoiceNo}  →  Rows: {string.Join(", ", d.RowNumbers)}"));
            }
            status.Text = $"Found {dupes.Count} duplicate invoice number(s) in column {col + 1}.";
        });

        AddActionBtn("Copy Clean TSV", () =>
        {
            var grid = ParseGrid();
            string tsv = MatrixCodec.Serialize(grid);
            System.Windows.Clipboard.SetText(tsv);
            status.Text = "Copied clean TSV to clipboard (ready to paste into Excel).";
        });

        AddActionBtn("Save to DataTab", () =>
        {
            var grid = ParseGrid();
            if (grid.Count == 0)
            {
                status.Text = "Nothing to save.";
                return;
            }
            var store = new WorkspaceStore();
            var ws = getWorkspace != null ? getWorkspace() : store.Load();
            string tabName = dataTabCombo.SelectedItem is DataTab existing
                ? existing.Name + " (Clean)"
                : "Accounting Prep " + DateTime.Now.ToString("HH:mm");
            ws.DataTabs.Add(new DataTab { Name = tabName, Cells = grid });
            if (saveWorkspace != null)
                saveWorkspace();
            else
                store.Save(ws);
            RefreshTabsCombo();
            status.Text = $"Saved '{tabName}' to Workspace DataTabs.";
        });

        topPanel.Children.Add(row2);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        var bodyStack = new StackPanel();
        bodyStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Grid Data (TSV / CSV)", p));
        bodyStack.Children.Add(inputBox);
        bodyStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Diagnostics & Duplicates", p));
        bodyStack.Children.Add(reportBox);

        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = DeskSpaceFluentBackdropV380.CreateCard(bodyStack, p)
        });

        tab.Content = root;
        return tab;
    }

    private static TabItem BuildGstinCheckerTab(DeskSpaceDesignV283.Palette p)
    {
        var tab = new TabItem { Header = "Quick GSTIN / PAN Checker" };
        var stack = new StackPanel { Margin = new Thickness(14) };

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Instant GSTIN Checksum (Mod-36) & PAN Verifier", p));

        var gstinBox = new WpfTextBox { FontSize = 15, Margin = new Thickness(0, 4, 0, 8) };
        DeskSpaceDesignV283.Input(gstinBox, p);
        var gstinResult = new TextBlock
        {
            Text = "Enter a 15-character Indian GSTIN to verify structure and Luhn mod-36 check digit.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 14)
        };

        void EvaluateGstin()
        {
            string val = gstinBox.Text;
            if (string.IsNullOrWhiteSpace(val))
            {
                gstinResult.Text = "Enter a 15-character GSTIN.";
                gstinResult.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
                return;
            }
            bool ok = AccountingNormalizerV380.ValidateGstin(val, out string reason);
            if (ok)
            {
                string norm = val.Trim().ToUpperInvariant();
                string embeddedPan = norm.Substring(2, 10);
                gstinResult.Text = $"{reason}  ·  State Code: {norm[..2]}  ·  Embedded PAN: {embeddedPan}";
                gstinResult.Foreground = DeskSpaceDesignV283.Solid(WpfColor.FromRgb(74, 222, 128));
            }
            else
            {
                gstinResult.Text = reason;
                gstinResult.Foreground = DeskSpaceDesignV283.Solid(WpfColor.FromRgb(248, 113, 113));
            }
        }

        gstinBox.TextChanged += (_, _) => EvaluateGstin();

        var panBox = new WpfTextBox { FontSize = 15, Margin = new Thickness(0, 4, 0, 8) };
        DeskSpaceDesignV283.Input(panBox, p);
        var panResult = new TextBlock
        {
            Text = "Enter a 10-character PAN (e.g. ABCDE1234F).",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        };
        panBox.TextChanged += (_, _) =>
        {
            string val = panBox.Text.Trim().ToUpperInvariant();
            if (val.Length == 0)
            {
                panResult.Text = "Enter a 10-character PAN.";
                panResult.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
            }
            else if (AccountingNormalizerV380.ValidatePan(val))
            {
                panResult.Text = $"Valid PAN format (Entity type code: '{val[3]}').";
                panResult.Foreground = DeskSpaceDesignV283.Solid(WpfColor.FromRgb(74, 222, 128));
            }
            else
            {
                panResult.Text = "Invalid PAN format. Expected 5 letters + 4 digits + 1 letter.";
                panResult.Foreground = DeskSpaceDesignV283.Solid(WpfColor.FromRgb(248, 113, 113));
            }
        };

        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "GSTIN (15 chars)", Foreground = DeskSpaceDesignV283.Solid(p.Text), FontWeight = FontWeights.SemiBold });
        cardInner.Children.Add(gstinBox);
        cardInner.Children.Add(gstinResult);
        cardInner.Children.Add(new TextBlock { Text = "PAN (10 chars)", Foreground = DeskSpaceDesignV283.Solid(p.Text), FontWeight = FontWeights.SemiBold });
        cardInner.Children.Add(panBox);
        cardInner.Children.Add(panResult);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(cardInner, p));
        tab.Content = stack;
        return tab;
    }

    private static TabItem BuildRollbackHistoryTab(DeskSpaceDesignV283.Palette p)
    {
        var tab = new TabItem { Header = "Paste Rollback History" };
        var root = new DockPanel { Margin = new Thickness(12) };

        var list = new ListBox { Width = 280, Margin = new Thickness(0, 0, 12, 0) };
        DeskSpaceDesignV283.Selection(list, p);

        var diffView = new WpfTextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12
        };
        DeskSpaceDesignV283.Input(diffView, p);

        var status = new TextBlock
        {
            Text = "Up to 10 recent Excel paste snapshots are stored locally.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        IReadOnlyList<ExcelRollbackEntryV380> history = Array.Empty<ExcelRollbackEntryV380>();

        void Reload()
        {
            history = ExcelRollbackStoreV380.LoadHistory();
            list.Items.Clear();
            foreach (var h in history)
                list.Items.Add($"{h.TimestampUtc.ToLocalTime():g}  ·  {h.WorkbookName} [{h.SheetName}] ({h.Cells.Count})");
            if (list.Items.Count > 0)
                list.SelectedIndex = 0;
            else
                diffView.Text = "No rollback snapshots recorded yet.";
        }

        list.SelectionChanged += (_, _) =>
        {
            int idx = list.SelectedIndex;
            if (idx < 0 || idx >= history.Count)
                return;
            var entry = history[idx];
            var sb = new StringBuilder();
            sb.AppendLine($"Workbook: {entry.WorkbookName}  |  Sheet: {entry.SheetName}");
            sb.AppendLine($"Captured: {entry.TimestampUtc.ToLocalTime():F}");
            sb.AppendLine(new string('-', 64));
            foreach (var c in entry.Cells)
                sb.AppendLine($"{c.CellAddress,-8} '{c.PreviousValue}'  <--  '{c.NewValue}'");
            diffView.Text = sb.ToString();
        };

        var actions = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var copyRestoreBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Restore TSV", () =>
        {
            int idx = list.SelectedIndex;
            if (idx < 0 || idx >= history.Count)
                return;
            string tsv = ExcelRollbackStoreV380.BuildRestoreTsv(history[idx]);
            System.Windows.Clipboard.SetText(tsv);
            status.Text = "Copied previous-value restore TSV to clipboard.";
        }, true, "Copy aligned previous values as TSV to paste back into Excel");
        copyRestoreBtn.Margin = new Thickness(0, 0, 8, 0);

        var refreshBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Refresh", Reload, false, "Reload rollback snapshots");
        actions.Children.Add(copyRestoreBtn);
        actions.Children.Add(refreshBtn);
        DockPanel.SetDock(actions, Dock.Top);
        root.Children.Add(actions);

        DockPanel.SetDock(list, Dock.Left);
        root.Children.Add(list);
        root.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(diffView, p));

        Reload();
        tab.Content = root;
        return tab;
    }

    private static Border CreateBadge(string text, WpfColor tone, DeskSpaceDesignV283.Palette p)
    {
        return new Border
        {
            Background = DeskSpaceDesignV283.Solid(WpfColor.FromArgb(42, tone.R, tone.G, tone.B)),
            BorderBrush = DeskSpaceDesignV283.Solid(tone),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeskSpaceDesignV283.Solid(p.Text)
            }
        };
    }

    private static UIElement BuildDiffRow(
        DeskSpaceDesignV283.Palette p,
        string cell,
        string currentVal,
        string arrow,
        string newVal,
        string decision,
        bool isHeader,
        bool isWritable,
        bool isFormula)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });

        WpfBrush decisionBrush = isHeader
            ? DeskSpaceDesignV283.Solid(p.Muted)
            : isWritable
                ? DeskSpaceDesignV283.Solid(WpfColor.FromRgb(74, 222, 128))
                : isFormula
                    ? DeskSpaceDesignV283.Solid(WpfColor.FromRgb(96, 165, 250))
                    : DeskSpaceDesignV283.Solid(WpfColor.FromRgb(251, 191, 36));

        void AddText(int col, string value, WpfBrush brush, FontWeight weight, WpfHorizontalAlignment align = WpfHorizontalAlignment.Left)
        {
            var tb = new TextBlock
            {
                Text = value,
                Foreground = brush,
                FontWeight = weight,
                FontSize = isHeader ? 11.5 : 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = value,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 3, 4, 3)
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        var textBrush = DeskSpaceDesignV283.Solid(isHeader ? p.Muted : p.Text);
        var fw = isHeader ? FontWeights.SemiBold : FontWeights.Normal;
        AddText(0, cell, textBrush, isHeader ? FontWeights.SemiBold : FontWeights.Medium);
        AddText(1, currentVal, textBrush, fw);
        AddText(2, arrow, decisionBrush, FontWeights.Bold, WpfHorizontalAlignment.Center);
        AddText(3, newVal, textBrush, fw);
        AddText(4, decision, decisionBrush, isHeader ? FontWeights.SemiBold : FontWeights.Medium);

        return new Border
        {
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(0, 0, 0, isHeader ? 1 : 0),
            Background = isWritable
                ? DeskSpaceDesignV283.Solid(WpfColor.FromArgb(18, 34, 197, 94))
                : WpfBrushes.Transparent,
            Child = grid
        };
    }

    internal static Style BuildTabItemStyle(DeskSpaceDesignV283.Palette p)
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Muted)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Navigation)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, WpfBrushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 14, 8)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 4)));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        var border = new FrameworkElementFactory(typeof(Border)) { Name = "TabChrome" };
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Control.Padding))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);

        var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };
        var hovered = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hovered.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Hover), "TabChrome"));
        hovered.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        template.Triggers.Add(hovered);

        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.AccentSoft), "TabChrome"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "TabChrome"));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        template.Triggers.Add(selected);
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static List<List<string>> ParseTsvOrCsv(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<List<string>>();

        if (text.Contains('\t', StringComparison.Ordinal))
            return MatrixCodec.Parse(text);

        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (quoted)
                {
                    quoted = false;
                }
                else if (cell.Length == 0)
                {
                    quoted = true;
                }
                else
                {
                    cell.Append(ch);
                }
                continue;
            }

            if (!quoted && ch == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (!quoted && (ch == '\r' || ch == '\n'))
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }

            cell.Append(ch);
        }

        if (row.Count > 0 || cell.Length > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
