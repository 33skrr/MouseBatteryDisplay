param(
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "dist\MouseBatteryDisplay.exe"
if (-not (Test-Path $source)) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "build.ps1")
    if (-not (Test-Path $source)) { throw "Build failed; MouseBatteryDisplay.exe not found." }
}

$installDir = Join-Path $env:LOCALAPPDATA "MouseBatteryDisplay"
$target = Join-Path $installDir "MouseBatteryDisplay.exe"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Get-Process -Name MouseBatteryDisplay -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
Copy-Item -Path $source -Destination $target -Force

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$command = '"' + $target + '" --startup'
Set-ItemProperty -Path $runKey -Name "MouseBatteryDisplay" -Value $command -Type String

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenu "MouseBatteryDisplay.lnk"
try {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = $target + ",0"
    $shortcut.Description = "Taskbar wireless mouse battery monitor"
    $shortcut.Save()
} catch {
    Write-Warning "Could not create the Start Menu shortcut: $($_.Exception.Message)"
}

Write-Host "Installed: $target"
Write-Host "Auto-start: $command"

if (-not $NoStart) {
    Start-Process -FilePath $target -ArgumentList "--startup"
    Write-Host "Started. Look for the battery percentage in the notification area (click ^ to show hidden icons)."
}
