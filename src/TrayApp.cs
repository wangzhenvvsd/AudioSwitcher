using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AudioSwitcher.App;
using AudioSwitcher.Audio;
using AudioSwitcher.Config;
using AudioSwitcher.Hotkeys;
using AudioSwitcher.Native;
using AudioSwitcher.UI;

namespace AudioSwitcher
{
    internal class TrayApp : ApplicationContext
    {
        private const int HotkeyIdBase = 0xA000;
        private const int CycleHotkeyId = 0xA0FF;

        private readonly AppConfig _config;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly HotkeyWindow _window;
        private readonly Dictionary<int, ComboConfig> _registered = new Dictionary<int, ComboConfig>();

        private SettingsForm _settings;
        private int _cycleCursor = -1;
        private bool _balloonShown;

        public AppConfig Config
        {
            get { return _config; }
        }

        public TrayApp(bool showSettings)
        {
            bool configExisted = AppConfig.Exists;
            _config = AppConfig.Load();
            if (!configExisted)
            {
                // 首次运行：把自动生成的组合落盘，托盘菜单和配置文件保持一致
                try
                {
                    AppConfig.Save(_config);
                }
                catch
                {
                }
            }

            _window = new HotkeyWindow();
            IntPtr handle = _window.Handle; // 强制创建句柄，用于接收 WM_HOTKEY
            _window.HotkeyPressed += OnHotkeyPressed;

            _menu = new ContextMenuStrip();
            _menu.Opening += delegate { RebuildMenuItems(); };

            _tray = new NotifyIcon();
            _tray.Icon = IconUtil.AppIcon;
            _tray.Text = AppEnv.ProductName;
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.MouseClick += OnTrayMouseClick;
            _tray.DoubleClick += delegate { ShowSettings(); };

            List<string> errors = RegisterHotkeys();
            RebuildMenuItems();
            Log.Write("托盘程序已启动：组合 " + _config.Combos.Count + " 个，注册成功 " + _registered.Count +
                      " 个快捷键；配置文件 " + AppConfig.ConfigPath);

            if (showSettings || errors.Count > 0)
            {
                ShowSettings();
            }
            if (errors.Count > 0)
            {
                Notify("快捷键注册提示", string.Join("\r\n", errors.ToArray()), false);
            }
        }

        public void ReloadFromDisk()
        {
            AppConfig loaded = AppConfig.Load();
            _config.Combos = loaded.Combos;
            _config.StartWithWindows = loaded.StartWithWindows;
            _config.ShowNotifications = loaded.ShowNotifications;
            _config.ShowInactiveDevices = loaded.ShowInactiveDevices;
            _config.TrayClickCycles = loaded.TrayClickCycles;
            _config.CycleHotkey = loaded.CycleHotkey;
            _cycleCursor = -1;
            RegisterHotkeys();
            RebuildMenuItems();
        }

        public void ApplyConfigFromSettings()
        {
            AppConfig.Save(_config);
            StartupManager.SetEnabled(_config.StartWithWindows);
            _cycleCursor = -1;
            RegisterHotkeys();
            RebuildMenuItems();
        }

        public List<string> RegisterHotkeys()
        {
            List<string> errors = new List<string>();
            UnregisterAll();

            for (int i = 0; i < _config.Combos.Count; i++)
            {
                ComboConfig combo = _config.Combos[i];
                if (combo == null || !combo.Enabled) continue;
                if (string.IsNullOrEmpty(combo.Hotkey)) continue;

                uint modifiers;
                uint virtualKey;
                string error;
                if (!HotkeyUtil.TryParse(combo.Hotkey, out modifiers, out virtualKey, out error))
                {
                    errors.Add("「" + combo.Name + "」的快捷键无效：" + error);
                    continue;
                }
                int id = HotkeyIdBase + i;
                if (_window.Register(id, modifiers | HotkeyUtil.MOD_NOREPEAT, virtualKey))
                {
                    _registered[id] = combo;
                    Log.Write("注册快捷键成功：" + combo.Name + " -> " + combo.Hotkey + " (id=0x" + id.ToString("X4") + ")");
                }
                else
                {
                    errors.Add("「" + combo.Name + "」的快捷键 " + combo.Hotkey + " 注册失败（可能已被其它程序占用）");
                    Log.Write("注册快捷键失败：" + combo.Name + " -> " + combo.Hotkey);
                }
            }

            if (!string.IsNullOrEmpty(_config.CycleHotkey))
            {
                uint modifiers;
                uint virtualKey;
                string error;
                if (HotkeyUtil.TryParse(_config.CycleHotkey, out modifiers, out virtualKey, out error))
                {
                    if (_window.Register(CycleHotkeyId, modifiers | HotkeyUtil.MOD_NOREPEAT, virtualKey))
                    {
                        _registered[CycleHotkeyId] = null;
                    }
                    else
                    {
                        errors.Add("循环切换快捷键 " + _config.CycleHotkey + " 注册失败（可能已被其它程序占用）");
                    }
                }
                else
                {
                    errors.Add("循环切换快捷键无效：" + error);
                }
            }
            return errors;
        }

