using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

internal sealed record NoteMetaV380(bool Pinned, string ColorTag);

internal static class NoteMetaStoreV380
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    internal static string StoragePath => Path.Combine(WorkspaceStore.Root, "notes-meta-v380.json");

    internal static Dictionary<Guid, NoteMetaV380> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
                return new Dictionary<Guid, NoteMetaV380>();
            string json = File.ReadAllText(StoragePath, Encoding.UTF8);
            var raw = JsonSerializer.Deserialize<Dictionary<string, NoteMetaV380>>(json, JsonOptions);
            var map = new Dictionary<Guid, NoteMetaV380>();
            if (raw != null)
            {
                foreach (var kv in raw)
                {
                    if (Guid.TryParse(kv.Key, out var id) && kv.Value != null)
                        map[id] = kv.Value;
                }
            }
            return map;
        }
        catch
        {
            return new Dictionary<Guid, NoteMetaV380>();
        }
    }

    internal static void Save(IReadOnlyDictionary<Guid, NoteMetaV380> map)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var serializedMap = new Dictionary<string, NoteMetaV380>(map.Count);
        foreach (var kv in map)
        {
            if (kv.Value.Pinned || !string.IsNullOrWhiteSpace(kv.Value.ColorTag))
                serializedMap[kv.Key.ToString("D")] = kv.Value;
        }

        string tmp = StoragePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(serializedMap, JsonOptions), new UTF8Encoding(false));
            File.Move(tmp, StoragePath, true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    internal static NoteMetaV380 Get(Guid noteId)
    {
        var map = Load();
        return map.TryGetValue(noteId, out var meta) ? meta : new NoteMetaV380(false, "");
    }

    internal static bool IsPinned(Guid noteId) => Get(noteId).Pinned;

    internal static void SetPinned(Guid noteId, bool pinned)
    {
        var map = Load();
        string existingTag = map.TryGetValue(noteId, out var old) ? old.ColorTag : "";
        if (!pinned && string.IsNullOrWhiteSpace(existingTag))
            map.Remove(noteId);
        else
            map[noteId] = new NoteMetaV380(pinned, existingTag);
        Save(map);
    }

    internal static void SetColorTag(Guid noteId, string colorTag)
    {
        var map = Load();
        bool pinned = map.TryGetValue(noteId, out var old) && old.Pinned;
        string clean = (colorTag ?? "").Trim();
        if (!pinned && string.IsNullOrWhiteSpace(clean))
            map.Remove(noteId);
        else
            map[noteId] = new NoteMetaV380(pinned, clean);
        Save(map);
    }
}

internal static class MarkdownNoteEngineV380
{
    private static readonly Regex TagRegex = new(
        @"(?:^|\s)#([A-Za-z][A-Za-z0-9_-]{1,30})",
        RegexOptions.Compiled);

