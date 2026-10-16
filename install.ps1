# Install AudioSwitcher for the current user (no admin rights needed).
# NOTE: keep ASCII-only for Windows PowerShell 5.1.
param(
    [string]$Source,
    [switch]$NoDesktopShortcut,
    [switch]$StartWithWindows
)

$ErrorActionPreference = 'Stop'

# 支持两种摆放方式：解压出来的安装包（exe 与脚本同目录）或源码仓库（dist\ 下）
if (-not $Source) {
    $candidates = @(
        (Join-Path $PSScriptRoot 'AudioSwitcher.exe'),
        (Join-Path $PSScriptRoot 'dist\AudioSwitcher.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { $Source = $candidate; break }
    }
}

if (-not $Source -or -not (Test-Path $Source)) {
    throw "AudioSwitcher.exe not found next to this script - run build.ps1 first, or extract the release zip fully"
}

$running = Get-Process AudioSwitcher -ErrorAction SilentlyContinue
if ($running) {
    Write-Output 'stopping the running instance ...'
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\AudioSwitcher'
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
$targetExe = Join-Path $targetDir 'AudioSwitcher.exe'
Copy-Item $Source $targetExe -Force
Write-Output ("installed : {0}" -f $targetExe)

$shell = New-Object -ComObject WScript.Shell

$startMenuLnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\AudioSwitcher.lnk'
$shortcut = $shell.CreateShortcut($startMenuLnk)
$shortcut.TargetPath = $targetExe
$shortcut.Arguments = '--settings'
$shortcut.WorkingDirectory = $targetDir
$shortcut.Description = 'AudioSwitcher - one-key speaker/microphone combo switching'
$shortcut.Save()
Write-Output ("start menu: {0}" -f $startMenuLnk)

if (-not $NoDesktopShortcut) {
    $desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'AudioSwitcher.lnk'
    $desktop = $shell.CreateShortcut($desktopLnk)
    $desktop.TargetPath = $targetExe
    $desktop.Arguments = '--settings'
    $desktop.WorkingDirectory = $targetDir
    $desktop.Description = 'AudioSwitcher - one-key speaker/microphone combo switching'
    $desktop.Save()
    Write-Output ("desktop   : {0}" -f $desktopLnk)
}

if ($StartWithWindows) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    Set-ItemProperty -Path $runKey -Name 'AudioSwitcher' -Value ('"' + $targetExe + '" --tray')
    Write-Output 'autostart : enabled (HKCU Run)'
}

Write-Output ''
Write-Output 'Done. Launch it from the Start Menu, then use the tray icon to configure combos.'
Write-Output 'Uninstall: delete the shortcut files and the folder above (and the HKCU Run value if set).'
