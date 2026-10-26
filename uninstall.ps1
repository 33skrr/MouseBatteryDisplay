$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "MouseBatteryDisplay"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\MouseBatteryDisplay.lnk"

Get-Process -Name MouseBatteryDisplay -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

Remove-ItemProperty -Path $runKey -Name "MouseBatteryDisplay" -ErrorAction SilentlyContinue
Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "MouseBatteryDisplay has been removed."