    private static readonly Regex ChecklistLineRegex = new(
        @"^(\s*[-*]\s+\[)([ xX])(\](?:\s.*|$))",
        RegexOptions.Compiled);

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,4})\s+(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex InlineRegex = new(
        @"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*)",
        RegexOptions.Compiled);

    internal static IReadOnlyList<string> ExtractTags(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();
        bool inCodeFence = false;

        foreach (string line in SplitLines(markdown))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }
            if (inCodeFence) continue;

            foreach (Match m in TagRegex.Matches(line))
            {
                string tag = m.Groups[1].Value;
                if (LooksLikeHexColor(tag)) continue;
                if (seen.Add(tag))
                    tags.Add(tag);
            }
        }
        return tags;
    }

    private static bool LooksLikeHexColor(string tag)
    {
        if (tag.Length is not (3 or 6 or 8)) return false;
        foreach (char c in tag)
        {
            bool hex = c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');
            if (!hex) return false;
        }
        return tag.Any(char.IsDigit)
            || tag.Equals("FFF", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase)
            || tag.All(c => c is >= 'A' and <= 'F');
    }

    internal static (int Done, int Total) CountChecklist(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return (0, 0);
        int done = 0, total = 0;
        bool inCodeFence = false;

        foreach (string line in SplitLines(markdown))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }
            if (inCodeFence) continue;

            var match = ChecklistLineRegex.Match(line);
            if (!match.Success) continue;
            total++;
            if (match.Groups[2].Value is "x" or "X")
                done++;
        }
        return (done, total);
    }

    internal static string ToggleChecklistItem(string markdown, int checklistIndex)
    {
        if (string.IsNullOrEmpty(markdown) || checklistIndex < 0)
            return markdown ?? "";

        var sb = new StringBuilder(markdown.Length + 8);
        bool inCodeFence = false;
        int currentIndex = 0;
        int pos = 0;

        while (pos <= markdown.Length)
        {
            int lf = markdown.IndexOf('\n', pos);
            int lineEnd = lf >= 0 ? lf : markdown.Length;
            int contentEnd = lineEnd;
            string lineEnding = "";
            if (lf >= 0)
            {
                if (lf > pos && markdown[lf - 1] == '\r')
                {
                    contentEnd = lf - 1;
                    lineEnding = "\r\n";
                }
                else
                {
                    lineEnding = "\n";
                }
            }

            string line = markdown.Substring(pos, contentEnd - pos);
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                sb.Append(line).Append(lineEnding);
            }
            else if (!inCodeFence)
            {
                var match = ChecklistLineRegex.Match(line);
                if (match.Success)
                {
                    if (currentIndex == checklistIndex)
                    {
                        string nextMark = match.Groups[2].Value is "x" or "X" ? " " : "x";
                        sb.Append(match.Groups[1].Value)
                          .Append(nextMark)
                          .Append(match.Groups[3].Value)
                          .Append(lineEnding);
                    }
                    else
                    {
                        sb.Append(line).Append(lineEnding);
                    }
                    currentIndex++;
                }
                else
                {
                    sb.Append(line).Append(lineEnding);
                }
            }
            else
            {
                sb.Append(line).Append(lineEnding);
            }

            if (lf < 0) break;
            pos = lf + 1;
            if (pos == markdown.Length)
                break;
        }

        return sb.ToString();
    }

    internal static (int Words, int Chars) WordStats(string? text)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0);
        int words = 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }
        return (words, text.Length);
    }

    internal static FlowDocument RenderToFlowDocument(
        string markdown,
        Action<int>? onToggleChecklist,
        DeskSpaceDesignV283.Palette palette)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(12),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 14,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Background = Brushes.Transparent
        };

        string source = markdown ?? "";
        var lines = SplitLines(source);
        int checklistIdx = 0;
        int i = 0;
        System.Windows.Documents.List? currentBulletList = null;

        void FlushBullets()
        {
            if (currentBulletList != null)
            {
                doc.Blocks.Add(currentBulletList);
                currentBulletList = null;
            }
        }

        while (i < lines.Count)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // Fenced code block
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushBullets();
                var codeSb = new StringBuilder();
                i++;
                while (i < lines.Count && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    if (codeSb.Length > 0) codeSb.AppendLine();
                    codeSb.Append(lines[i]);
                    i++;
                }
                if (i < lines.Count) i++; // consume closing fence

                var codePara = new Paragraph(new Run(codeSb.ToString()))
                {
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                    FontSize = 13,
                    Background = DeskSpaceDesignV283.Solid(palette.Surface),
                    BorderBrush = DeskSpaceDesignV283.Solid(palette.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 6, 0, 8)
                };
                doc.Blocks.Add(codePara);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushBullets();
                i++;
                continue;
            }

            // Horizontal rule
            if (trimmed is "---" or "***" or "___" || (trimmed.Length >= 3 && trimmed.All(c => c == '-')))
            {
                FlushBullets();
                var hr = new Border
                {
                    Height = 1,
                    Background = DeskSpaceDesignV283.Solid(palette.Border),
                    Margin = new Thickness(0, 8, 0, 8)
                };
                doc.Blocks.Add(new BlockUIContainer(hr) { Margin = new Thickness(0) });
                i++;
                continue;
            }

            // Headings
            var hMatch = HeadingRegex.Match(line);
            if (hMatch.Success)
            {
                FlushBullets();
                int level = hMatch.Groups[1].Value.Length;
                double fontSize = level switch { 1 => 22, 2 => 18, 3 => 15.5, _ => 14 };
                var p = new Paragraph
                {
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = DeskSpaceDesignV283.Solid(palette.Text),
                    Margin = new Thickness(0, level == 1 ? 10 : 8, 0, 4)
                };
                AddInlines(p.Inlines, hMatch.Groups[2].Value, palette);
                doc.Blocks.Add(p);
                i++;
                continue;
            }

            // Blockquote
            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushBullets();
                string quoteText = trimmed.Length > 1 ? trimmed[1..].TrimStart() : "";
                var quotePara = new Paragraph
                {
                    BorderBrush = DeskSpaceDesignV283.Solid(palette.Accent),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(10, 3, 4, 3),
                    Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
                    Margin = new Thickness(4, 4, 0, 6)
                };
                AddInlines(quotePara.Inlines, quoteText, palette);
                doc.Blocks.Add(quotePara);
                i++;
                continue;
            }

            // Checklist item
            var chkMatch = ChecklistLineRegex.Match(line);
            if (chkMatch.Success)
            {
                FlushBullets();
                bool isChecked = chkMatch.Groups[2].Value is "x" or "X";
                string labelText = chkMatch.Groups[3].Value.Length > 1 ? chkMatch.Groups[3].Value[1..].TrimStart() : "";
                int capturedIdx = checklistIdx++;

                var cb = new CheckBox
                {
                    IsChecked = isChecked,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand
                };
                cb.Click += (_, _) => onToggleChecklist?.Invoke(capturedIdx);

                var para = new Paragraph
                {
                    Margin = new Thickness(2, 2, 0, 4),
                    LineHeight = 22
                };
                para.Inlines.Add(new InlineUIContainer(cb) { BaselineAlignment = BaselineAlignment.Center });

                if (isChecked)
                {
                    var span = new Span
                    {
                        Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
                        TextDecorations = TextDecorations.Strikethrough
                    };
                    AddInlines(span.Inlines, labelText, palette);
                    para.Inlines.Add(span);
                }
                else
                {
                    AddInlines(para.Inlines, labelText, palette);
                }
                doc.Blocks.Add(para);
                i++;
                continue;
            }

            // Bullet list
            string lineTrimLeft = line.TrimStart();
            if ((lineTrimLeft.StartsWith("- ", StringComparison.Ordinal) || lineTrimLeft.StartsWith("* ", StringComparison.Ordinal)))
            {
                currentBulletList ??= new System.Windows.Documents.List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 2, 0, 6),
                    Padding = new Thickness(20, 0, 0, 0)
                };
                var itemPara = new Paragraph { Margin = new Thickness(0, 1, 0, 2) };
                AddInlines(itemPara.Inlines, lineTrimLeft[2..], palette);
                currentBulletList.ListItems.Add(new ListItem(itemPara));
                i++;
                continue;
            }

            // Regular paragraph
            FlushBullets();
            var normalPara = new Paragraph { Margin = new Thickness(0, 2, 0, 6), LineHeight = 21 };
            AddInlines(normalPara.Inlines, line, palette);
            doc.Blocks.Add(normalPara);
            i++;
        }

        FlushBullets();
        return doc;
    }

    private static void AddInlines(InlineCollection inlines, string text, DeskSpaceDesignV283.Palette palette)
    {
        if (string.IsNullOrEmpty(text)) return;
        int pos = 0;
        foreach (Match m in InlineRegex.Matches(text))
        {
            if (m.Index > pos)
                inlines.Add(new Run(text.Substring(pos, m.Index - pos)));

            string token = m.Value;
            if (token.StartsWith('`') && token.EndsWith('`') && token.Length >= 2)
            {
                inlines.Add(new Run(token[1..^1])
                {
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                    Background = DeskSpaceDesignV283.Solid(palette.Surface),
                    Foreground = DeskSpaceDesignV283.Solid(palette.Accent)
                });
            }
            else if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal) && token.Length >= 4)
            {
                inlines.Add(new Bold(new Run(token[2..^2])));
            }
            else if (token.StartsWith('*') && token.EndsWith('*') && token.Length >= 2)
            {
                inlines.Add(new Italic(new Run(token[1..^1])));
            }
            else
            {
                inlines.Add(new Run(token));
            }
            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text[pos..]));
    }

    private static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        int pos = 0;
        while (pos <= text.Length)
        {
            int lf = text.IndexOf('\n', pos);
            if (lf < 0)
            {
                list.Add(text[pos..]);
                break;
            }
            int end = (lf > pos && text[lf - 1] == '\r') ? lf - 1 : lf;
            list.Add(text.Substring(pos, end - pos));
            pos = lf + 1;
            if (pos == text.Length)
            {
                list.Add("");
                break;
            }
        }
        return list;
    }
}

