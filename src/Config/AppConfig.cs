using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using AudioSwitcher.Audio;
using AudioSwitcher.Native;

namespace AudioSwitcher.Config
{
    public class ComboConfig
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Hotkey { get; set; }

        [XmlAttribute]
        public bool Enabled { get; set; }

        public string OutputDeviceId { get; set; }
        public string OutputDeviceName { get; set; }
        public string InputDeviceId { get; set; }
        public string InputDeviceName { get; set; }

        public ComboConfig()
        {
            Name = "";
            Hotkey = "";
            Enabled = true;
            OutputDeviceId = "";
            OutputDeviceName = "";
            InputDeviceId = "";
            InputDeviceName = "";
        }

        public bool HasOutput
        {
            get { return !string.IsNullOrEmpty(OutputDeviceId) || !string.IsNullOrEmpty(OutputDeviceName); }
        }

        public bool HasInput
        {
            get { return !string.IsNullOrEmpty(InputDeviceId) || !string.IsNullOrEmpty(InputDeviceName); }
        }

        public ComboConfig Clone()
        {
            ComboConfig copy = new ComboConfig();
            copy.Name = Name;
            copy.Hotkey = Hotkey;
            copy.Enabled = Enabled;
            copy.OutputDeviceId = OutputDeviceId;
            copy.OutputDeviceName = OutputDeviceName;
            copy.InputDeviceId = InputDeviceId;
            copy.InputDeviceName = InputDeviceName;
            return copy;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? "(未命名组合)" : Name;
        }
    }

    [XmlRoot("AudioSwitcherConfig")]
    public class AppConfig
    {
        public int Version { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowNotifications { get; set; }
        public bool ShowInactiveDevices { get; set; }
        public bool TrayClickCycles { get; set; }
        public string CycleHotkey { get; set; }

        [XmlArray("Combos")]
        [XmlArrayItem("Combo")]
        public List<ComboConfig> Combos { get; set; }

        public AppConfig()
        {
            Version = 1;
            ShowNotifications = true;
            TrayClickCycles = false;
            CycleHotkey = "";
            Combos = new List<ComboConfig>();
        }

        public static string ConfigDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AudioSwitcher");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "config.xml"); }
        }

        public static bool Exists
        {
            get { return File.Exists(ConfigPath); }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
                    using (FileStream stream = File.OpenRead(ConfigPath))
                    {
                        AppConfig loaded = (AppConfig)serializer.Deserialize(stream);
                        if (loaded != null)
                        {
                            if (loaded.Combos == null) loaded.Combos = new List<ComboConfig>();
                            for (int i = 0; i < loaded.Combos.Count; i++)
                            {
                                if (loaded.Combos[i] == null)
                                {
                                    loaded.Combos[i] = new ComboConfig();
                                }
                            }
                            return loaded;
                        }
                    }
                }
            }
            catch
            {
                // 配置损坏时回退到默认配置（原文件保留为 .bak 便于排查）
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        File.Copy(ConfigPath, ConfigPath + ".bak", true);
                    }
                }
                catch
                {
                }
            }
            AppConfig fresh = CreateDefault();
            return fresh;
        }

        public static void Save(AppConfig config)
        {
            if (config == null) config = new AppConfig();
            Directory.CreateDirectory(ConfigDirectory);
            string temp = ConfigPath + ".tmp";
            XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.Serialize(stream, config);
            }
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
            File.Move(temp, ConfigPath);
        }

        /// <summary>首次运行时，按当前系统里的扬声器自动生成一批组合。</summary>
        public static AppConfig CreateDefault()
        {
            AppConfig config = new AppConfig();
            try
            {
                List<AudioDeviceInfo> outputs = AudioService.GetDevices(EDataFlow.Render, false);
                string defaultInputId = AudioService.GetDefaultDeviceId(EDataFlow.Capture);
                string defaultInputName = defaultInputId == null ? "" : AudioService.GetDefaultDeviceName(EDataFlow.Capture);

                int index = 0;
                for (int i = 0; i < outputs.Count; i++)
                {
                    if (index >= 6) break;
                    ComboConfig combo = new ComboConfig();
                    combo.Name = outputs[i].Name;
                    combo.OutputDeviceId = outputs[i].Id;
                    combo.OutputDeviceName = outputs[i].Name;
                    combo.InputDeviceId = defaultInputId == null ? "" : defaultInputId;
                    combo.InputDeviceName = defaultInputName == null ? "" : defaultInputName;
                    if (index < 5)
                    {
                        combo.Hotkey = "Ctrl+Alt+" + (index + 1);
                    }
                    config.Combos.Add(combo);
                    index++;
                }
            }
            catch
            {
            }

            if (config.Combos.Count == 0)
            {
                ComboConfig combo = new ComboConfig();
                combo.Name = "默认组合";
                combo.Hotkey = "Ctrl+Alt+1";
                config.Combos.Add(combo);
            }
            return config;
        }
    }
}
