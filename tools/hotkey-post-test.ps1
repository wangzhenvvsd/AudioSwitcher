# In-process end-to-end check of the hotkey handler: post the same WM_HOTKEY message
# the OS would post, to the running tray instance's message window.
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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnum {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);

    public static IntPtr[] WindowsOf(uint targetPid) {
        List<IntPtr> list = new List<IntPtr>();
        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam) {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) list.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }

    public static string ClassOf(IntPtr hWnd) {
        StringBuilder buffer = new StringBuilder(256);
        GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
'@

Write-Output '===== in-process WM_HOTKEY test ====='
$logPath = Join-Path $env:APPDATA 'AudioSwitcher\AudioSwitcher.log'
Remove-Item $logPath -ErrorAction SilentlyContinue

$tray = Start-Process -FilePath $Exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
Write-Output ('[start]                ' + (Get-Defaults))

$windows = [WinEnum]::WindowsOf([uint32]$tray.Id)
Write-Output ("windows of pid {0}: {1}" -f $tray.Id, $windows.Count)
foreach ($h in $windows) { Write-Output ("   hwnd {0}  class {1}" -f $h, [WinEnum]::ClassOf($h)) }

# 0xA000 = first combo (Ctrl+Alt+1 -> IG27Q), 0xA003 = fourth combo (headset, restore)
foreach ($h in $windows) { [WinEnum]::PostMessage($h, 0x0312, [IntPtr]0xA000, [IntPtr]0) | Out-Null }
Start-Sleep -Seconds 3
Write-Output ('[posted 0xA000]        ' + (Get-Defaults))

foreach ($h in $windows) { [WinEnum]::PostMessage($h, 0x0312, [IntPtr]0xA003, [IntPtr]0) | Out-Null }
Start-Sleep -Seconds 3
Write-Output ('[posted 0xA003]        ' + (Get-Defaults))

if (-not $tray.HasExited) { Stop-Process -Id $tray.Id -Force }
Start-Sleep -Seconds 1

Write-Output '===== app log ====='
Get-Content $logPath -ErrorAction SilentlyContinue
Write-Output 'done'
