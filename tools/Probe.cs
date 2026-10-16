using System;
using System.Runtime.InteropServices;

/// <summary>探测 PolicyConfig 相关 COM 类与接口 IID，找出本机可用的组合。</summary>
internal static class Probe
{
    private static int Main()
    {
        string[] clsids = new string[]
        {
            "870AF99C-171D-4F9E-AF0D-E63DF40C2BC9", // CPolicyConfigClient
            "294935CE-F637-4E7C-A41B-AB255460B862", // CPolicyConfigVistaClient
            "BCDE0395-E52F-467C-8E3D-C4579291692E"  // MMDeviceEnumerator (sanity check)
        };

        string[] iids = new string[]
        {
            "F8679F50-AA1C-4B0E-B0F8-0E6B1E5B0E1C",
            "568B9108-44BF-40B4-9006-86AFE5B5A620",
            "CA286FC3-91FD-42C3-8E9B-AAE966BB33D9",
            "A95664D2-9614-4F35-A746-DE8DB63617E6",
            "BCDE0395-E52F-467C-8E3D-C4579291692E"
        };

        foreach (string clsidText in clsids)
        {
            Console.WriteLine("=== CLSID " + clsidText);
            Type type;
            try
            {
                type = Type.GetTypeFromCLSID(new Guid(clsidText), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  GetTypeFromCLSID 失败: " + ex.Message);
                continue;
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
                Console.WriteLine("  CoCreateInstance: OK  (" + type.FullName + ")");
            }
            catch (Exception ex)
            {
                Console.WriteLine("  CoCreateInstance 失败: " + ex.Message);
                continue;
            }

            IntPtr unknown = IntPtr.Zero;
            try
            {
                unknown = Marshal.GetIUnknownForObject(instance);
                for (int i = 0; i < iids.Length; i++)
                {
                    Guid iid = new Guid(iids[i]);
                    IntPtr ptr = IntPtr.Zero;
                    int hr = Marshal.QueryInterface(unknown, ref iid, out ptr);
                    Console.WriteLine("  QI " + iids[i] + " -> " +
                        (hr == 0 ? "OK" : "0x" + hr.ToString("X8")));
                    if (ptr != IntPtr.Zero) Marshal.Release(ptr);
                }
            }
            finally
            {
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
                Marshal.ReleaseComObject(instance);
            }
        }
        return 0;
    }
}
