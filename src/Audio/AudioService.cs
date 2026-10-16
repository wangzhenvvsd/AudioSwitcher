using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AudioSwitcher.Native;

namespace AudioSwitcher.Audio
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AdapterName { get; set; }
        public EDataFlow Flow { get; set; }
        public DeviceState State { get; set; }

        public bool IsActive
        {
            get { return (State & DeviceState.Active) == DeviceState.Active; }
        }

        public string StateText
        {
            get
            {
                if ((State & DeviceState.Active) != 0) return "已启用";
                if ((State & DeviceState.Disabled) != 0) return "已停用";
                if ((State & DeviceState.NotPresent) != 0) return "未连接";
                if ((State & DeviceState.Unplugged) != 0) return "已拔出";
                return State.ToString();
            }
        }

        public string DisplayName
        {
            get
            {
                string name = string.IsNullOrEmpty(Name) ? "(未知设备)" : Name;
                if (!string.IsNullOrEmpty(AdapterName) &&
                    string.Compare(AdapterName, name, StringComparison.OrdinalIgnoreCase) != 0 &&
                    name.IndexOf(AdapterName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return name + "  (" + AdapterName + ")";
                }
                return name;
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class AudioService
    {
        public static List<AudioDeviceInfo> GetDevices(EDataFlow flow, bool includeInactive)
        {
            List<AudioDeviceInfo> result = new List<AudioDeviceInfo>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                DeviceState mask = includeInactive ? DeviceState.All : DeviceState.Active;
                int hr = enumerator.EnumAudioEndpoints(flow, mask, out collection);
                if (hr != 0)
                {
                    throw new COMException("枚举音频设备失败", hr);
                }
                int count;
                hr = collection.GetCount(out count);
                if (hr != 0)
                {
                    throw new COMException("读取音频设备数量失败", hr);
                }
                for (int i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        hr = collection.Item(i, out device);
                        if (hr != 0 || device == null) continue;

                        string id;
                        hr = device.GetId(out id);
                        if (hr != 0 || string.IsNullOrEmpty(id)) continue;

                        DeviceState state;
                        hr = device.GetState(out state);
                        if (hr != 0) state = DeviceState.Active;

                        AudioDeviceInfo info = new AudioDeviceInfo();
                        info.Id = id;
                        info.Flow = flow;
                        info.State = state;
                        info.Name = GetPropertyString(device, NativeConstants.PKEY_Device_FriendlyName);
                        if (string.IsNullOrEmpty(info.Name))
                        {
                            info.Name = GetPropertyString(device, NativeConstants.PKEY_Device_DeviceDesc);
                        }
                        info.AdapterName = GetPropertyString(device, NativeConstants.PKEY_DeviceInterface_FriendlyName);
                        // 未连接且读不到名字的“幽灵设备”不展示，避免下拉框被垃圾项刷屏
                        if (string.IsNullOrEmpty(info.Name) && (state & DeviceState.Active) == 0) continue;
                        result.Add(info);
                    }
                    finally
                    {
                        Release(device);
                    }
                }
            }
            finally
            {
                Release(collection);
                Release(enumerator);
            }

            result.Sort(delegate(AudioDeviceInfo a, AudioDeviceInfo b)
            {
                if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        public static List<AudioDeviceInfo> GetDevices(bool includeInactive)
        {
            List<AudioDeviceInfo> all = new List<AudioDeviceInfo>();
            all.AddRange(GetDevices(EDataFlow.Render, includeInactive));
            all.AddRange(GetDevices(EDataFlow.Capture, includeInactive));
            return all;
        }

        public static string GetDefaultDeviceId(EDataFlow flow)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator.GetDefaultAudioEndpoint(flow, ERole.Console, out device) != 0 || device == null)
                {
                    return null;
                }
                string id;
                if (device.GetId(out id) != 0) return null;
                return id;
            }
            catch
            {
                return null;
            }
            finally
            {
                Release(device);
                Release(enumerator);
            }
        }

        public static AudioDeviceInfo GetDefaultDevice(EDataFlow flow)
        {
            string id = GetDefaultDeviceId(flow);
            if (string.IsNullOrEmpty(id)) return null;
            AudioDeviceInfo info = FindById(id, flow, true);
            if (info == null)
            {
                info = new AudioDeviceInfo();
                info.Id = id;
                info.Flow = flow;
                info.State = DeviceState.Active;
                info.Name = "(当前设备)";
            }
            return info;
        }

        public static string GetDefaultDeviceName(EDataFlow flow)
        {
            AudioDeviceInfo info = GetDefaultDevice(flow);
            return info == null ? "(无)" : info.Name;
        }

        public static bool IsDefaultDevice(string deviceId, EDataFlow flow)
        {
            string current = GetDefaultDeviceId(flow);
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(deviceId)) return false;
            return string.Equals(current, deviceId, StringComparison.OrdinalIgnoreCase);
        }

        public static AudioDeviceInfo FindById(string id, EDataFlow flow, bool includeInactive)
        {
            if (string.IsNullOrEmpty(id)) return null;
            List<AudioDeviceInfo> list = GetDevices(flow, includeInactive);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase)) return list[i];
            }
            return null;
        }

        public static AudioDeviceInfo FindByName(string name, EDataFlow flow, bool includeInactive)
        {
            if (string.IsNullOrEmpty(name)) return null;
            List<AudioDeviceInfo> list = GetDevices(flow, includeInactive);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Name, name, StringComparison.CurrentCultureIgnoreCase)) return list[i];
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name != null && list[i].Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0) return list[i];
            }
            return null;
        }

        /// <summary>把配置里保存的设备解析成当前真实存在的设备。</summary>
        public static AudioDeviceInfo Resolve(string savedId, string savedName, EDataFlow flow, bool includeInactive, out string error)
        {
            error = null;
            AudioDeviceInfo info = FindById(savedId, flow, includeInactive);
            if (info != null) return info;

            info = FindByName(savedName, flow, includeInactive);
            if (info != null) return info;

            string friendly = string.IsNullOrEmpty(savedName) ? savedId : savedName;
            error = "找不到设备「" + (string.IsNullOrEmpty(friendly) ? "未指定" : friendly) + "」";
            if (!includeInactive)
            {
                error += "（可能已断开连接，可在设置中勾选“显示已停用设备”查看）";
            }
            return null;
        }

        private delegate int SetEndpointProc(string deviceId, ERole role);

        /// <summary>
        /// 拿到“设置默认设备”的调用入口。
        /// Win7 之后是 IPolicyConfig(CPolicyConfigClient)，部分 Win10/11 版本上它不暴露该接口，
        /// 这时退回 Vista 时代的 IPolicyConfigVista(CPolicyConfigVistaClient)，两者 vtable 不同。
        /// </summary>
        private static SetEndpointProc GetSetEndpointProc(out object comClient)
        {
            try
            {
                object client = new CPolicyConfigClient();
                IPolicyConfig policy = (IPolicyConfig)client;
                comClient = client;
                return delegate(string id, ERole role) { return policy.SetDefaultEndpoint(id, role); };
            }
            catch
            {
            }

            try
            {
                object client = new CPolicyConfigVistaClient();
                IPolicyConfigVista policy = (IPolicyConfigVista)client;
                comClient = client;
                return delegate(string id, ERole role) { return policy.SetDefaultEndpoint(id, role); };
            }
            catch
            {
            }

            comClient = null;
            return null;
        }

        public static bool SetDefaultDevice(string deviceId, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(deviceId))
            {
                error = "设备 ID 为空";
                return false;
            }

            object client = null;
            try
            {
                SetEndpointProc setEndpoint = GetSetEndpointProc(out client);
                if (setEndpoint == null)
                {
                    error = "系统不支持切换默认音频设备（PolicyConfig 接口不可用）";
                    return false;
                }

                int hrConsole = setEndpoint(deviceId, ERole.Console);
                setEndpoint(deviceId, ERole.Multimedia);
                // 通信设备设置失败不影响主流程
                setEndpoint(deviceId, ERole.Communications);

                if (hrConsole != 0)
                {
                    error = DescribeHResult(hrConsole);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                Release(client);
            }
        }

        private static string DescribeHResult(int hr)
        {
            switch (unchecked((uint)hr))
            {
                case 0x80070002:
                    return "设备不存在（可能已拔出或未连接）";
                case 0x80070005:
                    return "拒绝访问";
                case 0x80070490:
                    return "系统找不到该音频设备";
                case 0x80070057:
                    return "参数无效（设备 ID 不正确）";
                default:
                    return "切换失败 (HRESULT 0x" + hr.ToString("X8") + ")";
            }
        }

        private static string GetPropertyString(IMMDevice device, PROPERTYKEY key)
        {
            IPropertyStore store = null;
            try
            {
                if (device.OpenPropertyStore(NativeConstants.STGM_READ, out store) != 0 || store == null)
                {
                    return null;
                }
                PROPERTYKEY localKey = key;
                PROPVARIANT value;
                if (store.GetValue(ref localKey, out value) != 0)
                {
                    return null;
                }
                string result = null;
                if (value.vt == NativeConstants.VT_LPWSTR && value.pointerValue != IntPtr.Zero)
                {
                    result = Marshal.PtrToStringUni(value.pointerValue);
                }
                Ole32.PropVariantClear(ref value);
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                Release(store);
            }
        }

        private static void Release(object comObject)
        {
            if (comObject == null) return;
            try
            {
                if (Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            catch
            {
            }
        }
    }
}
