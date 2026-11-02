using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Stage 1: This is a read-only physical-coordinate probe. It never injects
// clicks, disables the touchpad, installs a driver, or opens network connections.
// Windows 11 usually delivers raw positions for TWO-finger gestures, NOT
// one-finger taps. A working gesture probe does not prove absolute 1-finger input.

namespace DeskTouch.Probe;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ProbeForm());
    }
}

internal sealed class ProbeForm : Form
{
    const int WM_POINTERUPDATE = 0x0245;
    const int WM_POINTERDOWN = 0x0246;
    const int WM_POINTERUP = 0x0247;
    readonly Label status = new() { Dock = DockStyle.Top, Height = 94,
        Padding = new Padding(12), TextAlign = ContentAlignment.MiddleLeft };
    readonly Label instructions = new() { Dock = DockStyle.Top, Height = 88,
        Padding = new Padding(12), Text = "Enable probe and perform TWO-finger gestures over the touchpad while this window is active. " +
        "The orange point will show a mapped physical position if Windows exposes it. " +
        "Single-finger absolute positions are not available through ordinary mouse input." };
    readonly Panel canvas = new() { Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(18, 29, 46) };
    readonly StringBuilder diagnostics = new();
    RegisterTouchpadCapableWindow? register;
    GetPointerTouchpadInfo? getInfo;
    bool active;
    int eventsSeen;
    PointF? position;

