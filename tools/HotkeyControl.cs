using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// 独立对照实验：不依赖 AudioSwitcher 的任何代码，
/// 自己注册一个全局热键(Ctrl+Alt+F9)并用 SendInput 注入，
/// 看 WM_HOTKEY 是否会到达消息循环。
/// 用来区分“环境不投递热键” vs “程序自己的 bug”。
/// </summary>
internal static class HotkeyControl
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint threadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const int HWND_MESSAGE = -3;
    private const int WM_HOTKEY = 0x0312;
    private const ushort VK_F9 = 0x78;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;

    private static int Main()
    {
        IntPtr hwnd = CreateWindowEx(0, "STATIC", "hotkey-control", 0, 0, 0, 0, 0,
            new IntPtr(HWND_MESSAGE), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine("message-only hwnd = " + hwnd);
        Console.WriteLine("thread desktop    = " + GetThreadDesktop(GetCurrentThreadId()));
        Console.WriteLine("foreground hwnd   = " + GetForegroundWindow());

        bool registered = RegisterHotKey(hwnd, 1, MOD_CONTROL | MOD_ALT, VK_F9);
        Console.WriteLine("RegisterHotKey(Ctrl+Alt+F9) = " + registered + " err=" + Marshal.GetLastWin32Error());
        if (!registered) return 2;

        INPUT[] inputs = new INPUT[6];
        ushort[] keys = new ushort[] { 0x11, 0x12, VK_F9, VK_F9, 0x12, 0x11 };
        uint[] flags = new uint[] { 0, 0, 0, 2, 2, 2 };
        for (int i = 0; i < keys.Length; i++)
        {
            inputs[i].type = 1;
            inputs[i].u.ki.wVk = keys[i];
            inputs[i].u.ki.dwFlags = flags[i];
        }
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        Console.WriteLine("SendInput = " + sent + "/6  err=" + Marshal.GetLastWin32Error());

        bool fired = false;
        DateTime deadline = DateTime.Now.AddSeconds(3);
        while (DateTime.Now < deadline)
        {
            MSG message;
            while (PeekMessage(out message, IntPtr.Zero, 0, 0, 1))
            {
                if (message.message == WM_HOTKEY)
                {
                    fired = true;
                    Console.WriteLine("WM_HOTKEY arrived, id=" + message.wParam);
                }
            }
            Thread.Sleep(20);
        }

        UnregisterHotKey(hwnd, 1);
        Console.WriteLine(fired ? "RESULT: hotkey fired (input delivery works)" : "RESULT: no hotkey (input not delivered to this session/desktop)");
        return fired ? 0 : 1;
    }
}
