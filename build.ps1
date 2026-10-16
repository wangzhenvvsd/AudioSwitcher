# Build AudioSwitcher.exe with the in-box .NET Framework C# compiler (no SDK needed).
# NOTE: keep this file ASCII-only - Windows PowerShell 5.1 reads .ps1 as ANSI.
param(
    [switch]$SkipIcon
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw 'csc.exe not found (.NET Framework 4.x is required)' }

$outDir = Join-Path $root 'dist'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$buildDir = Join-Path $root 'build'
if (-not (Test-Path $buildDir)) { New-Item -ItemType Directory -Path $buildDir | Out-Null }

# ---- 1) icon ----
$icoPath = Join-Path $root 'app.ico'
if (-not $SkipIcon) {
    $iconGenExe = Join-Path $buildDir 'IconGen.exe'
    $iconArgs = @(
        '/nologo', '/target:exe', '/platform:anycpu', '/optimize+',
        "/out:$iconGenExe",
        '/reference:System.dll',
        '/reference:System.Drawing.dll',
        (Join-Path $root 'tools\IconGen.cs')
    )
    & $csc $iconArgs
    if ($LASTEXITCODE -ne 0) { throw 'IconGen build failed' }
    & $iconGenExe $root
    if ($LASTEXITCODE -ne 0) { throw 'IconGen run failed' }
}

# ---- 2) main application ----
$outExe = Join-Path $outDir 'AudioSwitcher.exe'
$sources = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter *.cs |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xml.dll'
)

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    "/out:$outExe"
)
if (Test-Path $icoPath) { $cscArgs += "/win32icon:$icoPath" }
if (Test-Path (Join-Path $root 'app.manifest')) { $cscArgs += "/win32manifest:$(Join-Path $root 'app.manifest')" }
foreach ($reference in $references) { $cscArgs += "/reference:$reference" }
$cscArgs += $sources

Write-Output ("compiling {0} source files ..." -f $sources.Count)
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit $LASTEXITCODE)" }

$file = Get-Item $outExe
Write-Output ("OK: {0} ({1:N0} bytes)" -f $file.FullName, $file.Length)
