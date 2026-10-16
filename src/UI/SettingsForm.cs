using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AudioSwitcher.App;
using AudioSwitcher.Audio;
using AudioSwitcher.Config;
using AudioSwitcher.Hotkeys;
using AudioSwitcher.Native;

namespace AudioSwitcher.UI
{
    internal class SettingsForm : Form
    {
        private readonly TrayApp _app;
        private readonly List<ComboConfig> _working = new List<ComboConfig>();
        private readonly List<AudioDeviceInfo> _outputs = new List<AudioDeviceInfo>();
        private readonly List<AudioDeviceInfo> _inputs = new List<AudioDeviceInfo>();

        private ListView _list;
        private Label _status;
        private CheckBox _chkStartup;
        private CheckBox _chkShowInactive;
        private CheckBox _chkNotify;
        private CheckBox _chkTrayCycle;
        private HotkeyTextBox _txtCycleHotkey;
        private bool _dirty;

        public SettingsForm(TrayApp app)
        {
            _app = app;
            for (int i = 0; i < _app.Config.Combos.Count; i++)
            {
                _working.Add(_app.Config.Combos[i].Clone());
            }
            BuildUi();
            LoadDevices();
            RefreshList();
        }

        private void BuildUi()
        {
            Text = AppEnv.ProductName + " - 组合设置";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 600);
            MinimumSize = new Size(800, 560);
            Icon = IconUtil.AppIcon;
            AutoScaleMode = AutoScaleMode.Font;

            _list = new ListView();
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.Location = new Point(12, 12);
            _list.Size = new Size(856, 292);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _list.Columns.Add("组合名称", 190);
            _list.Columns.Add("扬声器（输出）", 250);
            _list.Columns.Add("麦克风（输入）", 250);
            _list.Columns.Add("快捷键", 110);
            _list.Columns.Add("状态", 60);
            _list.DoubleClick += delegate { EditSelected(); };
            Controls.Add(_list);

            int y = 316;
            AddButton("新增", 12, y, 80, delegate { AddCombo(); });
            AddButton("编辑", 100, y, 80, delegate { EditSelected(); });
            AddButton("删除", 188, y, 80, delegate { DeleteSelected(); });
            AddButton("上移", 276, y, 70, delegate { MoveSelected(-1); });
            AddButton("下移", 354, y, 70, delegate { MoveSelected(1); });
            AddButton("立即切换", 436, y, 90, delegate { SwitchSelected(); });
            AddButton("刷新设备", 536, y, 90, delegate { LoadDevices(); RefreshList(); SetStatus("设备列表已刷新。"); });

