# DeskTouch - Stage 1 (Offline Touchpad Probe)

A **separate experimental Windows application**. The end goal is to make the laptop's *existing touchpad* act as a miniature absolute coordinate map of a display. It must work with **no projector, mirror, extra camera or other hardware**.

**Current status: capability probe only.** The code does not disable touchpad hardware, suppress the cursor, install drivers, inject clicks, or communicate over the Internet. It visualizes touchpad physical contact coordinates on Windows 11 **two-finger gestures** if the Precision Touchpad APIs are available.

## Why the experiment is necessary

Windows normally converts one-finger touchpad motion and taps into mouse input, rather than giving every application the original absolute finger coordinates. New Windows 11 Precision Touchpad APIs expose device-relative contact coordinates via `WM_POINTER` when certain two-finger gestures are recognized. Seeing these gestures is NOT proof that physical position of a **single finger** is accessible.

Documentation:
- https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/precision-touchpad-portal
- https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/registertouchpadcapable
- https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/getpointertouchpadinfo

## Build on Windows 11

Requires .NET 8 SDK **only on the build computer**. In PowerShell at this folder:

```powershell
dotnet publish .\DeskTouch.Probe\DeskTouch.Probe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting `DeskTouch.Probe.exe` in `DeskTouch.Probe\bin\Release\net8.0-windows\win-x64\publish\` runs offline. Source checkout requires a GitHub connection to fetch, but runtime does not.

## Run a safe diagnostic

1. Leave the touchpad ENABLED in Windows Settings/Device Manager.
2. Run the probe. Click **Enable probe** and focus the app.
3. Perform **two-finger pan/pinch** over the touchpad. See whether the orange dot moves.
4. See whether moving just one finger generates any physical coordinates. Mouse cursor movement alone does not count.
5. Click **Copy diagnostics**. Examine before sharing.
6. Hit **Esc** or **F12** while the probe window is focused to disable. Closing the window unregisters the API.

No global hotkey is installed in this test; Esc/F12 work only while its window is focused. Normal touchpad gestures remain available.

## Gate before implementing actual absolute touch mode

- Confirm Windows version and actual touchpad device API availability.
- Verify coordinate normalization in each corner; test multi-monitor mapping.
- Determine safe/authorized way to obtain one-finger absolute contact coordinates for this device. This may NOT be possible through standard supported APIs.
- Only then add optional remapping / input injection, and a true global kill switch with fail-safe recovery.
- Publish a self-contained offline installer after Windows hardware tests.

No Windows hardware validation has been completed in the present development environment. **This prototype must not be described as a tested or finished touch-screen replacement.**

Source is deliberately staged under a separate `prototype/offline-desktouch-touchpad` branch; it does not modify the DeskSpace production app.
