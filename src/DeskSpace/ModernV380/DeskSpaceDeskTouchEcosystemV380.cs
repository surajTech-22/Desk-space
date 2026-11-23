using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskSpace;

internal static class DeskSpaceDeskTouchEcosystemV380
{
    private static Window? _openHubWindow;

    private static readonly Regex OtpKeywordBeforeRegex = new(
        @"(?i)\b(?:otp|one[- ]time(?:\s+(?:password|passcode|pin|code))?|verification\s+code|security\s+code|auth(?:entication)?\s+code|login\s+code|passcode|pin|2fa|mfa|code)\b(?:\s*(?::|=|-|#|is|are)){0,3}\s*(?:G-)?(\d{3,4}[-\s]\d{2,4}|\d{4,8})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex OtpKeywordPhraseBeforeRegex = new(
        @"(?i)\b(?:otp|one[- ]time(?:\s+(?:password|passcode|pin|code))?|verification\s+code|security\s+code|auth(?:entication)?\s+code|login\s+code|passcode|pin|2fa|mfa|code)\b(?:\s+[A-Za-z0-9_-]+){1,4}\s+(?:is|are|:|=)\s*(?:G-)?(\d{3,4}[-\s]\d{2,4}|\d{4,8})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex OtpKeywordAfterRegex = new(
        @"(?i)(?:^|[\s:(])(?:G-)?(\d{3,4}[-\s]\d{2,4}|\d{4,8})\s+(?:is|are|as)\s+(?:your\s+|the\s+|a\s+|an\s+)?(?:[\w-]+\s+){0,3}(?:otp|one[- ]time|verification|security|login|auth|passcode|pin|code)\b",
        RegexOptions.Compiled);

    private static readonly Regex GoogleStyleCodeRegex = new(
        @"(?<![A-Za-z0-9])G-(\d{4,8})(?![\d.-])",
        RegexOptions.Compiled);

    private static readonly Regex StandaloneDigitsRegex = new(
        @"(?<![\d.,₹$€£¥+\-])(\d{4,8})(?![\d.,%])",
        RegexOptions.Compiled);

    private static readonly Regex AnyOtpKeywordRegex = new(
        @"(?i)\b(?:otp|one[- ]time|verification|passcode|security\s+code|auth\s+code|login\s+code|2fa|mfa|pin)\b",
        RegexOptions.Compiled);

    private static readonly Regex CurrencyPrefixRegex = new(
        @"(?i)(?:[₹$€£¥]|\b(?:rs\.?|inr|usd|eur|gbp|cad|aud|aed))\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DateLikeRegex = new(
        @"\b(?:19|20)\d{2}[-/]\d{1,2}[-/]\d{1,2}\b|\b\d{1,2}[-/]\d{1,2}[-/](?:19|20)\d{2}\b",
        RegexOptions.Compiled);

    internal static string? ExtractOtpCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 1. Keyword-before-code patterns ("Your OTP is 482910", "Verification code: 719204", "Passcode 1234", "code is 884210")
        foreach (Match m in OtpKeywordBeforeRegex.Matches(text))
        {
            string? candidate = ValidateCandidate(text, m.Groups[1], hasKeyword: true);
            if (candidate != null) return candidate;
        }
        foreach (Match m in OtpKeywordPhraseBeforeRegex.Matches(text))
        {
            string? candidate = ValidateCandidate(text, m.Groups[1], hasKeyword: true);
            if (candidate != null) return candidate;
        }

        // 2. Code-before-keyword patterns ("G-938471 is your Google verification code", "Use 5591-22 as one-time PIN")
        foreach (Match m in OtpKeywordAfterRegex.Matches(text))
        {
            string? candidate = ValidateCandidate(text, m.Groups[1], hasKeyword: true);
            if (candidate != null) return candidate;
        }

        // 3. Explicit Google-style G-XXXXXX token
        foreach (Match m in GoogleStyleCodeRegex.Matches(text))
        {
            string? candidate = ValidateCandidate(text, m.Groups[1], hasKeyword: true);
            if (candidate != null) return candidate;
        }

