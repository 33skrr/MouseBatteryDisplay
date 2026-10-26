param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Cannot find .NET Framework csc.exe"
}

$assets = Join-Path $root "assets"
$tools = Join-Path $root "tools"
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $assets, $tools, $dist | Out-Null

Write-Host "1/3 Generating tray icon..."
& $csc /nologo /target:exe /out:"$tools\IconGen.exe" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll `
    "$src\TrayIconFactory.cs" "$tools\IconGen.cs"
if ($LASTEXITCODE -ne 0) { throw "Icon generator build failed" }
& "$tools\IconGen.exe" "$assets\app.ico" 87
if ($LASTEXITCODE -ne 0) { throw "Icon generation failed" }

Write-Host "2/3 Compiling tray application..."
$sources = Get-ChildItem -Path $src -Filter "*.cs" | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    /out:"$dist\MouseBatteryDisplay.exe" `
    /win32icon:"$assets\app.ico" `
    /win32manifest:"$src\app.manifest" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Application build failed" }

$exe = Join-Path $dist "MouseBatteryDisplay.exe"
$info = Get-Item $exe
Write-Host ("3/3 Done: {0} ({1:N0} bytes)" -f $info.FullName, $info.Length)

if ($Run) {
    Start-Process -FilePath $exe
    Write-Host "Started. Check the notification area of the taskbar."
}
