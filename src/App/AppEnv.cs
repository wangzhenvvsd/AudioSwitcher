using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AudioSwitcher.App
{
    internal static class AppEnv
    {
        public static string ExecutablePath
        {
            get
            {
                try
                {
                    return Process.GetCurrentProcess().MainModule.FileName;
                }
                catch
                {
                    return Application.ExecutablePath;
                }
            }
        }

        public static string ProductName
        {
            get { return "音频切换器 AudioSwitcher"; }
        }
    }

    internal static class IconUtil
    {
        private static Icon _cached;

        public static Icon AppIcon
        {
            get
            {
                if (_cached != null) return _cached;
                try
                {
                    Icon icon = Icon.ExtractAssociatedIcon(AppEnv.ExecutablePath);
                    if (icon != null)
                    {
                        _cached = icon;
                        return _cached;
                    }
                }
                catch
                {
                }
                _cached = SystemIcons.Application;
                return _cached;
            }
        }
    }

    /// <summary>轻量日志，方便排查“快捷键没反应”。文件：%APPDATA%\AudioSwitcher\AudioSwitcher.log</summary>
    internal static class Log
    {
        private static readonly object Sync = new object();

        public static string LogPath
        {
            get { return Path.Combine(Config.AppConfig.ConfigDirectory, "AudioSwitcher.log"); }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Config.AppConfig.ConfigDirectory);
                    FileInfo info = new FileInfo(LogPath);
                    if (info.Exists && info.Length > 512 * 1024)
                    {
                        File.Delete(LogPath);
                    }
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        System.Text.Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }

    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AudioSwitcher";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        key.SetValue(ValueName, "\"" + AppEnv.ExecutablePath + "\" --tray", RegistryValueKind.String);
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class ConsoleUi
    {
        private const int STD_OUTPUT_HANDLE = -11;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool AttachConsole(int dwProcessId);

        /// <summary>
        /// 这是一个 GUI 子系统程序，从命令行调用（--list / --switch 等）时
        /// 需要自己接上父进程的控制台，并把输出重定向过去。
        /// </summary>
        public static void EnsureOutput()
        {
            try
            {
                IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
                bool hasOutput = handle != IntPtr.Zero && handle != new IntPtr(-1);
                if (!hasOutput && AttachConsole(-1))
                {
                    hasOutput = true;
                }
                if (!hasOutput) return;

                try
                {
                    Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                }
                catch
                {
                }

                // 直接包一层 UTF-8 写入器，保证重定向到文件/管道时也是 UTF-8
                StreamWriter stdout = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false));
                stdout.AutoFlush = true;
                Console.SetOut(stdout);

                StreamWriter stderr = new StreamWriter(Console.OpenStandardError(), new System.Text.UTF8Encoding(false));
                stderr.AutoFlush = true;
                Console.SetError(stderr);
            }
            catch
            {
            }
        }
    }
}