            LinkLabel link = new LinkLabel();
            link.Text = "打开系统声音设置";
            link.Location = new Point(650, y + 6);
            link.Size = new Size(160, 22);
            link.LinkClicked += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start("ms-settings:sound");
                }
                catch (Exception ex)
                {
                    SetStatus("无法打开系统声音设置：" + ex.Message);
                }
            };
            Controls.Add(link);

            GroupBox group = new GroupBox();
            group.Text = "常规设置";
            group.Location = new Point(12, 362);
            group.Size = new Size(856, 140);
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(group);

            _chkStartup = new CheckBox();
            _chkStartup.Text = "开机自动启动（登录后最小化到托盘）";
            _chkStartup.Location = new Point(16, 28);
            _chkStartup.Size = new Size(280, 24);
            _chkStartup.Checked = _app.Config.StartWithWindows;
            group.Controls.Add(_chkStartup);

            _chkShowInactive = new CheckBox();
            _chkShowInactive.Text = "在设备列表里显示已停用 / 未连接的设备";
            _chkShowInactive.Location = new Point(320, 28);
            _chkShowInactive.Size = new Size(320, 24);
            _chkShowInactive.Checked = _app.Config.ShowInactiveDevices;
            group.Controls.Add(_chkShowInactive);

            _chkNotify = new CheckBox();
            _chkNotify.Text = "切换时显示气泡提示";
            _chkNotify.Location = new Point(660, 28);
            _chkNotify.Size = new Size(180, 24);
            _chkNotify.Checked = _app.Config.ShowNotifications;
            group.Controls.Add(_chkNotify);

            _chkTrayCycle = new CheckBox();
            _chkTrayCycle.Text = "单击托盘图标切换到“下一个组合”（不勾选则打开设置）";
            _chkTrayCycle.Location = new Point(16, 58);
            _chkTrayCycle.Size = new Size(430, 24);
            _chkTrayCycle.Checked = _app.Config.TrayClickCycles;
            group.Controls.Add(_chkTrayCycle);

            Label lblCycle = new Label();
            lblCycle.Text = "循环切换快捷键：";
            lblCycle.Location = new Point(16, 98);
            lblCycle.Size = new Size(110, 22);
            group.Controls.Add(lblCycle);

            _txtCycleHotkey = new HotkeyTextBox();
            _txtCycleHotkey.Location = new Point(130, 96);
            _txtCycleHotkey.Size = new Size(170, 25);
            _txtCycleHotkey.Text = _app.Config.CycleHotkey;
            group.Controls.Add(_txtCycleHotkey);

            Button btnCycleClear = new Button();
            btnCycleClear.Text = "清除";
            btnCycleClear.Location = new Point(308, 95);
            btnCycleClear.Size = new Size(60, 27);
            btnCycleClear.Click += delegate { _txtCycleHotkey.Text = ""; };
            group.Controls.Add(btnCycleClear);

            Label lblHint = new Label();
            lblHint.Text = "提示：快捷键全局生效；点“保存”后立即重新注册。配置文件位于 %APPDATA%\\AudioSwitcher\\config.xml，可直接编辑。";
            lblHint.ForeColor = SystemColors.GrayText;
            lblHint.Location = new Point(392, 92);
            lblHint.Size = new Size(450, 40);
            group.Controls.Add(lblHint);

            _status = new Label();
            _status.Text = "就绪。";
            _status.Location = new Point(14, 516);
            _status.Size = new Size(600, 22);
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(_status);

            Button btnSave = new Button();
            btnSave.Text = "保存";
            btnSave.Location = new Point(676, 512);
            btnSave.Size = new Size(90, 32);
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Click += delegate { SaveAll(); };
            Controls.Add(btnSave);

            Button btnClose = new Button();
            btnClose.Text = "关闭";
            btnClose.Location = new Point(776, 512);
            btnClose.Size = new Size(90, 32);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            FormClosing += OnFormClosing;
        }

        private void AddButton(string text, int x, int y, int width, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 30);
            button.Click += handler;
            Controls.Add(button);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_dirty) return;
            DialogResult result = MessageBox.Show(
                this,
                "有修改尚未保存，是否保存后关闭？",
                AppEnv.ProductName,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == DialogResult.Yes)
            {
                SaveAll();
                if (_dirty)
                {
                    e.Cancel = true; // 保存失败（例如快捷键冲突），不要关掉窗口
                }
            }
            else
            {
                _dirty = false;
            }
        }

        private void LoadDevices()
        {
            _outputs.Clear();
            _inputs.Clear();
            try
            {
                _outputs.AddRange(AudioService.GetDevices(EDataFlow.Render, true));
                _inputs.AddRange(AudioService.GetDevices(EDataFlow.Capture, true));
            }
            catch (Exception ex)
            {
                SetStatus("枚举音频设备失败：" + ex.Message);
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            SetStatus("有未保存的修改，记得点“保存”。");
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
        }

        public void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _working.Count; i++)
            {
                ComboConfig combo = _working[i];
                ListViewItem item = new ListViewItem(string.IsNullOrEmpty(combo.Name) ? "(未命名)" : combo.Name);
                item.SubItems.Add(DescribeDevice(combo.OutputDeviceId, combo.OutputDeviceName, EDataFlow.Render));
                item.SubItems.Add(DescribeDevice(combo.InputDeviceId, combo.InputDeviceName, EDataFlow.Capture));
                item.SubItems.Add(string.IsNullOrEmpty(combo.Hotkey) ? "（无）" : combo.Hotkey);
                item.SubItems.Add(combo.Enabled ? "已启用" : "已停用");
                item.Tag = combo;
                if (!combo.Enabled) item.ForeColor = SystemColors.GrayText;
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0 && _list.SelectedIndices.Count == 0)
            {
                _list.Items[0].Selected = true;
            }
        }

        private string DescribeDevice(string id, string name, EDataFlow flow)
        {
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name)) return "（不更改）";
            List<AudioDeviceInfo> source = flow == EDataFlow.Render ? _outputs : _inputs;
            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(id) && string.Equals(source[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return source[i].DisplayName + (source[i].IsActive ? "" : "（" + source[i].StateText + "）");
                }
            }
            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(name) && string.Equals(source[i].Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return source[i].DisplayName + (source[i].IsActive ? "" : "（" + source[i].StateText + "）");
                }
            }
            return (string.IsNullOrEmpty(name) ? "未知设备" : name) + "（当前不可用）";
        }

        private ComboConfig SelectedCombo()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return (ComboConfig)_list.SelectedItems[0].Tag;
        }

        private void AddCombo()
        {
            ComboConfig combo = new ComboConfig();
            combo.Name = "新组合 " + (_working.Count + 1);
            ComboEditForm form = new ComboEditForm(combo, _outputs, _inputs, _chkShowInactive.Checked);
            if (form.ShowDialog(this) == DialogResult.OK && form.Result != null)
            {
                _working.Add(form.Result);
                RefreshList();
                _list.Items[_list.Items.Count - 1].Selected = true;
                MarkDirty();
            }
        }

        private void EditSelected()
        {
            ComboConfig combo = SelectedCombo();
            if (combo == null)
            {
                SetStatus("请先选中一个组合。");
                return;
            }
            int index = _working.IndexOf(combo);
            ComboEditForm form = new ComboEditForm(combo, _outputs, _inputs, _chkShowInactive.Checked);
            if (form.ShowDialog(this) == DialogResult.OK && form.Result != null)
            {
                _working[index] = form.Result;
                RefreshList();
                _list.Items[index].Selected = true;
                MarkDirty();
            }
        }

        private void DeleteSelected()
        {
            ComboConfig combo = SelectedCombo();
            if (combo == null)
            {
                SetStatus("请先选中一个组合。");
                return;
            }
            if (MessageBox.Show(this, "确定删除组合「" + combo.Name + "」？", AppEnv.ProductName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            int index = _working.IndexOf(combo);
            _working.RemoveAt(index);
            RefreshList();
            if (_list.Items.Count > 0)
            {
                _list.Items[Math.Min(index, _list.Items.Count - 1)].Selected = true;
            }
            MarkDirty();
        }

        private void MoveSelected(int delta)
        {
            ComboConfig combo = SelectedCombo();
            if (combo == null)
            {
                SetStatus("请先选中一个组合。");
                return;
            }
            int index = _working.IndexOf(combo);
            int target = index + delta;
            if (target < 0 || target >= _working.Count) return;
            _working.RemoveAt(index);
            _working.Insert(target, combo);
            RefreshList();
            _list.Items[target].Selected = true;
            MarkDirty();
        }

        private void SwitchSelected()
        {
            ComboConfig combo = SelectedCombo();
            if (combo == null)
            {
                SetStatus("请先选中一个组合。");
                return;
            }
            SetStatus("正在切换…");
            string message = _app.SwitchCombo(combo, true);
            SetStatus(message.Replace("\r\n", "    "));
        }

        private void SaveAll()
        {
            // 1) 快捷键校验
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _working.Count; i++)
            {
                ComboConfig combo = _working[i];
                if (string.IsNullOrEmpty(combo.Hotkey)) continue;

                uint modifiers;
                uint virtualKey;
                string error;
                if (!HotkeyUtil.TryParse(combo.Hotkey, out modifiers, out virtualKey, out error))
                {
                    MessageBox.Show(this, "组合「" + combo.Name + "」的快捷键无效：" + error,
                        AppEnv.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                combo.Hotkey = HotkeyUtil.Format(modifiers, virtualKey);

                if (!combo.Enabled) continue;
                string owner;
                if (seen.TryGetValue(combo.Hotkey, out owner))
                {
                    MessageBox.Show(this, "组合「" + combo.Name + "」和「" + owner + "」使用了相同的快捷键 " + combo.Hotkey + "，请改成不同的。",
                        AppEnv.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                seen[combo.Hotkey] = combo.Name;
            }

            string cycle = _txtCycleHotkey.Text.Trim();
            if (cycle.Length > 0)
            {
                uint modifiers;
                uint virtualKey;
                string error;
                if (!HotkeyUtil.TryParse(cycle, out modifiers, out virtualKey, out error))
                {
                    MessageBox.Show(this, "循环切换快捷键无效：" + error,
                        AppEnv.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                cycle = HotkeyUtil.Format(modifiers, virtualKey);
            }

            // 2) 写进配置并保存
            _app.Config.Combos = _working;
            _app.Config.StartWithWindows = _chkStartup.Checked;
            _app.Config.ShowInactiveDevices = _chkShowInactive.Checked;
            _app.Config.ShowNotifications = _chkNotify.Checked;
            _app.Config.TrayClickCycles = _chkTrayCycle.Checked;
            _app.Config.CycleHotkey = cycle;

            try
            {
                _app.ApplyConfigFromSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, AppEnv.ProductName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _dirty = false;
            RefreshList();
            SetStatus("已保存：" + DateTime.Now.ToString("HH:mm:ss") + "，共 " + _working.Count + " 个组合。配置：" + AppConfig.ConfigPath);
        }
    }
}