internal static class DynamicSnippetEngineV380
{
    private static readonly Regex PromptRegex = new(
        @"\{\{\s*prompt\s*:\s*([^}]+?)\s*\}\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        @"\{\{([^{}]+)\}\}",
        RegexOptions.Compiled);

    internal static IReadOnlyList<string> FindPrompts(string? template)
    {
        if (string.IsNullOrEmpty(template))
            return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (Match m in PromptRegex.Matches(template))
        {
            string name = m.Groups[1].Value.Trim();
            if (name.Length > 0 && seen.Add(name))
                result.Add(name);
        }
        return result;
    }

    internal static string Expand(
        string template,
        string clipboardText,
        IReadOnlyDictionary<string, string>? promptValues = null,
        DateTime? now = null)
    {
        if (string.IsNullOrEmpty(template)) return "";
        DateTime ts = now ?? DateTime.Now;
        string clip = clipboardText ?? "";

        return TokenRegex.Replace(template, match =>
        {
            string rawToken = match.Groups[1].Value.Trim();
            if (rawToken.Equals("date", StringComparison.OrdinalIgnoreCase))
                return ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (rawToken.StartsWith("date:", StringComparison.OrdinalIgnoreCase))
            {
                string fmt = rawToken[5..].Trim();
                if (string.IsNullOrEmpty(fmt))
                    return ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                try
                {
                    return ts.ToString(fmt, CultureInfo.CurrentCulture);
                }
                catch (FormatException)
                {
                    return ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }
            if (rawToken.Equals("time", StringComparison.OrdinalIgnoreCase))
                return ts.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (rawToken.Equals("datetime", StringComparison.OrdinalIgnoreCase))
                return ts.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (rawToken.Equals("user", StringComparison.OrdinalIgnoreCase))
                return Environment.UserName;
            if (rawToken.Equals("machine", StringComparison.OrdinalIgnoreCase))
                return Environment.MachineName;
            if (rawToken.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                return Guid.NewGuid().ToString();
            if (rawToken.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
                return clip;

            if (rawToken.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase))
            {
                string field = rawToken[7..].Trim();
                if (promptValues != null)
                {
                    if (promptValues.TryGetValue(field, out string? exact))
                        return exact ?? "";
                    foreach (var kv in promptValues)
                    {
                        if (string.Equals(kv.Key.Trim(), field, StringComparison.OrdinalIgnoreCase))
                            return kv.Value ?? "";
                    }
                }
                return "";
            }

            return match.Value;
        });
    }

    internal static void ExpandAndCopyWithUi(Window? owner, SnippetItem snippet)
    {
        if (snippet == null) return;
        string clip = "";
        try
        {
            if (System.Windows.Clipboard.ContainsText())
                clip = System.Windows.Clipboard.GetText();
        }
        catch (COMException) { }

        var prompts = FindPrompts(snippet.Text);
        if (prompts.Count == 0)
        {
            string expanded = Expand(snippet.Text, clip);
            CopyExpandedAndNotify(owner, snippet.Name, expanded);
            return;
        }

        var palette = DeskSpaceDesignV283.Current;
        double height = Math.Clamp(210 + (prompts.Count * 72), 260, 560);
        var dlg = DeskSpaceFluentBackdropV380.CreateModernWindow(
            $"Expand Snippet · {snippet.Name}", 480, height, owner, transient: true);

        var root = new DockPanel { Margin = new Thickness(18) };
        dlg.Content = root;

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var fieldsStack = new StackPanel();
        scroll.Content = fieldsStack;
        root.Children.Add(scroll);

        fieldsStack.Children.Add(new TextBlock
        {
            Text = "Fill dynamic snippet fields before copying:",
            FontSize = 13,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var inputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        TextBox? firstBox = null;
        foreach (string promptName in prompts)
        {
            fieldsStack.Children.Add(new TextBlock
            {
                Text = promptName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeskSpaceDesignV283.Solid(palette.Text),
                Margin = new Thickness(0, 2, 0, 3)
            });
            var tb = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            DeskSpaceDesignV283.Input(tb, palette);
            AutomationProperties.SetName(tb, promptName);
            firstBox ??= tb;
            inputs[promptName] = tb;
            fieldsStack.Children.Add(tb);
        }

        void Submit()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in inputs)
                values[kv.Key] = kv.Value.Text;
            string expanded = Expand(snippet.Text, clip, values);
            CopyExpandedAndNotify(owner, snippet.Name, expanded);
            dlg.DialogResult = true;
        }

        var cancelBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Cancel", () =>
        {
            dlg.DialogResult = false;
        }, primary: false);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);

        var applyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Expand & Copy", Submit, primary: true);
        applyBtn.IsDefault = true;

        footer.Children.Add(cancelBtn);
        footer.Children.Add(applyBtn);

        dlg.Loaded += (_, _) => firstBox?.Focus();
        dlg.ShowDialog();
    }

