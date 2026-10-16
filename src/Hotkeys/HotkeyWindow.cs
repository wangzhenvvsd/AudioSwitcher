using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioSwitcher.Hotkeys
{
    public class HotkeyEventArgs : EventArgs
    {
        public int Id { get; private set; }

        public HotkeyEventArgs(int id)
        {
            Id = id;
        }
    }

    internal static class HotkeyNative
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    /// <summary>只用于接收 WM_HOTKEY 的隐藏窗口。</summary>
    public class HotkeyWindow : Form
    {
        private const int WM_HOTKEY = 0x0312;

        public event EventHandler<HotkeyEventArgs> HotkeyPressed;

        public HotkeyWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            Size = new Size(1, 1);
        }

        protected override void SetVisibleCore(bool value)
        {
            // 永远不显示这个窗口
            base.SetVisibleCore(false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                EventHandler<HotkeyEventArgs> handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, new HotkeyEventArgs(id));
                }
            }
            base.WndProc(ref m);
        }

        public bool Register(int id, uint modifiers, uint virtualKey)
        {
            return HotkeyNative.RegisterHotKey(Handle, id, modifiers, virtualKey);
        }

        public void Unregister(int id)
        {
            HotkeyNative.UnregisterHotKey(Handle, id);
        }
    }
}
