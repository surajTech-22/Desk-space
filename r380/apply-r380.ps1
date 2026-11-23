$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# R380: Copy DeskSpaceDesignV283 + ModernV380 suite (all 4 modernization phases +
# Pinned App Compatible Files & Smart Launcher) into DeskSpace_Windows_R1 and
# wire surgical hooks without mutating MainWindow.xaml or DeskSpaceDropShelfV15.

$lf = [string][char]10

function Begin-Patch([string]$path) {
    if (!(Test-Path -LiteralPath $path)) {
        throw ('R380 missing source: ' + $path)
    }
    $script:code = [regex]::Replace([IO.File]::ReadAllText($path), '\r+\n?', $lf)
}

function Swap-R380([string]$old, [string]$replacement, [string]$context) {
    $oldNorm = [regex]::Replace($old, '\r+\n?', $lf)
    $repNorm = [regex]::Replace($replacement, '\r+\n?', $lf)
    $count = ([regex]::Matches($script:code, [regex]::Escape($oldNorm))).Count
    if ($count -ne 1) {
        throw ('R380 ' + $context + ': expected 1 anchor match, got ' + $count)
    }
    $script:code = $script:code.Replace($oldNorm, $repNorm)
}

function Finish-Patch([string]$path) {
    [IO.File]::WriteAllText($path, $script:code, [Text.UTF8Encoding]::new($false))
}

$dstRoot = 'DeskSpace_Windows_R1/DeskSpace'
if (!(Test-Path -LiteralPath $dstRoot)) {
    throw "R380 missing reconstructed app folder: $dstRoot"
}

# 1. Copy DeskSpaceDesignV283.cs (required by all ModernV380 UI modules; depends only on V148)
$designSrc = 'src/DeskSpace/DeskSpaceDesignV283.cs'
$designDst = "$dstRoot/DeskSpaceDesignV283.cs"
if (!(Test-Path -LiteralPath $designSrc)) {
    throw 'R380 missing src/DeskSpace/DeskSpaceDesignV283.cs'
}
Copy-Item -LiteralPath $designSrc -Destination $designDst -Force
if ((Get-FileHash $designSrc -Algorithm SHA256).Hash -ne (Get-FileHash $designDst -Algorithm SHA256).Hash) {
    throw 'R380 DeskSpaceDesignV283.cs copy SHA256 mismatch'
}

# 2. Copy all ModernV380/*.cs files and verify SHA-256 parity
$modernSrcDir = 'src/DeskSpace/ModernV380'
$modernDstDir = "$dstRoot/ModernV380"
if (!(Test-Path -LiteralPath $modernSrcDir)) {
    throw "R380 missing $modernSrcDir"
}
if (Test-Path -LiteralPath $modernDstDir) {
    Remove-Item -LiteralPath $modernDstDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $modernDstDir | Out-Null

$modernFiles = @(Get-ChildItem -LiteralPath $modernSrcDir -Filter '*.cs' -File | Sort-Object Name)
if ($modernFiles.Count -lt 16) {
    throw "R380 expected at least 16 ModernV380 source files, found $($modernFiles.Count)"
}
foreach ($file in $modernFiles) {
    $dstFile = Join-Path $modernDstDir $file.Name
    Copy-Item -LiteralPath $file.FullName -Destination $dstFile -Force
    if ((Get-FileHash $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $dstFile -Algorithm SHA256).Hash) {
        throw "R380 copy SHA256 mismatch for $($file.Name)"
    }
}

# 3. Surgical hook: MainWindow.xaml.cs
$mainPath = "$dstRoot/MainWindow.xaml.cs"
Begin-Patch $mainPath
Swap-R380 @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
'@ @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
        Loaded += (_,_) => DeskSpaceModernHubV380.AttachMainWindow(this, () => data ?? new Workspace(), SafeSave, RefreshDashboard);
'@ 'MainWindow Loaded modern hub attach'
Swap-R380 @'
        trayMenu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        tray.ContextMenuStrip = trayMenu;
'@ @'
        trayMenu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        DeskSpaceModernHubV380.AddMainTraySubmenu(trayMenu, this, () => data ?? new Workspace());
        tray.ContextMenuStrip = trayMenu;
'@ 'MainWindow tray submenu'
Finish-Patch $mainPath

# 4. Surgical hook: WidgetHost.cs
$hostPath = "$dstRoot/WidgetHost.cs"
Begin-Patch $hostPath
Swap-R380 @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
'@ @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
        DeskSpaceModernHubV380.AttachWidgetHost(this, menu);
'@ 'WidgetHost tray modern submenu and global hotkeys'
Swap-R380 @'
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
    }
'@ @'
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
        DeskSpaceFocusRingV380.AttachToDonutCanvas(canvas);
    }
'@ 'WidgetHost donut Focus Ring canvas attachment'
Finish-Patch $hostPath

# 5. Surgical hook: DeskSpaceApplicationsV55.cs (preserve RecentRightClickV262 & OpenLatestOrApp class intact)
$appsPath = "$dstRoot/DeskSpaceApplicationsV55.cs"
Begin-Patch $appsPath
Swap-R380 @'
            case "launch":
                if(IsLaunchable(item.Path))
                {
                    try{DeskSpacePinnedRecentV122.OpenLatestOrApp(item.Path);}
                    catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                }
                return true;
'@ @'
            case "launch":
                if(IsLaunchable(item.Path))
                {
                    HideArc();
                    string pinnedTarget=item.Path;
                    owner.Dispatcher.BeginInvoke(new Action(()=>
                        DeskSpacePinnedAppFilesV380.Show(owner,pinnedTarget)));
                }
                return true;