    private static void CopyExpandedAndNotify(Window? owner, string name, string expanded)
    {
        try
        {
            System.Windows.Clipboard.SetText(expanded);
            if (owner is MainWindow mw && mw.FindName("StatusText") is TextBlock statusText)
                statusText.Text = $"Expanded & copied snippet '{name}' ({expanded.Length} chars).";
            else
                MessageBox.Show(owner ?? Application.Current?.MainWindow,
                    $"Expanded & copied '{name}' to clipboard.",
                    "DeskSpace Snippets",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }
        catch (COMException)
        {
            MessageBox.Show(owner ?? Application.Current?.MainWindow,
                "Clipboard is currently busy. Please try again.",
                "DeskSpace Snippets",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

internal static class DeskSpaceNotesSnippetsV380
{
    private const string NotesEnhancedTag = "DeskSpaceNotesEnhancedV380";
    private const string SnippetsEnhancedTag = "DeskSpaceSnippetsEnhancedV380";

    internal static void EnhanceMainWindowNotesAndSnippets(
        MainWindow window,
        Func<Workspace> getWorkspace,
        Action saveWorkspace)
    {
        if (window == null) return;
        EnhanceNotesTab(window, getWorkspace);
        EnhanceSnippetsTab(window, getWorkspace);
    }

    private static void EnhanceNotesTab(MainWindow window, Func<Workspace> getWorkspace)
    {
        var notesTab = window.MainTabs.Items.OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Tag as string, "Notes", StringComparison.OrdinalIgnoreCase));
        if (notesTab?.Content is not Grid rootGrid) return;
        if (Equals(rootGrid.Tag, NotesEnhancedTag)) return;

        var leftDock = rootGrid.Children.OfType<DockPanel>().FirstOrDefault(d => Grid.GetColumn(d) == 0);
        var rightDock = rootGrid.Children.OfType<DockPanel>().FirstOrDefault(d => Grid.GetColumn(d) == 2);
        var notesList = window.NotesList;
        var noteTitle = window.NoteTitle;
        var noteContent = window.NoteContent;
        if (leftDock == null || rightDock == null || notesList == null || noteTitle == null || noteContent == null)
            return;

        rootGrid.Tag = NotesEnhancedTag;
        var palette = DeskSpaceDesignV283.Current;

        // 1. Search / #tag filter bar in left DockPanel (between top buttons and NotesList)
        var filterBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 8),
            MinHeight = 32,
            ToolTip = "Filter notes by title, content, or #tag"
        };
        DeskSpaceDesignV283.Input(filterBox, palette);
        AutomationProperties.SetName(filterBox, "Filter notes and tags");
        DockPanel.SetDock(filterBox, Dock.Top);

        int notesListIdx = leftDock.Children.IndexOf(notesList);
        if (notesListIdx >= 0)
            leftDock.Children.Insert(notesListIdx, filterBox);
        else
            leftDock.Children.Add(filterBox);

        var notesMeta = NoteMetaStoreV380.Load();

        void ApplyNoteFilterAndSort()
        {
            var view = CollectionViewSource.GetDefaultView(notesList.ItemsSource);
            if (view == null) return;
            string q = filterBox.Text.Trim();
            view.Filter = obj =>
            {
                if (obj is not NoteItem n) return false;
                if (string.IsNullOrEmpty(q)) return true;
                if (q.StartsWith('#') && q.Length > 1)
                {
                    string targetTag = q[1..];
                    return MarkdownNoteEngineV380.ExtractTags(n.Content)
                        .Any(t => string.Equals(t, targetTag, StringComparison.OrdinalIgnoreCase));
                }
                return (n.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (n.Content?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
            };
            if (view is ListCollectionView lcv)
                lcv.CustomSort = new PinnedNoteComparer(notesMeta, getWorkspace);
            view.Refresh();
        }

        filterBox.TextChanged += (_, _) => ApplyNoteFilterAndSort();

        // 2. Modern toolbar inside rightDock top StackPanel
        var topStack = rightDock.Children.OfType<StackPanel>().FirstOrDefault(s => DockPanel.GetDock(s) == Dock.Top);
        if (topStack == null) return;

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        var statsPill = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 4),
            Background = DeskSpaceDesignV283.Solid(palette.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(palette.Border),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statsText = new TextBlock
        {
            Text = "0 words · 0 chars",
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted)
        };
        statsPill.Child = statsText;

        var tagsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

        bool previewOpen = false;
        Button? pinBtn = null;
        Button? previewBtn = null;

        // 3. Split Editor + Live FlowDocumentScrollViewer without removing NoteContent
        rightDock.Children.Remove(noteContent);
        var splitGrid = new Grid();
        var colEditor = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        var colGap = new ColumnDefinition { Width = new GridLength(0) };
        var colPreview = new ColumnDefinition { Width = new GridLength(0) };
        splitGrid.ColumnDefinitions.Add(colEditor);
        splitGrid.ColumnDefinitions.Add(colGap);
        splitGrid.ColumnDefinitions.Add(colPreview);

        Grid.SetColumn(noteContent, 0);
        splitGrid.Children.Add(noteContent);

        var previewViewer = new FlowDocumentScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsToolBarVisible = false,
            Background = Brushes.Transparent
        };
        var previewCard = DeskSpaceFluentBackdropV380.CreateCard(previewViewer, palette);
        previewCard.Padding = new Thickness(8);
        previewCard.Visibility = Visibility.Collapsed;
        Grid.SetColumn(previewCard, 2);
        splitGrid.Children.Add(previewCard);
        rightDock.Children.Add(splitGrid);

        void RefreshPreviewDocument()
        {
            if (!previewOpen) return;
            var currentPalette = DeskSpaceDesignV283.Current;
            previewViewer.Document = MarkdownNoteEngineV380.RenderToFlowDocument(
                noteContent.Text,
                idx =>
                {
                    string updated = MarkdownNoteEngineV380.ToggleChecklistItem(noteContent.Text, idx);
                    if (!string.Equals(updated, noteContent.Text, StringComparison.Ordinal))
                    {
                        int caret = noteContent.CaretIndex;
                        noteContent.Text = updated;
                        noteContent.CaretIndex = Math.Clamp(caret, 0, updated.Length);
                    }
                },
                currentPalette);
        }

        void UpdateEditorChrome()
        {
            var note = notesList.SelectedItem as NoteItem;
            bool pinned = note != null && notesMeta.TryGetValue(note.Id, out var m) && m.Pinned;
            if (pinBtn != null)
            {
                pinBtn.Content = pinned ? "★ Pinned" : "☆ Pin Note";
                pinBtn.IsEnabled = note != null;
            }

            var (words, chars) = MarkdownNoteEngineV380.WordStats(noteContent.Text);
            var (done, total) = MarkdownNoteEngineV380.CountChecklist(noteContent.Text);
            statsText.Text = total > 0
                ? $"{words} words · {chars} chars · ☑ {done}/{total}"
                : $"{words} words · {chars} chars";

            tagsWrap.Children.Clear();
            foreach (string tag in MarkdownNoteEngineV380.ExtractTags(noteContent.Text).Take(10))
            {
                string captured = tag;
                var chip = DeskSpaceFluentBackdropV380.CreatePillButton(
                    "#" + captured,
                    () =>
                    {
                        filterBox.Text = string.Equals(filterBox.Text.Trim(), "#" + captured, StringComparison.OrdinalIgnoreCase)
                            ? ""
                            : "#" + captured;
                    },
                    primary: false,
                    $"Filter notes by #{captured}");
                chip.Padding = new Thickness(8, 2, 8, 2);
                chip.MinHeight = 24;
                chip.FontSize = 11;
                chip.Margin = new Thickness(0, 0, 6, 4);
                tagsWrap.Children.Add(chip);
            }
        }

        pinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("☆ Pin Note", () =>
        {
            if (notesList.SelectedItem is not NoteItem note) return;
            bool nextPinned = !(notesMeta.TryGetValue(note.Id, out var existing) && existing.Pinned);
            NoteMetaStoreV380.SetPinned(note.Id, nextPinned);
            notesMeta = NoteMetaStoreV380.Load();
            ApplyNoteFilterAndSort();
            UpdateEditorChrome();
        }, primary: false, "Pin this note to the top of the list");
        pinBtn.Margin = new Thickness(0, 0, 8, 4);

        var checklistBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ Checklist", () =>
        {
            if (!noteContent.IsEnabled) return;
            int caret = Math.Clamp(noteContent.CaretIndex, 0, noteContent.Text.Length);
            string prefix = (caret > 0 && noteContent.Text[caret - 1] != '\n') ? "\n" : "";
            string insert = prefix + "- [ ] ";
            noteContent.Text = noteContent.Text.Insert(caret, insert);
            noteContent.CaretIndex = caret + insert.Length;
            noteContent.Focus();
        }, primary: false, "Insert an interactive Markdown checklist item");
        checklistBtn.Margin = new Thickness(0, 0, 8, 4);

        previewBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Preview", () =>
        {
            previewOpen = !previewOpen;
            if (previewBtn != null)
                previewBtn.Content = previewOpen ? "Hide Preview" : "Preview";
            colGap.Width = previewOpen ? new GridLength(12) : new GridLength(0);
            colPreview.Width = previewOpen ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            previewCard.Visibility = previewOpen ? Visibility.Visible : Visibility.Collapsed;
            if (previewOpen) RefreshPreviewDocument();
        }, primary: false, "Toggle live Markdown & interactive checklist preview");
        previewBtn.Margin = new Thickness(0, 0, 8, 4);

