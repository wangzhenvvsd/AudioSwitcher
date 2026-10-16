# Diagnose why injected hotkeys are not firing.
# NOTE: keep ASCII-only for Windows PowerShell 5.1.
param(
    [string]$Exe = 'E:\DSHcoder\AudioSwitcher\dist\AudioSwitcher.exe'
)
$runner = Join-Path $PSScriptRoot 'run-cli.ps1'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Kb {
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("kernel32.dll")] public static extern uint GetCurrentProcessId();
    public static string Send(ushort mod1, ushort mod2, ushort key) {
        INPUT[] inputs = new INPUT[6];
        ushort[] seq = new ushort[] { mod1, mod2, key, key, mod2, mod1 };
        uint[] flags = new uint[] { 0, 0, 0, 2, 2, 2 };
        for (int i = 0; i < 6; i++) {
            inputs[i].type = 1;
            inputs[i].u.ki.wVk = seq[i];
            inputs[i].u.ki.dwFlags = flags[i];
        }
        uint sent = SendInput(6, inputs, Marshal.SizeOf(typeof(INPUT)));
        return "sent=" + sent + " lastError=" + Marshal.GetLastWin32Error();
    }
}
'@

Write-Output '--- existing AudioSwitcher processes ---'
Get-Process AudioSwitcher -ErrorAction SilentlyContinue | Select-Object Id, SessionId, MainWindowTitle | Format-Table -AutoSize | Out-String -Width 200

Write-Output ("current session id = {0}" -f (Get-Process -Id $PID).SessionId)

$tray = Start-Process -FilePath $Exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
$alive = -not $tray.HasExited
Write-Output ("tray process alive = {0}  (pid {1}, session {2})" -f $alive, $tray.Id, (Get-Process -Id $tray.Id -ErrorAction SilentlyContinue).SessionId)

Write-Output '--- does the tray instance hold Ctrl+Alt+1..5? (running selftest in a 2nd process) ---'
$self = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -Exe $Exe --selftest
$self

Write-Output '--- SendInput result ---'
Write-Output ([Kb]::Send(0x11, 0x12, 0x31))
Start-Sleep -Seconds 3
$after = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -Exe $Exe --status
Write-Output (($after | Select-Object -First 2) -join ' || ')

if ($alive) { Stop-Process -Id $tray.Id -Force }
Start-Sleep -Seconds 1
Write-Output 'done'