        private void UnregisterAll()
        {
            foreach (int id in new List<int>(_registered.Keys))
            {
                _window.Unregister(id);
            }
            _registered.Clear();
        }

        private void OnHotkeyPressed(object sender, HotkeyEventArgs e)
        {
            Log.Write("收到热键消息 id=0x" + e.Id.ToString("X4"));
            if (e.Id == CycleHotkeyId)
            {
                CycleNext();
                return;
            }
            ComboConfig combo;
            if (_registered.TryGetValue(e.Id, out combo) && combo != null)
            {
                Log.Write("热键 -> 组合「" + combo.Name + "」");
                SwitchCombo(combo, false);
            }
            else
            {
                Log.Write("热键 id=0x" + e.Id.ToString("X4") + " 没有对应组合，已忽略");
            }
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_config.TrayClickCycles)
            {
                CycleNext();
            }
            else
            {
                ShowSettings();
            }
        }

        public void ShowSettings()
        {
            if (_settings != null && !_settings.IsDisposed)
            {
                if (_settings.WindowState == FormWindowState.Minimized)
                {
                    _settings.WindowState = FormWindowState.Normal;
                }
                _settings.Show();
                _settings.BringToFront();
                _settings.Activate();
                return;
            }
            _settings = new SettingsForm(this);
            _settings.Show();
            _settings.Activate();
        }

        public string SwitchCombo(ComboConfig combo, bool silent)
        {
            if (combo == null) return "组合不存在。";

            List<string> lines = new List<string>();
            bool ok = true;

            if (combo.HasOutput)
            {
                string applied;
                string error;
                if (ApplyDevice(combo.OutputDeviceId, combo.OutputDeviceName, EDataFlow.Render, out applied, out error))
                {
                    lines.Add("扬声器：" + applied);
                }
                else
                {
                    ok = false;
                    lines.Add("扬声器：切换失败 - " + error);
                }
            }
            else
            {
                lines.Add("扬声器：保持当前");
            }

            if (combo.HasInput)
            {
                string applied;
                string error;
                if (ApplyDevice(combo.InputDeviceId, combo.InputDeviceName, EDataFlow.Capture, out applied, out error))
                {
                    lines.Add("麦克风：" + applied);
                }
                else
                {
                    ok = false;
                    lines.Add("麦克风：切换失败 - " + error);
                }
            }
            else
            {
                lines.Add("麦克风：保持当前");
            }

            string message = string.Join("\r\n", lines.ToArray());
            Log.Write("切换「" + combo.Name + "」结果：" + message.Replace("\r\n", " ; "));
            if (!silent && _config.ShowNotifications)
            {
                Notify(ok ? "已切换到「" + combo.Name + "」" : "「" + combo.Name + "」切换未成功", message, ok);
            }
            return message;
        }

        private bool ApplyDevice(string savedId, string savedName, EDataFlow flow, out string appliedName, out string error)
        {
            appliedName = savedName;
            error = null;

            AudioDeviceInfo info = AudioService.Resolve(savedId, savedName, flow, true, out error);
            if (info == null) return false;

            if (!info.IsActive)
            {
                error = "「" + info.Name + "」当前" + info.StateText + "，请检查设备是否已连接/启用";
                return false;
            }

            if (!AudioService.SetDefaultDevice(info.Id, out error)) return false;

            appliedName = info.Name;
            return true;
        }

        public void CycleNext()
        {
            List<ComboConfig> enabled = new List<ComboConfig>();
            for (int i = 0; i < _config.Combos.Count; i++)
            {
                ComboConfig combo = _config.Combos[i];
                if (combo != null && combo.Enabled && (combo.HasOutput || combo.HasInput))
                {
                    enabled.Add(combo);
                }
            }

            if (enabled.Count == 0)
            {
                Notify("没有可切换的组合", "请先在设置里新增组合。", false);
                return;
            }

            if (_cycleCursor < 0)
            {
                _cycleCursor = FindActiveIndex(enabled);
            }
            _cycleCursor = (_cycleCursor + 1) % enabled.Count;
            ComboConfig target = enabled[_cycleCursor];
            string message = SwitchCombo(target, true);
            Notify("已切换到「" + target.Name + "」", message, true);
        }