        toolbar.Children.Add(pinBtn);
        toolbar.Children.Add(checklistBtn);
        toolbar.Children.Add(previewBtn);
        toolbar.Children.Add(statsPill);

        topStack.Children.Add(toolbar);
        topStack.Children.Add(tagsWrap);

        var previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        previewDebounce.Tick += (_, _) =>
        {
            previewDebounce.Stop();
            UpdateEditorChrome();
            RefreshPreviewDocument();
        };

        noteContent.TextChanged += (_, _) =>
        {
            previewDebounce.Stop();
            previewDebounce.Start();
        };
        notesList.SelectionChanged += (_, _) =>
        {
            previewDebounce.Stop();
            UpdateEditorChrome();
            RefreshPreviewDocument();
        };
        window.Closed += (_, _) => previewDebounce.Stop();

        ApplyNoteFilterAndSort();
        UpdateEditorChrome();
    }

    private static void EnhanceSnippetsTab(MainWindow window, Func<Workspace> getWorkspace)
    {
        var clipTab = window.MainTabs.Items.OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Tag as string, "Clipboard", StringComparison.OrdinalIgnoreCase));
        if (clipTab?.Content is not Grid rootGrid) return;
        if (Equals(rootGrid.Tag, SnippetsEnhancedTag)) return;

        var rightDock = rootGrid.Children.OfType<DockPanel>().FirstOrDefault(d => Grid.GetColumn(d) == 2);
        var topStack = rightDock?.Children.OfType<StackPanel>().FirstOrDefault(s => DockPanel.GetDock(s) == Dock.Top);
        var snippetName = window.SnippetName;
        var snippetContent = window.SnippetContent;
        var snippetsList = window.SnippetsList;
        if (topStack == null || snippetName == null || snippetContent == null || snippetsList == null)
            return;

        rootGrid.Tag = SnippetsEnhancedTag;

        void InsertVariable(string token)
        {
            if (!snippetContent.IsEnabled) return;
            int caret = Math.Clamp(snippetContent.CaretIndex, 0, snippetContent.Text.Length);
            snippetContent.Text = snippetContent.Text.Insert(caret, token);
            snippetContent.CaretIndex = caret + token.Length;
            snippetContent.Focus();
        }

        var modernRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };

        var expandBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Dynamic Expand & Copy", () =>
        {
            var item = snippetsList.SelectedItem as SnippetItem
                ?? new SnippetItem { Name = snippetName.Text, Text = snippetContent.Text };
            DynamicSnippetEngineV380.ExpandAndCopyWithUi(window, item);
        }, primary: true, "Expand {{date}}, {{clipboard}}, {{uuid}}, {{prompt:Name}} and copy");
        expandBtn.Margin = new Thickness(0, 0, 8, 4);
        modernRow.Children.Add(expandBtn);

        foreach (var (label, token) in new[]
        {
            ("+ {{date}}", "{{date}}"),
            ("+ {{time}}", "{{time}}"),
            ("+ {{clipboard}}", "{{clipboard}}"),
            ("+ {{uuid}}", "{{uuid}}"),
            ("+ {{prompt:Name}}", "{{prompt:Name}}")
        })
        {
            string capturedToken = token;
            var varBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                label,
                () => InsertVariable(capturedToken),
                primary: false,
                $"Insert {capturedToken} at cursor");
            varBtn.Margin = new Thickness(0, 0, 6, 4);
            varBtn.Padding = new Thickness(9, 4, 9, 4);
            varBtn.MinHeight = 28;
            varBtn.FontSize = 11.5;
            modernRow.Children.Add(varBtn);
        }

        var pickerBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Snippet Picker...", () =>
        {
            ShowSnippetLauncher(window, getWorkspace);
        }, primary: false, "Search and quickly expand any saved snippet");
        pickerBtn.Margin = new Thickness(0, 0, 8, 4);
        modernRow.Children.Add(pickerBtn);

        var historyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Rich Clipboard History →", () =>
        {
            DeskSpaceRichClipboardV380.Show(window);
        }, primary: false, "Open persistent rich clipboard history and pinned snippets");
        historyBtn.Margin = new Thickness(0, 0, 0, 4);
        modernRow.Children.Add(historyBtn);

        topStack.Children.Add(modernRow);
    }

    internal static void ShowSnippetLauncher(Window? owner, Func<Workspace> getWorkspace)
    {
        var palette = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "Snippet Launcher · Dynamic Templates", 620, 480, owner, transient: true);

        var root = new DockPanel { Margin = new Thickness(18) };
        win.Content = root;

        var topPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(topPanel, Dock.Top);
        topPanel.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Search Snippets (Enter to Expand & Copy)", palette));

        var searchBox = new TextBox { ToolTip = "Type to filter snippets by name or template body" };
        DeskSpaceDesignV283.Input(searchBox, palette);
        AutomationProperties.SetName(searchBox, "Search snippets");
        topPanel.Children.Add(searchBox);
        root.Children.Add(topPanel);

        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var hintText = new TextBlock
        {
            Text = "Supports {{date}}, {{time}}, {{user}}, {{machine}}, {{uuid}}, {{clipboard}}, {{prompt:Name}}",
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actionsPanel, Dock.Right);
        footer.Children.Add(actionsPanel);
        footer.Children.Add(hintText);
        root.Children.Add(footer);

        var list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text)
        };
        var listCard = DeskSpaceFluentBackdropV380.CreateCard(list, palette);
        listCard.Padding = new Thickness(8);
        root.Children.Add(listCard);

        List<SnippetItem> allSnippets = (getWorkspace?.Invoke()?.Snippets ?? new List<SnippetItem>()).ToList();

        void RefreshList()
        {
            string q = searchBox.Text.Trim();
            var filtered = allSnippets
                .Where(s => string.IsNullOrEmpty(q)
                    || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || s.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Select(s => new SnippetLauncherRow(s))
                .ToList();
            list.ItemsSource = filtered;
            if (filtered.Count > 0)
                list.SelectedIndex = 0;
        }

        void ExecuteSelected()
        {
            if (list.SelectedItem is not SnippetLauncherRow row) return;
            win.Hide();
            DynamicSnippetEngineV380.ExpandAndCopyWithUi(owner, row.Item);
            win.Close();
        }

        searchBox.TextChanged += (_, _) => RefreshList();
        searchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && list.Items.Count > 0)
            {
                list.SelectedIndex = Math.Min(list.Items.Count - 1, Math.Max(0, list.SelectedIndex + 1));
                e.Handled = true;
            }
            else if (e.Key == Key.Up && list.Items.Count > 0)
            {
                list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteSelected();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                win.Close();
            }
        };

        list.MouseDoubleClick += (_, _) => ExecuteSelected();
        list.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteSelected();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                win.Close();
            }
        };

        var closeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Close", win.Close, primary: false);
        closeBtn.Margin = new Thickness(0, 0, 8, 0);
        var expandBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Expand & Copy", ExecuteSelected, primary: true);
        actionsPanel.Children.Add(closeBtn);
        actionsPanel.Children.Add(expandBtn);

        RefreshList();
        win.Loaded += (_, _) => searchBox.Focus();
        win.Show();
        win.Activate();
    }

    private sealed class PinnedNoteComparer : IComparer
    {
        private readonly Dictionary<Guid, NoteMetaV380> _meta;
        private readonly Func<Workspace> _getWorkspace;

        public PinnedNoteComparer(Dictionary<Guid, NoteMetaV380> meta, Func<Workspace> getWorkspace)
        {
            _meta = meta;
            _getWorkspace = getWorkspace;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not NoteItem a || y is not NoteItem b) return 0;
            bool pa = _meta.TryGetValue(a.Id, out var ma) && ma.Pinned;
            bool pb = _meta.TryGetValue(b.Id, out var mb) && mb.Pinned;
            if (pa != pb) return pa ? -1 : 1;
            var notes = _getWorkspace?.Invoke()?.Notes;
            if (notes != null)
                return notes.IndexOf(a).CompareTo(notes.IndexOf(b));
            return 0;
        }
    }

    private sealed record SnippetLauncherRow(SnippetItem Item)
    {
        public override string ToString()
        {
            string flat = (Item.Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (flat.Length > 72) flat = flat[..72] + "…";
            var prompts = DynamicSnippetEngineV380.FindPrompts(Item.Text);
            string badge = prompts.Count > 0 ? $"  [{prompts.Count} prompt(s)]" : "";
            return $"{Item.Name}{badge}\n{flat}";
        }
    }
}
