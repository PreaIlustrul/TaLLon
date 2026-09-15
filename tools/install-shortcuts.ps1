# Creates Desktop shortcuts for the built TaLLon exe:
#   "TaLLon"           -> starts the dormant listener (tray icon)
#   "TaLLon Settings"  -> opens the settings app
# Run after `dotnet build src/TaLLon.sln -c Release`:
#   powershell -ExecutionPolicy Bypass -File tools/install-shortcuts.ps1
param([string]$Configuration = "Release")
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\TaLLon.App\bin\$Configuration\net8.0-windows\TaLLon.exe"
if (-not (Test-Path $exe)) { Write-Error "Build first: $exe not found"; exit 1 }
$desktop = [Environment]::GetFolderPath("Desktop")
$ws = New-Object -ComObject WScript.Shell

$s = $ws.CreateShortcut((Join-Path $desktop "TaLLon.lnk"))
$s.TargetPath = $exe; $s.WorkingDirectory = Split-Path $exe
$s.Description = "TaLLon window manager (dormant listener)"; $s.IconLocation = "$exe,0"; $s.Save()

$s = $ws.CreateShortcut((Join-Path $desktop "TaLLon Settings.lnk"))
$s.TargetPath = $exe; $s.Arguments = "--settings"; $s.WorkingDirectory = Split-Path $exe
$s.Description = "Configure TaLLon"; $s.IconLocation = "$exe,0"; $s.Save()
Write-Host "Shortcuts created on $desktop"