        private int FindActiveIndex(List<ComboConfig> combos)
        {
            string currentOut = AudioService.GetDefaultDeviceId(EDataFlow.Render);
            string currentIn = AudioService.GetDefaultDeviceId(EDataFlow.Capture);
            for (int i = 0; i < combos.Count; i++)
            {
                ComboConfig combo = combos[i];
                bool matches = true;
                if (combo.HasOutput && !IdEquals(combo.OutputDeviceId, currentOut)) matches = false;
                if (combo.HasInput && !IdEquals(combo.InputDeviceId, currentIn)) matches = false;
                if (matches) return i;
            }
            return -1;
        }

        private static bool IdEquals(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public void Notify(string title, string message, bool success)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = message;
                _tray.BalloonTipIcon = success ? ToolTipIcon.Info : ToolTipIcon.Warning;
                _tray.ShowBalloonTip(3000);
            }
            catch
            {
            }
        }

        public void RebuildMenuItems()
        {
            _menu.Items.Clear();

            string currentOut = AudioService.GetDefaultDeviceName(EDataFlow.Render);
            string currentIn = AudioService.GetDefaultDeviceName(EDataFlow.Capture);

            ToolStripMenuItem header = new ToolStripMenuItem("当前扬声器：" + currentOut);
            header.Enabled = false;
            _menu.Items.Add(header);

            ToolStripMenuItem headerIn = new ToolStripMenuItem("当前麦克风：" + currentIn);
            headerIn.Enabled = false;
            _menu.Items.Add(headerIn);
            _menu.Items.Add(new ToolStripSeparator());

            int usable = 0;
            for (int i = 0; i < _config.Combos.Count; i++)
            {
                ComboConfig combo = _config.Combos[i];
                if (combo == null) continue;
                usable++;

                bool active = IsComboActive(combo, currentOut, currentIn);
                string text = (active ? "● " : "    ") + combo.Name;
                if (!string.IsNullOrEmpty(combo.Hotkey)) text += "    (" + combo.Hotkey + ")";
                if (!combo.Enabled) text += "  [已停用]";

                ToolStripMenuItem item = new ToolStripMenuItem(text);
                item.Tag = combo;
                item.Enabled = true;
                item.Click += delegate(object sender, EventArgs e)
                {
                    ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
                    SwitchCombo((ComboConfig)menuItem.Tag, false);
                };
                _menu.Items.Add(item);
            }

            if (usable == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("（还没有组合，点“设置”新增）");
                empty.Enabled = false;
                _menu.Items.Add(empty);
            }

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem cycle = new ToolStripMenuItem("下一个组合");
            if (!string.IsNullOrEmpty(_config.CycleHotkey)) cycle.Text += "    (" + _config.CycleHotkey + ")";
            cycle.Click += delegate { CycleNext(); };
            _menu.Items.Add(cycle);

            ToolStripMenuItem settings = new ToolStripMenuItem("设置 / 组合管理…");
            settings.Font = new Font(_menu.Font, FontStyle.Bold);
            settings.Click += delegate { ShowSettings(); };
            _menu.Items.Add(settings);

            ToolStripMenuItem sound = new ToolStripMenuItem("打开系统声音设置");
            sound.Click += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start("ms-settings:sound");
                }
                catch (Exception ex)
                {
                    Notify("无法打开设置", ex.Message, false);
                }
            };
            _menu.Items.Add(sound);

            ToolStripMenuItem reload = new ToolStripMenuItem("重新读取配置文件");
            reload.Click += delegate
            {
                ReloadFromDisk();
                Notify("配置已重新加载", "共 " + _config.Combos.Count + " 个组合。", true);
            };
            _menu.Items.Add(reload);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { ExitApplication(); };
            _menu.Items.Add(exit);

            if (!_balloonShown)
            {
                _balloonShown = true;
                _tray.BalloonTipTitle = AppEnv.ProductName + " 已启动";
                _tray.BalloonTipText = "双击托盘图标打开设置；右键图标选择组合即可切换。";
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                try
                {
                    _tray.ShowBalloonTip(3000);
                }
                catch
                {
                }
            }
        }

        private bool IsComboActive(ComboConfig combo, string currentOutName, string currentInName)
        {
            string outId = AudioService.GetDefaultDeviceId(EDataFlow.Render);
            string inId = AudioService.GetDefaultDeviceId(EDataFlow.Capture);
            bool outOk = !combo.HasOutput || IdEquals(combo.OutputDeviceId, outId);
            bool inOk = !combo.HasInput || IdEquals(combo.InputDeviceId, inId);
            return outOk && inOk && (combo.HasOutput || combo.HasInput);
        }

        public void ExitApplication()
        {
            try
            {
                UnregisterAll();
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch
            {
            }
            ExitThread();
        }
    }
}