'@ 'DeskSpaceApplicationsV55 left-click compatible files browser'
Finish-Patch $appsPath

# 6. Surgical hook: DeskSpaceFeatureHubV199.cs
$hubPath = "$dstRoot/DeskSpaceFeatureHubV199.cs"
Begin-Patch $hubPath
Swap-R380 @'
        var explanation=new TextBlock
'@ @'
        Section("Modern Suite");
        DeskSpaceModernHubV380.AddFeatureHubButtons(Add);
        var explanation=new TextBlock
'@ 'DeskSpaceFeatureHubV199 Modern Suite section'
Finish-Patch $hubPath

# 7. Surgical hook: DeskSpaceClipboardV10.cs
$clipPath = "$dstRoot/DeskSpaceClipboardV10.cs"
Begin-Patch $clipPath
Swap-R380 @'
            lastContent=secret;
            Clipboard.SetText(secret);
'@ @'
            lastContent=secret;
            DeskSpaceRichClipboardV380.NotifySensitiveCopy(secret);
            Clipboard.SetText(secret);
'@ 'DeskSpaceClipboardV10 sensitive copy exclusion'
Finish-Patch $clipPath

# 8. Surgical hook: DeskSpaceActionPaletteV197.cs
$palettePath = "$dstRoot/DeskSpaceActionPaletteV197.cs"
Begin-Patch $palettePath
Swap-R380 @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
    };
'@ @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files"),
        new("v380_commandbar","Command Bar · Search, Math, GST & Units","Open Spotlight launcher with calculator, converters & quick actions","Tools"),
        new("v380_pinnedfiles","Pinned Apps · Compatible Files","Browse all compatible files & Tally companies for pinned apps (Recent first)","Files"),
        new("v380_clipboard","Rich Clipboard History & Transforms","Event-driven text, image, color & file clipboard history","Text"),
        new("v380_snip","Screen Snip, Table OCR & Pin on Screen","Capture region to OCR text, Excel TSV or floating pin","Tools"),
        new("v380_eyedropper","Screen Eyedropper Color Picker","Sample #HEX, RGB & HSL color anywhere on screen","Tools"),
        new("v380_focus","Focus Ring Pomodoro Studio","Start deep-work timer rendered on the DeskSpace donut","Workspace"),
        new("v380_vaultpro","Vault Pro · Hello, TOTP 2FA & Backup","Windows Hello unlock, RFC 6238 TOTP & AES-256-GCM backup","Privacy"),
        new("v380_desktouch","DeskTouch Ecosystem Hub & OTP Grabber","Drop files to phone/PC, extract OTPs & scan camera tables","System"),
        new("v380_snippets","Dynamic Snippet Expander","Expand snippets with {{date}}, {{clipboard}} & {{prompt:...}}","Text"),
        new("v380_excelpro","Excel & Accounting Pro Studio","Pre-paste diff, rollback history, GSTIN Luhn-36 & PAN validator","Excel"),
        new("v380_filetoolkit","File Toolkit · QR, EXIF, SHA-256, PDF & ZIP","Offline QR codes, EXIF removal, hash verify, PDF PNG & ZIP","Files"),
        new("v380_windowpower","Window Power · Snap Zones & Auto-Layouts","Monitor topology layouts, Snap Zones & Always-on-Top pin","Windows"),
        new("v380_snapzones","Snap Active Window to Zone","Pick visual layout zone for foreground window","Windows"),
        new("v380_topmost","Toggle Always-on-Top Window Pin","Pin or unpin the active foreground window","Windows"),
        new("v380_routines","Automation Routines & Smart Profiles","Multi-step routines with dry-run plan and per-app actions","Automation"),
        new("v380_localai","Local AI Copilot (Opt-In)","Ollama or custom endpoint assistant (disabled by default)","Tools")
    };
'@ 'DeskSpaceActionPaletteV197 V380 command entries'
Swap-R380 @'
                    catch(Exception ex) when(ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception or System.Security.SecurityException)
                    {
                        MessageBox.Show(owner,"Unable to open Downloads: "+ex.GetType().Name,
                            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }
                break;
        }
'@ @'
                    catch(Exception ex) when(ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception or System.Security.SecurityException)
                    {
                        MessageBox.Show(owner,"Unable to open Downloads: "+ex.GetType().Name,
                            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }
                break;
            default:
                DeskSpaceModernHubV380.TryExecutePaletteCommand(owner, command.Id);
                break;
        }
'@ 'DeskSpaceActionPaletteV197 V380 command dispatch'
Finish-Patch $palettePath

# 9. Post-patch invariants check
$deckCheck = [IO.File]::ReadAllText("$dstRoot/DeskSpaceDropShelfV15.cs")
if (([regex]::Matches($deckCheck, 'DeckHeaderButton[(]')).Count -ne 4) {
    throw 'R380 invariant broken: DeskSpaceDropShelfV15 DeckHeaderButton( count != 4'
}
$xamlCheck = [IO.File]::ReadAllText("$dstRoot/MainWindow.xaml")
foreach ($req in @('TextElement.Foreground="#243F5C"', 'Foreground="#1B3A57"', 'Value="#BFDFF4"', 'Ready — Workspace loaded')) {
    if (!$xamlCheck.Contains($req)) {
        throw "R380 invariant broken: MainWindow.xaml missing $req"
    }
}
if (!(Test-Path -LiteralPath "$dstRoot/DeskSpaceDesignV283.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpacePinnedAppFilesV380.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpaceModernHubV380.cs")) {
    throw 'R380 missing copied ModernV380 or DeskSpaceDesignV283 files in target tree'
}

Write-Host "R380 PASS: Copied DeskSpaceDesignV283 + $($modernFiles.Count) ModernV380 modules and wired all hooks safely."
