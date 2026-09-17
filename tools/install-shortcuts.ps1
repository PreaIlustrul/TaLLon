# Creates the Desktop shortcut "TaLLon" for the built exe (one app: launch button + settings inside).
# The app itself registers run-at-login and a Start-menu entry on first start.
# Run after `dotnet build src/TaLLon.sln -c Release`:
#   powershell -ExecutionPolicy Bypass -File tools/install-shortcuts.ps1
param([string]$Configuration = "Release")
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\TaLLon.App\bin\$Configuration\net8.0-windows10.0.19041.0\TaLLon.exe"
if (-not (Test-Path $exe)) { Write-Error "Build first: $exe not found"; exit 1 }
$desktop = [Environment]::GetFolderPath("Desktop")
$ws = New-Object -ComObject WScript.Shell

# remove the old two-shortcut layout
Remove-Item -LiteralPath (Join-Path $desktop "TaLLon Settings.lnk") -ErrorAction SilentlyContinue

$s = $ws.CreateShortcut((Join-Path $desktop "TaLLon.lnk"))
$s.TargetPath = $exe; $s.WorkingDirectory = Split-Path $exe
$s.Description = "TaLLon"; $s.IconLocation = "$exe,0"; $s.Save()
Write-Host "Shortcut created: $desktop\TaLLon.lnk -> $exe"
