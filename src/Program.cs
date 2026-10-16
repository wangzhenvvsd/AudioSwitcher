using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AudioSwitcher.App;
using AudioSwitcher.Audio;
using AudioSwitcher.Config;
using AudioSwitcher.Native;

namespace AudioSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ConsoleUi.EnsureOutput();

            if (args != null && args.Length > 0)
            {
                int result = Cli.Run(args);
                if (result >= 0)
                {
                    Environment.ExitCode = result;
                    return;
                }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\AudioSwitcher_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "音频切换器已经在运行了。\r\n请看任务栏右下角（系统托盘）的图标，双击即可打开设置。",
                        AppEnv.ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                bool showSettings = !AppConfig.Exists || Cli.HasSwitch(args, "--settings");
                TrayApp app = new TrayApp(showSettings);
                Application.Run(app);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowCrash(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowCrash(e.ExceptionObject as Exception);
        }

        private static void ShowCrash(Exception ex)
        {
            string message = ex == null ? "未知错误" : ex.ToString();
            try
            {
                MessageBox.Show(message, AppEnv.ProductName + " 出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }

    internal static class Cli
    {
        public static bool HasSwitch(string[] args, string name)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <returns>大于等于 0：命令行模式，直接作为退出码；-1：继续启动图形界面。</returns>
        public static int Run(string[] args)
        {
            string command = args[0].Trim().ToLowerInvariant();
            switch (command)
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    PrintHelp();
                    return 0;
                case "--version":
                case "-v":
                    Console.WriteLine("AudioSwitcher 1.0.0");
                    return 0;
                case "--list":
                case "--list-devices":
                case "-l":
                    return ListDevices(args);
                case "--status":
                    return Status();
                case "--selftest":
                    return SelfTest();
                case "--switch":
                case "-s":
                    return Switch(args);
                case "--set-output":
                    return SetEndpoint(args, EDataFlow.Render);
                case "--set-input":
                    return SetEndpoint(args, EDataFlow.Capture);
                case "--tray":
                case "--minimized":
                case "--settings":
                    return -1;
                default:
                    Console.WriteLine("未知参数：" + args[0]);
                    PrintHelp();
                    return 2;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("音频切换器 AudioSwitcher 1.0.0");
            Console.WriteLine();
            Console.WriteLine("不带参数运行       启动托盘程序（可配合 --settings 打开设置窗口）");
            Console.WriteLine("--list, -l         列出所有音频设备（加 --all 也会列出未连接的）");
            Console.WriteLine("--status           显示当前默认扬声器/麦克风，以及配置里的组合");
            Console.WriteLine("--selftest         自检所有快捷键能否注册（排查被其它程序占用）");
            Console.WriteLine("--switch <名称|序号>  按配置里的组合切换（序号从 1 开始）");
            Console.WriteLine("--set-output <设备名或ID片段>   直接设置默认扬声器");
            Console.WriteLine("--set-input  <设备名或ID片段>   直接设置默认麦克风");
            Console.WriteLine("--settings         启动并打开设置窗口");
            Console.WriteLine("--tray             启动并最小化到托盘（默认行为）");
            Console.WriteLine("--help, -h         显示本帮助");
            Console.WriteLine();
            Console.WriteLine("配置文件：" + AppConfig.ConfigPath);
        }

        private static int ListDevices(string[] args)
        {
            bool includeInactive = HasSwitch(args, "--all");
            Console.WriteLine("系统音频设备（" + (includeInactive ? "包含未启用/未连接" : "仅已启用") + "）：");
            Console.WriteLine();

            PrintDevices("扬声器 / 输出设备", EDataFlow.Render, includeInactive);
            Console.WriteLine();
            PrintDevices("麦克风 / 输入设备", EDataFlow.Capture, includeInactive);

            Console.WriteLine();
            Console.WriteLine("当前默认扬声器：" + AudioService.GetDefaultDeviceName(EDataFlow.Render));
            Console.WriteLine("当前默认麦克风：" + AudioService.GetDefaultDeviceName(EDataFlow.Capture));
            Console.WriteLine("配置文件：" + AppConfig.ConfigPath);
            return 0;
        }

        private static void PrintDevices(string title, EDataFlow flow, bool includeInactive)
        {
            Console.WriteLine("== " + title + " ==");
            List<AudioDeviceInfo> devices;
            try
            {
                devices = AudioService.GetDevices(flow, includeInactive);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  枚举失败：" + ex.Message);
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                AudioDeviceInfo device = devices[i];
                string mark = device.IsActive ? "[已启用]" : "[" + device.StateText + "]";
                Console.WriteLine("  " + (i + 1).ToString().PadLeft(2) + ". " + device.DisplayName + "  " + mark);
                Console.WriteLine("      ID: " + device.Id);
            }
            if (devices.Count == 0)
            {
                Console.WriteLine("  （没有找到设备）");
            }
        }

        /// <summary>注册一遍所有快捷键并立刻注销，用来排查“快捷键被其它程序占用”。</summary>
        private static int SelfTest()
        {
            AppConfig config = AppConfig.Load();
            Console.WriteLine("快捷键注册自检（本程序不会真的切换设备）：");

            int ok = 0;
            int bad = 0;
            using (AudioSwitcher.Hotkeys.HotkeyWindow window = new AudioSwitcher.Hotkeys.HotkeyWindow())
            {
                IntPtr handle = window.Handle;
                for (int i = 0; i < config.Combos.Count; i++)
                {
                    ComboConfig combo = config.Combos[i];
                    if (!combo.Enabled) continue;
                    if (string.IsNullOrEmpty(combo.Hotkey))
                    {
                        Console.WriteLine("  [无]   " + combo.Name + "  （没有设置快捷键）");
                        continue;
                    }

                    uint modifiers;
                    uint virtualKey;
                    string error;
                    if (!AudioSwitcher.Hotkeys.HotkeyUtil.TryParse(combo.Hotkey, out modifiers, out virtualKey, out error))
                    {
                        Console.WriteLine("  [无效] " + combo.Name + "  " + combo.Hotkey + "  -> " + error);
                        bad++;
                        continue;
                    }

                    int id = 0xB000 + i;
                    bool registered = window.Register(id, modifiers | AudioSwitcher.Hotkeys.HotkeyUtil.MOD_NOREPEAT, virtualKey);
                    Console.WriteLine("  " + (registered ? "[可用]" : "[占用]") + " " + combo.Name + "  " + combo.Hotkey);
                    if (registered)
                    {
                        window.Unregister(id);
                        ok++;
                    }
                    else
                    {
                        bad++;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("可用 " + ok + " 个，异常 " + bad + " 个。");
            if (bad > 0)
            {
                Console.WriteLine("提示：被占用说明该组合键已被其它程序（或另一个本程序实例）注册，换一个组合键即可。");
            }
            return bad == 0 ? 0 : 1;
        }

        private static int Status()
        {
            AppConfig config = AppConfig.Load();
            string currentOutId = AudioService.GetDefaultDeviceId(EDataFlow.Render);
            string currentInId = AudioService.GetDefaultDeviceId(EDataFlow.Capture);

            Console.WriteLine("当前默认扬声器：" + AudioService.GetDefaultDeviceName(EDataFlow.Render));
            Console.WriteLine("当前默认麦克风：" + AudioService.GetDefaultDeviceName(EDataFlow.Capture));
            Console.WriteLine("配置文件：" + AppConfig.ConfigPath);
            Console.WriteLine();
            Console.WriteLine("配置里的组合（共 " + config.Combos.Count + " 个）：");
            for (int i = 0; i < config.Combos.Count; i++)
            {
                ComboConfig combo = config.Combos[i];
                bool active = true;
                if (combo.HasOutput && !string.Equals(combo.OutputDeviceId, currentOutId, StringComparison.OrdinalIgnoreCase)) active = false;
                if (combo.HasInput && !string.Equals(combo.InputDeviceId, currentInId, StringComparison.OrdinalIgnoreCase)) active = false;
                if (!combo.HasOutput && !combo.HasInput) active = false;

                Console.WriteLine("  " + (i + 1).ToString().PadLeft(2) + ". " + (active ? "[当前] " : "       ") + combo.Name);
                Console.WriteLine("      扬声器：" + (combo.HasOutput ? combo.OutputDeviceName : "（不更改）"));
                Console.WriteLine("      麦克风：" + (combo.HasInput ? combo.InputDeviceName : "（不更改）"));
                Console.WriteLine("      快捷键：" + (string.IsNullOrEmpty(combo.Hotkey) ? "（无）" : combo.Hotkey) +
                                  "   状态：" + (combo.Enabled ? "已启用" : "已停用"));
            }
            return 0;
        }

        private static int Switch(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法：--switch <组合名称或序号>");
                return 2;
            }

            AppConfig config = AppConfig.Load();
            string key = string.Join(" ", args, 1, args.Length - 1).Trim();
            ComboConfig target = null;

            int index;
            if (int.TryParse(key, out index) && index >= 1 && index <= config.Combos.Count)
            {
                target = config.Combos[index - 1];
            }
            else
            {
                for (int i = 0; i < config.Combos.Count; i++)
                {
                    if (string.Equals(config.Combos[i].Name, key, StringComparison.CurrentCultureIgnoreCase))
                    {
                        target = config.Combos[i];
                        break;
                    }
                }
                if (target == null)
                {
                    for (int i = 0; i < config.Combos.Count; i++)
                    {
                        if (config.Combos[i].Name != null &&
                            config.Combos[i].Name.IndexOf(key, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        {
                            target = config.Combos[i];
                            break;
                        }
                    }
                }
            }

            if (target == null)
            {
                Console.WriteLine("找不到组合「" + key + "」。可用组合：");
                for (int i = 0; i < config.Combos.Count; i++)
                {
                    Console.WriteLine("  " + (i + 1) + ". " + config.Combos[i].Name);
                }
                return 1;
            }

            Console.WriteLine("切换到组合：" + target.Name);
            bool ok = true;
            if (target.HasOutput)
            {
                string error;
                if (!Apply(target.OutputDeviceId, target.OutputDeviceName, EDataFlow.Render, out error))
                {
                    Console.WriteLine("  扬声器：失败 - " + error);
                    ok = false;
                }
            }
            else
            {
                Console.WriteLine("  扬声器：（不更改）");
            }
            if (target.HasInput)
            {
                string error;
                if (!Apply(target.InputDeviceId, target.InputDeviceName, EDataFlow.Capture, out error))
                {
                    Console.WriteLine("  麦克风：失败 - " + error);
                    ok = false;
                }
            }
            else
            {
                Console.WriteLine("  麦克风：（不更改）");
            }

            Console.WriteLine("  现在的默认扬声器：" + AudioService.GetDefaultDeviceName(EDataFlow.Render));
            Console.WriteLine("  现在的默认麦克风：" + AudioService.GetDefaultDeviceName(EDataFlow.Capture));
            return ok ? 0 : 1;
        }

        private static int SetEndpoint(string[] args, EDataFlow flow)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法：" + args[0] + " <设备名或 ID 片段>");
                return 2;
            }
            string key = string.Join(" ", args, 1, args.Length - 1).Trim();
            string error;
            if (!Apply("", key, flow, out error))
            {
                Console.WriteLine("设置失败：" + error);
                return 1;
            }
            Console.WriteLine("已设置，当前" + (flow == EDataFlow.Render ? "扬声器" : "麦克风") + "：" +
                              AudioService.GetDefaultDeviceName(flow));
            return 0;
        }

        private static bool Apply(string savedId, string savedName, EDataFlow flow, out string error)
        {
            error = null;
            AudioDeviceInfo info = AudioService.Resolve(savedId, savedName, flow, true, out error);
            if (info == null) return false;
            if (!info.IsActive)
            {
                error = "「" + info.Name + "」当前" + info.StateText;
                return false;
            }
            if (!AudioService.SetDefaultDevice(info.Id, out error)) return false;
            Console.WriteLine("  " + (flow == EDataFlow.Render ? "扬声器" : "麦克风") + " → " + info.Name);
            return true;
        }
    }
}