        // 4. Standalone numeric candidate (with strict year / currency / phone / date filtering)
        bool textHasOtpKeyword = AnyOtpKeywordRegex.IsMatch(text);
        var candidates = new List<string>();
        foreach (Match m in StandaloneDigitsRegex.Matches(text))
        {
            string? candidate = ValidateCandidate(text, m.Groups[1], hasKeyword: textHasOtpKeyword);
            if (candidate != null)
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return null;

        // Prefer 6-digit OTPs first, then 5/7/8, then 4
        return candidates
            .OrderByDescending(c => c.Length == 6 ? 3 : (c.Length >= 5 ? 2 : 1))
            .First();
    }

    private static string? ValidateCandidate(string fullText, Group group, bool hasKeyword)
    {
        if (!group.Success) return null;
        int start = group.Index;
        int end = group.Index + group.Length;
        string raw = group.Value;

        var digitsSb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsDigit(c)) digitsSb.Append(c);
        }
        string digits = digitsSb.ToString();
        if (digits.Length < 4 || digits.Length > 8)
            return null;

        // Reject decimal / currency fractional suffix like $1250.00
        if (end < fullText.Length && (fullText[end] == '.' || fullText[end] == ',') && end + 1 < fullText.Length && char.IsDigit(fullText[end + 1]))
            return null;
        if (end < fullText.Length && fullText[end] == '%')
            return null;

        // Reject hyphenated neighbors unless explicitly a G- prefix
        if (start > 0 && fullText[start - 1] == '-')
        {
            bool isGPrefix = start >= 2
                && (fullText[start - 2] is 'G' or 'g')
                && (start == 2 || !char.IsLetterOrDigit(fullText[start - 3]));
            if (!isGPrefix) return null;
        }
        if (end < fullText.Length && fullText[end] == '-' && end + 1 < fullText.Length && char.IsDigit(fullText[end + 1]))
            return null;

        // Reject explicit currency prefixes immediately before the number
        int prefixWindowStart = Math.Max(0, start - 10);
        string beforeSnippet = fullText.Substring(prefixWindowStart, start - prefixWindowStart);
        if (CurrencyPrefixRegex.IsMatch(beforeSnippet))
            return null;

        // Reject phone numbers (>= 9 digits across single spaces/hyphens/parentheses)
        if (CountContiguousPhoneRunDigits(fullText, start, end) >= 9)
            return null;

        // Reject date components like 2026-11-23
        foreach (Match dm in DateLikeRegex.Matches(fullText))
        {
            if (start >= dm.Index && end <= dm.Index + dm.Length)
                return null;
        }

        // Reject standalone 4-digit years (1990..2099) unless paired with OTP/PIN/code keywords
        if (!hasKeyword && digits.Length == 4 && int.TryParse(digits, out int year) && year is >= 1990 and <= 2099)
            return null;

        return digits;
    }

    private static int CountContiguousPhoneRunDigits(string text, int start, int end)
    {
        int left = start;
        while (left > 0)
        {
            char c = text[left - 1];
            if (char.IsDigit(c) || c is ' ' or '-' or '(' or ')' or '+')
                left--;
            else
                break;
        }
        int right = end;
        while (right < text.Length)
        {
            char c = text[right];
            if (char.IsDigit(c) || c is ' ' or '-' or '(' or ')')
                right++;
            else
                break;
        }

        int totalDigits = 0;
        for (int i = left; i < right; i++)
        {
            if (char.IsDigit(text[i]))
                totalDigits++;
        }
        return totalDigits;
    }

    internal static void ShowHub(
        Window? owner,
        Func<Workspace>? getWorkspace = null,
        Action? saveWorkspace = null)
    {
        if (_openHubWindow != null)
        {
            try
            {
                if (_openHubWindow.IsLoaded)
                {
                    if (_openHubWindow.WindowState == WindowState.Minimized)
                        _openHubWindow.WindowState = WindowState.Normal;
                    _openHubWindow.Activate();
                    return;
                }
            }
            catch
            {
                _openHubWindow = null;
            }
        }

        var palette = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskTouch Ecosystem Hub · Devices, OTP & Camera OCR",
            980, 700, owner, transient: false);
        win.MinWidth = 780;
        win.MinHeight = 560;
        _openHubWindow = win;

        var root = new DockPanel { Margin = new Thickness(18) };
        win.Content = root;

        // Top header
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var launchVisionBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Open DeskTouch Vision",
            DeskSpaceDeskTouchBridgeV378.OpenDeskTouchAsync,
            primary: false,
            "Launch or focus DeskTouch Vision companion");
        DockPanel.SetDock(launchVisionBtn, Dock.Right);
        header.Children.Add(launchVisionBtn);

        var headerInfo = new StackPanel();
        headerInfo.Children.Add(new TextBlock
        {
            Text = "DeskTouch Ecosystem Hub",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text)
        });
        var bridgeStatusText = new TextBlock
        {
            Text = "Checking live DeskTouch Vision devices…",
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted)
        };
        headerInfo.Children.Add(bridgeStatusText);
        header.Children.Add(headerInfo);

        var mainScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var bodyStack = new StackPanel { Margin = new Thickness(0, 0, 6, 8) };
        mainScroll.Content = bodyStack;
        root.Children.Add(mainScroll);

        // 1. Live Connected Devices Strip
        var devicesStripInner = new StackPanel();
        devicesStripInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Connected Devices (Click to Switch Control · Drop Files to Send)", palette));
        var devicesWrap = new WrapPanel();
        devicesStripInner.Children.Add(devicesWrap);
        var devicesCard = DeskSpaceFluentBackdropV380.CreateCard(devicesStripInner, palette);
        devicesCard.Margin = new Thickness(0, 0, 0, 14);
        bodyStack.Children.Add(devicesCard);

        // Middle 2-column grid: Left = Notifications & OTP Grabber, Right = Instant Send & Phone OCR -> DataTab
        var midGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bodyStack.Children.Add(midGrid);

        // 2. Notifications & OTP Grabber Card
        var otpInner = new StackPanel();
        otpInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Notifications & Smart OTP Grabber", palette));

        var notifActionsRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var openNotifBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open Phone Notifications", () =>
        {
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            int targetId = ResolveRemoteTargetId(snap, preferAndroid: true);
            DeskSpaceDeskTouchBridgeV378.OpenNotificationsAsync(targetId);
        }, primary: false, "Open mirrored notifications for linked Android phone");
        openNotifBtn.Margin = new Thickness(0, 0, 8, 6);

        var paySafeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Toggle Pay-Safe", () =>
        {
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            int targetId = ResolveRemoteTargetId(snap, preferAndroid: true);
            DeskSpaceDeskTouchBridgeV378.TogglePayModeAsync(targetId);
        }, primary: false, "Pause or resume phone Accessibility during sensitive payments");
        paySafeBtn.Margin = new Thickness(0, 0, 0, 6);

        notifActionsRow.Children.Add(openNotifBtn);
        notifActionsRow.Children.Add(paySafeBtn);
        otpInner.Children.Add(notifActionsRow);

        otpInner.Children.Add(new TextBlock
        {
            Text = "Scan Clipboard / Pasted SMS for OTP",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var smsInputBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 84,
            MaxHeight = 130,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "Paste an SMS or notification text, or click Scan Clipboard"
        };
        DeskSpaceDesignV283.Input(smsInputBox, palette);
        AutomationProperties.SetName(smsInputBox, "Paste SMS or notification text for OTP detection");
        otpInner.Children.Add(smsInputBox);

        var otpResultRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var extractedOtpText = new TextBlock
        {
            Text = "No OTP detected yet",
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            VerticalAlignment = VerticalAlignment.Center
        };
        string? detectedOtp = null;

        var copyOtpBtn = DeskSpaceFluentBackdropV380.CreatePillButton("One-Click Copy OTP", () =>
        {
            if (string.IsNullOrEmpty(detectedOtp)) return;
            try
            {
                System.Windows.Clipboard.SetText(detectedOtp);
                bridgeStatusText.Text = $"Copied OTP {detectedOtp} to clipboard.";
            }
            catch (COMException)
            {
                bridgeStatusText.Text = "Clipboard busy; try again.";
            }
        }, primary: true, "Copy extracted OTP digits to clipboard");
        copyOtpBtn.IsEnabled = false;
        DockPanel.SetDock(copyOtpBtn, Dock.Right);
        otpResultRow.Children.Add(copyOtpBtn);
        otpResultRow.Children.Add(extractedOtpText);
        otpInner.Children.Add(otpResultRow);

        void RunOtpScan()
        {
            detectedOtp = ExtractOtpCode(smsInputBox.Text);
            if (!string.IsNullOrEmpty(detectedOtp))
            {
                extractedOtpText.Text = detectedOtp;
                extractedOtpText.Foreground = DeskSpaceDesignV283.Solid(palette.Accent);
                copyOtpBtn.IsEnabled = true;
            }
            else
            {
                extractedOtpText.Text = string.IsNullOrWhiteSpace(smsInputBox.Text)
                    ? "No OTP detected yet"
                    : "No valid 4–8 digit OTP found";
                extractedOtpText.Foreground = DeskSpaceDesignV283.Solid(palette.Muted);
                copyOtpBtn.IsEnabled = false;
            }
        }

        smsInputBox.TextChanged += (_, _) => RunOtpScan();

        var scanClipBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Scan Clipboard", () =>
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText())
                {
                    bridgeStatusText.Text = "Clipboard has no text to scan for OTP.";
                    return;
                }
                smsInputBox.Text = System.Windows.Clipboard.GetText();
                RunOtpScan();
            }
            catch (COMException)
            {
                bridgeStatusText.Text = "Clipboard is busy; retry in a moment.";
            }
        }, primary: false, "Read current clipboard text and extract OTP");
        otpInner.Children.Add(scanClipBtn);

        var otpCard = DeskSpaceFluentBackdropV380.CreateCard(otpInner, palette);
        Grid.SetColumn(otpCard, 0);
        midGrid.Children.Add(otpCard);

        // 3. Instant Send & Phone Camera OCR -> DataTab Card
        var sendInner = new StackPanel();
        sendInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Instant Send & Phone Camera OCR → DataTab", palette));

        var dropWellText = new TextBlock
        {
            Text = "Drop any file here to send to active DeskTouch device",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        var dropWell = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 16, 12, 16),
            Margin = new Thickness(0, 0, 0, 8),
            Background = DeskSpaceDesignV283.Solid(palette.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(palette.Border),
            BorderThickness = new Thickness(1.5),
            AllowDrop = true,
            Child = dropWellText
        };

        void SendFileToTarget(string path, int? explicitTargetId = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            int targetId = explicitTargetId ?? ResolveRemoteTargetId(snap, preferAndroid: false);
            DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(targetId, path);
            bridgeStatusText.Text = $"Dispatched '{Path.GetFileName(path)}' to DeskTouch target #{targetId}.";
        }

        dropWell.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                dropWell.BorderBrush = DeskSpaceDesignV283.Solid(palette.Accent);
                e.Handled = true;
            }
        };
        dropWell.DragLeave += (_, _) => dropWell.BorderBrush = DeskSpaceDesignV283.Solid(palette.Border);
        dropWell.Drop += (_, e) =>
        {
            dropWell.BorderBrush = DeskSpaceDesignV283.Solid(palette.Border);
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                SendFileToTarget(files[0]);
                e.Handled = true;
            }
        };
        sendInner.Children.Add(dropWell);

        var fileSendRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var pickSendFileBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Pick File to Send...", () =>
        {
            var dlg = new OpenFileDialog { Title = "Select file to send via DeskTouch", CheckFileExists = true };
            if (dlg.ShowDialog(win) == true)
                SendFileToTarget(dlg.FileName);
        }, primary: false);
        fileSendRow.Children.Add(pickSendFileBtn);
        sendInner.Children.Add(fileSendRow);

        // Send text / link handoff
        sendInner.Children.Add(new TextBlock
        {
            Text = "Send Text / Link Handoff to Active Device",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Margin = new Thickness(0, 0, 0, 4)
        });
        var handoffBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 6),
            ToolTip = "Type a link or quick note to hand off to your active device"
        };
        DeskSpaceDesignV283.Input(handoffBox, palette);
        sendInner.Children.Add(handoffBox);

        var handoffBtns = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var copyHandoffBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Share via Clipboard", () =>
        {
            string val = handoffBox.Text.Trim();
            if (string.IsNullOrEmpty(val)) return;
            try
            {
                System.Windows.Clipboard.SetText(val);
                var snap = DeskSpaceDeskTouchBridgeV378.Current;
                bridgeStatusText.Text = snap.ClipboardSyncEnabled
                    ? "Copied text to DeskTouch shared clipboard."
                    : "Copied locally (DeskTouch Clipboard Sync is currently paused).";
            }
            catch (COMException)
            {
                bridgeStatusText.Text = "Clipboard busy; retry.";
            }
        }, primary: false);
        copyHandoffBtn.Margin = new Thickness(0, 0, 8, 4);

        var fileHandoffBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Send as Note File", () =>
        {
            string val = handoffBox.Text.Trim();
            if (string.IsNullOrEmpty(val)) return;
            string dir = Path.Combine(WorkspaceStore.Root, "Handoff");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"DeskSpace-Handoff-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, val, new UTF8Encoding(false));
            SendFileToTarget(file);
            handoffBox.Clear();
        }, primary: false);
        fileHandoffBtn.Margin = new Thickness(0, 0, 8, 4);
        handoffBtns.Children.Add(copyHandoffBtn);
        handoffBtns.Children.Add(fileHandoffBtn);
        sendInner.Children.Add(handoffBtns);

        // Scan Image / Photo -> Table -> DataTab
        sendInner.Children.Add(new TextBlock
        {
            Text = "Scan Image / Photo → Table → DataTab",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Margin = new Thickness(0, 0, 0, 4)
        });

        List<List<string>>? scannedTable = null;
        string? scannedImagePath = null;

        var ocrPreviewBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            MinHeight = 68,
            MaxHeight = 110,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 8),
            Text = "Pick an image or phone scan to extract an editable table preview."
        };
        DeskSpaceDesignV283.Input(ocrPreviewBox, palette);

        Button? saveDataTabBtn = null;
        var ocrBtns = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        var scanImageBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Scan Image / Photo...", async () =>
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select image or phone camera scan",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(win) != true) return;
            scannedImagePath = dlg.FileName;
            ocrPreviewBox.Text = "Running OCR table recognition…";
            if (saveDataTabBtn != null) saveDataTabBtn.IsEnabled = false;
            try
            {
                var rows = await DeskSpaceScreenToolsV380.RecognizeTableFromFileAsync(scannedImagePath);
                scannedTable = rows;
                int maxCols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
                ocrPreviewBox.Text = rows.Count == 0
                    ? "No tabular rows recognized in this image."
                    : $"Recognized {rows.Count} row(s) × {maxCols} col(s):\n" + DeskSpaceScreenToolsV380.GridToTsv(rows);
                if (saveDataTabBtn != null) saveDataTabBtn.IsEnabled = rows.Count > 0;
            }
            catch (Exception ex)
            {
                scannedTable = null;
                ocrPreviewBox.Text = "OCR failed: " + ex.Message;
            }
        }, primary: false);
        scanImageBtn.Margin = new Thickness(0, 0, 8, 4);

        saveDataTabBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save → DataTab", () =>
        {
            if (scannedTable == null || scannedTable.Count == 0) return;
            try
            {
                var store = new WorkspaceStore();
                var ws = getWorkspace?.Invoke() ?? store.Load();
                string? storedImg = !string.IsNullOrEmpty(scannedImagePath) && File.Exists(scannedImagePath)
                    ? store.ImportAttachment(scannedImagePath)
                    : null;
                string baseName = !string.IsNullOrEmpty(scannedImagePath)
                    ? Path.GetFileNameWithoutExtension(scannedImagePath)
                    : "Phone Scan";
                var tab = new DataTab
                {
                    Name = $"{baseName} (Scan {DateTime.Now:HH:mm})",
                    Cells = scannedTable.Select(r => new List<string>(r)).ToList(),
                    SourceImage = storedImg,
                    OcrNeedsReview = true
                };
                ws.DataTabs.Add(tab);

                if (saveWorkspace != null)
                    saveWorkspace();
                else
                    store.Save(ws);

                var mainWin = owner as MainWindow ?? Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWin != null)
                {
                    mainWin.DataTabList.Items.Refresh();
                    mainWin.DataTabList.SelectedItem = tab;
                    mainWin.MainTabs.SelectedItem = mainWin.DataTabPage;
                }
                bridgeStatusText.Text = $"Saved '{tab.Name}' to Custom DataTabs (marked OcrNeedsReview).";
            }
            catch (Exception ex)
            {
                bridgeStatusText.Text = "Failed to save DataTab: " + ex.Message;
            }
        }, primary: true, "Save recognized table directly to DeskSpace Custom DataTabs");
        saveDataTabBtn.IsEnabled = false;
        saveDataTabBtn.Margin = new Thickness(0, 0, 0, 4);

        ocrBtns.Children.Add(scanImageBtn);
        ocrBtns.Children.Add(saveDataTabBtn);
        sendInner.Children.Add(ocrBtns);
        sendInner.Children.Add(ocrPreviewBox);

        var sendCard = DeskSpaceFluentBackdropV380.CreateCard(sendInner, palette);
        Grid.SetColumn(sendCard, 2);
        midGrid.Children.Add(sendCard);

        // 4. Ecosystem Quick Controls Card
        var controlsInner = new StackPanel();
        controlsInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Ecosystem Quick Controls", palette));
        var controlsWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        var profileButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        foreach (string prof in new[] { "Work", "Presentation", "Away" })
        {
            string captured = prof;
            var pb = DeskSpaceFluentBackdropV380.CreatePillButton(
                captured,
                () => DeskSpaceDeskTouchBridgeV378.SwitchProfileAsync(captured),
                primary: false,
                $"Switch DeskTouch profile to {captured}");
            pb.Margin = new Thickness(0, 0, 6, 6);
            profileButtons[captured] = pb;
            controlsWrap.Children.Add(pb);
        }

        var clipToggleBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Clipboard Sync: On",
            DeskSpaceDeskTouchBridgeV378.ToggleClipboardAsync,
            primary: false,
            "Toggle shared clipboard sync across linked devices");
        clipToggleBtn.Margin = new Thickness(8, 0, 8, 6);
        controlsWrap.Children.Add(clipToggleBtn);

        var mediaBtn = DeskSpaceFluentBackdropV380.CreatePillButton("⏯ Media Play/Pause", () =>
        {
            int targetId = ResolveRemoteTargetId(DeskSpaceDeskTouchBridgeV378.Current, preferAndroid: false);
            DeskSpaceDeskTouchBridgeV378.SendMediaAsync(targetId, "play-pause");
        }, primary: false, "Send Play/Pause media command to active device");
        mediaBtn.Margin = new Thickness(0, 0, 8, 6);
        controlsWrap.Children.Add(mediaBtn);

        var wakeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("☀ Wake Linked PCs", () =>
        {
            DeskSpaceDeskTouchBridgeV378.SendPowerAsync(0, "wake");
        }, primary: false, "Send Wake signal to linked PCs");
        wakeBtn.Margin = new Thickness(0, 0, 8, 6);
        controlsWrap.Children.Add(wakeBtn);

        var lockPcsBtn = DeskSpaceFluentBackdropV380.CreatePillButton("🔒 Lock All PCs", () =>
        {
            if (MessageBox.Show(win, "Lock all linked computers now?", "DeskTouch Ecosystem", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                DeskSpaceDeskTouchBridgeV378.SendPowerAsync(0, "lock-all");
        }, primary: false, "Lock all linked computers");
        lockPcsBtn.Margin = new Thickness(0, 0, 0, 6);
        controlsWrap.Children.Add(lockPcsBtn);

        controlsInner.Children.Add(controlsWrap);
        bodyStack.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(controlsInner, palette));

        void RefreshFromBridge()
        {
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            var p = DeskSpaceDesignV283.Current;

            bridgeStatusText.Text = snap.IsLive || snap.ConnectedCount > 0
                ? $"Live · {snap.ConnectedCount} connected · Active: {snap.ActiveDeviceName} · Profile: {snap.ActiveProfile}"
                : "DeskTouch Vision not detected yet — click Open DeskTouch Vision or wait for peer link.";

            clipToggleBtn.Content = snap.ClipboardSyncEnabled ? "Clipboard Sync: On" : "Clipboard Sync: Paused";

            foreach (var kv in profileButtons)
            {
                bool activeProf = string.Equals(snap.ActiveProfile, kv.Key, StringComparison.OrdinalIgnoreCase);
                kv.Value.Background = DeskSpaceDesignV283.Solid(activeProf ? p.Accent : DeskSpaceDesignV283.SecondaryButtonBg(p));
                kv.Value.Foreground = DeskSpaceDesignV283.Solid(activeProf ? DeskSpaceDesignV283.OnAccent(p.Accent, p.HighContrast) : p.Text);
                kv.Value.BorderBrush = DeskSpaceDesignV283.Solid(activeProf ? p.Accent : p.Border);
            }

            devicesWrap.Children.Clear();
            if (snap.Devices.Count == 0)
            {
                devicesWrap.Children.Add(new TextBlock
                {
                    Text = "No connected DeskTouch devices reported yet.",
                    FontSize = 12.5,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            foreach (var device in snap.Devices)
            {
                var captured = device;
                bool active = device.IsControlling || (device.IsSelected && device.IsLocal);
                var tileStack = new StackPanel { MinWidth = 175, MaxWidth = 225 };

                var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var platformBadge = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(6, 1, 6, 1),
                    Background = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Surface),
                    Child = new TextBlock
                    {
                        Text = device.Platform,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(active ? DeskSpaceDesignV283.OnAccent(p.Accent, p.HighContrast) : p.Text)
                    }
                };
                DockPanel.SetDock(platformBadge, Dock.Right);
                topRow.Children.Add(platformBadge);

                topRow.Children.Add(new TextBlock
                {
                    Text = device.IsLocal ? $"{device.Name} (Local)" : device.Name,
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                tileStack.Children.Add(topRow);

                var metaParts = new List<string>();
                if (device.BatteryPercent >= 0)
                    metaParts.Add((device.IsCharging ? "⚡ " : "") + $"{device.BatteryPercent}%");
                if (!string.IsNullOrWhiteSpace(device.SignalLabel))
                    metaParts.Add(device.SignalLabel);
                if (device.NotificationCount >= 0)
                    metaParts.Add($"{device.NotificationCount} notif");
                if (active)
                    metaParts.Add("Controlling");

                tileStack.Children.Add(new TextBlock
                {
                    Text = metaParts.Count > 0 ? string.Join(" · ", metaParts) : (string.IsNullOrWhiteSpace(device.StatusText) ? "Ready" : device.StatusText),
                    FontSize = 11,
                    Foreground = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Muted),
                    TextWrapping = TextWrapping.Wrap
                });

                var tileBorder = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(11, 9, 11, 9),
                    Margin = new Thickness(0, 0, 10, 8),
                    Background = DeskSpaceDesignV283.Solid(active ? p.AccentSoft : p.Surface),
                    BorderBrush = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Border),
                    BorderThickness = new Thickness(active ? 2 : 1),
                    Cursor = device.SupportsInput ? Cursors.Hand : Cursors.Arrow,
                    AllowDrop = true,
                    ToolTip = device.IsLocal
                        ? "Click to return mouse & keyboard to this PC"
                        : $"Click to control {device.Name} · Drop file to send",
                    Child = tileStack
                };

                tileBorder.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    if (captured.IsLocal) DeskSpaceDeskTouchBridgeV378.ReturnLocalAsync();
                    else if (captured.SupportsInput) DeskSpaceDeskTouchBridgeV378.SwitchDeviceAsync(captured.Id);
                };
                tileBorder.DragOver += (_, e) =>
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        e.Effects = DragDropEffects.Copy;
                        e.Handled = true;
                    }
                };
                tileBorder.Drop += (_, e) =>
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                        e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                        files.Length > 0)
                    {
                        SendFileToTarget(files[0], captured.Id);
                        e.Handled = true;
                    }
                };

                devicesWrap.Children.Add(tileBorder);
            }
        }

        void OnBridgeChanged()
        {
            try
            {
                win.Dispatcher.BeginInvoke(RefreshFromBridge);
            }
            catch { }
        }

        DeskSpaceDeskTouchBridgeV378.Changed += OnBridgeChanged;
        var pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        pollTimer.Tick += (_, _) =>
        {
            if (win.IsVisible)
                DeskSpaceDeskTouchBridgeV378.PollIfDueAsync();
        };
        pollTimer.Start();

        win.Closed += (_, _) =>
        {
            pollTimer.Stop();
            DeskSpaceDeskTouchBridgeV378.Changed -= OnBridgeChanged;
            if (ReferenceEquals(_openHubWindow, win))
                _openHubWindow = null;
        };

        DeskSpaceDeskTouchBridgeV378.PollIfDueAsync(force: true);
        RefreshFromBridge();
        win.Show();
        win.Activate();
    }

    private static int ResolveRemoteTargetId(DeskTouchBridgeSnapshotV378 snap, bool preferAndroid)
    {
        if (preferAndroid)
        {
            var phone = snap.Devices.FirstOrDefault(
                d => !d.IsLocal && string.Equals(d.Platform, "Android", StringComparison.OrdinalIgnoreCase));
            if (phone != null) return phone.Id;
        }
        if (snap.ActiveTargetId != int.MinValue)
            return snap.ActiveTargetId;
        return snap.Devices.FirstOrDefault(d => !d.IsLocal)?.Id ?? 0;
    }
}
