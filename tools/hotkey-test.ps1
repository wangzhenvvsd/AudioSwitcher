# Verify the real global-hotkey path with SendInput (SendKeys uses journal playback,
# which does NOT trigger RegisterHotKey hotkeys).
# NOTE: keep ASCII-only for Windows PowerShell 5.1.
param(
    [string]$Exe = 'E:\DSHcoder\AudioSwitcher\dist\AudioSwitcher.exe'
)

$runner = Join-Path $PSScriptRoot 'run-cli.ps1'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Get-Defaults {
    $lines = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -Exe $Exe --status
    (($lines | Select-Object -First 2) -join '  ||  ')
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Keyboard {
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void Key(ushort vk, bool up) {
        INPUT[] inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = vk;
        inputs[0].u.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void Press(ushort mod1, ushort mod2, ushort key) {
        Key(mod1, false);
        Key(mod2, false);
        Key(key, false);
        System.Threading.Thread.Sleep(80);
        Key(key, true);
        Key(mod2, true);
        Key(mod1, true);
    }
}
'@

Write-Output '===== global hotkey test (SendInput) ====='
$tray = Start-Process -FilePath $Exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
Write-Output ('[start]       ' + (Get-Defaults))

# Ctrl+Alt+1  -> combo 1 (IG27Q)
[Keyboard]::Press(0x11, 0x12, 0x31)
Start-Sleep -Seconds 3
Write-Output ('[Ctrl+Alt+1]  ' + (Get-Defaults))

# Ctrl+Alt+4  -> combo 4 (headset, restore)
[Keyboard]::Press(0x11, 0x12, 0x34)
Start-Sleep -Seconds 3
Write-Output ('[Ctrl+Alt+4]  ' + (Get-Defaults))

if (-not $tray.HasExited) { Stop-Process -Id $tray.Id -Force }
Start-Sleep -Seconds 1
Write-Output 'done'
