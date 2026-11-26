using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WwmRedeemer;

/// <summary>Goi truc tiep Win32: gui phim/chuot (SendInput), tim cua so game, doc phim toan cuc.</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    // ---- phim (scancode, giong ban phim that) ----
    public const ushort ScanCtrl = 0x1D, ScanV = 0x2F;

    public static void Key(ushort scan, bool up)
    {
        var i = new INPUT[1];
        i[0].type = 1;
        i[0].u.ki.wScan = scan;
        i[0].u.ki.dwFlags = 0x0008 | (up ? 0x0002u : 0u); // SCANCODE | KEYUP
        SendInput(1, i, Marshal.SizeOf<INPUT>());
    }

    private static void MouseFlag(uint flag)
    {
        var i = new INPUT[1];
        i[0].type = 0;
        i[0].u.mi.dwFlags = flag;
        SendInput(1, i, Marshal.SizeOf<INPUT>());
    }

    public static async Task CtrlVAsync()
    {
        Key(ScanCtrl, false); await Task.Delay(60);
        Key(ScanV, false); await Task.Delay(60);
        Key(ScanV, true); await Task.Delay(60);
        Key(ScanCtrl, true);
    }

    /// <summary>Bam chuot trai tai toa do man hinh (di chuyen nhe truoc de game nhan hover).</summary>
    public static async Task ClickAsync(int x, int y)
    {
        SetCursorPos(x - 6, y - 4); await Task.Delay(80);
        SetCursorPos(x, y); await Task.Delay(120);
        MouseFlag(0x0002); await Task.Delay(70); // down
        MouseFlag(0x0004);                        // up
    }

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    public const int VK_F9 = 0x78, VK_F12 = 0x7B;

    public static POINT CursorPos() { GetCursorPos(out var p); return p; }

    // ---- cua so game ----
    /// <summary>Tim cua so lon nhat cua tien trinh wwm.exe.</summary>
    public static IntPtr FindGameWindow()
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            try
            {
                using var p = Process.GetProcessById((int)pid);
                if (!p.ProcessName.Equals("wwm", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { return true; }
            if (!GetClientRect(h, out var r)) return true;
            long area = (long)r.Right * r.Bottom;
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    /// <summary>Vung client cua cua so game theo toa do man hinh.</summary>
    public static System.Drawing.Rectangle ClientRectOnScreen(IntPtr h)
    {
        GetClientRect(h, out var r);
        var p = new POINT { X = 0, Y = 0 };
        ClientToScreen(h, ref p);
        return new System.Drawing.Rectangle(p.X, p.Y, r.Right, r.Bottom);
    }

    public static void BringToFront(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, 9);
        var fg = GetForegroundWindow();
        if (fg == h) return;
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint me = GetCurrentThreadId();
        AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(h);
        AttachThreadInput(me, fgThread, false);
    }

    public static bool IsForeground(IntPtr h) => GetForegroundWindow() == h;
}
