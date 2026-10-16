using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace AudioSwitcher.Hotkeys
{
    /// <summary>快捷键文本 &lt;-&gt; (修饰键, 虚拟键) 互转。</summary>
    public static class HotkeyUtil
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private static readonly Dictionary<string, Keys> NameToKey = BuildNameToKey();
        private static readonly Dictionary<Keys, string> KeyToName = BuildKeyToName();

        private static Dictionary<string, Keys> BuildNameToKey()
        {
            Dictionary<string, Keys> map = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);

            for (char c = 'A'; c <= 'Z'; c++)
            {
                map[c.ToString()] = (Keys)(int)c;
            }
            for (int d = 0; d <= 9; d++)
            {
                map[d.ToString()] = (Keys)(0x30 + d);
                map["NUM" + d] = (Keys)(0x60 + d);
            }
            for (int f = 1; f <= 24; f++)
            {
                map["F" + f] = (Keys)(0x6F + f);
            }

            Add(map, "SPACE", Keys.Space);
            Add(map, "TAB", Keys.Tab);
            Add(map, "ENTER", Keys.Enter);
            Add(map, "RETURN", Keys.Enter);
            Add(map, "ESC", Keys.Escape);
            Add(map, "ESCAPE", Keys.Escape);
            Add(map, "BACKSPACE", Keys.Back);
            Add(map, "BACK", Keys.Back);
            Add(map, "INSERT", Keys.Insert);
            Add(map, "INS", Keys.Insert);
            Add(map, "DELETE", Keys.Delete);
            Add(map, "DEL", Keys.Delete);
            Add(map, "HOME", Keys.Home);
            Add(map, "END", Keys.End);
            Add(map, "PAGEUP", Keys.PageUp);
            Add(map, "PGUP", Keys.PageUp);
            Add(map, "PAGEDOWN", Keys.PageDown);
            Add(map, "PGDN", Keys.PageDown);
            Add(map, "UP", Keys.Up);
            Add(map, "DOWN", Keys.Down);
            Add(map, "LEFT", Keys.Left);
            Add(map, "RIGHT", Keys.Right);
            Add(map, "PRINTSCREEN", Keys.PrintScreen);
            Add(map, "PAUSE", Keys.Pause);
            Add(map, "CAPSLOCK", Keys.CapsLock);
            Add(map, "NUMLOCK", Keys.NumLock);
            Add(map, "SCROLLLOCK", Keys.Scroll);
            Add(map, "MUTE", Keys.VolumeMute);
            Add(map, "VOLUMEUP", Keys.VolumeUp);
            Add(map, "VOLUMEDOWN", Keys.VolumeDown);
            Add(map, "MEDIAPLAY", Keys.MediaPlayPause);
            Add(map, "MEDIANEXT", Keys.MediaNextTrack);
            Add(map, "MEDIAPREV", Keys.MediaPreviousTrack);
            Add(map, "MEDIASTOP", Keys.MediaStop);
            Add(map, "-", Keys.OemMinus);
            Add(map, "MINUS", Keys.OemMinus);
            Add(map, "=", Keys.Oemplus);
            Add(map, "PLUS", Keys.Oemplus);
            Add(map, "[", Keys.OemOpenBrackets);
            Add(map, "]", Keys.OemCloseBrackets);
            Add(map, "\\", Keys.OemPipe);
            Add(map, ";", Keys.OemSemicolon);
            Add(map, "'", Keys.OemQuotes);
            Add(map, ",", Keys.Oemcomma);
            Add(map, ".", Keys.OemPeriod);
            Add(map, "/", Keys.OemQuestion);
            Add(map, "`", Keys.Oemtilde);
            return map;
        }

        private static void Add(Dictionary<string, Keys> map, string name, Keys key)
        {
            map[name] = key;
        }

        private static Dictionary<Keys, string> BuildKeyToName()
        {
            Dictionary<Keys, string> map = new Dictionary<Keys, string>();
            foreach (KeyValuePair<string, Keys> pair in NameToKey)
            {
                string existing;
                if (!map.TryGetValue(pair.Value, out existing))
                {
                    map[pair.Value] = pair.Key;
                }
                else if (pair.Key.Length < existing.Length)
                {
                    map[pair.Value] = pair.Key;
                }
            }
            // 覆盖成更常见/更短的写法
            map[Keys.Space] = "Space";
            map[Keys.Back] = "Backspace";
            map[Keys.Escape] = "Esc";
            map[Keys.Delete] = "Delete";
            map[Keys.PageUp] = "PageUp";
            map[Keys.PageDown] = "PageDown";
            map[Keys.OemMinus] = "-";
            map[Keys.Oemplus] = "=";
            map[Keys.OemOpenBrackets] = "[";
            map[Keys.OemCloseBrackets] = "]";
            map[Keys.OemPipe] = "\\";
            map[Keys.OemSemicolon] = ";";
            map[Keys.OemQuotes] = "'";
            map[Keys.Oemcomma] = ",";
            map[Keys.OemPeriod] = ".";
            map[Keys.OemQuestion] = "/";
            map[Keys.Oemtilde] = "`";
            return map;
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        public static string KeyName(Keys key)
        {
            string name;
            if (KeyToName.TryGetValue(key, out name)) return name;

            if (key >= Keys.F1 && key <= Keys.F24) return "F" + ((int)key - 0x6F);
            int code = (int)key;
            if (code >= 0x30 && code <= 0x5A) return ((char)code).ToString();
            return "VK" + code.ToString("X2");
        }

        public static string Format(uint modifiers, uint virtualKey)
        {
            StringBuilder builder = new StringBuilder();
            if ((modifiers & MOD_CONTROL) != 0) builder.Append("Ctrl+");
            if ((modifiers & MOD_ALT) != 0) builder.Append("Alt+");
            if ((modifiers & MOD_SHIFT) != 0) builder.Append("Shift+");
            if ((modifiers & MOD_WIN) != 0) builder.Append("Win+");
            builder.Append(KeyName((Keys)virtualKey));
            return builder.ToString();
        }

        public static bool TryParse(string text, out uint modifiers, out uint virtualKey, out string error)
        {
            modifiers = 0;
            virtualKey = 0;
            error = null;

            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                error = "快捷键为空";
                return false;
            }

            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            Keys key = Keys.None;

            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length == 0) continue;

                if (EqualsAny(token, "CTRL", "CONTROL", "CTL"))
                {
                    modifiers |= MOD_CONTROL;
                }
                else if (EqualsAny(token, "ALT"))
                {
                    modifiers |= MOD_ALT;
                }
                else if (EqualsAny(token, "SHIFT"))
                {
                    modifiers |= MOD_SHIFT;
                }
                else if (EqualsAny(token, "WIN", "WINDOWS", "META", "SUPER"))
                {
                    modifiers |= MOD_WIN;
                }
                else
                {
                    Keys parsed;
                    if (!NameToKey.TryGetValue(token, out parsed))
                    {
                        error = "无法识别的按键「" + token + "」";
                        return false;
                    }
                    if (key != Keys.None)
                    {
                        error = "只能包含一个非修饰键";
                        return false;
                    }
                    key = parsed;
                }
            }

            if (key == Keys.None)
            {
                error = "缺少主按键（例如 1、A、F8）";
                return false;
            }

            virtualKey = (uint)(int)key;
            if (modifiers == 0 && !(key >= Keys.F1 && key <= Keys.F24))
            {
                error = "需要至少一个修饰键（Ctrl/Alt/Shift/Win）";
                return false;
            }
            return true;
        }

        private static bool EqualsAny(string token, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(token, candidates[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>把 WinForms 的按键数据转换为注册用的快捷键。</summary>
        public static bool TryFromKeys(Keys keyData, out uint modifiers, out uint virtualKey, out string error)
        {
            modifiers = 0;
            virtualKey = 0;
            error = null;

            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;

            if (key == Keys.None || key == Keys.KeyCode)
            {
                error = "未捕获到按键";
                return false;
            }
            if (IsModifierKey(key))
            {
                error = "只按住了修饰键";
                return false;
            }

            if ((mods & Keys.Control) != 0) modifiers |= MOD_CONTROL;
            if ((mods & Keys.Alt) != 0) modifiers |= MOD_ALT;
            if ((mods & Keys.Shift) != 0) modifiers |= MOD_SHIFT;

            virtualKey = (uint)(int)key;
            if (modifiers == 0 && !(key >= Keys.F1 && key <= Keys.F24))
            {
                error = "请同时按住 Ctrl / Alt / Shift 中的至少一个";
                return false;
            }
            return true;
        }
    }
}
