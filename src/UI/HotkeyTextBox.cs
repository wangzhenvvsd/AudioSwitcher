using System;
using System.Windows.Forms;
using AudioSwitcher.Hotkeys;

namespace AudioSwitcher.UI
{
    /// <summary>点击后直接按组合键即可录制快捷键的输入框。</summary>
    public class HotkeyTextBox : TextBox
    {
        public HotkeyTextBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            Cursor = Cursors.Hand;
            TextAlign = HorizontalAlignment.Center;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;

            // Esc / Enter / Tab 交给对话框本身处理
            if (mods == Keys.None && (key == Keys.Escape || key == Keys.Enter || key == Keys.Tab))
            {
                return false;
            }

            if (key == Keys.Back || key == Keys.Delete)
            {
                if (Text.Length > 0)
                {
                    Text = "";
                    OnTextChanged(EventArgs.Empty);
                }
                return true;
            }

            if (key == Keys.None || key == Keys.KeyCode || HotkeyUtil.IsModifierKey(key))
            {
                return true;
            }

            uint modifiers;
            uint virtualKey;
            string error;
            if (HotkeyUtil.TryFromKeys(keyData, out modifiers, out virtualKey, out error))
            {
                string text = HotkeyUtil.Format(modifiers, virtualKey);
                Text = text;
                SelectAll();
                OnTextChanged(EventArgs.Empty);
            }
            return true;
        }
    }
}
