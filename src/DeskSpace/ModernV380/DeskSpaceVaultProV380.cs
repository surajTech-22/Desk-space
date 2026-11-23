using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Security.Credentials.UI;

namespace DeskSpace;

internal static class DeskSpaceVaultProV380
{
    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x01;
    private static readonly TimeSpan IdleLockTimeout = TimeSpan.FromMinutes(10);
    private static Window? _openWindow;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    internal static byte[] DpapiProtectCurrentUser(byte[] plain)
    {
        if (plain == null) throw new ArgumentNullException(nameof(plain));
        var inBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();
        try
        {
            inBlob.cbData = plain.Length;
            if (plain.Length > 0)
            {
                inBlob.pbData = Marshal.AllocHGlobal(plain.Length);
                if (inBlob.pbData == IntPtr.Zero)
                    throw new OutOfMemoryException("Unable to allocate unmanaged buffer for DPAPI protection.");
                Marshal.Copy(plain, 0, inBlob.pbData, plain.Length);
            }

            if (!CryptProtectData(ref inBlob, "DeskSpaceVaultPro", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out outBlob))
            {
                int err = Marshal.GetLastWin32Error();
                throw new CryptographicException($"DPAPI CryptProtectData failed with Win32 error {err}.");
            }
            if (outBlob.pbData == IntPtr.Zero || outBlob.cbData < 0)
                throw new CryptographicException("DPAPI CryptProtectData returned an invalid buffer.");

            var protectedBytes = new byte[outBlob.cbData];
            if (outBlob.cbData > 0)
                Marshal.Copy(outBlob.pbData, protectedBytes, 0, outBlob.cbData);
            return protectedBytes;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero)
            {
                ZeroUnmanaged(inBlob.pbData, inBlob.cbData);
                Marshal.FreeHGlobal(inBlob.pbData);
            }
            if (outBlob.pbData != IntPtr.Zero)
                LocalFree(outBlob.pbData);
        }
    }

    internal static byte[] DpapiUnprotectCurrentUser(byte[] blob)
    {
        if (blob == null || blob.Length == 0)
            throw new CryptographicException("DPAPI protected payload is empty.");

        var inBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();
        try
        {
            inBlob.cbData = blob.Length;
            inBlob.pbData = Marshal.AllocHGlobal(blob.Length);
            if (inBlob.pbData == IntPtr.Zero)
                throw new OutOfMemoryException("Unable to allocate unmanaged buffer for DPAPI unprotection.");
            Marshal.Copy(blob, 0, inBlob.pbData, blob.Length);

            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out outBlob))
            {
                int err = Marshal.GetLastWin32Error();
                throw new CryptographicException($"DPAPI CryptUnprotectData failed with Win32 error {err}.");
            }
            if (outBlob.pbData == IntPtr.Zero || outBlob.cbData < 0)
                throw new CryptographicException("DPAPI CryptUnprotectData returned an invalid buffer.");

            var plainBytes = new byte[outBlob.cbData];
            if (outBlob.cbData > 0)
                Marshal.Copy(outBlob.pbData, plainBytes, 0, outBlob.cbData);
            return plainBytes;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero)
            {
                ZeroUnmanaged(outBlob.pbData, outBlob.cbData);
                LocalFree(outBlob.pbData);
            }
        }
    }

    private static void ZeroUnmanaged(IntPtr ptr, int length)
    {
        if (ptr == IntPtr.Zero || length <= 0) return;
        for (int i = 0; i < length; i++)
            Marshal.WriteByte(ptr, i, 0);
    }

    internal static void Show(Window? owner)
    {
        if (_openWindow != null)
        {
            try
            {
                if (_openWindow.IsLoaded)
                {
                    if (_openWindow.WindowState == WindowState.Minimized)
                        _openWindow.WindowState = WindowState.Normal;
                    _openWindow.Activate();
                    return;
                }
            }
            catch
            {
                _openWindow = null;
            }
        }

        var palette = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace Vault Pro · Passwords, 2FA & Encrypted Backup",
            960, 680, owner, transient: false);
        win.MinWidth = 780;
        win.MinHeight = 540;
        _openWindow = win;

        var vault = new DeskSpaceVaultV10();
        var totpStore = new TotpSidecarStoreV380();
        byte[]? sessionMasterDpapi = null;
        Guid? selectedEntryId = null;
        DateTime lastActivityUtc = DateTime.UtcNow;
        bool showSecretPlain = false;
        bool helloAvailable = false;

        var root = new DockPanel { Margin = new Thickness(18) };
        win.Content = root;

        // Header bar
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var headerRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(headerRight, Dock.Right);
        header.Children.Add(headerRight);

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Background = DeskSpaceDesignV283.Solid(palette.AccentSoft),
            BorderBrush = DeskSpaceDesignV283.Solid(palette.Accent),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusBadgeText = new TextBlock
        {
            Text = vault.IsInitialized ? "Locked" : "New Vault",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text)
        };
        statusBadge.Child = statusBadgeText;
        headerRight.Children.Add(statusBadge);

        Button? lockNowBtn = null;
        var headerLeft = new StackPanel();
        headerLeft.Children.Add(new TextBlock
        {
            Text = "Vault Pro & Security Studio",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text)
        });
        headerLeft.Children.Add(new TextBlock
        {
            Text = "AES-256-GCM · 600k PBKDF2-SHA256 · RFC 6238 TOTP · Win32 DPAPI + Windows Hello",
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted)
        });
        header.Children.Add(headerLeft);

        // Navigation tabs
        var navRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(navRow, Dock.Top);
        root.Children.Add(navRow);

        var contentHost = new Grid();
        root.Children.Add(contentHost);

        var vaultTabView = new Grid();
        var generatorTabView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
        var backupTabView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
        contentHost.Children.Add(vaultTabView);
        contentHost.Children.Add(generatorTabView);
        contentHost.Children.Add(backupTabView);

        string activeTab = "vault";
        Button? tabVaultBtn = null, tabGenBtn = null, tabBackupBtn = null;

        void SelectTab(string tab)
        {
            activeTab = tab;
            vaultTabView.Visibility = tab == "vault" ? Visibility.Visible : Visibility.Collapsed;
            generatorTabView.Visibility = tab == "generator" ? Visibility.Visible : Visibility.Collapsed;
            backupTabView.Visibility = tab == "backup" ? Visibility.Visible : Visibility.Collapsed;
            void StyleTab(Button? b, bool active)
            {
                if (b == null) return;
                b.Background = DeskSpaceDesignV283.Solid(active ? palette.Accent : palette.Raised);
                b.Foreground = DeskSpaceDesignV283.Solid(active && !palette.HighContrast ? Color.FromRgb(8, 16, 33) : palette.Text);
                b.BorderBrush = DeskSpaceDesignV283.Solid(active ? palette.Accent : palette.Border);
            }
            StyleTab(tabVaultBtn, tab == "vault");
            StyleTab(tabGenBtn, tab == "generator");
            StyleTab(tabBackupBtn, tab == "backup");
        }

        tabVaultBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Vault & 2FA", () => SelectTab("vault"), primary: true, "Passwords and live TOTP 2FA codes");
        tabVaultBtn.Margin = new Thickness(0, 0, 8, 0);
        tabGenBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Generator", () => SelectTab("generator"), primary: false, "Generate strong passwords and passphrases");
        tabGenBtn.Margin = new Thickness(0, 0, 8, 0);
        tabBackupBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Encrypted Backup", () => SelectTab("backup"), primary: false, "Export or restore encrypted workspace backups");
        navRow.Children.Add(tabVaultBtn);
        navRow.Children.Add(tabGenBtn);
        navRow.Children.Add(tabBackupBtn);

        // --- TAB 1: VAULT & 2FA ---
        var lockedPanel = new StackPanel
        {
            MaxWidth = 500,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var lockHeadline = new TextBlock
        {
            Text = vault.IsInitialized ? "Unlock Vault Pro" : "Create Your Encrypted Vault",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var lockHint = new TextBlock
        {
            Text = vault.IsInitialized
                ? "Enter your master password or use Windows Hello if enrolled."
                : "Choose a strong master password (12+ characters). It never leaves your device.",
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var masterPwdBox = new PasswordBox { MinHeight = 36, Margin = new Thickness(0, 0, 0, 10) };
        AutomationProperties.SetName(masterPwdBox, "Vault master password");

        var lockErrorText = new TextBlock
        {
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(Color.FromRgb(248, 113, 113)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = Visibility.Collapsed
        };

        var lockButtons = new WrapPanel();
        Button? unlockBtn = null;
        Button? helloUnlockBtn = null;

        lockedPanel.Children.Add(lockHeadline);
        lockedPanel.Children.Add(lockHint);
        lockedPanel.Children.Add(masterPwdBox);
        lockedPanel.Children.Add(lockErrorText);
        lockedPanel.Children.Add(lockButtons);
        var lockedCard = DeskSpaceFluentBackdropV380.CreateCard(lockedPanel, palette);
        lockedCard.HorizontalAlignment = HorizontalAlignment.Center;
        lockedCard.VerticalAlignment = VerticalAlignment.Center;
        lockedCard.MinWidth = 460;

        var unlockedGrid = new Grid { Visibility = Visibility.Collapsed };
        unlockedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        unlockedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var vaultTopBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(vaultTopBar, 0);
        unlockedGrid.Children.Add(vaultTopBar);

        var vaultTopRight = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(vaultTopRight, Dock.Right);
        vaultTopBar.Children.Add(vaultTopRight);

        var searchBox = new TextBox
        {
            MinHeight = 34,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = "Search entries by label, username, or website"
        };
        DeskSpaceDesignV283.Input(searchBox, palette);
        AutomationProperties.SetName(searchBox, "Search vault entries");
        vaultTopBar.Children.Add(searchBox);

        var bodyGrid = new Grid();
        Grid.SetRow(bodyGrid, 1);
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        unlockedGrid.Children.Add(bodyGrid);

        var entriesList = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text)
        };
        AutomationProperties.SetName(entriesList, "Vault secret entries");
        var listCard = DeskSpaceFluentBackdropV380.CreateCard(entriesList, palette);
        listCard.Padding = new Thickness(8);
        Grid.SetColumn(listCard, 0);
        bodyGrid.Children.Add(listCard);

        var editorScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var editorStack = new StackPanel();
        editorScroll.Content = editorStack;
        var editorCard = DeskSpaceFluentBackdropV380.CreateCard(editorScroll, palette);
        Grid.SetColumn(editorCard, 2);
        bodyGrid.Children.Add(editorCard);

        TextBox MakeField(string placeholderTip)
        {
            var tb = new TextBox { Margin = new Thickness(0, 2, 0, 8), ToolTip = placeholderTip };
            DeskSpaceDesignV283.Input(tb, palette);
            return tb;
        }

        editorStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Entry Details", palette));
        editorStack.Children.Add(new TextBlock { Text = "Label / Service", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        var labelBox = MakeField("Account or service name (required)");
        editorStack.Children.Add(labelBox);

        editorStack.Children.Add(new TextBlock { Text = "Username / Email", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        var userBox = MakeField("Username or email address");
        editorStack.Children.Add(userBox);

        editorStack.Children.Add(new TextBlock { Text = "Password", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        var pwdRow = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var entrySecretBox = new PasswordBox { MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center };
        var entrySecretPlainBox = new TextBox { Visibility = Visibility.Collapsed };
        DeskSpaceDesignV283.Input(entrySecretPlainBox, palette);
        Grid.SetColumn(entrySecretBox, 0);
        Grid.SetColumn(entrySecretPlainBox, 0);
        pwdRow.Children.Add(entrySecretBox);
        pwdRow.Children.Add(entrySecretPlainBox);

        string GetEntryPassword() => showSecretPlain ? entrySecretPlainBox.Text : entrySecretBox.Password;
        void SetEntryPassword(string value)
        {
            entrySecretBox.Password = value;
            entrySecretPlainBox.Text = value;
        }

        Button? toggleRevealBtn = null;
        toggleRevealBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Show", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            if (!showSecretPlain)
            {
                entrySecretPlainBox.Text = entrySecretBox.Password;
                entrySecretBox.Visibility = Visibility.Collapsed;
                entrySecretPlainBox.Visibility = Visibility.Visible;
                showSecretPlain = true;
                if (toggleRevealBtn != null) toggleRevealBtn.Content = "Hide";
            }
            else
            {
                entrySecretBox.Password = entrySecretPlainBox.Text;
                entrySecretPlainBox.Visibility = Visibility.Collapsed;
                entrySecretBox.Visibility = Visibility.Visible;
                showSecretPlain = false;
                if (toggleRevealBtn != null) toggleRevealBtn.Content = "Show";
            }
        }, primary: false, "Toggle password visibility");
        toggleRevealBtn.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(toggleRevealBtn, 1);
        pwdRow.Children.Add(toggleRevealBtn);

        var genInlineBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Generate", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            SetEntryPassword(PasswordGeneratorV380.GeneratePassword(20, true, true, true, true));
        }, primary: false, "Generate a random 20-character strong password");
        genInlineBtn.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(genInlineBtn, 2);
        pwdRow.Children.Add(genInlineBtn);
        editorStack.Children.Add(pwdRow);

        editorStack.Children.Add(new TextBlock { Text = "Website URL", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        var siteBox = MakeField("https://example.com");
        editorStack.Children.Add(siteBox);

        editorStack.Children.Add(new TextBlock { Text = "2FA / TOTP Secret (optional Base32 or otpauth:// URI)", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        var totpSecretBox = MakeField("e.g. JBSWY3DPEHPK3PXP or otpauth://totp/...");
        editorStack.Children.Add(totpSecretBox);

        // Live TOTP 2FA Panel
        var totpLiveContainer = new StackPanel { Margin = new Thickness(0, 4, 0, 10), Visibility = Visibility.Collapsed };
        var totpCodeRow = new DockPanel();
        var totpCopyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy 2FA", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            string raw = totpSecretBox.Text.Trim();
            if (TryExtractTotpConfig(raw, out string secret, out int digits, out int period))
            {
                string code = TotpEngineV380.GenerateCode(secret, DateTimeOffset.UtcNow, digits, period);
                CopySensitiveValue(code);
            }
        }, primary: false, "Copy current 2FA code (excluded from clipboard history and auto-cleared in 30s)");
        DockPanel.SetDock(totpCopyBtn, Dock.Right);
        totpCodeRow.Children.Add(totpCopyBtn);

        var totpCodeText = new TextBlock
        {
            Text = "------",
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Accent),
            VerticalAlignment = VerticalAlignment.Center
        };
        totpCodeRow.Children.Add(totpCodeText);
        totpLiveContainer.Children.Add(totpCodeRow);

        var totpCountdownBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 30,
            Value = 30,
            Height = 5,
            Margin = new Thickness(0, 6, 0, 3),
            Foreground = DeskSpaceDesignV283.Solid(palette.Accent),
            Background = DeskSpaceDesignV283.Solid(palette.Surface)
        };
        var totpCountdownLabel = new TextBlock
        {
            Text = "Refreshes in 30s",
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted)
        };
        totpLiveContainer.Children.Add(totpCountdownBar);
        totpLiveContainer.Children.Add(totpCountdownLabel);

        var totpCard = DeskSpaceFluentBackdropV380.CreateCard(totpLiveContainer, palette);
        totpCard.Padding = new Thickness(12);
        totpCard.Margin = new Thickness(0, 2, 0, 10);
        totpCard.Visibility = Visibility.Collapsed;
        editorStack.Children.Add(totpCard);

        var entryFeedbackText = new TextBlock
        {
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        editorStack.Children.Add(entryFeedbackText);

        var entryActions = new WrapPanel();
        editorStack.Children.Add(entryActions);

        vaultTabView.Children.Add(lockedCard);
        vaultTabView.Children.Add(unlockedGrid);

        void UpdateTotpLiveView()
        {
            if (!vault.IsUnlocked)
            {
                totpCard.Visibility = Visibility.Collapsed;
                totpLiveContainer.Visibility = Visibility.Collapsed;
                return;
            }
            string raw = totpSecretBox.Text.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                totpCard.Visibility = Visibility.Collapsed;
                totpLiveContainer.Visibility = Visibility.Collapsed;
                return;
            }
            if (TryExtractTotpConfig(raw, out string secret, out int digits, out int period))
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    string code = TotpEngineV380.GenerateCode(secret, now, digits, period);
                    int remaining = TotpEngineV380.SecondsRemaining(now, period);
                    totpCodeText.Text = code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;
                    totpCountdownBar.Maximum = period;
                    totpCountdownBar.Value = remaining;
                    totpCountdownLabel.Text = $"2FA active ({digits} digits · {period}s) · refreshes in {remaining}s";
                    totpCard.Visibility = Visibility.Visible;
                    totpLiveContainer.Visibility = Visibility.Visible;
                }
                catch
                {
                    totpCard.Visibility = Visibility.Collapsed;
                    totpLiveContainer.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                totpCodeText.Text = "Invalid Base32 / URI";
                totpCountdownLabel.Text = "Check your Base32 secret or otpauth:// URI.";
                totpCountdownBar.Value = 0;
                totpCard.Visibility = Visibility.Visible;
                totpLiveContainer.Visibility = Visibility.Visible;
            }
        }

        totpSecretBox.TextChanged += (_, _) => UpdateTotpLiveView();

        void ClearEditorFields()
        {
            selectedEntryId = null;
            labelBox.Clear();
            userBox.Clear();
            SetEntryPassword("");
            siteBox.Clear();
            totpSecretBox.Clear();
            entryFeedbackText.Text = "";
            UpdateTotpLiveView();
        }

        void RefreshEntriesList()
        {
            if (!vault.IsUnlocked)
            {
                entriesList.ItemsSource = Array.Empty<VaultEntryRow>();
                return;
            }
            string query = searchBox.Text.Trim();
            var rows = vault.Entries
                .Where(e => string.IsNullOrEmpty(query)
                    || e.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || e.Username.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || e.Website.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
                .Select(e => new VaultEntryRow(e.Id, e.Label, e.Username, totpStore.HasSecret(e.Id)))
                .ToList();
            entriesList.ItemsSource = rows;
            if (selectedEntryId.HasValue)
            {
                var match = rows.FirstOrDefault(r => r.Id == selectedEntryId.Value);
                if (match != null) entriesList.SelectedItem = match;
            }
        }

        searchBox.TextChanged += (_, _) =>
        {
            lastActivityUtc = DateTime.UtcNow;
            RefreshEntriesList();
        };

        entriesList.SelectionChanged += (_, _) =>
        {
            lastActivityUtc = DateTime.UtcNow;
            if (!vault.IsUnlocked || entriesList.SelectedItem is not VaultEntryRow row) return;
            var secret = vault.Entries.FirstOrDefault(e => e.Id == row.Id);
            if (secret == null) return;
            selectedEntryId = secret.Id;
            labelBox.Text = secret.Label;
            userBox.Text = secret.Username;
            SetEntryPassword(secret.Password);
            siteBox.Text = secret.Website;
            totpSecretBox.Text = totpStore.GetSecret(secret.Id) ?? "";
            entryFeedbackText.Text = "";
            UpdateTotpLiveView();
        };

        void LockVault(string reasonText)
        {
            vault.Lock();
            totpStore.Lock();
            if (sessionMasterDpapi != null)
            {
                CryptographicOperations.ZeroMemory(sessionMasterDpapi);
                sessionMasterDpapi = null;
            }
            masterPwdBox.Clear();
            ClearEditorFields();
            entriesList.ItemsSource = Array.Empty<VaultEntryRow>();
            if (showSecretPlain && toggleRevealBtn != null)
            {
                showSecretPlain = false;
                entrySecretPlainBox.Visibility = Visibility.Collapsed;
                entrySecretBox.Visibility = Visibility.Visible;
                toggleRevealBtn.Content = "Show";
            }
            unlockedGrid.Visibility = Visibility.Collapsed;
            lockedCard.Visibility = Visibility.Visible;
            if (lockNowBtn != null) lockNowBtn.Visibility = Visibility.Collapsed;
            statusBadgeText.Text = reasonText;
            lockHeadline.Text = vault.IsInitialized ? "Unlock Vault Pro" : "Create Your Encrypted Vault";
            if (unlockBtn != null) unlockBtn.Content = vault.IsInitialized ? "Unlock Vault" : "Create Vault";
            if (helloUnlockBtn != null)
                helloUnlockBtn.Visibility = WindowsHelloUnlockV380.IsEnrolled && helloAvailable ? Visibility.Visible : Visibility.Collapsed;
        }

        Button? helloToggleBtn = null;
        void UpdateHelloToggleLabel()
        {
            if (helloToggleBtn == null) return;
            helloToggleBtn.Content = WindowsHelloUnlockV380.IsEnrolled
                ? "Disable Windows Hello"
                : "Enable Windows Hello Quick Unlock";
            helloToggleBtn.IsEnabled = helloAvailable || WindowsHelloUnlockV380.IsEnrolled;
        }

        bool PerformUnlock(string masterPassword)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(masterPassword);
            try
            {
                vault.Unlock(masterPassword);
                totpStore.Unlock(masterPassword);

                if (sessionMasterDpapi != null)
                    CryptographicOperations.ZeroMemory(sessionMasterDpapi);
                sessionMasterDpapi = DpapiProtectCurrentUser(utf8);

                lastActivityUtc = DateTime.UtcNow;
                lockErrorText.Visibility = Visibility.Collapsed;
                lockedCard.Visibility = Visibility.Collapsed;
                unlockedGrid.Visibility = Visibility.Visible;
                if (lockNowBtn != null) lockNowBtn.Visibility = Visibility.Visible;
                statusBadgeText.Text = "Unlocked · Auto-locks after 10m idle or screen lock";
                UpdateHelloToggleLabel();
                RefreshEntriesList();
                return true;
            }
            catch (Exception ex)
            {
                vault.Lock();
                totpStore.Lock();
                lockErrorText.Text = ex.Message;
                lockErrorText.Visibility = Visibility.Visible;
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(utf8);
                masterPwdBox.Clear();
            }
        }

        unlockBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            vault.IsInitialized ? "Unlock Vault" : "Create Vault",
            () => PerformUnlock(masterPwdBox.Password),
            primary: true,
            "Decrypt vault using PBKDF2 + AES-256-GCM");
        unlockBtn.Margin = new Thickness(0, 0, 8, 6);
        lockButtons.Children.Add(unlockBtn);

        helloUnlockBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Unlock with Windows Hello",
            async () =>
            {
                try
                {
                    var res = await WindowsHelloUnlockV380.UnlockWithHelloDetailedAsync();
                    if (!res.Ok || string.IsNullOrEmpty(res.Password))
                    {
                        lockErrorText.Text = res.Message;
                        lockErrorText.Visibility = Visibility.Visible;
                        return;
                    }
                    PerformUnlock(res.Password);
                }
                catch (Exception ex)
                {
                    lockErrorText.Text = "Windows Hello failed: " + ex.Message;
                    lockErrorText.Visibility = Visibility.Visible;
                }
            },
            primary: false,
            "Verify your identity via Windows Hello to unlock Vault Pro");
        helloUnlockBtn.Margin = new Thickness(0, 0, 8, 6);
        helloUnlockBtn.Visibility = Visibility.Collapsed;
        lockButtons.Children.Add(helloUnlockBtn);

        masterPwdBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                PerformUnlock(masterPwdBox.Password);
            }
        };

        lockNowBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Lock Now",
            () => LockVault("Locked"),
            primary: false,
            "Immediately zero keys and lock Vault Pro");
        lockNowBtn.Visibility = Visibility.Collapsed;
        headerRight.Children.Add(lockNowBtn);

        var newEntryBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ New Entry", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            entriesList.SelectedItem = null;
            ClearEditorFields();
            labelBox.Focus();
        }, primary: true, "Create a new vault entry");
        newEntryBtn.Margin = new Thickness(0, 0, 8, 0);
        vaultTopRight.Children.Add(newEntryBtn);

        helloToggleBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Enable Windows Hello Quick Unlock", async () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            try
            {
                if (WindowsHelloUnlockV380.IsEnrolled)
                {
                    WindowsHelloUnlockV380.Disable();
                    UpdateHelloToggleLabel();
                    entryFeedbackText.Text = "Windows Hello Quick Unlock disabled and DPAPI token removed.";
                    return;
                }
                if (sessionMasterDpapi == null || !vault.IsUnlocked) return;
                var available = await WindowsHelloUnlockV380.IsAvailableAsync();
                if (!available)
                {
                    entryFeedbackText.Text = "Windows Hello is not configured on this Windows account.";
                    return;
                }
                var verify = await WindowsHelloUnlockV380.RequestConsentAsync("Enable Windows Hello Quick Unlock for DeskSpace Vault Pro");
                if (!verify.Ok)
                {
                    entryFeedbackText.Text = verify.Message;
                    return;
                }
                byte[]? pwdBytes = null;
                try
                {
                    pwdBytes = DpapiUnprotectCurrentUser(sessionMasterDpapi);
                    string pwd = Encoding.UTF8.GetString(pwdBytes);
                    WindowsHelloUnlockV380.Enroll(pwd);
                    UpdateHelloToggleLabel();
                    entryFeedbackText.Text = "Windows Hello Quick Unlock enabled using DPAPI protection.";
                }
                finally
                {
                    if (pwdBytes != null) CryptographicOperations.ZeroMemory(pwdBytes);
                }
            }
            catch (Exception ex)
            {
                entryFeedbackText.Text = "Windows Hello setup failed: " + ex.Message;
            }
        }, primary: false, "Store DPAPI-protected quick unlock token gated by Windows Hello");
        vaultTopRight.Children.Add(helloToggleBtn);

        var copyUserBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy User", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            if (string.IsNullOrEmpty(userBox.Text)) return;
            try
            {
                System.Windows.Clipboard.SetText(userBox.Text);
                entryFeedbackText.Text = "Username copied to clipboard.";
            }
            catch (COMException)
            {
                entryFeedbackText.Text = "Clipboard is busy; please try again.";
            }
        }, primary: false, "Copy username to clipboard");
        copyUserBtn.Margin = new Thickness(0, 0, 8, 6);
        entryActions.Children.Add(copyUserBtn);

        var copyPassBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Pass", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            string pwd = GetEntryPassword();
            if (string.IsNullOrEmpty(pwd)) return;
            CopySensitiveValue(pwd);
            entryFeedbackText.Text = "Password copied sensitively (auto-clears in 30s, excluded from clipboard history).";
        }, primary: false, "Copy password safely with 30s auto-clear");
        copyPassBtn.Margin = new Thickness(0, 0, 8, 6);
        entryActions.Children.Add(copyPassBtn);

        var saveEntryBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Entry", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            if (!vault.IsUnlocked) return;
            try
            {
                string rawTotp = totpSecretBox.Text.Trim();
                string? normalizedSecret = null;
                if (!string.IsNullOrEmpty(rawTotp))
                {
                    if (!TryExtractTotpConfig(rawTotp, out string extracted, out _, out _))
                        throw new FormatException("Invalid TOTP Base32 secret or otpauth:// URI.");
                    _ = TotpEngineV380.DecodeBase32(extracted);
                    normalizedSecret = extracted;
                }

                var id = selectedEntryId ?? Guid.NewGuid();
                vault.Upsert(new DeskSpaceVaultV10.Secret
                {
                    Id = id,
                    Label = labelBox.Text.Trim(),
                    Username = userBox.Text.Trim(),
                    Password = GetEntryPassword(),
                    Website = siteBox.Text.Trim()
                });
                totpStore.SetSecret(id, normalizedSecret);
                selectedEntryId = id;
                RefreshEntriesList();
                UpdateTotpLiveView();
                entryFeedbackText.Text = "Entry encrypted and saved.";
            }
            catch (Exception ex)
            {
                entryFeedbackText.Text = "Save error: " + ex.Message;
            }
        }, primary: true, "Encrypt and save entry to Vault Pro");
        saveEntryBtn.Margin = new Thickness(0, 0, 8, 6);
        entryActions.Children.Add(saveEntryBtn);

        var deleteEntryBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Delete Entry", () =>
        {
            lastActivityUtc = DateTime.UtcNow;
            if (!vault.IsUnlocked || selectedEntryId is not Guid id) return;
            if (MessageBox.Show(win, "Delete this encrypted entry?", "Vault Pro", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            vault.Delete(id);
            totpStore.SetSecret(id, null);
            ClearEditorFields();
            RefreshEntriesList();
            entryFeedbackText.Text = "Entry deleted.";
        }, primary: false, "Permanently remove entry from vault");
        deleteEntryBtn.Margin = new Thickness(0, 0, 8, 6);
        entryActions.Children.Add(deleteEntryBtn);

        // --- TAB 2: PASSWORD & PASSPHRASE GENERATOR ---
        var genStack = new StackPanel { Margin = new Thickness(0, 0, 8, 12) };
        generatorTabView.Content = genStack;

        var pwdGenInner = new StackPanel();
        pwdGenInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Cryptographic Password Generator", palette));

        var lenHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var lenValueText = new TextBlock { Text = "Length: 20", FontWeight = FontWeights.SemiBold, Foreground = DeskSpaceDesignV283.Solid(palette.Text) };
        lenHeader.Children.Add(lenValueText);
        pwdGenInner.Children.Add(lenHeader);

        var lenSlider = new Slider { Minimum = 8, Maximum = 64, Value = 20, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(0, 0, 0, 8) };
        pwdGenInner.Children.Add(lenSlider);

        var chkRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var chkUpper = new CheckBox { Content = "Uppercase (A-Z)", IsChecked = true, Foreground = DeskSpaceDesignV283.Solid(palette.Text), Margin = new Thickness(0, 0, 16, 4) };
        var chkLower = new CheckBox { Content = "Lowercase (a-z)", IsChecked = true, Foreground = DeskSpaceDesignV283.Solid(palette.Text), Margin = new Thickness(0, 0, 16, 4) };
        var chkDigits = new CheckBox { Content = "Digits (0-9)", IsChecked = true, Foreground = DeskSpaceDesignV283.Solid(palette.Text), Margin = new Thickness(0, 0, 16, 4) };
        var chkSymbols = new CheckBox { Content = "Symbols (!@#...)", IsChecked = true, Foreground = DeskSpaceDesignV283.Solid(palette.Text), Margin = new Thickness(0, 0, 16, 4) };
        chkRow.Children.Add(chkUpper);
        chkRow.Children.Add(chkLower);
        chkRow.Children.Add(chkDigits);
        chkRow.Children.Add(chkSymbols);
        pwdGenInner.Children.Add(chkRow);

        var genPwdBox = new TextBox
        {
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 15,
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DeskSpaceDesignV283.Input(genPwdBox, palette);
        pwdGenInner.Children.Add(genPwdBox);

        var entropyBar = new ProgressBar { Minimum = 0, Maximum = 140, Height = 6, Margin = new Thickness(0, 0, 0, 4) };
        var entropyLabel = new TextBlock { FontSize = 12, Foreground = DeskSpaceDesignV283.Solid(palette.Muted), Margin = new Thickness(0, 0, 0, 10) };
        pwdGenInner.Children.Add(entropyBar);
        pwdGenInner.Children.Add(entropyLabel);

        void RefreshGeneratedPassword()
        {
            int length = (int)Math.Round(lenSlider.Value);
            lenValueText.Text = $"Length: {length}";
            bool u = chkUpper.IsChecked == true;
            bool l = chkLower.IsChecked == true;
            bool d = chkDigits.IsChecked == true;
            bool s = chkSymbols.IsChecked == true;
            if (!u && !l && !d && !s)
            {
                l = true;
                chkLower.IsChecked = true;
            }
            string pw = PasswordGeneratorV380.GeneratePassword(length, u, l, d, s);
            genPwdBox.Text = pw;
            var (bits, rating) = PasswordGeneratorV380.EstimateEntropy(pw);
            entropyBar.Value = Math.Min(140, bits);
            entropyBar.Foreground = DeskSpaceDesignV283.Solid(rating switch
            {
                "Very Strong" => Color.FromRgb(52, 211, 153),
                "Strong" => palette.Accent,
                "Fair" => Color.FromRgb(251, 191, 36),
                _ => Color.FromRgb(248, 113, 113)
            });
            entropyLabel.Text = $"Estimated entropy: {bits:0.0} bits · {rating}";
        }

        lenSlider.ValueChanged += (_, _) => RefreshGeneratedPassword();
        chkUpper.Click += (_, _) => RefreshGeneratedPassword();
        chkLower.Click += (_, _) => RefreshGeneratedPassword();
        chkDigits.Click += (_, _) => RefreshGeneratedPassword();
        chkSymbols.Click += (_, _) => RefreshGeneratedPassword();

        var pwdGenBtns = new WrapPanel();
        var regenPwdBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Regenerate", RefreshGeneratedPassword, primary: false);
        regenPwdBtn.Margin = new Thickness(0, 0, 8, 0);
        var copyGenPwdBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Password", () => CopySensitiveValue(genPwdBox.Text), primary: true);
        pwdGenBtns.Children.Add(regenPwdBtn);
        pwdGenBtns.Children.Add(copyGenPwdBtn);
        pwdGenInner.Children.Add(pwdGenBtns);

        var pwdGenCard = DeskSpaceFluentBackdropV380.CreateCard(pwdGenInner, palette);
        pwdGenCard.Margin = new Thickness(0, 0, 0, 14);
        genStack.Children.Add(pwdGenCard);

        // Passphrase sub-card
        var phraseInner = new StackPanel();
        phraseInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Diceware-Style Passphrase Generator (256-word dictionary)", palette));

        var wordsLabel = new TextBlock { Text = "Words: 5", FontWeight = FontWeights.SemiBold, Foreground = DeskSpaceDesignV283.Solid(palette.Text), Margin = new Thickness(0, 0, 0, 4) };
        var wordsSlider = new Slider { Minimum = 3, Maximum = 10, Value = 5, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(0, 0, 0, 8) };
        var sepCombo = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), ItemsSource = new[] { "-", ".", "_", " " }, SelectedIndex = 0 };
        var phraseBox = new TextBox { FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"), FontSize = 15, IsReadOnly = true, Margin = new Thickness(0, 0, 0, 6) };
        DeskSpaceDesignV283.Input(phraseBox, palette);
        var phraseInfo = new TextBlock { FontSize = 12, Foreground = DeskSpaceDesignV283.Solid(palette.Muted), Margin = new Thickness(0, 0, 0, 10) };

        void RefreshPassphrase()
        {
            int count = (int)Math.Round(wordsSlider.Value);
            wordsLabel.Text = $"Words: {count}";
            string sep = sepCombo.SelectedItem as string ?? "-";
            string phrase = PasswordGeneratorV380.GeneratePassphrase(count, sep);
            phraseBox.Text = phrase;
            double wordBits = count * 8.0; // log2(256) = 8 bits per word
            phraseInfo.Text = $"Dictionary search space: {wordBits:0} bits ({count} words × 256 words)";
        }

        wordsSlider.ValueChanged += (_, _) => RefreshPassphrase();
        sepCombo.SelectionChanged += (_, _) => RefreshPassphrase();

        var phraseBtns = new WrapPanel();
        var regenPhraseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Regenerate", RefreshPassphrase, primary: false);
        regenPhraseBtn.Margin = new Thickness(0, 0, 8, 0);
        var copyPhraseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Passphrase", () => CopySensitiveValue(phraseBox.Text), primary: true);
        phraseBtns.Children.Add(regenPhraseBtn);
        phraseBtns.Children.Add(copyPhraseBtn);

        phraseInner.Children.Add(wordsLabel);
        phraseInner.Children.Add(wordsSlider);
        phraseInner.Children.Add(new TextBlock { Text = "Separator", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        phraseInner.Children.Add(sepCombo);
        phraseInner.Children.Add(phraseBox);
        phraseInner.Children.Add(phraseInfo);
        phraseInner.Children.Add(phraseBtns);

        genStack.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(phraseInner, palette));

        // --- TAB 3: ENCRYPTED WORKSPACE BACKUP & RESTORE ---
        var backupStack = new StackPanel { Margin = new Thickness(0, 0, 8, 12) };
        backupTabView.Content = backupStack;

        var exportInner = new StackPanel();
        exportInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Export Encrypted Workspace Backup", palette));
        exportInner.Children.Add(new TextBlock
        {
            Text = "Bundles workspace.json and safe appearance/focus/notes/routines settings inside an AES-256-GCM encrypted archive (600k PBKDF2-SHA256).",
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            Margin = new Thickness(0, 0, 0, 10)
        });
        var expPass1 = new PasswordBox { MinHeight = 34, Margin = new Thickness(0, 2, 0, 8) };
        var expPass2 = new PasswordBox { MinHeight = 34, Margin = new Thickness(0, 2, 0, 10) };
        var expStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DeskSpaceDesignV283.Solid(palette.Muted), Margin = new Thickness(0, 8, 0, 0) };
        exportInner.Children.Add(new TextBlock { Text = "Backup Passphrase (8+ characters)", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        exportInner.Children.Add(expPass1);
        exportInner.Children.Add(new TextBlock { Text = "Confirm Passphrase", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        exportInner.Children.Add(expPass2);
        exportInner.Children.Add(DeskSpaceFluentBackdropV380.CreatePillButton("Export Encrypted Backup (.dsbak)...", () =>
        {
            string p1 = expPass1.Password;
            string p2 = expPass2.Password;
            if (p1.Length < 8)
            {
                expStatus.Text = "Passphrase must be at least 8 characters.";
                return;
            }
            if (!string.Equals(p1, p2, StringComparison.Ordinal))
            {
                expStatus.Text = "Passphrases do not match.";
                return;
            }
            var dlg = new SaveFileDialog
            {
                Title = "Save Encrypted DeskSpace Backup",
                Filter = "DeskSpace Encrypted Backup (*.dsbak)|*.dsbak|All files (*.*)|*.*",
                FileName = $"DeskSpace-Backup-{DateTime.Now:yyyyMMdd}.dsbak"
            };
            if (dlg.ShowDialog(win) != true) return;
            try
            {
                EncryptedWorkspaceBackupV380.ExportEncryptedBackup(dlg.FileName, p1);
                expPass1.Clear();
                expPass2.Clear();
                expStatus.Text = $"Encrypted backup saved cleanly to {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                expStatus.Text = "Export failed: " + ex.Message;
            }
        }, primary: true));
        exportInner.Children.Add(expStatus);
        var exportCard = DeskSpaceFluentBackdropV380.CreateCard(exportInner, palette);
        exportCard.Margin = new Thickness(0, 0, 0, 14);
        backupStack.Children.Add(exportCard);

        var importInner = new StackPanel();
        importInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Restore Workspace from Encrypted Backup", palette));
        importInner.Children.Add(new TextBlock
        {
            Text = "Automatically creates a timestamped .pre-restore.bak copy of your existing workspace.json before restoring.",
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            Margin = new Thickness(0, 0, 0, 10)
        });
        var impFileBox = MakeField("Path to .dsbak backup archive");
        var pickFileBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Browse...", () =>
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Encrypted DeskSpace Backup",
                Filter = "DeskSpace Encrypted Backup (*.dsbak)|*.dsbak|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(win) == true)
                impFileBox.Text = dlg.FileName;
        }, primary: false);
        var impRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(pickFileBtn, Dock.Right);
        pickFileBtn.Margin = new Thickness(8, 0, 0, 0);
        impRow.Children.Add(pickFileBtn);
        impRow.Children.Add(impFileBox);

        var impPass = new PasswordBox { MinHeight = 34, Margin = new Thickness(0, 2, 0, 10) };
        var impStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DeskSpaceDesignV283.Solid(palette.Muted), Margin = new Thickness(0, 8, 0, 0) };
        importInner.Children.Add(impRow);
        importInner.Children.Add(new TextBlock { Text = "Backup Passphrase", FontSize = 11.5, Foreground = DeskSpaceDesignV283.Solid(palette.Muted) });
        importInner.Children.Add(impPass);
        importInner.Children.Add(DeskSpaceFluentBackdropV380.CreatePillButton("Restore Backup", () =>
        {
            try
            {
                string summary = EncryptedWorkspaceBackupV380.ImportEncryptedBackup(impFileBox.Text.Trim(), impPass.Password);
                impPass.Clear();
                impStatus.Text = summary;
            }
            catch (Exception ex)
            {
                impStatus.Text = "Restore failed: " + ex.Message;
            }
        }, primary: false));
        importInner.Children.Add(impStatus);
        backupStack.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(importInner, palette));

        // Timers & OS SessionSwitch Auto-Lock
        void TouchActivity(object? sender, RoutedEventArgs e) => lastActivityUtc = DateTime.UtcNow;
        win.PreviewMouseDown += TouchActivity;
        win.PreviewKeyDown += TouchActivity;

        SessionSwitchEventHandler onSessionSwitch = (_, e) =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect)
            {
                win.Dispatcher.BeginInvoke(() =>
                {
                    if (vault.IsUnlocked)
                        LockVault("Locked (Windows session locked)");
                });
            }
        };
        SystemEvents.SessionSwitch += onSessionSwitch;

        var totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        totpTimer.Tick += (_, _) =>
        {
            if (vault.IsUnlocked && totpCard.Visibility == Visibility.Visible)
                UpdateTotpLiveView();
        };
        totpTimer.Start();

        var idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        idleTimer.Tick += (_, _) =>
        {
            if (vault.IsUnlocked && DateTime.UtcNow - lastActivityUtc >= IdleLockTimeout)
                LockVault("Locked (10m inactivity)");
        };
        idleTimer.Start();

        win.Closed += (_, _) =>
        {
            totpTimer.Stop();
            idleTimer.Stop();
            SystemEvents.SessionSwitch -= onSessionSwitch;
            win.PreviewMouseDown -= TouchActivity;
            win.PreviewKeyDown -= TouchActivity;
            LockVault("Closed");
            vault.Dispose();
            totpStore.Dispose();
            if (ReferenceEquals(_openWindow, win))
                _openWindow = null;
        };

        RefreshGeneratedPassword();
        RefreshPassphrase();
        SelectTab("vault");

        win.Loaded += async (_, _) =>
        {
            helloAvailable = await WindowsHelloUnlockV380.IsAvailableAsync();
            if (!vault.IsUnlocked && helloUnlockBtn != null)
                helloUnlockBtn.Visibility = WindowsHelloUnlockV380.IsEnrolled && helloAvailable ? Visibility.Visible : Visibility.Collapsed;
            UpdateHelloToggleLabel();
            masterPwdBox.Focus();
        };

        win.Show();
        win.Activate();
    }

    private static void CopySensitiveValue(string secret)
    {
        DeskSpaceRichClipboardV380.NotifySensitiveCopy(secret);
        DeskSpaceClipboardV10.CopySensitive(secret);
    }

    private static bool TryExtractTotpConfig(string raw, out string base32Secret, out int digits, out int period)
    {
        base32Secret = "";
        digits = 6;
        period = 30;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        if (raw.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return false;
            string query = uri.Query.TrimStart('?');
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = Uri.UnescapeDataString(part[..eq]);
                string val = Uri.UnescapeDataString(part[(eq + 1)..]);
                if (key.Equals("secret", StringComparison.OrdinalIgnoreCase))
                    base32Secret = val.Trim();
                else if (key.Equals("digits", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int d) && d is >= 6 and <= 8)
                    digits = d;
                else if (key.Equals("period", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int p) && p is >= 10 and <= 120)
                    period = p;
            }
            if (string.IsNullOrWhiteSpace(base32Secret)) return false;
            try
            {
                _ = TotpEngineV380.DecodeBase32(base32Secret);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            _ = TotpEngineV380.DecodeBase32(raw);
            base32Secret = raw.Trim();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record VaultEntryRow(Guid Id, string Label, string Username, bool HasTotp)
    {
        public override string ToString()
        {
            string badge = HasTotp ? " [2FA]" : "";
            return string.IsNullOrWhiteSpace(Username) ? $"{Label}{badge}" : $"{Label}{badge}\n{Username}";
        }
    }
}

internal static class TotpEngineV380
{
    internal static byte[] DecodeBase32(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Base32 secret is empty.");

        var cleaned = new StringBuilder(input.Length);
        bool seenPadding = false;
        foreach (char ch in input)
        {
            if (char.IsWhiteSpace(ch) || ch == '-') continue;
            if (ch == '=')
            {
                seenPadding = true;
                continue;
            }
            if (seenPadding)
                throw new FormatException("Invalid Base32 padding placement.");
            cleaned.Append(char.ToUpperInvariant(ch));
        }

        if (cleaned.Length == 0)
            throw new FormatException("Base32 secret contains no data.");

        var output = new List<byte>((cleaned.Length * 5) / 8);
        int buffer = 0;
        int bitsLeft = 0;

        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            int val = c switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= '2' and <= '7' => 26 + (c - '2'),
                _ => throw new FormatException($"Invalid Base32 character '{c}'.")
            };

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
                buffer &= (1 << bitsLeft) - 1;
            }
        }

        if (output.Count == 0)
            throw new FormatException("Base32 secret is too short to decode a byte.");

        return output.ToArray();
    }

    internal static string GenerateCode(string base32Secret, DateTimeOffset utcNow, int digits = 6, int periodSeconds = 30)
    {
        if (digits < 4 || digits > 10)
            throw new ArgumentOutOfRangeException(nameof(digits), "TOTP digits must be between 4 and 10.");
        if (periodSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(periodSeconds), "TOTP period must be positive.");

        byte[] key = DecodeBase32(base32Secret);
        try
        {
            long unix = utcNow.ToUnixTimeSeconds();
            long counter = unix >= 0 ? unix / periodSeconds : (unix - periodSeconds + 1) / periodSeconds;
            Span<byte> counterBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

            byte[] hash = HMACSHA1.HashData(key, counterBytes);
            int offset = hash[^1] & 0x0F;
            int binary =
                ((hash[offset] & 0x7F) << 24) |
                ((hash[offset + 1] & 0xFF) << 16) |
                ((hash[offset + 2] & 0xFF) << 8) |
                (hash[offset + 3] & 0xFF);

            long mod = 1;
            for (int i = 0; i < digits; i++) mod *= 10;
            long otp = binary % mod;
            return otp.ToString("D" + digits, CultureInfo.InvariantCulture);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static int SecondsRemaining(DateTimeOffset utcNow, int periodSeconds = 30)
    {
        if (periodSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(periodSeconds), "TOTP period must be positive.");
        long unix = utcNow.ToUnixTimeSeconds();
        int elapsed = (int)(((unix % periodSeconds) + periodSeconds) % periodSeconds);
        return periodSeconds - elapsed;
    }
}

internal sealed class TotpSidecarStoreV380 : IDisposable
{
    private sealed class Envelope
    {
        public int Format { get; set; } = 1;
        public int Iterations { get; set; } = 600000;
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Ciphertext { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    private readonly string _path;
    private byte[]? _key;
    private byte[]? _salt;
    private Dictionary<Guid, string> _secrets = new();
    private bool _disposed;

    internal TotpSidecarStoreV380(string? storagePath = null)
    {
        _path = storagePath ?? Path.Combine(WorkspaceStore.Root, "vault-totp-v380.enc");
    }

    internal bool IsUnlocked => _key != null && !_disposed;

    internal void Unlock(string masterPassword)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TotpSidecarStoreV380));
        if (string.IsNullOrEmpty(masterPassword))
            throw new ArgumentException("Master password is required.", nameof(masterPassword));

        byte[]? derived = null;
        try
        {
            if (!File.Exists(_path))
            {
                _salt = RandomNumberGenerator.GetBytes(16);
                derived = Rfc2898DeriveBytes.Pbkdf2(masterPassword, _salt, 600000, HashAlgorithmName.SHA256, 32);
                _secrets = new Dictionary<Guid, string>();
                _key = derived;
                derived = null;
                return;
            }

            var doc = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(_path, Encoding.UTF8))
                ?? throw new CryptographicException("Invalid TOTP sidecar header.");
            if (doc.Format != 1 || doc.Iterations < 200000 || doc.Iterations > 3000000)
                throw new CryptographicException("Unsupported TOTP sidecar format.");

            byte[] localSalt = Convert.FromBase64String(doc.Salt);
            byte[] nonce = Convert.FromBase64String(doc.Nonce);
            byte[] ciphertext = Convert.FromBase64String(doc.Ciphertext);
            byte[] tag = Convert.FromBase64String(doc.Tag);
            if (localSalt.Length != 16 || nonce.Length != 12 || tag.Length != 16 || ciphertext.Length > 16_000_000)
                throw new CryptographicException("Invalid TOTP sidecar encryption parameters.");

            derived = Rfc2898DeriveBytes.Pbkdf2(masterPassword, localSalt, doc.Iterations, HashAlgorithmName.SHA256, 32);
            byte[] plain = new byte[ciphertext.Length];
            try
            {
                using (var aes = new AesGcm(derived, 16))
                    aes.Decrypt(nonce, ciphertext, tag, plain);

                var map = JsonSerializer.Deserialize<Dictionary<Guid, string>>(plain) ?? new Dictionary<Guid, string>();
                _secrets = map;
                _salt = localSalt;
                _key = derived;
                derived = null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        finally
        {
            if (derived != null) CryptographicOperations.ZeroMemory(derived);
        }
    }

    internal bool HasSecret(Guid entryId) => IsUnlocked && _secrets.ContainsKey(entryId);

    internal string? GetSecret(Guid entryId)
    {
        if (!IsUnlocked) return null;
        return _secrets.TryGetValue(entryId, out var s) ? s : null;
    }

    internal void SetSecret(Guid entryId, string? base32Secret)
    {
        if (!IsUnlocked) throw new InvalidOperationException("Unlock the vault first.");
        if (string.IsNullOrWhiteSpace(base32Secret))
        {
            if (_secrets.Remove(entryId))
                Save();
            return;
        }
        _ = TotpEngineV380.DecodeBase32(base32Secret);
        _secrets[entryId] = base32Secret.Trim();
        Save();
    }

    internal void Save()
    {
        if (!IsUnlocked) throw new InvalidOperationException("Unlock the vault first.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? WorkspaceStore.Root);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(_secrets);
        try
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] ciphertext = new byte[json.Length];
            byte[] tag = new byte[16];
            using (var aes = new AesGcm(_key!, 16))
                aes.Encrypt(nonce, json, ciphertext, tag);

            var doc = new Envelope
            {
                Format = 1,
                Iterations = 600000,
                Salt = Convert.ToBase64String(_salt!),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };

            string tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(doc), new UTF8Encoding(false));
                File.Move(tmp, _path, true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    internal void Lock()
    {
        if (_key != null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
        _salt = null;
        _secrets.Clear();
    }

    public void Dispose()
    {
        Lock();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal static class PasswordGeneratorV380
{
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    private static readonly string[] PassphraseWords =
    {
        "acorn", "amber", "anchor", "angel", "apple", "apricot", "arch", "arrow", "aspen", "atlas", "aurora", "autumn", "azure", "badge", "bamboo", "banner",
        "barrel", "basil", "beacon", "berry", "birch", "bison", "blade", "blaze", "bloom", "blossom", "board", "boulder", "breeze", "brick", "bridge", "brook",
        "bubble", "bucket", "buffalo", "bugle", "butter", "cabin", "cactus", "camel", "camera", "candle", "canyon", "canvas", "carbon", "carpet", "carrot", "castle",
        "cedar", "chalk", "cherry", "chess", "chime", "cider", "circle", "citron", "cliff", "clock", "cloud", "clover", "cobalt", "coffee", "comet", "compass",
        "copper", "coral", "corner", "cotton", "cougar", "cove", "cradle", "crane", "creek", "cricket", "crown", "crystal", "cube", "current", "cypress", "daisy",
        "dancer", "dawn", "delta", "desert", "diamond", "dolphin", "dome", "dove", "dragon", "drift", "drum", "dune", "eagle", "earth", "echo", "ember",
        "emerald", "engine", "falcon", "feather", "fern", "ferry", "field", "finch", "fjord", "flame", "flash", "flint", "flora", "flute", "forest", "fossil",
        "fountain", "fox", "frost", "galaxy", "garden", "garnet", "gate", "gazelle", "geyser", "glacier", "glass", "glider", "globe", "gold", "granite", "grape",
        "gravel", "grove", "guitar", "harbor", "harvest", "haven", "hawk", "hazel", "hearth", "helium", "heron", "hollow", "honey", "horizon", "hornet", "hunter",
        "iceberg", "indigo", "ink", "iris", "iron", "island", "ivory", "ivy", "jacket", "jade", "jaguar", "jasper", "javelin", "jazz", "juniper", "kayak",
        "kettle", "key", "kestrel", "kite", "koala", "ladder", "lagoon", "lantern", "larch", "lark", "laser", "laurel", "lava", "leaf", "lemon", "leopard",
        "lichen", "light", "lilac", "lily", "linden", "linen", "lion", "lizard", "lobby", "locket", "lotus", "lunar", "lynx", "magnet", "mango", "maple",
        "marble", "marsh", "meadow", "melon", "mercury", "mesa", "metal", "meteor", "mint", "mirror", "mist", "moon", "moss", "motion", "mountain", "muffin",
        "nebula", "nectar", "needle", "neon", "nest", "nettle", "nimbus", "north", "nutmeg", "oak", "oasis", "ocean", "olive", "omega", "onyx", "opal",
        "orbit", "orchid", "oregano", "osprey", "otter", "owl", "oxygen", "paddle", "palm", "panda", "panther", "paper", "parcel", "park", "parrot", "pebble",
        "pelican", "pepper", "petal", "phoenix", "piano", "pillow", "pine", "planet", "plaza", "plum", "pocket", "polar", "pollen", "pond", "poplar", "prism"
    };

    internal static string GeneratePassword(int length = 20, bool upper = true, bool lower = true, bool digits = true, bool symbols = true)
    {
        var sets = new List<string>(4);
        if (upper) sets.Add(UpperChars);
        if (lower) sets.Add(LowerChars);
        if (digits) sets.Add(DigitChars);
        if (symbols) sets.Add(SymbolChars);
        if (sets.Count == 0)
            throw new ArgumentException("At least one character category must be selected.");
        if (length < sets.Count)
            throw new ArgumentOutOfRangeException(nameof(length), $"Password length must be at least {sets.Count} to include every selected category.");

        var poolBuilder = new StringBuilder();
        var chars = new char[length];
        int idx = 0;
        foreach (var set in sets)
        {
            poolBuilder.Append(set);
            chars[idx++] = set[RandomNumberGenerator.GetInt32(set.Length)];
        }

        string pool = poolBuilder.ToString();
        while (idx < length)
            chars[idx++] = pool[RandomNumberGenerator.GetInt32(pool.Length)];

        // Cryptographic Fisher-Yates shuffle
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    internal static string GeneratePassphrase(int wordCount = 5, string separator = "-")
    {
        if (wordCount < 1)
            throw new ArgumentOutOfRangeException(nameof(wordCount), "Word count must be at least 1.");
        string sep = separator ?? "-";
        var picked = new string[wordCount];
        for (int i = 0; i < wordCount; i++)
            picked[i] = PassphraseWords[RandomNumberGenerator.GetInt32(PassphraseWords.Length)];
        return string.Join(sep, picked);
    }

    internal static (double Bits, string Rating) EstimateEntropy(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return (0.0, "Weak");

        bool hasLower = false, hasUpper = false, hasDigit = false, hasSymbol = false, hasNonAscii = false;
        foreach (char c in password)
        {
            if (c is >= 'a' and <= 'z') hasLower = true;
            else if (c is >= 'A' and <= 'Z') hasUpper = true;
            else if (c is >= '0' and <= '9') hasDigit = true;
            else if (c <= 0x7F) hasSymbol = true;
            else hasNonAscii = true;
        }

        int pool = 0;
        if (hasLower) pool += 26;
        if (hasUpper) pool += 26;
        if (hasDigit) pool += 10;
        if (hasSymbol) pool += 32;
        if (hasNonAscii) pool += 64;
        if (pool <= 1) pool = 2;

        double bits = password.Length * Math.Log2(pool);
        if (password.Length >= 4 && password.Distinct().Count() <= 1)
            bits = Math.Min(bits, 12.0);

        string rating = bits < 40.0
            ? "Weak"
            : (bits < 65.0 ? "Fair" : (bits < 90.0 ? "Strong" : "Very Strong"));
        return (Math.Round(bits, 1), rating);
    }
}

internal static class WindowsHelloUnlockV380
{
    private static string TokenPath => Path.Combine(WorkspaceStore.Root, "vault-hello-v380.dpapi");

    internal static bool IsEnrolled => File.Exists(TokenPath);

    internal static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            return availability == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    internal static async Task<(bool Ok, string Message)> RequestConsentAsync(string prompt)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if (availability != UserConsentVerifierAvailability.Available)
                return (false, $"Windows Hello unavailable ({availability}).");

            var result = await UserConsentVerifier.RequestVerificationAsync(prompt);
            return result == UserConsentVerificationResult.Verified
                ? (true, "Verified")
                : (false, $"Windows Hello verification not completed ({result}).");
        }
        catch (Exception ex)
        {
            return (false, "Windows Hello failed: " + ex.Message);
        }
    }

    internal static void Enroll(string masterPassword)
    {
        if (string.IsNullOrEmpty(masterPassword))
            throw new ArgumentException("Master password is required to enroll Windows Hello.", nameof(masterPassword));

        Directory.CreateDirectory(WorkspaceStore.Root);
        byte[] plain = Encoding.UTF8.GetBytes(masterPassword);
        try
        {
            byte[] protectedBlob = DeskSpaceVaultProV380.DpapiProtectCurrentUser(plain);
            string tmp = TokenPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, protectedBlob);
                File.Move(tmp, TokenPath, true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    internal static void Disable()
    {
        if (File.Exists(TokenPath))
            File.Delete(TokenPath);
    }

    internal static async Task<string?> TryUnlockWithHelloAsync()
    {
        var res = await UnlockWithHelloDetailedAsync();
        return res.Ok ? res.Password : null;
    }

    internal static async Task<(bool Ok, string? Password, string Message)> UnlockWithHelloDetailedAsync()
    {
        if (!IsEnrolled)
            return (false, null, "Windows Hello Quick Unlock is not enrolled.");

        var consent = await RequestConsentAsync("Unlock DeskSpace Vault Pro");
        if (!consent.Ok)
            return (false, null, consent.Message);

        byte[]? plain = null;
        try
        {
            byte[] protectedBlob = File.ReadAllBytes(TokenPath);
            plain = DeskSpaceVaultProV380.DpapiUnprotectCurrentUser(protectedBlob);
            string pwd = Encoding.UTF8.GetString(plain);
            return (true, pwd, "Unlocked via Windows Hello.");
        }
        catch (Exception ex)
        {
            return (false, null, "Could not decrypt Windows Hello token: " + ex.Message);
        }
        finally
        {
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
        }
    }
}

internal static class EncryptedWorkspaceBackupV380
{
    private const int Pbkdf2Iterations = 600000;
    private const string ExpectedKind = "DeskSpaceWorkspaceBackupV380";

    private static readonly string[] SafeBackupFileNames =
    {
        "workspace.json",
        "appearance-v148.json",
        "fluent-v380.json",
        "focus-v380.json",
        "notes-meta-v380.json",
        "routines-v380.json"
    };

    private sealed class BackupEnvelope
    {
        public int Format { get; set; } = 1;
        public string Kind { get; set; } = ExpectedKind;
        public int Iterations { get; set; } = Pbkdf2Iterations;
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Ciphertext { get; set; } = "";
    }

    private sealed class BackupBundle
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public string MachineName { get; set; } = Environment.MachineName;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal static void ExportEncryptedBackup(string destinationPath, string passphrase, string? sourceRoot = null)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 8)
            throw new ArgumentException("Backup passphrase must be at least 8 characters.", nameof(passphrase));

        string root = sourceRoot ?? WorkspaceStore.Root;
        var bundle = new BackupBundle();
        foreach (string fileName in SafeBackupFileNames)
        {
            string full = Path.Combine(root, fileName);
            if (File.Exists(full))
                bundle.Files[fileName] = File.ReadAllText(full, Encoding.UTF8);
        }

        string? destDir = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(destDir))
            Directory.CreateDirectory(destDir);

        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(bundle);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[]? key = null;
        try
        {
            key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
            byte[] ciphertext = new byte[plain.Length];
            byte[] tag = new byte[16];
            using (var aes = new AesGcm(key, 16))
                aes.Encrypt(nonce, plain, ciphertext, tag);

            var env = new BackupEnvelope
            {
                Format = 1,
                Kind = ExpectedKind,
                Iterations = Pbkdf2Iterations,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };

            string tmp = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(env), new UTF8Encoding(false));
                File.Move(tmp, destinationPath, true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (key != null) CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static string ImportEncryptedBackup(string sourcePath, string passphrase, string? targetRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Backup archive not found.", sourcePath);
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Backup passphrase is required.", nameof(passphrase));

        var env = JsonSerializer.Deserialize<BackupEnvelope>(File.ReadAllText(sourcePath, Encoding.UTF8))
            ?? throw new CryptographicException("Invalid backup archive header.");
        if (env.Format != 1 || !string.Equals(env.Kind, ExpectedKind, StringComparison.Ordinal))
            throw new CryptographicException("Unsupported backup archive format.");
        if (env.Iterations < 200000 || env.Iterations > 3000000)
            throw new CryptographicException("Unsupported PBKDF2 iteration count.");

        byte[] salt = Convert.FromBase64String(env.Salt);
        byte[] nonce = Convert.FromBase64String(env.Nonce);
        byte[] tag = Convert.FromBase64String(env.Tag);
        byte[] ciphertext = Convert.FromBase64String(env.Ciphertext);
        if (salt.Length != 16 || nonce.Length != 12 || tag.Length != 16 || ciphertext.Length > 64_000_000)
            throw new CryptographicException("Invalid backup encryption parameters.");

        byte[]? key = null;
        byte[] plain = new byte[ciphertext.Length];
        try
        {
            key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, env.Iterations, HashAlgorithmName.SHA256, 32);
            try
            {
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, ciphertext, tag, plain);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("Incorrect backup passphrase or corrupted archive.", ex);
            }

            var bundle = JsonSerializer.Deserialize<BackupBundle>(plain)
                ?? throw new InvalidDataException("Backup archive payload is invalid.");
            if (bundle.Files == null)
                throw new InvalidDataException("Backup archive contains no file dictionary.");

            var validatedFiles = new List<(string FileName, string Content)>();
            foreach (var kv in bundle.Files)
            {
                string? allowed = SafeBackupFileNames.FirstOrDefault(
                    x => string.Equals(x, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (allowed == null || !string.Equals(Path.GetFileName(kv.Key), kv.Key, StringComparison.Ordinal))
                    throw new InvalidDataException($"Unexpected file entry '{kv.Key}' in backup archive.");

                using var parsedDoc = JsonDocument.Parse(kv.Value);
                if (string.Equals(allowed, "workspace.json", StringComparison.OrdinalIgnoreCase))
                {
                    var ws = JsonSerializer.Deserialize<Workspace>(
                        kv.Value,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new InvalidDataException("Backup workspace.json could not be deserialized.");
                    _ = ws.Notes.Count;
                }
                validatedFiles.Add((allowed, kv.Value));
            }

            string root = targetRoot ?? WorkspaceStore.Root;
            Directory.CreateDirectory(root);

            string? preRestoreBakName = null;
            string existingWorkspace = Path.Combine(root, "workspace.json");
            if (File.Exists(existingWorkspace))
            {
                preRestoreBakName = $"workspace.json.{DateTime.Now:yyyyMMdd_HHmmss}.pre-restore.bak";
                File.Copy(existingWorkspace, Path.Combine(root, preRestoreBakName), overwrite: true);
            }

            foreach (var (fileName, content) in validatedFiles)
            {
                string dest = Path.Combine(root, fileName);
                string tmp = dest + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(tmp, content, new UTF8Encoding(false));
                    File.Move(tmp, dest, true);
                }
                finally
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
            }

            string names = validatedFiles.Count == 0
                ? "none"
                : string.Join(", ", validatedFiles.Select(f => f.FileName));
            string bakNote = preRestoreBakName != null
                ? $" Pre-restore backup saved as {preRestoreBakName}."
                : "";
            return $"Restored {validatedFiles.Count} file(s) ({names}).{bakNote}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (key != null) CryptographicOperations.ZeroMemory(key);
        }
    }
}
