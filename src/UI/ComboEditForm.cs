using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AudioSwitcher.Audio;
using AudioSwitcher.Config;
using AudioSwitcher.Hotkeys;
using AudioSwitcher.Native;

namespace AudioSwitcher.UI
{
    internal class ComboEditForm : Form
    {
        private class DeviceOption
        {
            public string Id;
            public string Name;
            public string Label;

            public override string ToString()
            {
                return Label;
            }
        }

        private readonly ComboConfig _combo;
        private readonly List<AudioDeviceInfo> _outputs;
        private readonly List<AudioDeviceInfo> _inputs;
        private readonly bool _showInactive;

        private TextBox _txtName;
        private ComboBox _cmbOutput;
        private ComboBox _cmbInput;
        private HotkeyTextBox _txtHotkey;
        private CheckBox _chkEnabled;

        public ComboConfig Result { get; private set; }

        public ComboEditForm(ComboConfig combo, List<AudioDeviceInfo> outputs, List<AudioDeviceInfo> inputs, bool showInactive)
        {
            _combo = combo.Clone();
            _outputs = outputs;
            _inputs = inputs;
            _showInactive = showInactive;

            BuildUi();
            LoadCombo();
        }

        private void BuildUi()
        {
            Text = "组合设置";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 330);
            Icon = App.IconUtil.AppIcon;

            Label lblName = new Label();
            lblName.Text = "组合名称";
            lblName.Location = new Point(18, 18);
            lblName.Size = new Size(100, 20);
            Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(18, 40);
            _txtName.Size = new Size(484, 25);
            Controls.Add(_txtName);

            Label lblOut = new Label();
            lblOut.Text = "扬声器 / 输出设备";
            lblOut.Location = new Point(18, 76);
            lblOut.Size = new Size(200, 20);
            Controls.Add(lblOut);

            _cmbOutput = new ComboBox();
            _cmbOutput.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbOutput.Location = new Point(18, 98);
            _cmbOutput.Size = new Size(410, 25);
            Controls.Add(_cmbOutput);

            Button btnCurrentOut = new Button();
            btnCurrentOut.Text = "取当前";
            btnCurrentOut.Location = new Point(434, 97);
            btnCurrentOut.Size = new Size(68, 27);
            btnCurrentOut.Click += delegate { SelectCurrent(_cmbOutput, EDataFlow.Render); };
            Controls.Add(btnCurrentOut);

            Label lblIn = new Label();
            lblIn.Text = "麦克风 / 输入设备";
            lblIn.Location = new Point(18, 134);
            lblIn.Size = new Size(200, 20);
            Controls.Add(lblIn);

            _cmbInput = new ComboBox();
            _cmbInput.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbInput.Location = new Point(18, 156);
            _cmbInput.Size = new Size(410, 25);
            Controls.Add(_cmbInput);

            Button btnCurrentIn = new Button();
            btnCurrentIn.Text = "取当前";
            btnCurrentIn.Location = new Point(434, 155);
            btnCurrentIn.Size = new Size(68, 27);
            btnCurrentIn.Click += delegate { SelectCurrent(_cmbInput, EDataFlow.Capture); };
            Controls.Add(btnCurrentIn);

            Label lblHotkey = new Label();
            lblHotkey.Text = "快捷键（点一下框，然后直接按组合键，如 Ctrl+Alt+1）";
            lblHotkey.Location = new Point(18, 192);
            lblHotkey.Size = new Size(430, 20);
            Controls.Add(lblHotkey);

            _txtHotkey = new HotkeyTextBox();
            _txtHotkey.Location = new Point(18, 214);
            _txtHotkey.Size = new Size(200, 25);
            Controls.Add(_txtHotkey);

            Button btnClear = new Button();
            btnClear.Text = "清除";
            btnClear.Location = new Point(224, 213);
            btnClear.Size = new Size(60, 27);
            btnClear.Click += delegate { _txtHotkey.Text = ""; };
            Controls.Add(btnClear);

            Label lblHotkeyHint = new Label();
            lblHotkeyHint.Text = "Backspace / Delete 也可清除；按 F1~F24 可以不加修饰键。";
            lblHotkeyHint.ForeColor = SystemColors.GrayText;
            lblHotkeyHint.Location = new Point(292, 218);
            lblHotkeyHint.Size = new Size(220, 20);
            Controls.Add(lblHotkeyHint);

            _chkEnabled = new CheckBox();
            _chkEnabled.Text = "启用此组合（并注册上面的快捷键）";
            _chkEnabled.Location = new Point(18, 256);
            _chkEnabled.Size = new Size(330, 24);
            Controls.Add(_chkEnabled);