    internal ProbeForm()
    {
        Text = "DeskTouch - OFFLINE physical touchpad coordinate probe (NO clicks)";
        ClientSize = new Size(760, 520);
        MinimumSize = new Size(620, 400);
        KeyPreview = true;
        var buttonBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(10) };
        var on = new Button { Text = "Enable probe", AutoSize = true };
        var off = new Button { Text = "Disable (Esc / F12)", AutoSize = true };
        var copy = new Button { Text = "Copy diagnostics", AutoSize = true };
        buttonBar.Controls.AddRange(new Control[] { on, off, copy });
        Controls.Add(canvas);
        Controls.Add(instructions);
        Controls.Add(status);
        Controls.Add(buttonBar);
        on.Click += (_, _) => Enable();
        off.Click += (_, _) => Disable();
        copy.Click += (_, _) => Clipboard.SetText(diagnostics.ToString());
        KeyDown += (_, e) => { if (e.KeyCode is Keys.Escape or Keys.F12) { Disable(); e.Handled = true; } };
        canvas.Paint += (_, e) => Draw(e.Graphics);
        diagnostics.AppendLine("OS: " + Environment.OSVersion.VersionString);
        SetStatus("Idle. Touchpad hardware is left enabled. This test sends no input.");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        IntPtr user32 = Native.GetModuleHandle("user32.dll");
        IntPtr reg = user32 == IntPtr.Zero ? IntPtr.Zero : Native.GetProcAddress(user32, new IntPtr(2689));
        IntPtr info = user32 == IntPtr.Zero ? IntPtr.Zero : Native.GetProcAddress(user32, new IntPtr(2691));
        if (reg != IntPtr.Zero && info != IntPtr.Zero)
        {
            register = Marshal.GetDelegateForFunctionPointer<RegisterTouchpadCapableWindow>(reg);
            getInfo = Marshal.GetDelegateForFunctionPointer<GetPointerTouchpadInfo>(info);
            diagnostics.AppendLine("Touchpad API ordinal exports: present");
        }
        else
        {
            diagnostics.AppendLine("Touchpad API ordinal exports: missing");
            SetStatus("Missing Windows 11 Precision Touchpad API. Do not disable the device.");
        }
    }

    void Enable()
    {
        if (active) return;
        if (register == null || getInfo == null)
        {
            SetStatus("Unsupported on this Windows version: required API missing.");
            return;
        }
        active = register(Handle, true);
        SetStatus(active ? "Probe enabled: perform TWO-finger pan with this window focused." :
            "Registration failed; Windows error: " + Marshal.GetLastWin32Error());
        if (active) Focus();
    }

    void Disable()
    {
        if (active && register != null && IsHandleCreated)
        {
            try { register(Handle, false); } catch { }
        }
        active = false;
        position = null;
        canvas.Invalidate();
        SetStatus("Disabled. Events seen: " + eventsSeen);
    }

    void SetStatus(string msg)
    {
        status.Text = msg;
        diagnostics.AppendLine(msg);
    }

    protected override void WndProc(ref Message m)
    {
        if (active && (m.Msg == WM_POINTERUPDATE || m.Msg == WM_POINTERDOWN || m.Msg == WM_POINTERUP) &&
            getInfo != null)
        {
            uint id = unchecked((uint)m.WParam.ToInt64()) & 0xffffu;
            if (getInfo(id, out var touch))
            {
                eventsSeen++;
                var p = touch.pointerInfo.ptHimetricLocationRaw;
                if (Native.GetPointerDeviceRects(touch.pointerInfo.sourceDevice, out _, out var rect) &&
                    rect.Right > rect.Left && rect.Bottom > rect.Top)
                {
                    float x = Math.Clamp((float)(p.X - rect.Left) / (rect.Right - rect.Left), 0f, 1f);
                    float y = Math.Clamp((float)(p.Y - rect.Top) / (rect.Bottom - rect.Top), 0f, 1f);
                    position = new PointF(x, y);
                    status.Text = $"Two-finger gesture events: {eventsSeen} | Touchpad normalized X={x:0.000}, Y={y:0.000}";
                    if (eventsSeen <= 10 || eventsSeen % 40 == 0)
                        diagnostics.AppendLine($"contact id={id} X={x:0.000} Y={y:0.000}");
                    canvas.Invalidate();
                }
                else status.Text = "Gesture received but physical touchpad bounds are unavailable.";
            }
        }
        // Preserve OS handling and normal scroll/pinch gestures.
        base.WndProc(ref m);
    }

    void Draw(Graphics g)
    {
        g.Clear(Color.FromArgb(18, 29, 46));
        var r = new Rectangle(40, 30, Math.Max(100, canvas.Width - 80),
            Math.Max(100, canvas.Height - 70));
        using var edge = new Pen(Color.SkyBlue, 2);
        using var grid = new Pen(Color.FromArgb(70, 110, 150));
        using var ink = new SolidBrush(Color.WhiteSmoke);
        g.DrawRectangle(edge, r);
        for (int i = 1; i < 3; i++)
        {
            g.DrawLine(grid, r.X + r.Width * i / 3, r.Top, r.X + r.Width * i / 3, r.Bottom);
            g.DrawLine(grid, r.Left, r.Y + r.Height * i / 3, r.Right, r.Y + r.Height * i / 3);
        }
        g.DrawString("Physical touchpad position - visualization only", Font, ink, 40, 6);
        if (position is PointF p)
        {
            using var dot = new SolidBrush(Color.Orange);
            g.FillEllipse(dot, r.X + p.X * r.Width - 10,
                r.Y + p.Y * r.Height - 10, 20, 20);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Disable();
        base.OnFormClosing(e);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool RegisterTouchpadCapableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool GetPointerTouchpadInfo(uint pointerId, out PointerTouchInfo value);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct PointerInfo
{
    public int pointerType;
    public uint pointerId, frameId, pointerFlags;
    public IntPtr sourceDevice, hwndTarget;
    public Point ptPixelLocation, ptHimetricLocation, ptPixelLocationRaw, ptHimetricLocationRaw;
    public uint dwTime, historyCount;
    public int InputData;
    public uint dwKeyStates;
    public ulong PerformanceCount;
    public int ButtonChangeType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PointerTouchInfo
{
    public PointerInfo pointerInfo;
    public uint touchFlags, touchMask;
    public NativeRect rcContact, rcContactRaw;
    public uint orientation, pressure;
}

internal static class Native
{
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string name);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    internal static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPointerDeviceRects(IntPtr device,
        out NativeRect screen, out NativeRect deviceRect);
}
