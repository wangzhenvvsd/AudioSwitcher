# Run the app in CLI mode and capture UTF-8 output reliably (GUI subsystem => need redirect).
# NOTE: keep ASCII-only for Windows PowerShell 5.1.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$AppArgs
)

$stdout = [System.IO.Path]::GetTempFileName()
$stderr = [System.IO.Path]::GetTempFileName()
try {
    $process = Start-Process -FilePath $Exe -ArgumentList $AppArgs -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $text = ''
    if (Test-Path $stdout) { $text += [System.IO.File]::ReadAllText($stdout, [System.Text.Encoding]::UTF8) }
    $errText = ''
    if (Test-Path $stderr) { $errText += [System.IO.File]::ReadAllText($stderr, [System.Text.Encoding]::UTF8) }
    Write-Output $text
    if ($errText.Trim().Length -gt 0) { Write-Output ('[stderr] ' + $errText) }
    Write-Output ("[exit {0}]" -f $process.ExitCode)
}
finally {
    Remove-Item $stdout, $stderr -ErrorAction SilentlyContinue
}