            Button btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.Location = new Point(322, 288);
            btnOk.Size = new Size(84, 30);
            btnOk.DialogResult = DialogResult.None;
            btnOk.Click += OnOk;
            Controls.Add(btnOk);

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(416, 288);
            btnCancel.Size = new Size(84, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void LoadCombo()
        {
            _txtName.Text = _combo.Name;
            _chkEnabled.Checked = _combo.Enabled;
            _txtHotkey.Text = _combo.Hotkey;

            FillDeviceCombo(_cmbOutput, _outputs, _combo.OutputDeviceId, _combo.OutputDeviceName);
            FillDeviceCombo(_cmbInput, _inputs, _combo.InputDeviceId, _combo.InputDeviceName);
        }

        private void FillDeviceCombo(ComboBox box, List<AudioDeviceInfo> devices, string savedId, string savedName)
        {
            box.Items.Clear();

            DeviceOption keep = new DeviceOption();
            keep.Id = "";
            keep.Name = "";
            keep.Label = "（不更改，保持系统当前设置）";
            box.Items.Add(keep);

            bool found = false;
            for (int i = 0; i < devices.Count; i++)
            {
                AudioDeviceInfo device = devices[i];
                if (!_showInactive && !device.IsActive) continue;
                DeviceOption option = new DeviceOption();
                option.Id = device.Id;
                option.Name = device.Name;
                option.Label = device.DisplayName + (device.IsActive ? "" : "（" + device.StateText + "）");
                box.Items.Add(option);
                if (IsSame(device.Id, device.Name, savedId, savedName)) found = true;
            }

            // 配置里保存的设备现在不在列表中（被移除/禁用且未勾选显示）也要显示出来
            if (!found && (!string.IsNullOrEmpty(savedId) || !string.IsNullOrEmpty(savedName)))
            {
                DeviceOption missing = new DeviceOption();
                missing.Id = savedId;
                missing.Name = savedName;
                missing.Label = (string.IsNullOrEmpty(savedName) ? savedId : savedName) + "（当前不可用）";
                box.Items.Add(missing);
            }

            for (int i = 0; i < box.Items.Count; i++)
            {
                DeviceOption option = (DeviceOption)box.Items[i];
                if (IsSame(option.Id, option.Name, savedId, savedName))
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
            box.SelectedIndex = 0;
        }

        private static bool IsSame(string idA, string nameA, string idB, string nameB)
        {
            if (!string.IsNullOrEmpty(idB) && string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrEmpty(idB) && string.IsNullOrEmpty(nameB)) return string.IsNullOrEmpty(idA) && string.IsNullOrEmpty(nameA);
            if (!string.IsNullOrEmpty(nameB) && string.Equals(nameA, nameB, StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }

        private void SelectCurrent(ComboBox box, EDataFlow flow)
        {
            string id = AudioService.GetDefaultDeviceId(flow);
            if (string.IsNullOrEmpty(id))
            {
                MessageBox.Show(this, "系统当前没有默认设备。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (int i = 0; i < box.Items.Count; i++)
            {
                DeviceOption option = (DeviceOption)box.Items[i];
                if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
            MessageBox.Show(this, "当前默认设备不在列表里，请先点“刷新设备”。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnOk(object sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "请填写组合名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            string hotkey = _txtHotkey.Text.Trim();
            if (hotkey.Length > 0)
            {
                uint modifiers;
                uint virtualKey;
                string error;
                if (!HotkeyUtil.TryParse(hotkey, out modifiers, out virtualKey, out error))
                {
                    MessageBox.Show(this, "快捷键无效：" + error, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtHotkey.Focus();
                    return;
                }
                hotkey = HotkeyUtil.Format(modifiers, virtualKey);
            }

            DeviceOption outOption = (DeviceOption)_cmbOutput.SelectedItem;
            DeviceOption inOption = (DeviceOption)_cmbInput.SelectedItem;

            if (outOption == null && inOption == null)
            {
                MessageBox.Show(this, "输出和输入至少要选一个（否则组合没有任何作用）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ComboConfig result = new ComboConfig();
            result.Name = name;
            result.Hotkey = hotkey;
            result.Enabled = _chkEnabled.Checked;
            if (outOption != null) { result.OutputDeviceId = outOption.Id; result.OutputDeviceName = outOption.Name; }
            if (inOption != null) { result.InputDeviceId = inOption.Id; result.InputDeviceName = inOption.Name; }

            Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
