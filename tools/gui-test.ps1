# End-to-end GUI test: global hotkey path + settings window screenshot.
# NOTE: keep ASCII-only for Windows PowerShell 5.1.
param(
    [string]$Exe = 'E:\DSHcoder\AudioSwitcher\dist\AudioSwitcher.exe',
    [string]$Shot = 'E:\DSHcoder\AudioSwitcher\docs\settings.png'
)

$runner = Join-Path $PSScriptRoot 'run-cli.ps1'

# our app writes UTF-8; decode native output as UTF-8
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Get-Defaults {
    $lines = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -Exe $Exe --status
    (($lines | Select-Object -First 2) -join '  ||  ')
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public class Win32Helper {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@

[Win32Helper]::SetProcessDPIAware() | Out-Null

Write-Output '===== A) tray app + global hotkey ====='
$tray = Start-Process -FilePath $Exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
Write-Output ('[start]      ' + (Get-Defaults))

[System.Windows.Forms.SendKeys]::SendWait('^%1')
Start-Sleep -Seconds 3
Write-Output ('[Ctrl+Alt+1] ' + (Get-Defaults))

[System.Windows.Forms.SendKeys]::SendWait('^%4')
Start-Sleep -Seconds 3
Write-Output ('[Ctrl+Alt+4] ' + (Get-Defaults))

if (-not $tray.HasExited) { Stop-Process -Id $tray.Id -Force }
Start-Sleep -Seconds 2

Write-Output '===== B) settings window screenshot ====='
$gui = Start-Process -FilePath $Exe -ArgumentList '--settings' -PassThru
Start-Sleep -Seconds 6

$proc = Get-Process -Id $gui.Id -ErrorAction SilentlyContinue
if ($null -eq $proc) {
    Write-Output 'process exited unexpectedly'
} else {
    $hwnd = $proc.MainWindowHandle
    Write-Output ("title='{0}' handle={1} visible={2}" -f $proc.MainWindowTitle, $hwnd, [Win32Helper]::IsWindowVisible($hwnd))
    if ($hwnd -ne [IntPtr]::Zero) {
        $rect = New-Object RECT
        [Win32Helper]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        Write-Output ("window rect = {0},{1} {2}x{3}" -f $rect.Left, $rect.Top, $width, $height)

        $bitmap = New-Object System.Drawing.Bitmap($width, $height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        $printed = [Win32Helper]::PrintWindow($hwnd, $hdc, 2)
        $graphics.ReleaseHdc($hdc)
        Write-Output ("PrintWindow = {0}" -f $printed)
        if (-not $printed) {
            [Win32Helper]::SetForegroundWindow($hwnd) | Out-Null
            Start-Sleep -Milliseconds 800
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
        }
        $dir = Split-Path -Parent $Shot
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        $bitmap.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bitmap.Dispose()
        Write-Output ("screenshot saved: {0}" -f $Shot)
    }
    if (-not $gui.HasExited) { Stop-Process -Id $gui.Id -Force }
}
Start-Sleep -Seconds 2
Write-Output 'done'
